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
        private long traitMods;
        private long geneMods;

        public long Level => level;
        public long Progress => progress;
        public int PendingRewards => pending;
        public long TraitMods => traitMods;
        public long GeneMods => geneMods;

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

        // 特质/基因修改的点数消耗：与血祭升级共用 ProgressCurve（指数/线性，四舍五入）。
        // 计数全局共享，每次修改后下一次更贵。
        public long TraitModCost
        {
            get
            {
                KillingRewardSettings s = KillingRewardMod.Settings;
                return ProgressCurve.RequiredKills(s.TraitModMode, s.TraitModInitialCost, s.TraitModFactor, s.TraitModIncrement, traitMods);
            }
        }

        public long GeneModCost
        {
            get
            {
                KillingRewardSettings s = KillingRewardMod.Settings;
                return ProgressCurve.RequiredKills(s.GeneModMode, s.GeneModInitialCost, s.GeneModFactor, s.GeneModIncrement, geneMods);
            }
        }

        public bool TryConsumeTraitMod()
        {
            long cost = TraitModCost;
            if (pending < cost)
            {
                return false;
            }
            pending -= (int)cost;
            traitMods++;
            return true;
        }

        public bool TryConsumeGeneMod()
        {
            long cost = GeneModCost;
            if (pending < cost)
            {
                return false;
            }
            pending -= (int)cost;
            geneMods++;
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref level, "level", 0L);
            Scribe_Values.Look(ref progress, "progress", 0L);
            Scribe_Values.Look(ref pending, "pending", 0);
            Scribe_Values.Look(ref traitMods, "traitMods", 0L);
            Scribe_Values.Look(ref geneMods, "geneMods", 0L);
        }
    }
}
