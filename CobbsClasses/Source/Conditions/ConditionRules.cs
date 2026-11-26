using Vintagestory.API.Common;

namespace CobbsClasses.Source.Conditions
{
    // Precompute tag strings
    internal sealed class ConditionRules
    {
        private static readonly (string Input, string Output)[] StandardStats =
        [
            (TraitAttributes.Movement.MoveSpeed, TraitAttributes.Movement.Walkspeed),
            (TraitAttributes.Combat.GlobalDamage, TraitAttributes.Combat.GlobalDamage),
            (TraitAttributes.Tools.GlobalSpeedMul, TraitAttributes.Tools.GlobalSpeedMul),
            (TraitAttributes.Tools.MiningSpeedMul, TraitAttributes.Tools.MiningSpeedMul),
            (TraitAttributes.Survival.HealthRegenRate, TraitAttributes.Survival.HealthRegenRate),
            (TraitAttributes.Survival.Hungerrate, TraitAttributes.Survival.Hungerrate),
            (TraitAttributes.Combat.RangedWeaponsAcc, TraitAttributes.Combat.RangedWeaponsAcc),
            (TraitAttributes.Combat.DamageResistance, TraitAttributes.Combat.DamageResistance),
            (TraitAttributes.Survival.ColdTemperatureRange, TraitAttributes.Survival.ColdTemperatureRange),
            (TraitAttributes.Movement.SwimSpeed, TraitAttributes.Movement.SwimSpeed)
        ];

        private readonly (string Input, string Output)[] rules;

        public ConditionRules(params (string Input, string Output)[] rules)
        {
            this.rules = rules;
        }

        public static ConditionRules WithSuffix(string suffix)
            => WithSuffix(suffix, StandardStats);

        public static ConditionRules WithSuffix(string suffix, (string Input, string Output)[] stats)
        {
            (string Input, string Output)[] rules = new (string Input, string Output)[stats.Length];
            for (int index = 0; index < stats.Length; index++)
            {
                rules[index] = (stats[index].Input + suffix, stats[index].Output);
            }

            return new ConditionRules(rules);
        }

        public bool HasAny(EntityStats stats)
        {
            foreach ((string Input, string Output) rule in rules)
            {
                if (stats.GetBlended(rule.Input) != 1f)
                {
                    return true;
                }
            }

            return false;
        }

        public void Apply(EntityStats stats, ConditionalModifiers modifiers, float multiplier = 1f)
        {
            foreach ((string Input, string Output) rule in rules)
            {
                modifiers.Add(rule.Output, (stats.GetBlended(rule.Input) - 1f) * multiplier);
            }
        }
    }
}