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
    [HarmonyPatch]
    internal static class ClayVoxelYieldPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(ItemClay), nameof(ItemClay.OnHeldInteractStart));
            yield return AccessTools.DeclaredMethod(typeof(ItemClay), nameof(ItemClay.OnHeldInteractStop));
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            if (original.Name == nameof(ItemClay.OnHeldInteractStart))
            {
                MethodInfo putClay = AccessTools.Method(typeof(BlockEntityClayForm), nameof(BlockEntityClayForm.PutClay));
                MethodInfo replacement = AccessTools.Method(typeof(ClayVoxelYieldPatch), nameof(PutInitialClay));
                return ReplaceCall(instructions, putClay, replacement, OpCodes.Ldarg_2, "initial clay voxels");
            }

            List<CodeInstruction> body = [.. instructions];
            FieldInfo availableVoxels = AccessTools.Field(typeof(BlockEntityClayForm), nameof(BlockEntityClayForm.AvailableVoxels));
            MethodInfo adjust = AccessTools.Method(typeof(ClayVoxelYieldPatch), nameof(AdjustVoxelAmount));
            int[] matches = [.. Enumerable.Range(1, body.Count - 3).Where(index => body[index - 1].LoadsField(availableVoxels) && body[index].LoadsConstant(25) && body[index + 1].opcode == OpCodes.Add && body[index + 2].StoresField(availableVoxels))];
            RequireMatches(matches.Length, 1, "refill clay voxels");
            body.InsertRange(matches[0] + 1,
            [
                new CodeInstruction(OpCodes.Ldarg_3),
                new CodeInstruction(OpCodes.Call, adjust)
            ]);
            return body;
        }

        private static void PutInitialClay(BlockEntityClayForm form, ItemSlot slot, EntityAgent player)
        {
            form.PutClay(slot);
            int extraVoxels = AdjustVoxelAmount(25, player) - 25;
            if (extraVoxels != 0)
            {
                form.AvailableVoxels += extraVoxels;
                form.MarkDirty();
            }
        }

        private static int AdjustVoxelAmount(int amount, EntityAgent agent)
        {
            if (agent is not EntityPlayer player)
            {
                return amount;
            }

            float extraVoxels = player.Stats.GetBlended(TraitAttributes.Crafting.ClayformingPoints) - 1f;
            return Math.Max(0, amount + (int)MathF.Round(extraVoxels));
        }
    }
}
