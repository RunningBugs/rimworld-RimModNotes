using System;

namespace UsefulStats.Core
{
    public static class CraftableEfficiencyMetrics
    {
        public static float Ratio(float numerator, float workAmount)
        {
            if (workAmount <= 0f || float.IsNaN(numerator) || float.IsNaN(workAmount) || float.IsInfinity(numerator) || float.IsInfinity(workAmount))
            {
                return 0f;
            }
            return numerator / workAmount;
        }

        public static string FormatRatio(float value)
        {
            if (Math.Abs(value) >= 100f) return value.ToString("0");
            if (Math.Abs(value) >= 10f) return value.ToString("0.0");
            return value.ToString("0.00");
        }
    }
}
