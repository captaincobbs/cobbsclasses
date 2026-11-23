using CobbsClasses.Source.CollectibleBehaviors;
using CobbsClasses.Source.Renderers;
using CobbsClasses.Source.Systems;

using System.Linq;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace CobbsClasses.Source;

public class CobbsClassesModSystem : ModSystem
{
    public override double ExecuteOrder() => 0.6;

    private DarkvisionRenderer? darkvisionRenderer;

    public override void StartClientSide(ICoreClientAPI api)
    {
        darkvisionRenderer = new(api);
        api.Event.RegisterRenderer(darkvisionRenderer, EnumRenderStage.Before, "cobbsclasses-darkvision");
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        api.Event.PlayerReady += BlockReachHandler.UpdateBlockReach;
        api.Event.PlayerRespawn += BlockReachHandler.UpdateBlockReach;
        api.Event.PlayerSwitchGameMode += BlockReachHandler.UpdateBlockReach;
        api.Event.AfterActiveSlotChanged += (player, _) => BlockReachHandler.UpdateBlockReach(player);
    }

    public override void Start(ICoreAPI api)
    {
        api.RegisterCollectibleBehaviorClass("CobbsToolTraits", typeof(ToolTraitBehavior));
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        if (api.Side != EnumAppSide.Server)
        {
            return;
        }

        TagSet toolTag = api.CollectibleTagRegistry.CreateTagSet(["tool"]);
        TagSet weaponTag = api.CollectibleTagRegistry.CreateTagSet(["weapon"]);

        foreach (Item item in api.World.Items)
        {
            if (item?.Code is null || item.CollectibleBehaviors.OfType<ToolTraitBehavior>().Any())
            {
                continue;
            }

            if (!(item.Tool.HasValue || toolTag.IsFullyContainedIn(item.Tags) || weaponTag.IsFullyContainedIn(item.Tags)))
            {
                continue;
            }

            ToolTraitBehavior behavior = new(item);
            behavior.Initialize(JsonObject.FromJson("{}"));
            item.CollectibleBehaviors = [.. item.CollectibleBehaviors, behavior];
        }
    }

    public override void Dispose()
    {
        darkvisionRenderer?.Dispose();
        base.Dispose();
    }
}
