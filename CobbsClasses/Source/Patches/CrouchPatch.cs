using HarmonyLib;

using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Vintagestory.API.Common;
using Vintagestory.API.Config;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(EntityAgent), nameof(EntityAgent.GetWalkSpeedMultiplier))]
    internal static class CrouchPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo sneakMultiplier = AccessTools.Field(typeof(GlobalConstants), nameof(GlobalConstants.SneakSpeedMultiplier));
            MethodInfo adjust = AccessTools.Method(typeof(CrouchPatch), nameof(AdjustSneakMultiplier));
            List<CodeInstruction> body = [.. instructions];
            RequireMatches(body.Count(instruction => instruction.LoadsField(sneakMultiplier)), 1, "sneak speed");

            foreach (CodeInstruction instruction in body)
            {
                yield return instruction;
                if (instruction.LoadsField(sneakMultiplier))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, adjust);
                }
            }
        }

        private static float AdjustSneakMultiplier(float normalSneakSpeed, EntityAgent agent)
        {
            if (agent is EntityPlayer player)
            {
                // Sneak speed bonus is based on % of speed difference from walk speed, rather than a % bonus
                float ratio = Math.Clamp(player.Stats.GetBlended("sneakSpeed") - 1f, -1f, 1f);
                return ratio < 0f ? normalSneakSpeed * (1f + ratio) : normalSneakSpeed + (1f - normalSneakSpeed) * ratio;
            }

            return normalSneakSpeed;
        }
    }
}
