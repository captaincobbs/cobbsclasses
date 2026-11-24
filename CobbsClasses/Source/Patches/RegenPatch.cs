using HarmonyLib;

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Vintagestory.API.Util;

using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(EntityBehaviorHealth), "ApplyRegenAndHunger")]
    internal static class RegenPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo vanillaParse = AccessTools.Method(typeof(StringUtil), nameof(StringUtil.ToFloat), [typeof(string), typeof(float)]);
            return ReplaceCall(instructions, vanillaParse, AccessTools.Method(typeof(RegenPatch), nameof(ReadRegenMultiplier)), OpCodes.Ldarg_0, "health regeneration");
        }

        private static float ReadRegenMultiplier(string setting, float fallback, EntityBehaviorHealth health)
        {
            return setting.ToFloat(fallback) * Multiplier(health.entity, "healthRegenRate");
        }
    }
}
