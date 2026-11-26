using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.GameContent;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(BlockEntityFruitTreePart), nameof(BlockEntityFruitTreePart.OnBlockInteractStop))]
    internal static class FruitYieldPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => FruitYieldUtils.YieldMultiplier(instructions, TraitAttributes.Gathering.FruitYieldMul, OpCodes.Ldarg_2);
    }
}

