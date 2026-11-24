using HarmonyLib;

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(PModulePlayerInLiquid), "HandleSwimming")]
    internal static class SwimPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo vanillaAdd = AccessTools.Method(typeof(Vec3d), nameof(Vec3d.Add), [typeof(double), typeof(double), typeof(double)]);
            MethodInfo replacement = AccessTools.Method(typeof(SwimPatch), nameof(AddSwimMotion));
            return ReplaceCall(instructions, vanillaAdd, replacement, OpCodes.Ldarg_2, "swimming");
        }

        private static Vec3d AddSwimMotion(Vec3d motion, double horizontalX, double vertical, double horizontalZ, Entity swimmer)
        {
            float speed = Multiplier(swimmer, "swimSpeed");
            return motion.Add(horizontalX * speed, vertical, horizontalZ * speed);
        }
    }
}
