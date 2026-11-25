using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(EntityBehaviorHarvestable), nameof(EntityBehaviorHarvestable.GenerateDrops))]
    internal static class AnimalHarvestYieldPatch
    {
        private static TagSetFast animalTag;
        private static TagSetFast rustedTag;

        internal static void InitializeTags(ICoreAPI api)
        {
            animalTag = api.EntityTagRegistry.CreateTagSet("animal");
            rustedTag = api.EntityTagRegistry.CreateTagSet("rust-creature");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo roll = AccessTools.Method(typeof(BlockDropItemStack), nameof(BlockDropItemStack.GetNextItemStack), [typeof(float)]);
            MethodInfo adjustedRoll = AccessTools.Method(typeof(AnimalHarvestYieldPatch), nameof(RollAnimalDrop));
            int matches = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(roll))
                {
                    matches++;
                    yield return new CodeInstruction(instruction) { opcode = OpCodes.Ldarg_0, operand = null };
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, adjustedRoll);
                }
                else
                {
                    yield return instruction;
                }
            }

            RequireMatches(matches, 1, "animal harvest yield");
        }

        private static ItemStack? RollAnimalDrop(BlockDropItemStack drop, float multiplier, EntityBehaviorHarvestable harvest, IPlayer? harvester)
        {
            if (harvester?.Entity is not { } player)
            {
                return drop.GetNextItemStack(multiplier);
            }

            if (harvest.entity.Tags.Overlaps(rustedTag))
            {
                return drop.GetNextItemStack(multiplier * Multiplier(player, "rustedSalvageYield"));
            }

            if (harvest.entity.Tags.Overlaps(animalTag))
            {
                multiplier *= Multiplier(player, "animalAllYield");
                if (drop.ResolvedItemstack?.Collectible.Code is { Domain: "game" } itemCode)
                {
                    string? stat = itemCode.Path switch
                    {
                        "redmeat-raw" or "bushmeat-raw" or "poultry-raw" => "animalMeatYield",
                        "bone" or "bone-fish" or "bone-tiny" => "animalBoneYield",
                        "fat" => "animalFatYield",
                        _ when itemCode.Path.StartsWith("hide-raw-", StringComparison.Ordinal) => "animalHideYield",
                        _ => null
                    };

                    if (stat is not null)
                    {
                        multiplier *= Multiplier(player, stat);
                    }
                }
            }

            return drop.GetNextItemStack(multiplier);
        }
    }
}
