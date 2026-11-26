using HarmonyLib;

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

using static CobbsClasses.Source.Patches.PatchUtils;

namespace CobbsClasses.Source.Patches
{
    [HarmonyPatch(typeof(ModSystemWearableStats), "updateWearableStats")]
    internal static class ArmorPenaltyPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo vanillaModifiers = AccessTools.Method(typeof(IWearableStatsSupplier), nameof(IWearableStatsSupplier.GetStatModifiers));
            MethodInfo adjustedModifiers = AccessTools.Method(typeof(ArmorPenaltyPatch), nameof(GetAdjustedModifiers));
            return ReplaceCall(instructions, vanillaModifiers, adjustedModifiers, OpCodes.Ldarg_2, "armor penalties");
        }

        private static StatModifiers? GetAdjustedModifiers(IWearableStatsSupplier wearable, ItemSlot slot, IPlayer player)
        {
            StatModifiers? modifiers = wearable.GetStatModifiers(slot);
            if (modifiers is null || !wearable.IsArmorType(slot))
            {
                return modifiers;
            }

            float healing = AdjustPenalty(modifiers.healingeffectivness, Multiplier(player.Entity, TraitAttributes.Equipment.ArmorHealingAffectedness), positivePenalty: false);
            float hunger = AdjustPenalty(modifiers.hungerrate, Multiplier(player.Entity, TraitAttributes.Equipment.ArmorHungerAffectedness), positivePenalty: true);
            float rangedFactor = Multiplier(player.Entity, TraitAttributes.Equipment.ArmorRangedAffectedness);
            float aimAccuracy = AdjustPenalty(modifiers.rangedWeaponsAcc, rangedFactor, positivePenalty: false);
            float aimSpeed = AdjustPenalty(modifiers.rangedWeaponsSpeed, rangedFactor, positivePenalty: false);

            if (healing == modifiers.healingeffectivness && hunger == modifiers.hungerrate && aimAccuracy == modifiers.rangedWeaponsAcc && aimSpeed == modifiers.rangedWeaponsSpeed)
            {
                return modifiers;
            }

            return new StatModifiers
            {
                canEat = modifiers.canEat,
                walkSpeed = modifiers.walkSpeed,
                healingeffectivness = healing,
                hungerrate = hunger,
                rangedWeaponsAcc = aimAccuracy,
                rangedWeaponsSpeed = aimSpeed
            };
        }

        private static float AdjustPenalty(float modifier, float factor, bool positivePenalty)
        {
            bool isPenalty = positivePenalty ? modifier > 0f : modifier < 0f;
            return isPenalty ? modifier * factor : modifier;
        }
    }
}
