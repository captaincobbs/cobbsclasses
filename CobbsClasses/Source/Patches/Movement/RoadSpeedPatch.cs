using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.API.Common;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(EntityAgent), nameof(EntityAgent.GetWalkSpeedMultiplier))]
    internal static class RoadSpeedPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> body = [.. instructions];
            FieldInfo speed = AccessTools.Field(typeof(Block), nameof(Block.WalkSpeedMultiplier));
            RequireMatches(body.Count(instruction => instruction.LoadsField(speed)), 2, "ground movement speed");
            foreach (CodeInstruction instruction in body)
            {
                yield return instruction;
                if (instruction.LoadsField(speed))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RoadSpeedPatch), nameof(Adjust)));
                }
            }
        }

        private static float Adjust(float speed, EntityAgent agent)
        {
            return speed > 1f ? 1f + (speed - 1f) * Multiplier(agent, TraitAttributes.Movement.RoadSpeedBonus) : speed;
        }
    }
}
