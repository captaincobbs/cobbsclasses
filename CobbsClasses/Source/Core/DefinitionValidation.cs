using System;
using System.Collections.Generic;
using System.Linq;

using CobbsClasses.Source.API;

using Newtonsoft.Json.Linq;

namespace CobbsClasses.Source.Core
{
    internal static class DefinitionValidation
    {
        internal static void Validate(JArray traits, JArray classes, TraitRegistry registry, Action<string> warn, IEnumerable<string>? externalTraits = null)
        {
            HashSet<string> traitCodes = Unique(traits, "trait", warn);
            if (externalTraits is not null)
            {
                traitCodes.UnionWith(externalTraits);
            }
            Unique(classes, "class", warn);
            foreach (JObject trait in traits.OfType<JObject>())
            {
                string code = (string)trait["code"]!;
                ValidateRequirements(trait, warn);
                if (trait["attributes"] is not JObject)
                {
                    trait["attributes"] = new JObject();
                }
                ValidateAttributes(trait["attributes"], code, registry, warn);
                if (trait["modifiers"] is JArray modifiers)
                {
                    foreach (JToken modifier in modifiers.ToArray())
                    {
                        if (modifier is not JObject entry || !ValidateRequirements(entry, warn))
                        {
                            modifier.Remove();
                            continue;
                        }

                        ValidateAttributes(entry["attributes"], code, registry, warn);
                    }
                }
            }

            foreach (JObject characterClass in classes.OfType<JObject>())
            {
                ValidateRequirements(characterClass, warn);
                ValidateRequirements(characterClass, warn, "mandatoryMods");
                if (characterClass["enabled"] is JToken enabled && enabled.Type != JTokenType.Boolean)
                {
                    characterClass["enabled"] = false;
                    warn($"Invalid enabled flag on {characterClass["code"]}; disabled.");
                }

                if (characterClass["gear"] is not JArray)
                {
                    characterClass["gear"] = new JArray();
                }
                CleanReferences(characterClass["traits"], traitCodes, warn);
                if (characterClass["conditionalTraits"] is JArray conditional)
                {
                    foreach (JToken entry in conditional.ToArray())
                    {
                        if (entry is not JObject condition || !ValidateRequirements(condition, warn))
                        {
                            entry.Remove();
                            continue;
                        }

                        CleanReferences(condition["traits"], traitCodes, warn);
                    }
                }
            }
        }

        private static HashSet<string> Unique(JArray definitions, string kind, Action<string> warn)
        {
            HashSet<string> codes = new(StringComparer.Ordinal);
            foreach (JToken token in definitions.ToArray())
            {
                if (token is not JObject definition || definition["code"]?.Type != JTokenType.String
                    || string.IsNullOrWhiteSpace((string?)definition["code"]) || !codes.Add((string)definition["code"]!))
                {
                    warn($"Removed malformed or duplicate {kind} definition.");
                    token.Remove();
                }
            }

            return codes;
        }

        private static bool ValidateRequirements(JObject definition, Action<string> warn, string key = "requiresMods")
        {
            if (definition[key] is not JToken requirement)
            {
                return true;
            }

            if (requirement is JObject conditions && conditions.Properties().All(condition =>
                condition.Name is "allOf" or "anyOf" or "noneOf" && condition.Value is JArray mods &&
                mods.All(mod => mod.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string?)mod))))
            {
                return true;
            }

            warn($"Invalid mod requirements on {definition["code"] ?? new JValue("modifier")}; requirement will never match.");
            definition[key] = new JObject { ["anyOf"] = new JArray() };
            return false;
        }

        private static void ValidateAttributes(JToken? token, string code, TraitRegistry registry, Action<string> warn, IEnumerable<string>? externalTraits = null)
        {
            if (token is not JObject attributes)
            {
                return;
            }

            foreach (JProperty attribute in attributes.Properties().ToArray())
            {
                if (attribute.Value.Type is not (JTokenType.Integer or JTokenType.Float)
                    || !double.IsFinite((double)attribute.Value) || Math.Abs((double)attribute.Value) > float.MaxValue)
                {
                    warn($"Removed invalid numeric value {code}/{attribute.Name}.");
                    attribute.Remove();
                    continue;
                }

                if (!registry.TryGet(attribute.Name, out TraitAttributeDefinition? definition) || definition is null)
                {
                    warn($"Unregistered attribute {code}/{attribute.Name}; retained for external mods.");
                    continue;
                }

                double value = (double)attribute.Value;
                if (value < definition.Minimum || value > definition.Maximum ||
                    (definition.Kind == AttributeKind.Boolean && value is not (0 or 1)))
                {
                    warn($"Out-of-range attribute {code}/{attribute.Name}: {value}; retained for compatibility.");
                }
            }
        }

        private static void CleanReferences(JToken? token, HashSet<string> known, Action<string> warn)
        {
            if (token is not JArray traits)
            {
                return;
            }

            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (JToken reference in traits.ToArray())
            {
                if (reference.Type != JTokenType.String || !seen.Add((string)reference!))
                {
                    warn($"Removed unresolved or duplicate trait reference {reference}.");
                    reference.Remove();
                }
                else if (!known.Contains((string)reference!))
                {
                    warn($"Removed unresolved trait reference {reference}.");
                    reference.Remove();
                }
            }
        }

        internal static void EnsureAvailable(JArray classes, Action<string> warn)
        {
            if (classes.OfType<JObject>().Any(characterClass => (bool?)characterClass["enabled"] != false))
            {
                return;
            }

            JObject? commoner = classes.OfType<JObject>().FirstOrDefault(characterClass => (string?)characterClass["code"] == "cobbs-commoner");
            if (commoner is null)
            {
                commoner = new JObject { ["code"] = "cobbs-commoner", ["traits"] = new JArray(), ["gear"] = new JArray() };
                classes.Add(commoner);
            }

            commoner["enabled"] = true;
            warn("No enabled CobbsClasses classes remain; enabled emergency Commoner.");
        }
    }
}