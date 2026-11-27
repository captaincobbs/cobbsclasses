using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Newtonsoft.Json.Linq;

using Vintagestory.GameContent;

namespace CobbsClasses.Source.Compatibility.Requirements;

internal static class TraitDescriptionOverrides
{
    private static Dictionary<(string Trait, string Attribute), string> descriptions = [];

    internal static void Load(JArray traits)
    {
        Dictionary<(string, string), string> next = [];
        foreach (JObject trait in traits.OfType<JObject>())
        {
            if (trait["code"] is not JValue code || trait["attributeDescriptions"] is not JObject overrides)
            {
                continue;
            }

            foreach (JProperty description in overrides.Properties())
            {
                if (description.Value.Type == JTokenType.String && (string?)description.Value is string key && trait["attributes"]?[description.Name] is not null)
                {
                    next[((string)code!, description.Name)] = key;
                }
            }
        }

        descriptions = next;
    }

    internal static void Clear()
    {
        descriptions = [];
    }

    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        List<CodeInstruction> code = [.. instructions];
        int traitLocal = original.GetMethodBody()!.LocalVariables.Single(local => local.LocalType == typeof(Trait)).LocalIndex;
        MethodInfo formatter = AccessTools.Method(typeof(string), nameof(string.Format), [typeof(IFormatProvider), typeof(string), typeof(object), typeof(object)]);
        int pattern = code.FindIndex(instruction => Equals(instruction.operand, "charattribute-{0}-{1}"));
        int index = pattern < 0 ? -1 : code.FindIndex(pattern + 1, instruction => instruction.Calls(formatter));
        if (index < 0)
        {
            throw new InvalidOperationException("Expected trait description formatter not found.");
        }

        CodeInstruction loadTrait = CodeInstruction.LoadLocal(traitLocal);
        loadTrait.labels.AddRange(code[index].labels);
        code[index].labels.Clear();
        code.Insert(index, loadTrait);
        code[index + 1].opcode = OpCodes.Call;
        code[index + 1].operand = AccessTools.Method(typeof(TraitDescriptionOverrides), nameof(Format));
        return code;
    }

    private static string Format(IFormatProvider culture, string format, object attribute, object value, Trait trait)
    {
        if (attribute is string name && descriptions.TryGetValue((trait.Code, name), out string? key))
        {
            return key;
        }

        return string.Format(culture, format, attribute, value);
    }
}