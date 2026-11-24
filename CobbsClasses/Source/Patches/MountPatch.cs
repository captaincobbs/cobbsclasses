using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(EntityBehaviorGait), "Move")]
    internal static class MountPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo vanillaMultiply = AccessTools.Method(typeof(Vec3d), nameof(Vec3d.Mul), [typeof(double)]);
            MethodInfo replacement = AccessTools.Method(typeof(MountPatch), nameof(ScaleGait));
            return ReplaceCall(instructions, vanillaMultiply, replacement, OpCodes.Ldarg_0, "mount gait");
        }

        private static Vec3d ScaleGait(Vec3d walking, double speed, EntityBehaviorGait gait)
        {
            Entity? rider = gait.entity.GetBehavior<EntityBehaviorRideable>()?.Controller;
            return walking.Mul(speed * Multiplier(rider, "vehicleMovespeed") * Multiplier(rider, "mountSpeed"));
        }
    }
}
