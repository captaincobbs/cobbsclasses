using System;

using Vintagestory.GameContent;

namespace CobbsClasses.Source.Conditions
{
    internal sealed class SatietyCondition(float satedThreshold, float fullThreshold) : IConditionalTrait
    {
        private static readonly ConditionRules Sated = new(
            ("globalDamageWhenSated", "globalDamage"),
            ("globalSpeedMulWhenSated", "globalSpeedMul"),
            ("moveSpeedWhenSated", "walkspeed"),
            ("healthRegenRateWhenSated", "healthRegenRate"));

        public void Evaluate(in ConditionContext context, ConditionalModifiers modifiers)
        {
            if (context.Entity.GetBehavior<EntityBehaviorHunger>() is not { } hunger || hunger.MaxSaturation <= 0f)
            {
                return;
            }

            float fullness = hunger.Saturation / hunger.MaxSaturation;
            if (fullness < satedThreshold)
            {
                return;
            }

            bool full = fullness >= fullThreshold;
            float multiplier = full ? Math.Max(0f, context.Stats.GetBlended("satedBonusFullMultiplier")) : 1f;
            Sated.Apply(context.Stats, modifiers, multiplier);

            if (full)
            {
                modifiers.Add("globalSpeedMul", context.Stats.GetBlended("globalSpeedMulWhenFull") - 1f);
            }
        }
    }
}