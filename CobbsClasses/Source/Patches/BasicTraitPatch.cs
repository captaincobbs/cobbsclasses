using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    internal static class BasicTraitPatch
    {
        internal static void Register(Harmony harmony)
        {
            List<Exception> failures = [];
            foreach (Type patch in new[] { typeof(Drying), typeof(Healing), typeof(HealingSpeed), typeof(Watering), typeof(BerryYield), typeof(FruitYield), typeof(HarvestSpeed), typeof(ArrowBreakChance), typeof(Fertilizer), typeof(NutritionDecay), typeof(WormYield), typeof(ManualHarvestSpeed), typeof(ResinYield), typeof(Shield), typeof(Repair) })
            {
                try
                {
                    harmony.CreateClassProcessor(patch).Patch();
                }
                catch (Exception exception)
                {
                    failures.Add(new InvalidOperationException(patch.Name, exception));
                }
            }
            if (failures.Count > 0)
            {
                throw new AggregateException(failures);
            }
        }

        [HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "updateBodyTemperature")]
        private static class Drying
        {
            private static void Prefix(EntityBehaviorBodyTemperature __instance, out float __state) => __state = __instance.Wetness;

            private static void Postfix(EntityBehaviorBodyTemperature __instance, float __state)
            {
                float current = __instance.Wetness;
                if (current < __state && __instance.entity is EntityPlayer player)
                {
                    __instance.Wetness = Math.Clamp(__state - (__state - current) * Multiplier(player, "dryingSpeedMul"), 0f, 1f);
                }
            }
        }

        [HarmonyPatch(typeof(CollectibleBehaviorHealingItem), nameof(CollectibleBehaviorHealingItem.OnHeldInteractStop))]
        private static class Healing
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> body = [.. instructions];
                MethodInfo getter = AccessTools.PropertyGetter(typeof(CollectibleBehaviorHealingItem), "Health");
                int[] matches = [.. Enumerable.Range(0, body.Count).Where(index => body[index].Calls(getter))];
                RequireMatches(matches.Length, 1, "healing item effectiveness");
                body.InsertRange(matches[0] + 1, [new(OpCodes.Ldarg_3), new(OpCodes.Call, AccessTools.Method(typeof(Healing), nameof(Adjust)))]);
                return body;
            }

            private static float Adjust(float health, EntityAgent player) => health * Multiplier(player, "healingItemEffectiveness");
        }

        [HarmonyPatch]
        private static class HealingSpeed
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.DeclaredMethod(typeof(CollectibleBehaviorHealingItem), "OnHeldInteractStep");
                yield return AccessTools.DeclaredMethod(typeof(CollectibleBehaviorHealingItem), "OnHeldInteractStop");
                yield return AccessTools.DeclaredMethod(typeof(CollectibleBehaviorHealingItem), "OnHeldInteractCancel");
            }

            private static void Prefix(ref float __0, EntityAgent __2) => __0 *= Math.Clamp(Multiplier(__2, "healingItemSpeedMul"), 0.05f, 20f);
        }

        [HarmonyPatch(typeof(BlockWateringCan), nameof(BlockWateringCan.OnHeldInteractStep))]
        private static class Watering
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> body = [.. instructions];
                MethodInfo setter = AccessTools.Method(typeof(BlockWateringCan), "SetRemainingWateringSeconds");
                int[] matches = [.. Enumerable.Range(3, body.Count - 3).Where(index => body[index].Calls(setter) && body[index - 1].opcode == OpCodes.Stloc_1 && body[index - 2].opcode == OpCodes.Dup && body[index - 3].opcode == OpCodes.Sub)];
                RequireMatches(matches.Length, 1, "watering consumption");
                body.InsertRange(matches[0] - 3, [new(OpCodes.Ldarg_3), new(OpCodes.Call, AccessTools.Method(typeof(Watering), nameof(Adjust)))]);
                return body;
            }

            private static float Adjust(float amount, EntityAgent player) => amount * Multiplier(player, "wateringConsumption");
        }

        [HarmonyPatch(typeof(BEBehaviorFruitingBush), nameof(BEBehaviorFruitingBush.GetRipeDrops))]
        private static class BerryYield
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => YieldMultiplier(instructions, "berryYieldMul", OpCodes.Ldarg_1);
        }

        [HarmonyPatch(typeof(BlockEntityFruitTreePart), nameof(BlockEntityFruitTreePart.OnBlockInteractStop))]
        private static class FruitYield
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => YieldMultiplier(instructions, "fruitYieldMul", OpCodes.Ldarg_2);
        }

        private static IEnumerable<CodeInstruction> YieldMultiplier(IEnumerable<CodeInstruction> instructions, string stat, OpCode playerArgument)
        {
            List<CodeInstruction> body = [.. instructions];
            MethodInfo roll = AccessTools.Method(typeof(BlockDropItemStack), nameof(BlockDropItemStack.GetNextItemStack), [typeof(float)]);
            int[] matches = [.. Enumerable.Range(0, body.Count).Where(index => body[index].Calls(roll))];
            RequireMatches(matches.Length, 1, stat);
            CodeInstruction first = new(playerArgument);
            first.labels.AddRange(body[matches[0]].labels);
            body[matches[0]].labels.Clear();
            body.InsertRange(matches[0], [first, new(OpCodes.Ldstr, stat), new(OpCodes.Call, AccessTools.Method(typeof(BasicTraitPatch), nameof(AdjustYield)))]);
            return body;
        }

        private static float AdjustYield(float amount, IPlayer? player, string stat) => amount * Multiplier(player?.Entity, stat);

        [HarmonyPatch(typeof(BEBehaviorFruitingBush), nameof(BEBehaviorFruitingBush.GetHarvestDuration))]
        private static class HarvestSpeed
        {
            private static void Postfix(Entity __1, ref float __result) => __result /= Math.Clamp(Multiplier(__1, "harvestSpeedMul"), 0.05f, 20f);
        }

        [HarmonyPatch(typeof(BlockEntitySoilNutrition), nameof(BlockEntitySoilNutrition.OnBlockInteract))]
        private static class Fertilizer
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> body = [.. instructions];
                FieldInfo[] nutrients = [AccessTools.Field(typeof(FertilizerProps), "N"), AccessTools.Field(typeof(FertilizerProps), "P"), AccessTools.Field(typeof(FertilizerProps), "K")];
                RequireMatches(body.Count(instruction => nutrients.Any(field => instruction.LoadsField(field))), 3, "fertilizer efficiency");
                foreach (CodeInstruction instruction in body)
                {
                    yield return instruction;
                    if (nutrients.Any(field => instruction.LoadsField(field)))
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_1);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Fertilizer), nameof(Adjust)));
                    }
                }
            }

            private static float Adjust(float amount, IPlayer player) => amount * Multiplier(player?.Entity, "fertilizerEfficiency");
        }

        [HarmonyPatch(typeof(EntityBehaviorHunger), "ReduceSaturation")]
        private static class NutritionDecay
        {
            private static void Prefix(EntityBehaviorHunger __instance, out float[]? __state)
            {
                __state = null;
                if (Multiplier(__instance.entity, "nutritionDecayRate") != 1f)
                {
                    __state = [__instance.FruitLevel, __instance.VegetableLevel, __instance.ProteinLevel, __instance.GrainLevel, __instance.DairyLevel];
                }
            }

            private static void Postfix(EntityBehaviorHunger __instance, float[]? __state)
            {
                if (__state is null)
                {
                    return;
                }

                float factor = Multiplier(__instance.entity, "nutritionDecayRate");
                __instance.FruitLevel = Scale(__state[0], __instance.FruitLevel, factor);
                __instance.VegetableLevel = Scale(__state[1], __instance.VegetableLevel, factor);
                __instance.ProteinLevel = Scale(__state[2], __instance.ProteinLevel, factor);
                __instance.GrainLevel = Scale(__state[3], __instance.GrainLevel, factor);
                __instance.DairyLevel = Scale(__state[4], __instance.DairyLevel, factor);
            }

            private static float Scale(float previous, float current, float factor) => current < previous ? Math.Max(0f, previous - (previous - current) * factor) : current;
        }

        [HarmonyPatch(typeof(ItemWormGrunter), "spawnWorm")]
        private static class WormYield
        {
            private static void Prefix(EntityAgent __1, ref int __3)
            {
                if (__1.World.Side == EnumAppSide.Server && __3 > 0)
                {
                    __3 = Vintagestory.API.MathTools.GameMath.RoundRandom(__1.World.Rand, __3 * Multiplier(__1, "wormYieldMul"));
                }
            }
        }

        [HarmonyPatch]
        private static class ManualHarvestSpeed
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.DeclaredMethod(typeof(BlockBehaviorHarvestable), "OnBlockInteractStep");
                yield return AccessTools.DeclaredMethod(typeof(BlockBehaviorHarvestable), "OnBlockInteractStop");
            }

            private static void Prefix(ref float __0, IPlayer __2)
                => __0 *= Math.Clamp(Multiplier(__2?.Entity, "harvestSpeedMul"), 0.05f, 20f);
        }

        [HarmonyPatch(typeof(BlockBehaviorHarvestable), nameof(BlockBehaviorHarvestable.OnBlockInteractStop))]
        private static class ResinYield
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> body = [.. instructions];
                int[] matches = [.. Enumerable.Range(0, body.Count).Where(index => body[index].opcode == OpCodes.Stfld && body[index].operand is FieldInfo field && field.Name == "dropRate")];
                RequireMatches(matches.Length, 2, "manual harvest yield");
                foreach (int index in matches.Take(1))
                {
                    body.InsertRange(index, [new(OpCodes.Ldarg_2), new(OpCodes.Ldarg_3), new(OpCodes.Ldarg_S, (byte)4), new(OpCodes.Call, AccessTools.Method(typeof(ResinYield), nameof(Adjust)))]);
                }
                return body;
            }

            private static float Adjust(float amount, IWorldAccessor world, IPlayer player, BlockSelection selection)
            {
                Block block = world.BlockAccessor.GetBlock(selection.Position);
                if (block.Code.Domain == "game" && block.Code.Path.StartsWith("log-resin-", StringComparison.Ordinal))
                {
                    return amount * Multiplier(player?.Entity, "resinYieldMul");
                }

                return amount;
            }
        }

        [HarmonyPatch(typeof(ModSystemWearableStats), "applyShieldProtection")]
        private static class Shield
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> body = [.. instructions];
                MethodInfo getter = AccessTools.Method(typeof(Vintagestory.API.Datastructures.JsonObject), "AsFloat", [typeof(float)]);
                int[] matches = [.. Enumerable.Range(1, body.Count - 1).Where(index => body[index - 1].Calls(getter) && body[index].opcode == OpCodes.Stloc_S && body[index].operand is LocalBuilder local && (local.LocalIndex == 8 || local.LocalIndex == 9))];
                RequireMatches(matches.Length, 4, "shield protection");
                foreach (int index in matches.Reverse())
                {
                    int local = ((LocalBuilder)body[index].operand).LocalIndex;
                    body.InsertRange(index, [new(OpCodes.Ldarg_1), new(OpCodes.Ldc_I4, local), new(OpCodes.Call, AccessTools.Method(typeof(Shield), nameof(Adjust)))]);
                }
                return body;
            }

            private static float Adjust(float amount, IPlayer player, int local)
            {
                return local == 9 ? Math.Clamp(amount * Multiplier(player.Entity, "shieldBlockChance"), 0f, 1f) : Math.Max(0f, amount * Multiplier(player.Entity, "shieldProtection"));
            }
        }

        [HarmonyPatch(typeof(CollectibleBehaviorWearable), nameof(CollectibleBehaviorWearable.CalculateRepairValue))]
        private static class Repair
        {
            private static void Postfix(CollectibleBehaviorWearable __instance, ItemSlot[] __0, ItemSlot __1, ref float __2)
            {
                IPlayer? player = (__1.Inventory as InventoryBasePlayer)?.Player;
                player ??= __0.Select(slot => (slot?.Inventory as InventoryBasePlayer)?.Player).FirstOrDefault(owner => owner is not null);

                if (player?.Entity is not { } entity || __2 <= 0f)
                {
                    return;
                }

                __2 *= Multiplier(entity, "repairEfficiency");
                if (!__instance.IsArmorType(__1))
                {
                    __2 *= Multiplier(entity, "clothingRepairEfficiency");
                }
            }
        }

        [HarmonyPatch(typeof(EntityProjectileBase), nameof(EntityProjectileBase.SpawnProjectile))]
        private static class ArrowBreakChance
        {
            private static void Prefix(Entity __0, EntityAgent __1)
            {
                if (__0 is EntityProjectileBase projectile && __1 is EntityPlayer player && player.World.Side == EnumAppSide.Server && projectile.ProjectileStack?.Collectible is ItemArrow)
                {
                    float change = player.Stats.GetBlended("arrowBreakChance") - 1f;
                    projectile.DropOnImpactChance = 1f - Math.Clamp((1f - projectile.DropOnImpactChance) * (1f + change), 0f, 1f);
                }
            }
        }
    }
}








