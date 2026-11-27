using System;
using System.Linq;

using Newtonsoft.Json.Linq;

namespace CobbsClasses.Source.Compatibility.Requirements;

internal static class ModRequirementEvaluator
{
    public static bool Matches(JToken? requirement, Func<string, bool> isEnabled)
    {
        if (requirement is null)
        {
            return true;
        }

        if (requirement is not JObject conditions)
        {
            throw new FormatException("requiresMods must be an object.");
        }

        foreach (JProperty condition in conditions.Properties())
        {
            if (condition.Value is not JArray mods ||
                mods.Any(mod => mod.Type != JTokenType.String || string.IsNullOrWhiteSpace((string?)mod)))
            {
                throw new FormatException($"Invalid mod requirement {condition.Name}.");
            }

            bool passes = condition.Name switch
            {
                "allOf" => mods.All(mod => isEnabled((string)mod!)),
                "anyOf" => mods.Any(mod => isEnabled((string)mod!)),
                "noneOf" => mods.All(mod => !isEnabled((string)mod!)),
                _ => throw new FormatException($"Unknown mod requirement {condition.Name}.")
            };

            if (!passes)
            {
                return false;
            }
        }

        return true;
    }
}