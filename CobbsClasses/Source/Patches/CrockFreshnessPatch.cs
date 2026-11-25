using HarmonyLib;

using System;
using System.Runtime.CompilerServices;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Patches
{
    internal static class CrockFreshnessPatch
    {
        private const string Key = "cobbsclasses:crockFreshness";
        private static readonly ConditionalWeakTable<BlockEntityCrock, Freshness> Placed = [];

        internal static void Register(Harmony harmony)
        {
            foreach (Type patch in new[] { typeof(MergeSeal), typeof(ContainedSeal), typeof(ContainedRate), typeof(PlacedRate), typeof(Placement), typeof(Pickup), typeof(Save), typeof(Load), typeof(Unseal) })
            {
                harmony.CreateClassProcessor(patch).Patch();
            }
        }

        private static void Apply(ItemSlot slot, IPlayer? player, bool wasSealed)
        {
            if (wasSealed || player?.Entity is not { } entity || entity.World.Side != EnumAppSide.Server || slot.Itemstack is not { } stack || !IsSealed(stack))
            {
                return;
            }

            stack.Attributes.RemoveAttribute(Key);
            float factor = entity.Stats.GetBlended("crockFreshness");
            if (float.IsFinite(factor) && factor > 1f)
            {
                stack.Attributes.SetFloat(Key, factor);
            }

            slot.MarkDirty();
        }

        private static bool IsSealed(ItemStack? stack) => stack?.Attributes.GetBool("sealed") == true;

        private static void SetPlaced(BlockEntityCrock crock, float factor)
        {
            if (crock.Sealed && factor > 1f && float.IsFinite(factor))
            {
                Placed.GetOrCreateValue(crock).Factor = factor;
            }
            else
            {
                Placed.Remove(crock);
            }
        }

        [HarmonyPatch(typeof(BlockCrock), nameof(BlockCrock.TryMergeStacks))]
        private static class MergeSeal
        {
            private static void Prefix(ItemStackMergeOperation __0, out bool __state)
            {
                __state = IsSealed(__0.SinkSlot.Itemstack);
            }

            private static void Postfix(ItemStackMergeOperation __0, bool __state)
            {
                if (__0.MovedQuantity > 0)
                {
                    Apply(__0.SinkSlot, __0.ActingPlayer, __state);
                }
            }
        }

        [HarmonyPatch(typeof(BlockCrock), nameof(BlockCrock.OnContainedInteractStart))]
        private static class ContainedSeal
        {
            private static void Prefix(ItemSlot __1, out bool __state)
            {
                __state = IsSealed(__1.Itemstack);
            }

            private static void Postfix(BlockEntityContainer __0, ItemSlot __1, IPlayer __2, bool __result, bool __state)
            {
                if (__result)
                {
                    Apply(__1, __2, __state);
                    if (!__state && IsSealed(__1.Itemstack) && __2.Entity.World.Side == EnumAppSide.Server)
                    {
                        __0.MarkDirty();
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BlockCrock), "GetContainingTransitionModifierContained")]
        private static class ContainedRate
        {
            private static void Postfix(ItemSlot __1, EnumTransitionType __2, ref float __result)
            {
                if (__2 == EnumTransitionType.Perish && __1?.Itemstack is { } stack && IsSealed(stack))
                {
                    float factor = stack.Attributes.GetFloat(Key, 1f);
                    if (float.IsFinite(factor) && factor > 1f)
                    {
                        __result /= factor;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BlockCrock), "GetContainingTransitionModifierPlaced")]
        private static class PlacedRate
        {
            private static void Postfix(IWorldAccessor __0, Vintagestory.API.MathTools.BlockPos __1,
                EnumTransitionType __2, ref float __result)
            {
                if (__2 == EnumTransitionType.Perish && __0?.BlockAccessor.GetBlockEntity(__1) is BlockEntityCrock { Sealed: true } crock && Placed.TryGetValue(crock, out Freshness? bonus))
                {
                    __result /= bonus.Factor;
                }
            }
        }

        [HarmonyPatch(typeof(BlockEntityCrock), nameof(BlockEntityCrock.OnBlockPlaced))]
        private static class Placement
        {
            private static void Postfix(BlockEntityCrock __instance, ItemStack? __0)
            {
                SetPlaced(__instance, __0?.Attributes.GetFloat(Key, 1f) ?? 1f);
            }
        }

        [HarmonyPatch(typeof(BlockCrock), nameof(BlockCrock.OnPickBlock))]
        private static class Pickup
        {
            private static void Postfix(IWorldAccessor __0, Vintagestory.API.MathTools.BlockPos __1, ItemStack? __result)
            {
                if (__result is not null && __0?.BlockAccessor.GetBlockEntity(__1) is BlockEntityCrock { Sealed: true } crock && Placed.TryGetValue(crock, out Freshness? bonus))
                {
                    __result.Attributes.SetFloat(Key, bonus.Factor);
                }
            }
        }

        [HarmonyPatch(typeof(BlockEntityCrock), nameof(BlockEntityCrock.ToTreeAttributes))]
        private static class Save
        {
            private static void Postfix(BlockEntityCrock __instance, ITreeAttribute __0)
            {
                if (__instance.Sealed && Placed.TryGetValue(__instance, out Freshness? bonus))
                {
                    __0.SetFloat(Key, bonus.Factor);
                }
            }
        }

        [HarmonyPatch(typeof(BlockEntityCrock), nameof(BlockEntityCrock.FromTreeAttributes))]
        private static class Load
        {
            private static void Postfix(BlockEntityCrock __instance, ITreeAttribute __0)
            {
                SetPlaced(__instance, __0.GetFloat(Key, 1f));
            }
        }

        [HarmonyPatch(typeof(BlockEntityCrock), nameof(BlockEntityCrock.Sealed), MethodType.Setter)]
        private static class Unseal
        {
            private static void Postfix(BlockEntityCrock __instance, bool __0)
            {
                if (!__0)
                {
                    Placed.Remove(__instance);
                }
            }
        }

        private sealed class Freshness
        {
            public float Factor;
        }
    }
}
