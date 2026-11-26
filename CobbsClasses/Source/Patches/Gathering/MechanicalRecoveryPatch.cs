using HarmonyLib;

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(Entity), nameof(Entity.Die))]
    internal static class MechanicalRecoveryPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getDrops = AccessTools.Method(typeof(Entity), nameof(Entity.GetDrops), [typeof(IWorldAccessor), typeof(BlockPos), typeof(IPlayer)]);
            MethodInfo adjust = AccessTools.Method(typeof(MechanicalRecoveryPatch), nameof(GetDropsWithRecovery));
            return ReplaceCall(instructions, getDrops, adjust, OpCodes.Ldarg_2, "mechanical recovery");
        }

        private static ItemStack[]? GetDropsWithRecovery(Entity target, IWorldAccessor world, BlockPos position, IPlayer? vanillaPlayer, DamageSource? cause)
        {
            ItemStack[]? drops = target.GetDrops(world, position, vanillaPlayer);
            if (world.Side != EnumAppSide.Server || target.Properties.Attributes?["isMechanical"].AsBool() != true || cause?.GetCauseEntity() is not EntityPlayer killer)
            {
                return drops;
            }

            float multiplier = Multiplier(killer, TraitAttributes.Gathering.MechanicalHarvestRate);
            bool recoverComponent = killer.Stats.GetBlended(TraitAttributes.Gathering.CanSalvageMetal) > 1f;
            if (multiplier == 1f && !recoverComponent)
            {
                return drops;
            }

            List<ItemStack> recovered = [];
            foreach (ItemStack drop in drops ?? [])
            {
                int amount = multiplier == 1f ? drop.StackSize : GameMath.RoundRandom(world.Rand, drop.StackSize * multiplier);
                while (amount > 0)
                {
                    ItemStack stack = drop.Clone();
                    stack.StackSize = System.Math.Min(amount, System.Math.Max(1, stack.Collectible.MaxStackSize));
                    recovered.Add(stack);
                    amount -= stack.StackSize;
                }
            }

            if (recoverComponent && world.GetBlock(new AssetLocation("game", "metal-parts")) is { } component)
            {
                recovered.Add(new ItemStack(component));
            }

            return [.. recovered];
        }
    }
}
