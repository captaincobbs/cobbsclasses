using HarmonyLib;

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(BEBehaviorFruitingBush), "SetHarvested")]
    internal static class FruitingBushCuttingPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> editedInstructions = [.. instructions];
            int writeTraitsIndex = editedInstructions.FindIndex(instruction => instruction.opcode == OpCodes.Callvirt && instruction.operand is MethodInfo method && method.Name == "SetString");

            if (writeTraitsIndex < 0)
            {
                return editedInstructions;
            }

            editedInstructions.InsertRange(writeTraitsIndex + 1, [
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FruitingBushCuttingPatch), nameof(ApplyCuttingYield)))
            ]);

            return editedInstructions;
        }

        private static void ApplyCuttingYield(IPlayer player, ItemStack cutting)
        {
            if (player is IServerPlayer serverPlayer)
            {
                EntityPlayer entity = serverPlayer.Entity;
                float multiplier = entity.Stats.GetBlended(TraitAttributes.Gathering.CuttingYieldMul);
                if (multiplier > 1f)
                {
                    cutting.StackSize = GameMath.RoundRandom(entity.World.Rand, cutting.StackSize * multiplier);
                }
            }
        }
    }
}
