using System;

namespace UsefulStats.Core
{
    public static class CraftableEfficiencyMetrics
    {
        public static float Ratio(float numerator, float denominator)
        {
            if (denominator <= 0f || float.IsNaN(numerator) || float.IsNaN(denominator) || float.IsInfinity(numerator) || float.IsInfinity(denominator))
            {
                return 0f;
            }
            return numerator / denominator;
        }

        public static float PerDisplayedWork(float numerator, float rawWorkAmount)
        {
            return Ratio(numerator, rawWorkAmount / 60f);
        }

        public static string FormatRatio(float value)
        {
            float abs = Math.Abs(value);
            if (abs >= 100f) return value.ToString("0");
            if (abs >= 10f) return value.ToString("0.0");
            if (abs >= 1f) return value.ToString("0.00");
            if (abs >= 0.01f) return value.ToString("0.000");
            if (abs >= 0.001f) return value.ToString("0.0000");
            return value == 0f ? "0" : value.ToString("0.#####");
        }
    }
}
