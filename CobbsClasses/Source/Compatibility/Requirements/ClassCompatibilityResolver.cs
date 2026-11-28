using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json.Linq;

namespace CobbsClasses.Source.Compatibility.Requirements
{
    internal static class ClassCompatibilityResolver
    {
        public static void Resolve(JArray classes, HashSet<string> unavailable, Func<string, bool> isEnabled,
            Func<string, bool> manuallyDisabled, bool ignoreRequirements)
        {
            foreach (JObject characterClass in classes.OfType<JObject>())
            {
                string code = (string?)characterClass["code"] ?? throw new FormatException("Class code missing.");
                if (manuallyDisabled(code) || !ModRequirementEvaluator.Matches(characterClass["mandatoryMods"], isEnabled) || (!ignoreRequirements && !ModRequirementEvaluator.Matches(characterClass["requiresMods"], isEnabled)))
                {
                    characterClass["enabled"] = false;
                }

                if (characterClass["traits"] is not JArray traits)
                {
                    traits = [];
                    characterClass["traits"] = traits;
                }
                if (characterClass["conditionalTraits"] is JArray conditional)
                {
                    foreach (JObject entry in conditional.OfType<JObject>())
                    {
                        if (!ModRequirementEvaluator.Matches(entry["requiresMods"], isEnabled) || entry["traits"] is not JArray additions)
                        {
                            continue;
                        }

                        foreach (JToken addition in additions)
                        {
                            if (addition.Type == JTokenType.String && !traits.Any(existing => JToken.DeepEquals(existing, addition)))
                            {
                                traits.Add(addition.DeepClone());
                            }
                        }
                    }
                }

                foreach (JToken trait in traits.Where(trait => unavailable.Contains((string?)trait ?? "")).ToList())
                {
                    trait.Remove();
                }

                characterClass.Remove("requiresMods");
                characterClass.Remove("mandatoryMods");
                characterClass.Remove("conditionalTraits");
            }
        }
    }
}