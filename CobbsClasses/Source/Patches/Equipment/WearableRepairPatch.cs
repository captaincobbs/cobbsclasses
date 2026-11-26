using System.Linq;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(CollectibleBehaviorWearable), nameof(CollectibleBehaviorWearable.CalculateRepairValue))]
    internal static class WearableRepairPatch
    {
        private static void Postfix(CollectibleBehaviorWearable __instance, ItemSlot[] __0, ItemSlot __1, ref float __2)
        {
            IPlayer? player = (__1.Inventory as InventoryBasePlayer)?.Player;
            player ??= __0.Select(slot => (slot?.Inventory as InventoryBasePlayer)?.Player).FirstOrDefault(owner => owner is not null);

            if (player?.Entity is not { } entity || __2 <= 0f)
            {
                return;
            }

            __2 *= Multiplier(entity, TraitAttributes.Equipment.RepairEfficiency);
            if (!__instance.IsArmorType(__1))
            {
                __2 *= Multiplier(entity, TraitAttributes.Equipment.ClothingRepairEfficiency);
            }
        }
    }
}

