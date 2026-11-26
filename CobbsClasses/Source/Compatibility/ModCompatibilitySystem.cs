using System;

using CobbsClasses.Source.Compatibility.Integrations;

using HarmonyLib;

using Vintagestory.API.Common;

namespace CobbsClasses.Source.Compatibility
{
    public sealed class ModCompatibilitySystem : ModSystem
    {
        private const string HarmonyOwner = "cobbsclasses.compatibility";
        private Harmony? harmony;

        private readonly IModIntegration[] integrations =
        [
            new GoldenCombsIntegration(),
            new CulinaryArtilleryIntegration()
        ];

        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

        public override void AssetsFinalize(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Server || harmony is not null)
            {
                return;
            }

            harmony = new Harmony(HarmonyOwner);
            foreach (IModIntegration integration in integrations)
            {
                if (!api.ModLoader.IsModEnabled(integration.ModId))
                {
                    continue;
                }

                try
                {
                    integration.Register(api, harmony);
                }
                catch (Exception exception)
                {
                    api.Logger.Warning($"CobbsClasses integration for {integration.ModId} could not load: {exception}");
                }
            }
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyOwner);
            harmony = null;
            base.Dispose();
        }
    }
}