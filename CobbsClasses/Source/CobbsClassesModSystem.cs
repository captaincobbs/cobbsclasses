using System.Linq;

using CobbsClasses.Source.Behaviors;
using CobbsClasses.Source.Handlers;
using CobbsClasses.Source.Renderers;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace CobbsClasses.Source
{
    public class CobbsClassesModSystem : ModSystem
    {
        public API.CobbsClassesAPI ClassesAPI { get; } = new();

        public override double ExecuteOrder() => 0.6;

        private DarkvisionRenderer? darkvisionRenderer;
        private ICoreServerAPI? serverAPI;

        public override void StartClientSide(ICoreClientAPI api)
        {
            darkvisionRenderer = new(api);
            api.Event.RegisterRenderer(darkvisionRenderer, EnumRenderStage.Before, "cobbsclasses-darkvision");
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverAPI = api;
            api.Event.PlayerReady += PlayerStatHandler.Refresh;
            api.Event.PlayerRespawn += PlayerStatHandler.Refresh;
            api.Event.PlayerSwitchGameMode += PlayerStatHandler.Refresh;
            api.Event.AfterActiveSlotChanged += OnActiveSlotChanged;
        }

        private static void OnActiveSlotChanged(IServerPlayer player, ActiveSlotChangeEventArgs slot) => PlayerStatHandler.Refresh(player);

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
                bool isWeapon = weaponTag.IsFullyContainedIn(item.Tags);
                bool isTool = toolTag.IsFullyContainedIn(item.Tags) || (item.Tool.HasValue && !isWeapon);
                behavior.Initialize(JsonObject.FromJson("{}"));
                behavior.ConfigureDurabilityCategories(isTool, isWeapon);
                item.CollectibleBehaviors = [.. item.CollectibleBehaviors, behavior];
            }
        }

        public override void Dispose()
        {
            if (serverAPI is not null)
            {
                serverAPI.Event.PlayerReady -= PlayerStatHandler.Refresh;
                serverAPI.Event.PlayerRespawn -= PlayerStatHandler.Refresh;
                serverAPI.Event.PlayerSwitchGameMode -= PlayerStatHandler.Refresh;
                serverAPI.Event.AfterActiveSlotChanged -= OnActiveSlotChanged;
                serverAPI = null;
            }
            darkvisionRenderer?.Dispose();
            darkvisionRenderer = null;
            base.Dispose();
        }
    }
}