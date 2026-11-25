using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    internal static class ClayRecoveryPatch
    {
        private const string PiecesKey = "cobbsclasses:clayPiecesSpent";
        private static readonly ConditionalWeakTable<BlockEntityClayForm, ClayCost> Costs = [];

        internal static void Register(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(InitialClay)).Patch();
            harmony.CreateClassProcessor(typeof(RefillClay)).Patch();
            harmony.CreateClassProcessor(typeof(SaveClayCost)).Patch();
            harmony.CreateClassProcessor(typeof(LoadClayCost)).Patch();
            harmony.CreateClassProcessor(typeof(RecoverClay)).Patch();
        }

        private static void Record(BlockEntityClayForm form, int pieces)
        {
            if (pieces > 0 && form.Api?.Side == EnumAppSide.Server)
            {
                Costs.GetOrCreateValue(form).Pieces += pieces;
                form.MarkDirty();
            }
        }

        [HarmonyPatch(typeof(BlockEntityClayForm), nameof(BlockEntityClayForm.PutClay))]
        private static class InitialClay
        {
            private static void Prefix(ItemSlot slot, out int __state)
            {
                __state = slot.StackSize;
            }

            private static void Postfix(BlockEntityClayForm __instance, ItemSlot slot, int __state)
            {
                Record(__instance, __state - slot.StackSize);
            }
        }

        [HarmonyPatch(typeof(ItemClay), nameof(ItemClay.OnHeldInteractStop))]
        private static class RefillClay
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo takeOut = AccessTools.Method(typeof(ItemSlot), nameof(ItemSlot.TakeOut), [typeof(int)]);
                MethodInfo record = AccessTools.Method(typeof(RefillClay), nameof(TakeOutAndRecord));
                return ReplaceCall(instructions, takeOut, record, OpCodes.Ldloc_0, "clay refill cost");
            }

            private static ItemStack? TakeOutAndRecord(ItemSlot slot, int amount, BlockEntityClayForm form)
            {
                ItemStack? consumed = slot.TakeOut(amount);
                Record(form, consumed?.StackSize ?? 0);
                return consumed;
            }
        }

        [HarmonyPatch(typeof(BlockEntityClayForm), nameof(BlockEntityClayForm.ToTreeAttributes))]
        private static class SaveClayCost
        {
            private static void Postfix(BlockEntityClayForm __instance, ITreeAttribute __0)
            {
                if (Costs.TryGetValue(__instance, out ClayCost? cost))
                {
                    __0.SetInt(PiecesKey, cost.Pieces);
                }
            }
        }

        [HarmonyPatch(typeof(BlockEntityClayForm), nameof(BlockEntityClayForm.FromTreeAttributes))]
        private static class LoadClayCost
        {
            private static void Postfix(BlockEntityClayForm __instance, ITreeAttribute __0)
            {
                int pieces = __0.GetInt(PiecesKey);
                if (pieces > 0)
                {
                    Costs.GetOrCreateValue(__instance).Pieces = pieces;
                }
                else
                {
                    Costs.Remove(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(BlockClayForm), nameof(BlockClayForm.GetDrops))]
        private static class RecoverClay
        {
            private static void Postfix(IWorldAccessor __0, BlockPos __1, IPlayer? __2, ref ItemStack[] __result)
            {
                if (__0.Side != EnumAppSide.Server || __2?.Entity is not { } player || __2.WorldData.CurrentGameMode != EnumGameMode.Survival || __0.BlockAccessor.GetBlockEntity(__1) is not BlockEntityClayForm form || form.BaseMaterial is not { } clay || !Costs.TryGetValue(form, out ClayCost? cost))
                {
                    return;
                }

                float fraction = Math.Clamp(player.Stats.GetBlended("clayRecovery") - 1f, 0f, 1f);
                if (fraction <= 0f)
                {
                    return;
                }

                int remaining = GameMath.RoundRandom(__0.Rand, cost.Pieces * fraction);
                List<ItemStack> drops = [.. __result];
                while (remaining > 0)
                {
                    ItemStack stack = clay.Clone();
                    stack.StackSize = Math.Min(remaining, Math.Max(1, clay.Collectible.MaxStackSize));
                    drops.Add(stack);
                    remaining -= stack.StackSize;
                }

                __result = [.. drops];
            }
        }

        private sealed class ClayCost
        {
            public int Pieces;
        }
    }
}
