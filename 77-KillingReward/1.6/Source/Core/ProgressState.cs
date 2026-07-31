using System;

namespace KillingReward.Core
{
    public readonly struct ProgressState
    {
        public readonly long Level;
        public readonly long Progress;
        public readonly int Pending;

        public ProgressState(long level, long progress, int pending)
        {
            Level = level;
            Progress = progress;
            Pending = pending;
        }

        public ProgressState AddKill(Func<long, long> requiredForLevel)
        {
            long level = Level;
            long progress = Progress + 1;
            int pending = Pending;
            long required = requiredForLevel(level);
            while (required > 0 && progress >= required)
            {
                progress -= required;
                level++;
                pending++;
                required = requiredForLevel(level);
            }
            return new ProgressState(level, progress, pending);
        }
    }
}
