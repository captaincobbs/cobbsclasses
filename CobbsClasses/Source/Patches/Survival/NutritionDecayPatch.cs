using System;

using HarmonyLib;

using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(EntityBehaviorHunger), "ReduceSaturation")]
    internal static class NutritionDecayPatch
    {
        private static void Prefix(EntityBehaviorHunger __instance, out float[]? __state)
        {
            __state = null;
            if (Multiplier(__instance.entity, TraitAttributes.Food.NutritionDecayRate) != 1f)
            {
                __state = [__instance.FruitLevel, __instance.VegetableLevel, __instance.ProteinLevel, __instance.GrainLevel, __instance.DairyLevel];
            }
        }

        private static void Postfix(EntityBehaviorHunger __instance, float[]? __state)
        {
            if (__state is null)
            {
                return;
            }

            float factor = Multiplier(__instance.entity, TraitAttributes.Food.NutritionDecayRate);
            __instance.FruitLevel = Scale(__state[0], __instance.FruitLevel, factor);
            __instance.VegetableLevel = Scale(__state[1], __instance.VegetableLevel, factor);
            __instance.ProteinLevel = Scale(__state[2], __instance.ProteinLevel, factor);
            __instance.GrainLevel = Scale(__state[3], __instance.GrainLevel, factor);
            __instance.DairyLevel = Scale(__state[4], __instance.DairyLevel, factor);
        }

        private static float Scale(float previous, float current, float factor) => current < previous ? Math.Max(0f, previous - (previous - current) * factor) : current;
    }
}

