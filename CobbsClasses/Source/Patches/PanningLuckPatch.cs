using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;

using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;

using static CobbsClasses.Source.Patches.PatchUtils;


namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(BlockPan), nameof(BlockPan.OnHeldInteractStop))]
    internal static class PanningLuckPatch
    {
        private static readonly MethodInfo CreateDropMethod = AccessTools.DeclaredMethod(typeof(BlockPan), "CreateDrop", [typeof(EntityAgent), typeof(string)]);
        private static readonly Action<BlockPan, EntityAgent, string> CreateDrop = AccessTools.MethodDelegate<Action<BlockPan, EntityAgent, string>>(CreateDropMethod);
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> body = [.. instructions];
            RequireMatches(body.Count(instructions => instructions.Calls(CreateDropMethod)), 1, "panning luck");

            int dropCallIndex = body.FindIndex(instruction => instruction.Calls(CreateDropMethod));
            body[dropCallIndex] = new CodeInstruction(body[dropCallIndex])
            {
                opcode = OpCodes.Call,
                operand = AccessTools.Method(typeof(PanningLuckPatch), nameof(CreateDropsWithLuck))
            };

            return body;
        }

        private static void CreateDropsWithLuck(BlockPan pan, EntityAgent byEntity, string fromBlockCode)
        {
            CreateDrop(pan, byEntity, fromBlockCode);
            if (byEntity is EntityPlayer player)
            {
                float luck = player.Stats.GetBlended("panningLuck") - 1f;
                if (luck > 0f)
                {
                    int extraRolls = GameMath.RoundRandom(player.World.Rand, luck);
                    for (int roll = 0; roll < extraRolls; roll++)
                    {
                        CreateDrop(pan, player, fromBlockCode);
                    }
                }
            }
        }
    }
}
