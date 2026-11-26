using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    internal static class BloomeryFuelPatch
    {
        private const string CreditKey = "cobbsclasses:bloomeryFuelCredit";
        private static readonly ConditionalWeakTable<BlockEntityBloomery, FuelCredit> Credits = [];
        private static readonly System.Func<BlockEntityBloomery, ItemSlot> GetFuelSlot = AccessTools.MethodDelegate<System.Func<BlockEntityBloomery, ItemSlot>>(AccessTools.PropertyGetter(typeof(BlockEntityBloomery), "FuelSlot"));

        internal static void Register(Harmony harmony)
        {
            foreach (Type patch in new[] { typeof(LoadFuel), typeof(Ignite), typeof(Save), typeof(Load), typeof(Smelt) })
            {
                harmony.CreateClassProcessor(patch).Patch();
            }
        }

        [HarmonyPatch(typeof(BlockEntityBloomery), nameof(BlockEntityBloomery.TryAdd))]
        private static class LoadFuel
        {
            private static void Prefix(BlockEntityBloomery __instance, out int __state)
            {
                __state = GetFuelSlot(__instance).StackSize;
                if (__state == 0)
                {
                    Credits.Remove(__instance);
                }
            }

            private static void Postfix(BlockEntityBloomery __instance, IPlayer __0, int __state)
            {
                int added = GetFuelSlot(__instance).StackSize - __state;
                float extra = __0.Entity.Stats.GetBlended(TraitAttributes.Crafting.BloomeryFuelEfficiency) - 1f;
                if (added > 0 && extra > 0f && float.IsFinite(extra))
                {
                    Credits.GetOrCreateValue(__instance).Amount += added * (double)extra;
                    __instance.MarkDirty();
                }
            }
        }

        [HarmonyPatch(typeof(BlockEntityBloomery), nameof(BlockEntityBloomery.CanIgnite))]
        private static class Ignite
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> body = [.. instructions];
                MethodInfo getFuelSlot = AccessTools.PropertyGetter(typeof(BlockEntityBloomery), "FuelSlot");
                MethodInfo getStackSize = AccessTools.PropertyGetter(typeof(ItemSlot), nameof(ItemSlot.StackSize));
                int[] matches = [.. Enumerable.Range(0, body.Count - 3).Where(index => body[index].Calls(getFuelSlot) && body[index + 1].Calls(getStackSize) && body[index + 2].opcode == OpCodes.Conv_R4 && body[index + 3].opcode == OpCodes.Div)];
                RequireMatches(matches.Length, 1, "bloomery effective fuel");
                body.InsertRange(matches[0] + 3,
                [
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Ignite), nameof(AddCredit)))
                ]);
                return body;
            }

            private static float AddCredit(float count, BlockEntityBloomery bloomery)
            {
                return Credits.TryGetValue(bloomery, out FuelCredit? credit) ? (float)(count + credit.Amount) : count;
            }
        }

        [HarmonyPatch(typeof(BlockEntityBloomery), nameof(BlockEntityBloomery.ToTreeAttributes))]
        private static class Save
        {
            private static void Postfix(BlockEntityBloomery __instance, ITreeAttribute __0)
            {
                if (GetFuelSlot(__instance).StackSize > 0 && Credits.TryGetValue(__instance, out FuelCredit? credit))
                {
                    __0.SetDouble(CreditKey, credit.Amount);
                }
            }
        }

        [HarmonyPatch(typeof(BlockEntityBloomery), nameof(BlockEntityBloomery.FromTreeAttributes))]
        private static class Load
        {
            private static void Postfix(BlockEntityBloomery __instance, ITreeAttribute __0)
            {
                double amount = __0.GetDouble(CreditKey);
                if (amount > 0d && double.IsFinite(amount))
                {
                    Credits.GetOrCreateValue(__instance).Amount = amount;
                }
                else
                {
                    Credits.Remove(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(BlockEntityBloomery), "DoSmelt")]
        private static class Smelt
        {
            private static void Postfix(BlockEntityBloomery __instance)
            {
                if (GetFuelSlot(__instance).StackSize == 0)
                {
                    Credits.Remove(__instance);
                }
            }
        }

        private sealed class FuelCredit
        {
            public double Amount;
        }
    }
}
