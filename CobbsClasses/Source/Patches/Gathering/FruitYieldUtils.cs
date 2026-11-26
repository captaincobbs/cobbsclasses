using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.API.Common;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    internal static class FruitYieldUtils
    {
        internal static IEnumerable<CodeInstruction> YieldMultiplier(IEnumerable<CodeInstruction> instructions, string stat, OpCode playerArgument)
        {
            List<CodeInstruction> body = [.. instructions];
            MethodInfo roll = AccessTools.Method(typeof(BlockDropItemStack), nameof(BlockDropItemStack.GetNextItemStack), [typeof(float)]);
            int[] matches = [.. Enumerable.Range(0, body.Count).Where(index => body[index].Calls(roll))];
            RequireMatches(matches.Length, 1, stat);
            CodeInstruction first = new(playerArgument);
            first.labels.AddRange(body[matches[0]].labels);
            body[matches[0]].labels.Clear();
            body.InsertRange(matches[0], [first, new(OpCodes.Ldstr, stat), new(OpCodes.Call, AccessTools.Method(typeof(FruitYieldUtils), nameof(AdjustYield)))]);
            return body;
        }

        private static float AdjustYield(float amount, IPlayer? player, string stat) => amount * Multiplier(player?.Entity, stat);

    }
}
