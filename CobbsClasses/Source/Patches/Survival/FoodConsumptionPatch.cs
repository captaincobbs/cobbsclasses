using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch]
    internal static class FoodConsumptionPatch
    {
        internal static void Register(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(Spoilage)).Patch();
            harmony.CreateClassProcessor(typeof(Eating)).Patch();
        }

        [HarmonyPatch(typeof(GlobalConstants), nameof(GlobalConstants.FoodSpoilageSatLossMul))]
        private static class Spoilage
        {
            private static void Postfix(EntityAgent __2, ref float __result)
            {
                if (__2 is EntityPlayer player)
                {
                    float resistance = Math.Clamp(player.Stats.GetBlended(TraitAttributes.Food.SpoiltFoodPenalty) - 1f, 0f, 1f);
                    __result += (1f - __result) * resistance;
                }
            }
        }

        [HarmonyPatch(typeof(CollectibleObject), "tryEatStop")]
        private static class Eating
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                CodeInstruction[] body = [.. instructions];
                FieldInfo satiety = AccessTools.Field(typeof(FoodNutritionProperties), nameof(FoodNutritionProperties.Satiety));
                MethodInfo psychedelic = AccessTools.PropertyGetter(typeof(FoodNutritionProperties), "Psychedelic");
                RequireMatches(body.Count(instruction => instruction.LoadsField(satiety)), 1, "raw food satiety");
                RequireMatches(body.Count(instruction => instruction.Calls(psychedelic)), 1, "hallucinogen resistance");

                foreach (CodeInstruction instruction in body)
                {
                    yield return instruction;
                    if (instruction.LoadsField(satiety))
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_2);
                        yield return new CodeInstruction(OpCodes.Ldarg_3);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Eating), nameof(AdjustSatiety)));
                    }
                    else if (instruction.Calls(psychedelic))
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_3);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Eating), nameof(AdjustPsychedelic)));
                    }
                }
            }

            private static float AdjustSatiety(float amount, ItemSlot slot, EntityAgent eater)
            {
                if (slot.Itemstack is not { } stack)
                {
                    return amount;
                }

                CollectibleObject collectible = stack.Collectible;
                JsonObject? option = collectible.Attributes?["cobbsclassesRawIngredient"];
                string path = collectible.Code.Path;
                bool raw = option?.Exists == true ? option.AsBool() : collectible.Code.Domain == "game" && (path.StartsWith("fruit-", StringComparison.Ordinal) || path.StartsWith("vegetable-", StringComparison.Ordinal) || path.StartsWith("legume-", StringComparison.Ordinal) || path is "bambooshoot" or "rawcassava" || path.StartsWith("grain-", StringComparison.Ordinal) || path.StartsWith("mushroom-", StringComparison.Ordinal) && path.EndsWith("-normal", StringComparison.Ordinal) || path is "redmeat-raw" or "bushmeat-raw" or "poultry-raw" or "fish-raw");
                JsonObject? basicOption = collectible.Attributes?["cobbsclassesBasicFood"];
                bool basic = basicOption?.Exists == true ? basicOption.AsBool() : raw
                    || collectible.Code.Domain == "game" && (
                        path.StartsWith("redmeat-", StringComparison.Ordinal)
                        || path.StartsWith("bushmeat-", StringComparison.Ordinal)
                        || path.StartsWith("poultry-", StringComparison.Ordinal)
                        || path.StartsWith("fish-", StringComparison.Ordinal)
                        || path.StartsWith("fishchunk-", StringComparison.Ordinal)
                        || path.StartsWith("fishraw-", StringComparison.Ordinal)
                        || path is "fishchunk" or "fishraw" or "egg"
                        || path.StartsWith("mushroom-", StringComparison.Ordinal)
                        || path.StartsWith("egg-", StringComparison.Ordinal)
                        || path.StartsWith("bread-", StringComparison.Ordinal)
                        || path.StartsWith("pickledvegetable-", StringComparison.Ordinal)
                        || path.StartsWith("pickledlegume-", StringComparison.Ordinal)
                        || path.StartsWith("vegetable-", StringComparison.Ordinal)
                        || path.StartsWith("fruit-", StringComparison.Ordinal));
                if (raw)
                {
                    amount *= Multiplier(eater, TraitAttributes.Food.RawFoodSatiety);
                }

                return basic ? amount * Multiplier(eater, TraitAttributes.Food.BasicFoodSatiety) : amount;
            }

            private static float AdjustPsychedelic(float amount, EntityAgent eater)
            {
                if (eater is not EntityPlayer player)
                {
                    return amount;
                }

                float resistance = Math.Clamp(player.Stats.GetBlended(TraitAttributes.Survival.HallucinationResistance) - 1f, 0f, 1f);
                return amount * (1f - resistance) * Multiplier(eater, TraitAttributes.Survival.HallucinogenPenalty);
            }
        }
    }
}


