using Vintagestory.API.Common;

namespace CobbsClasses.Source.Conditions
{
    // Precompute tag strings
    internal sealed class ConditionRules
    {
        private static readonly (string Input, string Output)[] StandardStats =
        [
            ("moveSpeed", "walkspeed"),
            ("globalDamage", "globalDamage"),
            ("globalSpeedMul", "globalSpeedMul"),
            ("miningSpeedMul", "miningSpeedMul"),
            ("healthRegenRate", "healthRegenRate"),
            ("hungerrate", "hungerrate"),
            ("rangedWeaponsAcc", "rangedWeaponsAcc"),
            ("damageResistance", "damageResistance"),
            ("coldTemperatureRange", "coldTemperatureRange"),
            ("swimSpeed", "swimSpeed")
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