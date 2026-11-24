using HarmonyLib;

using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(EntityBoat), nameof(EntityBoat.SpeedMultiplier), MethodType.Getter)]
    internal static class BoatPatch
    {
        private static void Postfix(EntityBoat __instance, ref float __result)
        {
            Entity? driver = __instance.GetBehavior<EntityBehaviorSeatable>()?.Controller;
            __result *= Multiplier(driver, "vehicleMovespeed") * Multiplier(driver, "boatSpeed");
        }
    }
}
