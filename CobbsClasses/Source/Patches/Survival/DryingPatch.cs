using System;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "updateBodyTemperature")]
    internal static class DryingPatch
    {
        private static void Prefix(EntityBehaviorBodyTemperature __instance, out float __state) => __state = __instance.Wetness;

        private static void Postfix(EntityBehaviorBodyTemperature __instance, float __state)
        {
            float current = __instance.Wetness;
            if (current < __state && __instance.entity is EntityPlayer player)
            {
                __instance.Wetness = Math.Clamp(__state - (__state - current) * Multiplier(player, TraitAttributes.Survival.DryingSpeedMul), 0f, 1f);
            }
        }
    }
}

