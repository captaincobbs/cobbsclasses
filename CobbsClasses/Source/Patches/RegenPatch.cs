using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using Vintagestory.API.Common;
using Vintagestory.API.Util;

using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(EntityBehaviorHealth), "ApplyRegenAndHunger")]
    internal static class RegenPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo parseSpeed = AccessTools.Method(typeof(StringUtil), nameof(StringUtil.ToFloat), [typeof(string), typeof(float)]);
            List<CodeInstruction> body = [.. instructions];
            RequireMatches(body.Count(instruction => instruction.Calls(parseSpeed)), 1, "health regeneration speed");

            int parseIndex = body.FindIndex(instruction => instruction.Calls(parseSpeed));
            int rateIndex = parseIndex + 4;
            if (rateIndex >= body.Count || !body[parseIndex + 1].IsStloc() || body[parseIndex + 2].opcode != OpCodes.Ldc_R4 || !body[parseIndex + 3].IsLdloc() || body[rateIndex].opcode != OpCodes.Mul)
            {
                throw new InvalidOperationException("Could not find the vanilla health regeneration rate calculation.");
            }

            body.InsertRange(rateIndex + 1,
            [
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RegenPatch), nameof(AdjustRegenRate)))
            ]);
            return body;
        }

        private static float AdjustRegenRate(float vanillaRate, EntityBehaviorHealth health)
        {
            if (vanillaRate <= 0f || health.entity is not EntityPlayer player)
            {
                return vanillaRate;
            }

            float flatPerHour = player.Stats.GetBlended("healthRegenFlat") - 1f;
            return Math.Max(0f, vanillaRate * Multiplier(player, "healthRegenRate") + flatPerHour / 3600f);
        }
    }
}
