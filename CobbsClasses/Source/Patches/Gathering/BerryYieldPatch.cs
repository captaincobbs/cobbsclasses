using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.GameContent;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(BEBehaviorFruitingBush), nameof(BEBehaviorFruitingBush.GetRipeDrops))]
    internal static class BerryYieldPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => FruitYieldUtils.YieldMultiplier(instructions, TraitAttributes.Gathering.BerryYieldMul, OpCodes.Ldarg_1);
    }
}

