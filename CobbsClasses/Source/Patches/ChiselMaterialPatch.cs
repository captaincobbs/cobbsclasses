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
    [HarmonyPatch(typeof(BlockEntityChisel), "SetVoxel", [typeof(Vintagestory.API.MathTools.Vec3i), typeof(bool), typeof(IPlayer), typeof(byte)])]
    internal static class ChiselMaterialPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> body = [.. instructions];
            FieldInfo quantities = AccessTools.Field(typeof(BlockEntityChisel), "AvailMaterialQuantities");
            int[] matches = [.. Enumerable.Range(0, body.Count - 3).Where(index => body[index].LoadsField(quantities) && body[index + 2].opcode == OpCodes.Ldelem_U2 && body[index + 3].opcode == OpCodes.Stloc_1)];
            RequireMatches(matches.Length, 1, "chisel material allowance");
            body.InsertRange(matches[0] + 3,
            [
                new CodeInstruction(OpCodes.Ldarg_3),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChiselMaterialPatch), nameof(ExpandAllowance)))
            ]);
            return body;
        }

        private static int ExpandAllowance(int available, IPlayer? player)
        {
            float additional = (player?.Entity?.Stats.GetBlended("chiselExtraVoxels") ?? 1f) - 1f;
            return available == 0 ? 0 : available + (int)Math.Clamp(additional, 0f, 4096f);
        }
    }
}
