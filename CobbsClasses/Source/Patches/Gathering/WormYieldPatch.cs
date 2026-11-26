using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(ItemWormGrunter), "spawnWorm")]
    internal static class WormYieldPatch
    {
        private static void Prefix(EntityAgent __1, ref int __3)
        {
            if (__1.World.Side == EnumAppSide.Server && __3 > 0)
            {
                __3 = Vintagestory.API.MathTools.GameMath.RoundRandom(__1.World.Rand, __3 * Multiplier(__1, TraitAttributes.Gathering.WormYieldMul));
            }
        }
    }
}

