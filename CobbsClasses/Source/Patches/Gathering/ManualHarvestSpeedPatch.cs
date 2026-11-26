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
    internal static class ManualHarvestSpeedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(BlockBehaviorHarvestable), "OnBlockInteractStep");
            yield return AccessTools.DeclaredMethod(typeof(BlockBehaviorHarvestable), "OnBlockInteractStop");
        }

        private static void Prefix(ref float __0, IPlayer __2)
            => __0 *= Math.Clamp(Multiplier(__2?.Entity, TraitAttributes.Gathering.HarvestSpeedMul), 0.05f, 20f);
    }
}

