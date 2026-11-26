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
    [HarmonyPatch(typeof(BlockWateringCan), nameof(BlockWateringCan.OnHeldInteractStep))]
    internal static class WateringPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> body = [.. instructions];
            MethodInfo setter = AccessTools.Method(typeof(BlockWateringCan), "SetRemainingWateringSeconds");
            int[] matches = [.. Enumerable.Range(3, body.Count - 3).Where(index => body[index].Calls(setter) && body[index - 1].opcode == OpCodes.Stloc_1 && body[index - 2].opcode == OpCodes.Dup && body[index - 3].opcode == OpCodes.Sub)];
            RequireMatches(matches.Length, 1, "watering consumption");
            body.InsertRange(matches[0] - 3, [new(OpCodes.Ldarg_3), new(OpCodes.Call, AccessTools.Method(typeof(WateringPatch), nameof(Adjust)))]);
            return body;
        }

        private static float Adjust(float amount, EntityAgent player) => amount * Multiplier(player, TraitAttributes.Gathering.WateringConsumption);
    }
}

