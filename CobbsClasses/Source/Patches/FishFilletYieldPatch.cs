using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch]
    internal static class FishFilletYieldPatch
    {
        private static MethodBase TargetMethod()
        {
            MethodInfo finish = AccessTools.Method(typeof(CollectibleBehaviorGroundStoredProcessable), nameof(CollectibleBehaviorGroundStoredProcessable.OnContainedInteractStop));
            return PatchProcessor.GetOriginalInstructions(finish).Where(instruction => instruction.opcode == OpCodes.Ldftn).Select(instruction => instruction.operand).OfType<MethodInfo>().Single(method => method.ReturnType == typeof(void) && method.GetParameters() is [{ ParameterType: var parameterType }] && parameterType == typeof(BlockDropItemStack));
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            Type closure = original.DeclaringType!;
            FieldInfo player = AccessTools.Field(closure, "byPlayer");
            FieldInfo slot = AccessTools.Field(closure, "slot");
            MethodInfo roll = AccessTools.Method(typeof(BlockDropItemStack), nameof(BlockDropItemStack.GetNextItemStack), [typeof(float)]);
            MethodInfo adjustedRoll = AccessTools.Method(typeof(FishFilletYieldPatch), nameof(RollFillet));
            int matches = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(roll))
                {
                    matches++;
                    yield return new CodeInstruction(instruction) { opcode = OpCodes.Ldarg_0, operand = null };
                    yield return new CodeInstruction(OpCodes.Ldfld, player);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, slot);
                    yield return new CodeInstruction(OpCodes.Call, adjustedRoll);
                }
                else
                {
                    yield return instruction;
                }
            }

            RequireMatches(matches, 1, "fish fillet yield");
        }

        private static ItemStack? RollFillet(BlockDropItemStack drop, float multiplier, IPlayer? processor, ItemSlot input)
        {
            ItemStack? output = drop.GetNextItemStack(multiplier);
            if (processor?.Entity is not { } player || output?.Collectible.Code is not { Domain: "game", Path: "fish-raw" } || input.Itemstack?.Collectible.Code is not { Domain: "game" } inputCode || !inputCode.Path.StartsWith("fishraw-", StringComparison.Ordinal))
            {
                return output;
            }

            // Keep the existing Sailor trait key as a fallback for older configs.
            float configured = player.Stats.GetBlended("fishMeatYield");
            float factor = Math.Max(0f, configured == 1f ? player.Stats.GetBlended("fishmeatYieldMul") : configured);
            if (factor == 1f)
            {
                return output;
            }

            ItemStack adjusted = output.Clone();
            adjusted.StackSize = GameMath.RoundRandom(player.World.Rand, output.StackSize * factor);
            return adjusted.StackSize > 0 ? adjusted : null;
        }
    }
}
