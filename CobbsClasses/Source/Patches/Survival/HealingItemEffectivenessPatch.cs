using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(CollectibleBehaviorHealingItem), nameof(CollectibleBehaviorHealingItem.OnHeldInteractStop))]
    internal static class HealingItemEffectivenessPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> body = [.. instructions];
            MethodInfo getter = AccessTools.PropertyGetter(typeof(CollectibleBehaviorHealingItem), "Health");
            int[] matches = [.. Enumerable.Range(0, body.Count).Where(index => body[index].Calls(getter))];
            RequireMatches(matches.Length, 1, "healing item effectiveness");
            body.InsertRange(matches[0] + 1, [new(OpCodes.Ldarg_3), new(OpCodes.Call, AccessTools.Method(typeof(HealingItemEffectivenessPatch), nameof(Adjust)))]);
            return body;
        }

        private static float Adjust(float health, EntityAgent player) => health * Multiplier(player, TraitAttributes.Survival.HealingItemEffectiveness);
    }
}

