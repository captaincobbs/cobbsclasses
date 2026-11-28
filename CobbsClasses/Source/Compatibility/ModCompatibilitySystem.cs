using System;
using System.Collections.Generic;
using System.Threading;

using CobbsClasses.Source.API;
using CobbsClasses.Source.Compatibility.Integrations;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace CobbsClasses.Source.Compatibility
{
    public sealed class ModCompatibilitySystem : ModSystem
    {
        private const string HarmonyOwner = "cobbsclasses.compatibility";
        private static readonly Lock PatchLock = new();
        private static int activeSystems;
        private Harmony? harmony;

        private readonly IModIntegration[] serverIntegrations =
        [
            new GoldenCombsIntegration(),
            new CulinaryArtilleryIntegration()
        ];

        private readonly IModIntegration[] sharedIntegrations =
        [
            new ButcheringIntegration(),
            new GlassmakingIntegration(),
            new FibercraftIntegration(),
            new FootprintsIntegration(),
            new WoodworkingIntegration(),
            new StoneQuarryIntegration(),
            new FishingIntegration(),
            new TailorsDelightIntegration(),
            new AlchemyIntegration(),
            new ForagersGambleIntegration()
        ];

        public override void StartClientSide(ICoreClientAPI api)
        {
            RegisterIntegrations(api);
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Server)
            {
                RegisterIntegrations(api);
            }
        }

        private void RegisterIntegrations(ICoreAPI api)
        {
            lock (PatchLock)
            {
                if (harmony is not null)
                {
                    return;
                }

                harmony = new Harmony(HarmonyOwner);
                activeSystems++;
                foreach (IModIntegration integration in sharedIntegrations)
                {
                    Register(api, integration);
                }

                IEnumerable<KeyValuePair<string, Func<IModIntegration>>>? extensions = CobbsClassesAPI.For(api)?.Integrations();
                if (extensions is not null)
                {
                    foreach (KeyValuePair<string, Func<IModIntegration>> extension in extensions)
                    {
                        try
                        {
                            if (extension.Value() is IModIntegration integration)
                            {
                                Register(api, integration);
                            }
                        }
                        catch (Exception exception)
                        {
                            CobbsClassesAPI.For(api)?.Record(new PatchDiagnostic(extension.Key, "factory", PatchStatus.Failed, exception.Message));
                        }
                    }
                }

                if (api.Side == EnumAppSide.Server)
                {
                    foreach (IModIntegration integration in serverIntegrations)
                    {
                        Register(api, integration);
                    }
                }
            }
        }

        private void Register(ICoreAPI api, IModIntegration integration)
        {
            if (!api.ModLoader.IsModEnabled(integration.ModId))
            {
                CobbsClassesAPI.For(api)?.Record(new PatchDiagnostic(integration.ModId, "integration", PatchStatus.NotRequired));
                return;
            }

            try
            {
                integration.Register(api, harmony!);
            }
            catch (Exception exception)
            {
                CobbsClassesAPI.For(api)?.Record(new PatchDiagnostic(integration.ModId, "integration", PatchStatus.Failed, exception.Message));
                api.Logger.Warning($"CobbsClasses integration for {integration.ModId} could not load: {exception}");
            }
        }

        public override void Dispose()
        {
            lock (PatchLock)
            {
                if (harmony is not null)
                {
                    activeSystems--;
                    if (activeSystems == 0)
                    {
                        harmony.UnpatchAll(HarmonyOwner);
                    }

                    harmony = null;
                }
            }

            base.Dispose();
        }
    }
}