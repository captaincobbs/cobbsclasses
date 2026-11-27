using System;
using System.Collections.Generic;

namespace CobbsClasses.Source.Conditions
{
    public sealed class ConditionalModifiers
    {
        private readonly Dictionary<string, float> values = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, float> Values => values;

        public void Add(string stat, float value)
        {
            if (value == 0f || !float.IsFinite(value))
            {
                return;
            }

            float total = values.GetValueOrDefault(stat) + value;
            if (!float.IsFinite(total))
            {
                return;
            }

            if (total == 0f)
            {
                values.Remove(stat);
            }
            else
            {
                values[stat] = total;
            }
        }

        public void Clear() => values.Clear();
    }

}