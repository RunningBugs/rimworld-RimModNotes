using System;

namespace KillingReward.Core
{
    public static class ProgressCurve
    {
        public static long RequiredKills(GrowthMode mode, int initial, double factor, int increment, long completedLevels)
        {
            if (initial < 1) initial = 1;
            if (completedLevels < 0) completedLevels = 0;
            double value;
            if (mode == GrowthMode.Exponential)
            {
                if (factor < 1.0) factor = 1.0;
                value = initial * Math.Pow(factor, completedLevels);
            }
            else
            {
                if (increment < 0) increment = 0;
                value = initial + (double)increment * completedLevels;
            }
            long rounded = (long)Math.Round(value, MidpointRounding.AwayFromZero);
            if (rounded < 1) rounded = 1;
            if (rounded > int.MaxValue) rounded = int.MaxValue;
            return rounded;
        }
    }
}
