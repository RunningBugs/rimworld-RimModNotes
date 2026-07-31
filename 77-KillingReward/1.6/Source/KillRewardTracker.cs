using KillingReward.Core;
using RimWorld;
using Verse;

namespace KillingReward
{
    public class KillRewardTracker : GameComponent
    {
        private long level;
        private long progress;
        private int pending;

        public long Level => level;
        public long Progress => progress;
        public int PendingRewards => pending;

        public static KillRewardTracker Instance => Current.Game?.GetComponent<KillRewardTracker>();

        public KillRewardTracker(Game game)
        {
        }

        public long RequiredForCurrentLevel
        {
            get
            {
                KillingRewardSettings s = KillingRewardMod.Settings;
                return ProgressCurve.RequiredKills(s.Mode, s.InitialKills, s.ExponentialFactor, s.LinearIncrement, level);
            }
        }

        public void AddKill()
        {
            KillingRewardSettings s = KillingRewardMod.Settings;
            ProgressState before = new ProgressState(level, progress, pending);
            ProgressState after = before.AddKill(l => ProgressCurve.RequiredKills(s.Mode, s.InitialKills, s.ExponentialFactor, s.LinearIncrement, l));
            level = after.Level;
            progress = after.Progress;
            pending = after.Pending;
            if (after.Level > before.Level)
            {
                RewardNotifier.NotifyLevelUp();
            }
        }

        public bool TryConsumeReward()
        {
            if (pending <= 0)
            {
                return false;
            }
            pending--;
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref level, "level", 0L);
            Scribe_Values.Look(ref progress, "progress", 0L);
            Scribe_Values.Look(ref pending, "pending", 0);
        }
    }
}
