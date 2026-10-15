using System;

namespace KillingReward.Core
{
    public static class StackMath
    {
        public static int FullStackCount(int stackLimit)
        {
            return Math.Max(1, stackLimit);
        }
    }
}
