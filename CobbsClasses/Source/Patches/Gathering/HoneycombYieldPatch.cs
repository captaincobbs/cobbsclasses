using System;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    internal static class HoneycombYieldPatch
    {
        internal static void Register(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(Skep)).Patch();
        }

        internal static void Adjust(ItemStack stack, IPlayer? player)
        {
            if (player?.Entity is not { } entity || entity.World.Side != EnumAppSide.Server || stack.Collectible.FirstCodePart() != "honeycomb")
            {
                return;
            }

            float factor = Multiplier(entity, TraitAttributes.Gathering.HoneycombYield);
            if (float.IsFinite(factor) && factor > 1f)
            {
                stack.StackSize = Math.Min(stack.Collectible.MaxStackSize, GameMath.RoundRandom(entity.World.Rand, stack.StackSize * factor));
            }
        }

        [HarmonyPatch(typeof(BlockSkep), "getHarvestableDrops")]
        private static class Skep
        {
            private static void Postfix(IPlayer? __2, ItemStack[]? __result)
            {
                foreach (ItemStack stack in __result ?? [])
                {
                    Adjust(stack, __2);
                }
            }
        }
    }
}

