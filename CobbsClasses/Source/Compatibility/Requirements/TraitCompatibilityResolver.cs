using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json.Linq;

namespace CobbsClasses.Source.Compatibility.Requirements;

internal static class TraitCompatibilityResolver
{
    public static HashSet<string> Resolve(JArray traits, Func<string, bool> isEnabled)
    {
        HashSet<string> unavailable = new(StringComparer.Ordinal);
        foreach (JObject trait in traits.OfType<JObject>())
        {
            string code = (string?)trait["code"] ?? throw new FormatException("Trait code missing.");
            if (trait["attributes"] is not JObject attributes)
            {
                attributes = [];
                trait["attributes"] = attributes;
            }
            if (!ModRequirementEvaluator.Matches(trait["requiresMods"], isEnabled))
            {
                unavailable.Add(code);
                attributes.RemoveAll();
            }
            else if (trait["modifiers"] is JArray modifiers)
            {
                foreach (JObject modifier in modifiers.OfType<JObject>())
                {
                    if (!ModRequirementEvaluator.Matches(modifier["requiresMods"], isEnabled))
                    {
                        continue;
                    }

                    if (modifier["attributes"] is JObject additions)
                    {
                        foreach (JProperty attribute in additions.Properties())
                        {
                            attributes[attribute.Name] = attribute.Value.DeepClone();
                        }
                    }
                }
            }

            trait.Remove("requiresMods");
            trait.Remove("modifiers");
        }

        return unavailable;
    }
}