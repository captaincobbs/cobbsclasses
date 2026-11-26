using System;

using HarmonyLib;

using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(BEBehaviorFruitingBush), nameof(BEBehaviorFruitingBush.GetHarvestDuration))]
    internal static class FruitingBushHarvestSpeedPatch
    {
        private static void Postfix(Entity __1, ref float __result) => __result /= Math.Clamp(Multiplier(__1, TraitAttributes.Gathering.HarvestSpeedMul), 0.05f, 20f);
    }
}

