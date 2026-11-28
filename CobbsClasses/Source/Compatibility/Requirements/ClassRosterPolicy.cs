using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CobbsClasses.Source.Compatibility.Requirements
{
    internal static class ClassRosterPolicy
    {
        private static readonly HashSet<string> VanillaCodes = new(StringComparer.Ordinal)
        {
            "commoner", "hunter", "malefactor", "clockmaker", "blackguard", "tailor"
        };

        internal static void ApplyVanilla(JArray vanilla, bool disableVanilla)
        {
            if (!disableVanilla)
            {
                return;
            }

            foreach (JObject entry in vanilla.OfType<JObject>())
            {
                if (VanillaCodes.Contains((string?)entry["code"] ?? ""))
                {
                    entry["enabled"] = false;
                }
            }
        }

        internal static void ApplyLocal(JArray classes, bool frameworkOnly, IEnumerable<string> externalCodes)
        {
            HashSet<string> external = new(externalCodes, StringComparer.Ordinal);
            foreach (JObject entry in classes.OfType<JObject>())
            {
                string code = (string?)entry["code"] ?? "";
                if (frameworkOnly || external.Contains(code))
                {
                    entry["enabled"] = false;
                }
            }
        }

        internal static IEnumerable<string> EnabledCodes(JArray classes)
            => classes.OfType<JObject>()
                .Where(entry => entry["enabled"] is null || (entry["enabled"]?.Type == JTokenType.Boolean && (bool)entry["enabled"]!))
                .Select(entry => (string?)entry["code"])
                .OfType<string>()
                .Where(code => !string.IsNullOrWhiteSpace(code));

        internal static string? MigrationTarget(string? current, IEnumerable<string> available, IReadOnlyDictionary<string, string> mappings)
        {
            if (current is null || !current.StartsWith("cobbs-", StringComparison.Ordinal))
            {
                return null;
            }

            HashSet<string> codes = new(available, StringComparer.Ordinal);
            if (codes.Contains(current))
            {
                return null;
            }

            if (mappings.TryGetValue(current, out string? mapped) && codes.Contains(mapped))
            {
                return mapped;
            }

            if (codes.Contains("cobbs-commoner"))
            {
                return "cobbs-commoner";
            }

            if (codes.Contains("commoner"))
            {
                return "commoner";
            }

            return codes.OrderBy(code => code, StringComparer.Ordinal).FirstOrDefault();
        }
    }
}
