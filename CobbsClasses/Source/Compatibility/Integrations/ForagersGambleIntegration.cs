using HarmonyLib;

using Vintagestory.API.Common;

namespace CobbsClasses.Source.Compatibility.Integrations
{
    internal sealed class ForagersGambleIntegration : IModIntegration
    {
        public string ModId => "foragersgamble";

        public void Register(ICoreAPI api, Harmony harmony)
        {
            if (api.Side != EnumAppSide.Client)
            {
                return;
            }

            OptionalPatchRegistrar registrar = new(api, harmony, ModId);
            registrar.Patch("ForagersGamble.Patches.DisplayGating", "HideNutrition", postfix: new(typeof(ForagersGambleIntegration), nameof(Nutrition)));
            registrar.Patch("ForagersGamble.Patches.DisplayGating", "HideCrafting", postfix: new(typeof(ForagersGambleIntegration), nameof(Crafting)));
            registrar.Patch("Patch_BlockMeal_GetContentNutritionFacts_Postfix", "Postfix", prefix: new(typeof(ForagersGambleIntegration), nameof(MealSafety)));
        }

        private static bool Reveals(EntityAgent? player, string stat)
        {
            return player is EntityPlayer && player.Stats.GetBlended(stat) < 1f;
        }

        private static void Nutrition(EntityAgent __0, ref bool __result)
        {
            if (Reveals(__0, TraitAttributes.Food.HideNutritionInfo))
            {
                __result = false;
            }
        }

        private static void Crafting(EntityAgent __0, ref bool __result)
        {
            if (Reveals(__0, TraitAttributes.Food.HideCraftingInfo))
            {
                __result = false;
            }
        }

        private static bool MealSafety(EntityAgent __3)
        {
            return !Reveals(__3, TraitAttributes.Food.HideMealSafety);
        }
    }
}