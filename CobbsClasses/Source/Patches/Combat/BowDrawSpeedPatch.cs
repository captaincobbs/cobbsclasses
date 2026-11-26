using HarmonyLib;

using System.Collections.Generic;
using System.Reflection;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch]
    internal static class BowDrawSpeedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(ItemBow), nameof(ItemBow.OnHeldInteractStep));
            yield return AccessTools.DeclaredMethod(typeof(ItemBow), nameof(ItemBow.OnHeldInteractStop));
        }

        private static void Prefix(ref float secondsUsed, EntityAgent byEntity)
        {
            secondsUsed *= Multiplier(byEntity, TraitAttributes.Combat.BowDrawSpeed);
        }
    }
}
