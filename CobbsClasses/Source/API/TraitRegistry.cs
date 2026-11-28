using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CobbsClasses.Source.API
{
    public sealed class TraitRegistry
    {
        private readonly Dictionary<string, TraitAttributeDefinition> definitions = new(StringComparer.Ordinal);

        public TraitRegistry()
        {
            foreach (Type category in typeof(TraitAttributes).GetNestedTypes(BindingFlags.Public))
            {
                foreach (FieldInfo field in category.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (field.IsLiteral && field.GetRawConstantValue() is string code)
                    {
                        definitions.TryAdd(code, new TraitAttributeDefinition(code, AttributeKind.Additive, 0, -double.MaxValue, double.MaxValue));
                    }
                }
            }

            // Item behavior attributes use fractional modifiers; reach is measured in blocks.
            foreach (FieldInfo field in typeof(TraitAttributes.Tools).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetRawConstantValue() is not string code)
                {
                    continue;
                }

                if (code.EndsWith("DurabilityLoss", StringComparison.Ordinal))
                {
                    Define(code, AttributeKind.Multiplier, -1, double.MaxValue);
                }
                else if (code.EndsWith("SpeedMul", StringComparison.Ordinal))
                {
                    Define(code, AttributeKind.Multiplier, -0.95, double.MaxValue);
                }
                else if (code.EndsWith("Reach", StringComparison.Ordinal))
                {
                    Define(code, AttributeKind.Additive, -100, 100);
                }
            }

            Define("hotTemperatureRange", AttributeKind.Temperature, -100, 100);
            Define("trackdecay", AttributeKind.Multiplier, -1, double.MaxValue, ["footprints"]);
            Define("carcassDrainRate", AttributeKind.Additive, -double.MaxValue, double.MaxValue, ["butchering"]);
            Define("heavyCarryMovespeedAffectedness", AttributeKind.Additive, -double.MaxValue, double.MaxValue, ["butchering"]);
            Define("ruinedCarcassPenalty", AttributeKind.Additive, -double.MaxValue, double.MaxValue, ["butchering"]);
            Define("coatRaw", AttributeKind.Boolean, 0, 1, ["alchemy"]);
            Define("eatReagents", AttributeKind.Boolean, 0, 1, ["alchemy"]);
            Define("poisonPenalty", AttributeKind.Additive, -double.MaxValue, double.MaxValue, ["alchemy"]);
            Define("wetResistance", AttributeKind.Probability, 0, 1);
            Define("unrestedSpeedPenalty", AttributeKind.Boolean, 0, 1);
            Define("coldTemperatureRange", AttributeKind.Temperature, -100, 100);
            Define("toolSpeedMul", AttributeKind.Multiplier, -0.95, double.MaxValue);
            Define("globalSpeedMul", AttributeKind.Multiplier, -0.95, double.MaxValue);
            Define("maxHealthmul", AttributeKind.Multiplier, -0.95, double.MaxValue);
            Define("weaponDurabilityLoss", AttributeKind.Multiplier, -1, double.MaxValue);
            Define("toolDurabilityLoss", AttributeKind.Multiplier, -1, double.MaxValue);
            Define("globalDurabilityLoss", AttributeKind.Multiplier, -1, double.MaxValue);
            Define("globalReach", AttributeKind.Additive, -100, 100);
        }

        private void Define(string code, AttributeKind kind, double minimum, double maximum, string[]? requiredMods = null)
        {
            definitions[code] = new TraitAttributeDefinition(code, kind, 0, minimum, maximum, requiredMods);
        }

        public IReadOnlyList<TraitAttributeDefinition> Definitions => [.. definitions.Values.Select(Clone)];

        public bool TryGet(string code, out TraitAttributeDefinition? definition)
        {
            if (definitions.TryGetValue(code, out TraitAttributeDefinition? stored))
            {
                definition = Clone(stored);
                return true;
            }

            definition = null;
            return false;
        }

        private static TraitAttributeDefinition Clone(TraitAttributeDefinition definition) => definition with { RequiredMods = definition.RequiredMods?.ToArray() };

        public void Register(TraitAttributeDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);
            if (string.IsNullOrWhiteSpace(definition.Code) || !double.IsFinite(definition.DefaultValue) || double.IsNaN(definition.Minimum) || double.IsNaN(definition.Maximum) || definition.Minimum > definition.Maximum || definition.DefaultValue < definition.Minimum || definition.DefaultValue > definition.Maximum)
            {
                throw new ArgumentException("Invalid attribute definition.", nameof(definition));
            }

            if (!definitions.TryAdd(definition.Code, Clone(definition)))
            {
                throw new InvalidOperationException($"Attribute {definition.Code} is already registered.");
            }
        }
    }
}