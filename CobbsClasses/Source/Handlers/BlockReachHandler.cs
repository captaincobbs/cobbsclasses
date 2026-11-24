using CobbsClasses.Source.Behaviors;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace CobbsClasses.Source.Handlers;

public static class BlockReachHandler
{
    public static float GetRange(IServerPlayer player)
    {
        ItemStack? heldItem = player.InventoryManager.ActiveHotbarSlot?.Itemstack;
        ToolTraitBehavior? toolTraits = heldItem?.Collectible.GetCollectibleBehavior<ToolTraitBehavior>(false);

        float globalBonus = player.Entity.Stats.GetBlended("globalReach") - 1f;
        float heldBonus = toolTraits?.GetReachBonus(player.Entity) ?? 0f;
        return GlobalConstants.DefaultPickingRange + globalBonus + heldBonus;
    }

    public static void Refresh(IServerPlayer player)
    {
        if (player.WorldData.CurrentGameMode != EnumGameMode.Survival)
        {
            return;
        }

        float range = GetRange(player);
        if (player.WorldData.PickingRange != range)
        {
            player.WorldData.PickingRange = range;
            player.BroadcastPlayerData();
        }
    }
}