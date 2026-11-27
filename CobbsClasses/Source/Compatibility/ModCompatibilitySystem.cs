using System;

using CobbsClasses.Source.Compatibility.Integrations;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace CobbsClasses.Source.Compatibility;

public sealed class ModCompatibilitySystem : ModSystem
{
    private const string HarmonyOwner = "cobbsclasses.compatibility";
    private static readonly object PatchLock = new();
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
        new FootprintsIntegration()
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
            return;
        }

        try
        {
            integration.Register(api, harmony!);
        }
        catch (Exception exception)
        {
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