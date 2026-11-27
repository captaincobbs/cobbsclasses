using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch]
    internal static class PanningSpeedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(BlockPan), nameof(BlockPan.OnHeldInteractStep));
            yield return AccessTools.DeclaredMethod(typeof(BlockPan), nameof(BlockPan.OnHeldInteractStop));
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            List<CodeInstruction> body = [.. instructions];
            bool isStep = original.Name == nameof(BlockPan.OnHeldInteractStep);
            float duration = isStep ? 4f : 3.4f;
            OpCode comparison = isStep ? OpCodes.Cgt_Un : OpCodes.Blt_Un_S;
            MethodInfo adjust = AccessTools.Method(typeof(PanningSpeedPatch), nameof(AdjustDuration));
            List<int> matches = [];

            for (int index = 1; index + 1 < body.Count; index++)
            {
                if (body[index - 1].opcode == OpCodes.Ldarg_1 && body[index].LoadsConstant(duration)
                    && (body[index + 1].opcode == comparison || (!isStep && body[index + 1].opcode == OpCodes.Blt_Un)))
                {
                    matches.Add(index);
                }
            }

            RequireMatches(matches.Count, 1, "panning duration");
            body.InsertRange(matches[0] + 1,
            [
                new CodeInstruction(OpCodes.Ldarg_3),
                new CodeInstruction(OpCodes.Call, adjust)
            ]);
            return body;
        }

        private static float AdjustDuration(float duration, EntityAgent player)
        {
            return duration / Math.Max(0.01f, Multiplier(player, TraitAttributes.Gathering.PanningSpeedMul));
        }
    }
}
