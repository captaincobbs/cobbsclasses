using System;
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
    [HarmonyPatch]
    internal static class IntoxicationPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(CollectibleObject), "tryEatStop");
            yield return AccessTools.DeclaredMethod(typeof(BlockLiquidContainerBase), "tryEatStop");
            yield return AccessTools.DeclaredMethod(typeof(BlockMeal), "Consume");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            List<CodeInstruction> body = [.. instructions];
            MethodInfo getter = AccessTools.PropertyGetter(typeof(FoodNutritionProperties), "Intoxication");
            int[] matches = [.. Enumerable.Range(0, body.Count - 1).Where(index => body[index].Calls(getter) && (original.DeclaringType == typeof(BlockMeal) || body[index + 1].opcode == OpCodes.Add))];
            RequireMatches(matches.Length, 1, "intoxication buildup");
            bool meal = original.DeclaringType == typeof(BlockMeal);
            List<CodeInstruction> added =
            [
                new CodeInstruction(meal ? OpCodes.Ldarg_2 : OpCodes.Ldarg_3)
            ];
            if (meal)
            {
                added.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IPlayer), nameof(IPlayer.Entity))));
            }

            added.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(IntoxicationPatch), nameof(Adjust))));
            body.InsertRange(matches[0] + 1, added);
            return body;
        }

        private static float Adjust(float amount, EntityAgent eater)
        {
            return amount > 0f ? amount * Multiplier(eater, "intoxicationPenalty") : amount;
        }
    }
}