using System;
using System.Collections.Generic;

using Vintagestory.API.Common;

namespace CobbsClasses.Source.API
{
    public static class TraitMath
    {
        public static float GetMultiplier(EntityStats? stats, string attribute, float minimum = 0f, float maximum = float.MaxValue)
        {
            return ClampFinite(stats?.GetBlended(attribute) ?? 1f, minimum, maximum, 1f);
        }

        public static float GetAdditive(EntityStats? stats, string attribute)
        {
            float value = stats?.GetBlended(attribute) ?? 1f;
            return float.IsFinite(value) ? value - 1f : 0f;
        }

        public static float GetProbability(EntityStats? stats, string attribute)
        {
            return Math.Clamp(GetAdditive(stats, attribute), 0f, 1f);
        }

        public static bool GetBoolean(EntityStats? stats, string attribute)
        {
            return GetAdditive(stats, attribute) > 0f;
        }

        public static float ClampFinite(float value, float minimum, float maximum, float fallback)
        {
            if (!float.IsFinite(minimum) || !float.IsFinite(maximum) || minimum > maximum)
            {
                throw new ArgumentOutOfRangeException(nameof(minimum));
            }

            return Math.Clamp(float.IsFinite(value) ? value : float.IsFinite(fallback) ? fallback : 0f, minimum, maximum);
        }

        public static int ScaleQuantity(Random random, int quantity, double multiplier, int maximum = int.MaxValue)
        {
            ArgumentNullException.ThrowIfNull(random);
            ArgumentOutOfRangeException.ThrowIfNegative(maximum);
            double amount = Math.Clamp(Math.Max(0, quantity) * (double.IsFinite(multiplier) ? Math.Max(0d, multiplier) : 1d), 0d, maximum);
            int integral = (int)Math.Floor(amount);
            return amount > integral && random.NextDouble() < amount - integral ? integral + 1 : integral;
        }

        public static int ScaleDurabilityLoss(Random random, int quantity, double multiplier, int maximum = int.MaxValue) => ScaleQuantity(random, quantity, multiplier, maximum);

        public static int ScaleCapacity(int quantity, double multiplier, int minimum = 0, int maximum = int.MaxValue) => (int)Math.Clamp(Math.Round(Math.Max(0, quantity) * (double.IsFinite(multiplier) ? Math.Max(0d, multiplier) : 1d)), minimum, maximum);

        public static float GetDuration(float duration, float multiplier) => (float)Math.Clamp((double)ClampFinite(duration, 0f, float.MaxValue, 0f) * ClampFinite(multiplier, 0f, float.MaxValue, 1f), 0d, float.MaxValue);

        public static float ScaleProcessingTime(float duration, float speed) => GetDuration(duration, 1f / ClampFinite(speed, 0.05f, float.MaxValue, 1f));

        public static float Combine(EntityStats? stats, IEnumerable<string?> attributes, float minimum = 0f, float maximum = float.MaxValue)
        {
            double result = 1d;
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (string? attribute in attributes)
            {
                if (!string.IsNullOrWhiteSpace(attribute) && seen.Add(attribute))
                {
                    result += GetAdditive(stats, attribute);
                }
            }

            return ClampFinite((float)Math.Clamp(result, -float.MaxValue, float.MaxValue), minimum, maximum, 1f);
        }
    }
}
