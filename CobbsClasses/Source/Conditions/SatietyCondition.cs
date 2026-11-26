using System;

using Vintagestory.GameContent;

namespace CobbsClasses.Source.Conditions
{
    internal sealed class SatietyCondition(float satedThreshold, float fullThreshold) : IConditionalTrait
    {
        private static readonly ConditionRules Sated = new(
            (TraitAttributes.Combat.GlobalDamageWhenSated, TraitAttributes.Combat.GlobalDamage),
            (TraitAttributes.Tools.GlobalSpeedMulWhenSated, TraitAttributes.Tools.GlobalSpeedMul),
            (TraitAttributes.Movement.MoveSpeedWhenSated, TraitAttributes.Movement.Walkspeed),
            (TraitAttributes.Survival.HealthRegenRateWhenSated, TraitAttributes.Survival.HealthRegenRate));

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
            float multiplier = full ? Math.Max(0f, context.Stats.GetBlended(TraitAttributes.Survival.SatedBonusFullMultiplier)) : 1f;
            Sated.Apply(context.Stats, modifiers, multiplier);

            if (full)
            {
                modifiers.Add(TraitAttributes.Tools.GlobalSpeedMul, context.Stats.GetBlended(TraitAttributes.Tools.GlobalSpeedMulWhenFull) - 1f);
            }
        }
    }
}