using System;

using HarmonyLib;

using Vintagestory.API.Common;

namespace CobbsClasses.Source.Compatibility.Integrations;

internal sealed class FibercraftIntegration : IModIntegration
{
    public string ModId => "spinningwheel";

    public void Register(ICoreAPI api, Harmony harmony)
    {
        OptionalPatchRegistrar registrar = new(api, harmony, ModId);
        registrar.Patch("SpinningWheel.BlockEntities.BlockEntitySpinningWheel", "OnSpinTick", prefix: new HarmonyMethod(typeof(FibercraftIntegration), nameof(SpinWheel)));
        registrar.Patch("SpinningWheel.BlockEntities.BlockEntityFlyShuttleLoom", "OnWeaveTick", prefix: new HarmonyMethod(typeof(FibercraftIntegration), nameof(Weave)));
        HarmonyMethod spindle = new(typeof(FibercraftIntegration), nameof(SpinSpindle));
        registrar.Patch("SpinningWheel.Items.ItemDropSpindle", "OnHeldInteractStep", prefix: spindle);
        registrar.Patch("SpinningWheel.Items.ItemDropSpindle", "OnHeldInteractStop", prefix: spindle);
    }

    private static float Speed(EntityAgent? player, string stat)
    {
        return Math.Max(0.05f, player?.Stats?.GetBlended(stat) ?? 1f);
    }

    private static void SpinWheel(ref float __0, EntityAgent? ___MountedBy)
    {
        __0 *= Speed(___MountedBy, TraitAttributes.Crafting.SpinningWheelSpeedMul);
    }

    private static void Weave(ref float __0, EntityAgent? ___MountedBy)
    {
        __0 *= Speed(___MountedBy, TraitAttributes.Crafting.LoomSpeedMul);
    }

    private static void SpinSpindle(ref float __0, EntityAgent __2)
    {
        __0 *= Speed(__2, TraitAttributes.Crafting.SpindleSpeedMul);
    }
}
