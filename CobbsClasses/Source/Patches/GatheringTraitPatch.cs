using System;
using System.Collections.Generic;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    internal static class GatheringTraitPatch
    {
        internal static void Register(Harmony harmony)
        {
            foreach (Type patch in new[] { typeof(CropYield), typeof(LogYield), typeof(MilkYield), typeof(ClothingWear) })
            {
                harmony.CreateClassProcessor(patch).Patch();
            }
        }

        [HarmonyPatch(typeof(BlockCrop), nameof(BlockCrop.GetDrops))]
        private static class CropYield
        {
            private static void Postfix(IWorldAccessor __0, BlockPos __1, IPlayer? __2, ref ItemStack[] __result)
            {
                if (__0.Side != EnumAppSide.Server || __2?.Entity is not { } player || __0.BlockAccessor.GetBlockEntity(__1.DownCopy()) is not BlockEntityFarmland)
                {
                    return;
                }

                ScaleDrops(__0, player, ref __result, "cropDropRate", "seedYieldMul");
            }
        }

        [HarmonyPatch(typeof(Block), nameof(Block.GetDrops), [typeof(IWorldAccessor), typeof(BlockPos), typeof(IPlayer), typeof(float)])]
        private static class LogYield
        {
            private static void Postfix(Block __instance, IWorldAccessor __0, IPlayer? __2, ref ItemStack[] __result)
            {
                if (__0.Side == EnumAppSide.Server && __2?.Entity is { } player && __instance is BlockLog && __instance.Variant["type"] == "grown")
                {
                    ScaleDrops(__0, player, ref __result, "logYieldMul");
                }
            }
        }

        private static void ScaleDrops(IWorldAccessor world, EntityPlayer player, ref ItemStack[] drops,
            string stat, string? seedStat = null)
        {
            if (drops is null || drops.Length == 0)
            {
                return;
            }

            float factor = Multiplier(player, stat);
            float seedFactor = seedStat is null ? factor : Multiplier(player, seedStat);
            if (factor == 1f && seedFactor == 1f)
            {
                return;
            }

            List<ItemStack> result = new(drops.Length);
            foreach (ItemStack stack in drops)
            {
                if (stack is null)
                {
                    continue;
                }

                float scale = stack.Item is ItemPlantableSeed ? seedFactor : factor;
                int count = GameMath.RoundRandom(world.Rand, stack.StackSize * scale);
                int maximum = Math.Max(1, stack.Collectible.MaxStackSize);
                while (count > 0)
                {
                    ItemStack copy = stack.Clone();
                    copy.StackSize = Math.Min(maximum, count);
                    result.Add(copy);
                    count -= copy.StackSize;
                }
            }

            drops = [.. result];
        }

        [HarmonyPatch(typeof(EntityBehaviorMilkable), nameof(EntityBehaviorMilkable.MilkingComplete))]
        private static class MilkYield
        {
            private static void Prefix(EntityBehaviorMilkable __instance, EntityAgent __1, ref float ___yieldLitres, out float __state)
            {
                __state = ___yieldLitres;
                if (__instance.entity.World.Side == EnumAppSide.Server)
                {
                    ___yieldLitres *= Multiplier(__1, "animalHarvestRate");
                }
            }

            private static Exception? Finalizer(ref float ___yieldLitres, float __state, Exception? __exception)
            {
                ___yieldLitres = __state;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(CollectibleBehaviorWearable), nameof(CollectibleBehaviorWearable.OnDamageItem))]
        private static class ClothingWear
        {
            private static void Prefix(CollectibleBehaviorWearable __instance, IWorldAccessor __0, Entity __1, ItemSlot __2, ref int __3)
            {
                if (__0.Side == EnumAppSide.Server && __1 is EntityPlayer player && __3 > 0 && __2.Itemstack is not null && !__instance.IsArmorType(__2))
                {
                    float factor = Multiplier(player, "clothingDurabilityLoss");
                    if (factor != 1f)
                    {
                        __3 = GameMath.RoundRandom(__0.Rand, __3 * factor);
                    }
                }
            }
        }
    }
}
