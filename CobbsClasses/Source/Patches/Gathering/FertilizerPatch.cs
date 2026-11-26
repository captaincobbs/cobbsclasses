using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(BlockEntitySoilNutrition), nameof(BlockEntitySoilNutrition.OnBlockInteract))]
    internal static class FertilizerPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> body = [.. instructions];
            FieldInfo[] nutrients = [AccessTools.Field(typeof(FertilizerProps), "N"), AccessTools.Field(typeof(FertilizerProps), "P"), AccessTools.Field(typeof(FertilizerProps), "K")];
            RequireMatches(body.Count(instruction => nutrients.Any(field => instruction.LoadsField(field))), 3, "fertilizer efficiency");
            foreach (CodeInstruction instruction in body)
            {
                yield return instruction;
                if (nutrients.Any(field => instruction.LoadsField(field)))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FertilizerPatch), nameof(Adjust)));
                }
            }
        }

        private static float Adjust(float amount, IPlayer player) => amount * Multiplier(player?.Entity, TraitAttributes.Gathering.FertilizerEfficiency);
    }
}

