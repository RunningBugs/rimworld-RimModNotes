using System;

namespace KillingReward.Core
{
    public static class SkillMath
    {
        public static int ClampedAdd(int current, int delta, int max)
        {
            return Math.Max(0, Math.Min(current + delta, max));
        }
    }
}
