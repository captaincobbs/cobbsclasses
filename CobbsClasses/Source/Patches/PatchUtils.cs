using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using CobbsClasses.Source.API;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace CobbsClasses.Source.Patches
{
    internal static class PatchUtils
    {
        internal static float Multiplier(Entity? entity, string stat) => entity is EntityPlayer player ? TraitMath.GetMultiplier(player.Stats, stat) : 1f;

        internal static IEnumerable<CodeInstruction> ReplaceCall(IEnumerable<CodeInstruction> instructions, MethodInfo original, MethodInfo replacement, OpCode additionalArgument, string feature)
        {
            List<CodeInstruction> body = [.. instructions];
            RequireMatches(body.Count(instruction => instruction.Calls(original)), 1, feature);

            foreach (CodeInstruction instruction in body)
            {
                if (instruction.Calls(original))
                {
                    // Retain the branch labels on the first instruction of the replacement.
                    CodeInstruction loadArgument = new(instruction)
                    {
                        opcode = additionalArgument,
                        operand = null
                    };
                    yield return loadArgument;
                    yield return new CodeInstruction(OpCodes.Call, replacement);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        internal static void RequireMatches(int actual, int expected, string feature)
        {
            if (actual != expected)
            {
                throw new InvalidOperationException($"Expected {expected} Vintage Story code matches for {feature}, found {actual}.");
            }
        }
    }
}