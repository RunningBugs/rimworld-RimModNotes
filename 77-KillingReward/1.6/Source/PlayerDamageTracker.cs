using System.Collections.Generic;
using KillingReward.Core;
using RimWorld;
using Verse;

namespace KillingReward
{
    /// <summary>
    /// 记录敌对单位最近一次被玩家派系小人伤害的时间。
    /// 用于击杀归属补窗：被小人打倒后流血/休克而死的敌人也算我方击杀。
    /// 仅内存态（归属判定是即时的，无需随存档持久化）。
    /// </summary>
    public static class PlayerDamageTracker
    {
        /// <summary>归属窗口：10000 tick ≈ 4 游戏小时。</summary>
        public const int AttributionWindowTicks = 10000;

        private static readonly Dictionary<int, int> LastPlayerDamageTick = new Dictionary<int, int>();

        public static void NotifyPlayerDamaged(Pawn victim)
        {
            if (victim == null)
            {
                return;
            }
            Prune(Find.TickManager.TicksGame);
            LastPlayerDamageTick[victim.thingIDNumber] = Find.TickManager.TicksGame;
        }

        public static bool WasRecentlyDamagedByPlayer(Pawn victim)
        {
            if (victim == null)
            {
                return false;
            }
            return LastPlayerDamageTick.TryGetValue(victim.thingIDNumber, out int tick)
                && Find.TickManager.TicksGame - tick <= AttributionWindowTicks;
        }

        private static void Prune(int now)
        {
            if (LastPlayerDamageTick.Count < 256)
            {
                return;
            }
            List<int> expired = null;
            foreach (KeyValuePair<int, int> pair in LastPlayerDamageTick)
            {
                if (now - pair.Value > AttributionWindowTicks)
                {
                    (expired ??= new List<int>()).Add(pair.Key);
                }
            }
            if (expired == null)
            {
                return;
            }
            foreach (int key in expired)
            {
                LastPlayerDamageTick.Remove(key);
            }
        }
    }
}
