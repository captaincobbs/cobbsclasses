using System;
using System.Collections.Generic;
using System.Reflection;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch]
    internal static class HealingItemSpeedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(CollectibleBehaviorHealingItem), "OnHeldInteractStep");
            yield return AccessTools.DeclaredMethod(typeof(CollectibleBehaviorHealingItem), "OnHeldInteractStop");
            yield return AccessTools.DeclaredMethod(typeof(CollectibleBehaviorHealingItem), "OnHeldInteractCancel");
        }

        private static void Prefix(ref float __0, EntityAgent __2) => __0 *= Math.Clamp(Multiplier(__2, TraitAttributes.Survival.HealingItemSpeedMul), 0.05f, 20f);
    }
}

