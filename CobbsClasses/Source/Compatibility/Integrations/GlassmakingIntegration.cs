using System;

using HarmonyLib;

using Vintagestory.API.Common;

namespace CobbsClasses.Source.Compatibility.Integrations
{
    internal sealed class GlassmakingIntegration : IModIntegration
    {
        public string ModId => "glassmaking";

        public void Register(ICoreAPI api, Harmony harmony)
        {
            OptionalPatchRegistrar registrar = new(api, harmony, ModId);
            HarmonyMethod speed = new(typeof(GlassmakingIntegration), nameof(AdjustTime));
            foreach (string type in new[] { "GlassMaking.GlassblowingTools.BlowingTool", "GlassMaking.GlassblowingTools.GlassIntakeTool",  "GlassMaking.Items.Behavior.GlasspipeMoldBehavior" })
            {
                registrar.Patch(type, "OnHeldInteractStep", prefix: speed);
                registrar.Patch(type, "OnHeldInteractStop", prefix: speed);
            }
        }

        private static void AdjustTime(ref float __0, EntityAgent __2)
        {
            __0 *= Math.Max(0.05f, __2?.Stats?.GetBlended(TraitAttributes.Crafting.GlassworkingSpeedMul) ?? 1f);
        }
    }
}
