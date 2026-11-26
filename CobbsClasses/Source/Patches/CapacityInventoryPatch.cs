using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    internal static class CapacityInventoryPatch
    {
        private const string CapacityKey = "cobbsclasses:bagCapacity";
        private const string StatKey = "cobbsclasses:bagCapacityStat";
        private const string Personal = "backpackInventoryMultiplier";
        private const string Mount = "animalInventoryMultiplier";
        private const string Vehicle = "vehicleInventoryMultiplier";
        private static readonly ConditionalWeakTable<ItemSlot, Overflow> RestrictedSlots = [];
        private static readonly ConditionalWeakTable<ItemSlot, CursorOrigin> CursorOrigins = [];
        [ThreadStatic]
        private static IPlayer? actingPlayer;

        internal static void Register(Harmony harmony)
        {
            foreach (Type patch in new[] { typeof(OpenAttachedBag), typeof(RefreshAttachedSlots), typeof(BuildBagSlots), typeof(ActivateSlot), typeof(MoveInto), typeof(CanTakeFrom), typeof(Swap), typeof(NetworkSwap) })
            {
                harmony.CreateClassProcessor(patch).Patch();
            }
        }

        private static void SetCapacity(ItemStack bag, string stat, EntityPlayer player, int normal)
        {
            float factor = Multiplier(player, stat);
            if (!float.IsFinite(factor) || factor <= 1f || normal <= 0)
            {
                return;
            }

            int expanded = GetCapacity(normal, factor, stat == Personal);
            if (expanded > normal && expanded >= bag.Attributes.GetInt(CapacityKey))
            {
                bag.Attributes.SetInt(CapacityKey, expanded);
                bag.Attributes.SetString(StatKey, stat);
            }
        }

        private static int GetCapacity(int normal, float factor, bool roundUp)
        {
            if (!float.IsFinite(factor) || factor <= 1f)
            {
                return normal;
            }

            double bonus = normal * (double)(factor - 1f);
            return normal + (int)Math.Clamp(roundUp ? Math.Ceiling(bonus - 0.00001d) : Math.Floor(bonus + 0.00001d), 0d, 64d);
        }

        [HarmonyPatch(typeof(AttachedContainerWorkspace), nameof(AttachedContainerWorkspace.OnInteract))]
        private static class OpenAttachedBag
        {
            private static void Prefix(ItemSlot __0, Entity __2, EntityAgent __3)
            {
                if (__3 is not EntityPlayer player || __0.Itemstack is not { } bag
                    || bag.Collectible.GetCollectibleInterface<IHeldBag>() is not { } heldBag)
                {
                    return;
                }

                string stat;
                if (__2 is EntityBoat)
                {
                    stat = Vehicle;
                }
                else if (__2 is EntityAgent && __2 is not EntityPlayer && __2.GetBehavior<EntityBehaviorAttachable>() is not null)
                {
                    stat = Mount;
                }
                else
                {
                    return;
                }

                int old = bag.Attributes.GetInt(CapacityKey);
                string? oldStat = bag.Attributes.GetString(StatKey);
                int normal = heldBag.GetQuantitySlots(bag);
                SetCapacity(bag, stat, player, normal);
                if (bag.Attributes.GetInt(CapacityKey) > normal)
                {
                    bag.Attributes.SetString(StatKey, stat);
                }
                if (old != bag.Attributes.GetInt(CapacityKey) || oldStat != bag.Attributes.GetString(StatKey))
                {
                    __0.MarkDirty();
                }
            }
        }

        [HarmonyPatch(typeof(AttachedContainerWorkspace), "TryLoadInv")]
        private static class RefreshAttachedSlots
        {
            private static void Postfix(AttachedContainerWorkspace __instance, bool __result)
            {
                if (!__result || __instance.WrapperInv is not { } inventory)
                {
                    return;
                }

                BagInventory bags = __instance.BagInventory;
                if (bags.Count > inventory.Count)
                {
                    inventory.AddSlots(bags.Count - inventory.Count);
                }

                for (int index = 0; index < Math.Min(inventory.Count, bags.Count); index++)
                {
                    inventory[index] = bags[index];
                }
            }
        }

        [HarmonyPatch(typeof(CollectibleBehaviorHeldBag), nameof(CollectibleBehaviorHeldBag.GetOrCreateSlots))]
        private static class BuildBagSlots
        {
            private static void Postfix(CollectibleBehaviorHeldBag __instance, ItemStack __0, InventoryBase __1, int __2, List<ItemSlotBagContent> __result)
            {
                int normal = __instance.GetQuantitySlots(__0);
                if (__1 is InventoryBasePlayer personal && personal.Api is not null && personal.Player?.Entity is { } owner)
                {
                    SetCapacity(__0, Personal, owner, normal);
                }

                int expanded = Math.Clamp(__0.Attributes.GetInt(CapacityKey), normal, normal + 64);
                string? storedStat = __0.Attributes.GetString(StatKey);
                string? stat = __1 is InventoryBasePlayer ? Personal : storedStat;
                if (stat is not (Personal or Mount or Vehicle) || __result.Count == 0)
                {
                    return;
                }

                ItemSlotBagContent template = __result[0];
                while (__result.Count < expanded)
                {
                    __result.Add(new ItemSlotBagContent(__1, __2, __result.Count, template.StorageType)
                    {
                        CanStoreTags = template.CanStoreTags
                    });
                }

                for (int index = normal; index < __result.Count; index++)
                {
                    ItemSlot slot = __result[index];
                    RestrictedSlots.Remove(slot);
                    RestrictedSlots.Add(slot, new Overflow(stat, normal, index));
                    slot.HexBackgroundColor = "#747474";
                }
            }
        }

        private static bool MayDeposit(ItemSlot target, ItemSlot source)
        {
            if (source.Empty || !RestrictedSlots.TryGetValue(target, out Overflow? restriction))
            {
                return true;
            }

            if ((target.Inventory is not null && ReferenceEquals(target.Inventory, source.Inventory)) || (CursorOrigins.TryGetValue(source, out CursorOrigin? origin) && ReferenceEquals(origin.Stack, source.Itemstack) && ReferenceEquals(origin.Inventory, target.Inventory)))
            {
                return true;
            }

            IPlayer? actor = actingPlayer;
            if (restriction.Stat == Personal && actor is null && target.Inventory is InventoryBasePlayer personal)
            {
                actor = personal.Player;
            }
            return actor?.Entity is { } player && restriction.Index < GetCapacity(restriction.Normal, Multiplier(player, restriction.Stat), restriction.Stat == Personal);
        }

        [HarmonyPatch(typeof(InventoryBase), nameof(InventoryBase.ActivateSlot))]
        private static class ActivateSlot
        {
            private static void Prefix(InventoryBase __instance, int __0, ItemSlot __1, ItemStackMoveOperation __2, out Interaction __state)
            {
                __state = new Interaction(actingPlayer, __1.Itemstack, __0 >= 0 && __0 < __instance.Count ? __instance[__0].Itemstack : null, __1.StackSize);
                actingPlayer = __2.ActingPlayer ?? actingPlayer;
            }

            private static void Postfix(InventoryBase __instance, ItemSlot __1, Interaction __state)
            {
                if (__1.Itemstack is { } stack && __state.Slot is { } previous && (!ReferenceEquals(stack, __state.Cursor) || __1.StackSize > __state.Count) && stack.Collectible == previous.Collectible)
                {
                    bool mixedWithOutsideItems = ReferenceEquals(stack, __state.Cursor) && (!CursorOrigins.TryGetValue(__1, out CursorOrigin? origin) || !ReferenceEquals(origin.Inventory, __instance));
                    CursorOrigins.Remove(__1);
                    if (!mixedWithOutsideItems)
                    {
                        CursorOrigins.Add(__1, new CursorOrigin(__instance, stack));
                    }
                }
                else if (!ReferenceEquals(__state.Cursor, __1.Itemstack))
                {
                    CursorOrigins.Remove(__1);
                }
            }

            private static void Finalizer(Interaction __state)
            {
                actingPlayer = __state.Player;
            }
        }

        [HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.TryPutInto), [typeof(ItemSlot), typeof(ItemStackMoveOperation)], [ArgumentType.Normal, ArgumentType.Ref])]
        private static class MoveInto
        {
            private static bool Prefix(ItemSlot __instance, ItemSlot __0, ItemStackMoveOperation __1, out IPlayer? __state)
            {
                __state = actingPlayer;
                actingPlayer = __1.ActingPlayer ?? actingPlayer;
                return MayDeposit(__0, __instance);
            }

            private static void Finalizer(IPlayer? __state)
            {
                actingPlayer = __state;
            }
        }

        [HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.CanTakeFrom))]
        private static class CanTakeFrom
        {
            private static bool Prefix(ItemSlot __instance, ItemSlot __0) => MayDeposit(__instance, __0);
        }

        [HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.TryFlipWith))]
        private static class Swap
        {
            private static bool Prefix(ItemSlot __instance, ItemSlot __0) => MayDeposit(__instance, __0) && MayDeposit(__0, __instance);
        }

        [HarmonyPatch(typeof(InventoryBase), nameof(InventoryBase.TryFlipItemStack))]
        private static class NetworkSwap
        {
            private static void Prefix(IPlayer __0, out IPlayer? __state)
            {
                __state = actingPlayer;
                actingPlayer = __0 ?? actingPlayer;
            }

            private static void Finalizer(IPlayer? __state)
            {
                actingPlayer = __state;
            }
        }

        private sealed record Overflow(string Stat, int Normal, int Index);
        private sealed record CursorOrigin(InventoryBase Inventory, ItemStack Stack);
        private readonly record struct Interaction(IPlayer? Player, ItemStack? Cursor, ItemStack? Slot, int Count);
    }
}