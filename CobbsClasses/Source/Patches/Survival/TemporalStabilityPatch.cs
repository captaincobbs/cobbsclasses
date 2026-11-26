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
    [HarmonyPatch(typeof(EntityBehaviorTemporalStabilityAffected), nameof(EntityBehaviorTemporalStabilityAffected.OnGameTick))]
    internal static class TemporalStabilityPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> body = [.. instructions];
            MethodInfo getter = AccessTools.PropertyGetter(typeof(EntityBehaviorTemporalStabilityAffected), "OwnStability");
            int[] matches = [.. Enumerable.Range(0, body.Count - 2).Where(index => body[index].Calls(getter) && body[index + 1].opcode == OpCodes.Ldloc_2 && body[index + 2].opcode == OpCodes.Add)];
            RequireMatches(matches.Length, 1, "temporal stability change");
            body.InsertRange(matches[0] + 2,
            [
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TemporalStabilityPatch), nameof(AdjustChange)))
            ]);
            return body;
        }

        private static double AdjustChange(double change, EntityBehaviorTemporalStabilityAffected behavior, float elapsed)
        {
            if (behavior.entity is not EntityPlayer player)
            {
                return change;
            }

            if (change < 0d)
            {
                float resistance = player.Stats.GetBlended(TraitAttributes.Survival.StabilityLossResistance) - 1f;
                float environment = player.Stats.GetBlended(TraitAttributes.Survival.EnvironmentStabilityResistance) - 1f;
                change *= 1f - Math.Clamp(resistance + environment, 0f, 1f);
            }

            return change + (player.Stats.GetBlended(TraitAttributes.Survival.EnvironmentStabilityChange) - 1f) * elapsed;
        }
    }
}