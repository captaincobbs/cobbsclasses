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
    [HarmonyPatch(typeof(ModSystemWearableStats), "applyShieldProtection")]
    internal static class ShieldProtectionPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> body = [.. instructions];
            MethodInfo getter = AccessTools.Method(typeof(Vintagestory.API.Datastructures.JsonObject), "AsFloat", [typeof(float)]);
            int[] matches = [.. Enumerable.Range(1, body.Count - 1).Where(index => body[index - 1].Calls(getter) && body[index].opcode == OpCodes.Stloc_S && body[index].operand is LocalBuilder local && (local.LocalIndex == 8 || local.LocalIndex == 9))];
            RequireMatches(matches.Length, 4, "shield protection");
            foreach (int index in matches.Reverse())
            {
                int local = ((LocalBuilder)body[index].operand).LocalIndex;
                body.InsertRange(index, [new(OpCodes.Ldarg_1), new(OpCodes.Ldc_I4, local), new(OpCodes.Call, AccessTools.Method(typeof(ShieldProtectionPatch), nameof(Adjust)))]);
            }
            return body;
        }

        private static float Adjust(float amount, IPlayer player, int local)
        {
            return local == 9 ? Math.Clamp(amount * Multiplier(player.Entity, TraitAttributes.Equipment.ShieldBlockChance), 0f, 1f) : Math.Max(0f, amount * Multiplier(player.Entity, TraitAttributes.Equipment.ShieldProtection));
        }
    }
}

