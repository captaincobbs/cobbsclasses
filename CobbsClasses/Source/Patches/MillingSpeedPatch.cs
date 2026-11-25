using HarmonyLib;

using System.Collections.Generic;

using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(BlockEntityQuern), "GrindSpeed", MethodType.Getter)]
    internal static class MillingSpeedPatch
    {
        private static void Postfix(BlockEntityQuern __instance, Dictionary<string, long> ___playersGrinding, ref float __result)
        {
            if (___playersGrinding.Count == 0 || __result <= 0f || __instance.Api?.World is not { } world)
            {
                return;
            }

            float total = 0f;
            int grinders = 0;
            foreach (string playerUid in ___playersGrinding.Keys)
            {
                if (world.PlayerByUid(playerUid)?.Entity is { } player)
                {
                    total += Multiplier(player, "millingSpeedMul");
                    grinders++;
                }
            }

            if (grinders > 0)
            {
                __result *= total / grinders;
            }
        }
    }
}
