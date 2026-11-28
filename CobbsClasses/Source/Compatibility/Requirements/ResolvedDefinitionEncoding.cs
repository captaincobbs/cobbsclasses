using System;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CobbsClasses.Source.Compatibility.Requirements
{
    internal static class ResolvedDefinitionEncoding
    {
        private const string Prefix = "base64:";

        internal static string Encode(JObject definitions)
        {
            return Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(definitions.ToString(Formatting.None, Array.Empty<JsonConverter>())));
        }

        internal static JObject Decode(string definitions)
        {
            string json = definitions.StartsWith(Prefix, StringComparison.Ordinal) ? Encoding.UTF8.GetString(Convert.FromBase64String(definitions[Prefix.Length..])) : definitions;
            return JObject.Parse(json);
        }
    }
}