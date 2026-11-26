using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
      
using Vintagestory.API.Common;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "updateBodyTemperature")]
    internal static class TemperatureTolerancePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo originalField = AccessTools.Field(typeof(EntityBehaviorBodyTemperature), "bodyTemperatureResistance");
            MethodInfo adjust = AccessTools.Method(typeof(TemperatureTolerancePatch), nameof(AdjustColdThreshold));
            List<CodeInstruction> body = [.. instructions];
            int matches = body.Count(instruction => instruction.LoadsField(originalField));
            RequireMatches(matches, 2, "body temperature");

            MethodInfo wetnessGetter = AccessTools.PropertyGetter(typeof(EntityBehaviorBodyTemperature), nameof(EntityBehaviorBodyTemperature.Wetness));
            MethodInfo adjustWetness = AccessTools.Method(typeof(TemperatureTolerancePatch), nameof(AdjustWetnessPenalty));
            RequireMatches(body.Count(instruction => instruction.Calls(wetnessGetter)), 2, "wetness cooling");
            int wetnessReads = 0;

            foreach (CodeInstruction instruction in body)
            {
                yield return instruction;
                if (instruction.Calls(wetnessGetter) && ++wetnessReads == 2)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, adjustWetness);
                }

                if (instruction.LoadsField(originalField))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, adjust);
                }
            }
        }

        private static float AdjustColdThreshold(float threshold, EntityBehaviorBodyTemperature temperature)
        {
            float bonus = temperature.entity is EntityPlayer player ? player.Stats.GetBlended(TraitAttributes.Survival.ColdTemperatureRange) - 1f : 0f;

            return threshold - bonus;
        }

        private static float AdjustWetnessPenalty(float wetness, EntityBehaviorBodyTemperature temperature)
        {
            if (temperature.entity is EntityPlayer player)
            {
                float resistance = Math.Clamp(player.Stats.GetBlended(TraitAttributes.Survival.WetResistance) - 1f, 0f, 1f);
                return wetness * (1f - resistance);
            }

            return wetness;
        }
    }
}
