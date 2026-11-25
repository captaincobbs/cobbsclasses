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
    [HarmonyPatch(typeof(ModSystemWearableStats), "handleDamaged")]
    internal static class ArmorProtectionPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> body = [.. instructions];
            MethodInfo maximum = AccessTools.Method(typeof(Math), nameof(Math.Max), [typeof(float), typeof(float)]);

            // Adjust the values read by the final damage calculation after damage-tier losses.
            int relative = FindProtectionUse(body, nameof(ProtectionModifiers.RelativeProtection), (code, index) => index + 3 < code.Count && code[index + 1].Calls(maximum) && code[index + 2].opcode == OpCodes.Sub && code[index + 3].opcode == OpCodes.Mul);
            InsertAdjustment(body, relative, nameof(AdjustProtection));

            int flat = FindProtectionUse(body, nameof(ProtectionModifiers.FlatDamageReduction), (code, index) => index + 2 < code.Count && code[index + 1].opcode == OpCodes.Sub && code[index + 2].Calls(maximum));
            InsertAdjustment(body, flat, nameof(AdjustFlatProtection));
            return body;
        }

        private static int FindProtectionUse(List<CodeInstruction> body, string fieldName, System.Func<List<CodeInstruction>, int, bool> matchesDamageCalculation)
        {
            FieldInfo field = AccessTools.Field(typeof(ProtectionModifiers), fieldName);
            RequireMatches(body.Count(instruction => instruction.LoadsField(field)), 1, fieldName);
            int fieldIndex = body.FindIndex(instruction => instruction.LoadsField(field));
            if (fieldIndex + 1 >= body.Count || !body[fieldIndex + 1].IsStloc() || body[fieldIndex + 1].operand is not LocalBuilder protection)
            {
                throw new InvalidOperationException($"Could not find the local for {fieldName}.");
            }

            List<int> matches = [];
            for (int index = fieldIndex + 2; index < body.Count; index++)
            {
                if (body[index].IsLdloc(protection) && matchesDamageCalculation(body, index))
                {
                    matches.Add(index);
                }
            }

            RequireMatches(matches.Count, 1, $"final {fieldName}");
            return matches[0];
        }

        private static void InsertAdjustment(List<CodeInstruction> body, int index, string method)
        {
            body.InsertRange(index + 1,
            [
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ArmorProtectionPatch), method))
            ]);
        }

        private static float AdjustFlatProtection(float protection, IPlayer player)
        {
            return protection > 0f ? protection * Multiplier(player.Entity, "armorFlatProtection") : protection;
        }

        private static float AdjustProtection(float protection, IPlayer player)
        {
            float multiplier = Multiplier(player.Entity, "armorProtection");
            return multiplier == 1f ? protection : Math.Clamp(protection * multiplier, 0f, 1f);
        }
    }
}
