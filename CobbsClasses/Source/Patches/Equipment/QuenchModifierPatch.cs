using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    internal static class QuenchModifierPatch
    {
        private const string DurabilityKey = "cobbsclasses:quenchDurabilityFactor";
        private const string ShatterKey = "cobbsclasses:quenchShatterAdjustment";

        internal static void Register(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(RecordPlacement)).Patch();
            harmony.CreateClassProcessor(typeof(ShatterRoll)).Patch();
            harmony.CreateClassProcessor(typeof(DurabilityGain)).Patch();
            harmony.CreateClassProcessor(typeof(ClearOnSettlement)).Patch();
        }

        [HarmonyPatch(typeof(BlockEntityGroundStorage), "OnPlayerInteractStart")]
        private static class RecordPlacement
        {
            private static void Prefix(BlockEntityGroundStorage __instance, IPlayer __0, out HashSet<ItemStack>? __state)
            {
                __state = null;
                if (__instance.Api?.Side != EnumAppSide.Server || __0.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible.GetBehavior<CollectibleBehaviorQuenchable>() is null)
                {
                    return;
                }

                __state = new HashSet<ItemStack>(ReferenceEqualityComparer.Instance);
                foreach (ItemSlot slot in __instance.Inventory)
                {
                    if (slot.Itemstack is { } stack)
                    {
                        __state.Add(stack);
                    }
                }
            }

            private static void Postfix(BlockEntityGroundStorage __instance, IPlayer __0, bool __result, HashSet<ItemStack>? __state)
            {
                if (!__result || __state is null)
                {
                    return;
                }

                foreach (ItemSlot slot in __instance.Inventory)
                {
                    if (slot.Itemstack is not { } stack || __state.Contains(stack) || stack.Collectible.GetBehavior<CollectibleBehaviorQuenchable>() is null)
                    {
                        continue;
                    }

                    Clear(stack);
                    float durability = Multiplier(__0.Entity, TraitAttributes.Equipment.ExtraTemperingDurability);
                    float shatter = __0.Entity.Stats.GetBlended(TraitAttributes.Equipment.QuenchingShatterChance) - 1f;
                    if (durability != 1f)
                    {
                        stack.Attributes.SetFloat(DurabilityKey, durability);
                    }

                    if (shatter != 0f)
                    {
                        stack.Attributes.SetFloat(ShatterKey, shatter);
                    }

                    slot.MarkDirty();
                    __instance.MarkDirty();
                }
            }
        }

        [HarmonyPatch(typeof(CollectibleBehaviorQuenchable), "IsGettingCooled")]
        private static class ShatterRoll
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> body = [.. instructions];
                MethodInfo chance = AccessTools.Method(typeof(CollectibleBehaviorQuenchable), "GetShatterChance");
                RequireMatches(body.Count(instruction => instruction.Calls(chance)), 1, "quenching shatter roll");
                body.InsertRange(body.FindIndex(instruction => instruction.Calls(chance)) + 1,
                [
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ShatterRoll), nameof(AdjustChance)))
                ]);
                return body;
            }

            private static float AdjustChance(float chance, ItemSlot slot)
            {
                float adjustment = slot.Itemstack?.Attributes.GetFloat(ShatterKey) ?? 0f;
                return adjustment == 0f ? chance : Math.Clamp(chance + adjustment, 0f, 1f);
            }
        }

        [HarmonyPatch(typeof(CollectibleBehaviorQuenchable), "applyQuenchedStats")]
        private static class DurabilityGain
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> body = [.. instructions];
                MethodInfo setBonus = AccessTools.Method(typeof(CollectibleBehaviorQuenchable), "SetDurationBonus");
                RequireMatches(body.Count(instruction => instruction.Calls(setBonus)), 1, "heat treatment durability");
                MethodInfo getBonus = AccessTools.Method(typeof(CollectibleBehaviorQuenchable), "GetDurationBonus");
                int start = body.FindIndex(instruction => instruction.Calls(getBonus));
                int end = body.FindIndex(instruction => instruction.Calls(setBonus));
                int addition = start < 0 ? -1 : body.FindIndex(start + 1, instruction => instruction.opcode == OpCodes.Div);
                if (addition < 0 || addition + 1 >= end || body[addition + 1].opcode != OpCodes.Add)
                {
                    throw new InvalidOperationException("Could not locate the durability increment for this quench.");
                }

                // Scale the new gain before vanilla adds it to the accumulated bonus
                body.InsertRange(addition + 1,
                [
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DurabilityGain), nameof(AdjustBonus)))
                ]);
                return body;
            }

            private static float AdjustBonus(float bonus, ItemStack stack)
            {
                return bonus * stack.Attributes.GetFloat(DurabilityKey, 1f);
            }
        }

        [HarmonyPatch(typeof(CollectibleBehaviorQuenchable), "trySettleWorkItem")]
        private static class ClearOnSettlement
        {
            private static void Postfix(IWorldAccessor __0, ItemStack __1, string __3)
            {
                if (__0.Side == EnumAppSide.Server && __3 != "settled" && __1.Attributes.GetString("metalworkingstate", "settled") == "settled")
                {
                    Clear(__1);
                }
            }
        }

        private static void Clear(ItemStack stack)
        {
            stack.Attributes.RemoveAttribute(DurabilityKey);
            stack.Attributes.RemoveAttribute(ShatterKey);
        }
    }
}
