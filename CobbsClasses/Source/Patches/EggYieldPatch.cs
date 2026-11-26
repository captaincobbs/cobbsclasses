using System;
using System.Collections.Generic;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Patches
{
    internal static class EggYieldPatch
    {
        private const string LaidKey = "cobbsclasses:laidInHenBox";

        internal static void Register(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(LayEgg)).Patch();
            harmony.CreateClassProcessor(typeof(CollectEgg)).Patch();
        }

        [HarmonyPatch(typeof(BlockEntityHenBox), nameof(BlockEntityHenBox.TryAddEgg))]
        private static class LayEgg
        {
            private static void Postfix(BlockEntityHenBox __instance, ItemStack __0, bool __result)
            {
                if (__result && __instance.Api?.Side == EnumAppSide.Server)
                {
                    __0.Attributes.SetBool(LaidKey, true);
                    __instance.MarkDirty();
                }
            }
        }

        [HarmonyPatch(typeof(BlockEntityHenBox), nameof(BlockEntityHenBox.OnInteract))]
        private static class CollectEgg
        {
            private static void Prefix(BlockEntityHenBox __instance, IWorldAccessor __0, IPlayer __1, BlockSelection __2, out Dictionary<int, ItemStack>? __state)
            {
                __state = null;
                if (__0.Side != EnumAppSide.Server || __1?.Entity is null || !__0.Claims.TryAccess(__1, __2.Position, EnumBlockAccessFlags.Use))
                {
                    return;
                }

                bool placing = __instance.CanPlayerPlaceItem(__1.InventoryManager.ActiveHotbarSlot.Itemstack);
                __state = [];
                for (int index = 0; index < __instance.Inventory.Count; index++)
                {
                    ItemStack? stack = __instance.Inventory[index].Itemstack;
                    if (stack?.Attributes.GetBool(LaidKey) == true)
                    {
                        __state[index] = stack.Clone();
                        if (!placing)
                        {
                            stack.Attributes.RemoveAttribute(LaidKey);
                        }
                    }
                }

                if (placing)
                {
                    __state[-1] = __1.InventoryManager.ActiveHotbarSlot.Itemstack!;
                }
            }

            private static void Postfix(BlockEntityHenBox __instance, IWorldAccessor __0, IPlayer __1, bool __result, Dictionary<int, ItemStack>? __state)
            {
                if (__state is null)
                {
                    return;
                }

                if (__state.ContainsKey(-1))
                {
                    // Eggs put in by a player do not benefit from yield bonuses
                    for (int index = 0; index < __instance.Inventory.Count; index++)
                    {
                        if (!__state.ContainsKey(index))
                        {
                            ItemSlot inserted = __instance.Inventory[index];
                            if (inserted.Itemstack?.Attributes.HasAttribute(LaidKey) == true)
                            {
                                inserted.Itemstack.Attributes.RemoveAttribute(LaidKey);
                                inserted.MarkDirty();
                                __instance.MarkDirty();
                            }
                        }
                    }

                    return;
                }

                float extra = Math.Clamp(__1.Entity.Stats.GetBlended("eggYieldMul") - 1f, 0f, 10f);
                foreach ((int index, ItemStack original) in __state)
                {
                    ItemSlot slot = __instance.Inventory[index];
                    int removed = Math.Max(0, original.StackSize - slot.StackSize);
                    if (!slot.Empty)
                    {
                        slot.Itemstack.Attributes.SetBool(LaidKey, true);
                        slot.MarkDirty();
                    }

                    if (!__result || removed == 0 || extra <= 0f)
                    {
                        continue;
                    }

                    int count = GameMath.RoundRandom(__0.Rand, removed * extra);
                    if (count <= 0)
                    {
                        continue;
                    }

                    ItemStack bonus = original.Clone();
                    bonus.Attributes.RemoveAttribute(LaidKey);
                    bonus.Attributes.RemoveAttribute("chick");
                    bonus.StackSize = count;
                    if (!__1.InventoryManager.TryGiveItemstack(bonus))
                    {
                        __0.SpawnItemEntity(bonus, __instance.Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                }

                __instance.MarkDirty();
            }
        }
    }
}