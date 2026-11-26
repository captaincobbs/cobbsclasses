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
    [HarmonyPatch(typeof(BlockBehaviorHarvestable), nameof(BlockBehaviorHarvestable.OnBlockInteractStop))]
    internal static class ResinYieldPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> body = [.. instructions];
            int[] matches = [.. Enumerable.Range(0, body.Count).Where(index => body[index].opcode == OpCodes.Stfld && body[index].operand is FieldInfo field && field.Name == "dropRate")];
            RequireMatches(matches.Length, 2, "manual harvest yield");
            foreach (int index in matches.Take(1))
            {
                body.InsertRange(index, [new(OpCodes.Ldarg_2), new(OpCodes.Ldarg_3), new(OpCodes.Ldarg_S, (byte)4), new(OpCodes.Call, AccessTools.Method(typeof(ResinYieldPatch), nameof(Adjust)))]);
            }
            return body;
        }

        private static float Adjust(float amount, IWorldAccessor world, IPlayer player, BlockSelection selection)
        {
            Block block = world.BlockAccessor.GetBlock(selection.Position);
            if (block.Code.Domain == "game" && block.Code.Path.StartsWith("log-resin-", StringComparison.Ordinal))
            {
                return amount * Multiplier(player?.Entity, TraitAttributes.Gathering.ResinYieldMul);
            }

            return amount;
        }
    }
}

