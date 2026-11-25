using HarmonyLib;

using Vintagestory.API.Common;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(InventoryBase), "DropSlotIfHot")]
    internal static class HotItemPatch
    {
        private static bool Prefix(ItemSlot __0, IPlayer? __1)
        {
            string stat = __0.Inventory?.ClassName == "craftinggrid" ? "canCraftHotItems" : "canHoldHotItems";
            return (__1?.Entity?.Stats.GetBlended(stat) ?? 1f) <= 1f;
        }
    }
}
