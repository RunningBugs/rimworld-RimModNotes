using KillingReward.Core;
using RimWorld;
using Verse;

namespace KillingReward
{
    public static class KillEligibilityAdapter
    {
        public static bool ShouldCount(Pawn victim, DamageInfo? dinfo)
        {
            bool victimHasFaction = victim?.Faction != null;
            bool victimHostileToPlayer = victimHasFaction && victim.Faction.HostileTo(Faction.OfPlayer);
            bool instigatorIsPlayerPawn = dinfo.HasValue
                && dinfo.Value.Instigator is Pawn instigator
                && instigator.Faction != null
                && instigator.Faction.IsPlayer;
            if (KillEligibility.ShouldCount(victimHasFaction, victimHostileToPlayer, instigatorIsPlayerPawn))
            {
                return true;
            }
            // 归属补窗：直接击杀者不是我方，但该敌对单位近期被我方小人伤害过
            // （如被打倒后流血/休克而死），同样计为我方击杀。
            return victimHostileToPlayer && PlayerDamageTracker.WasRecentlyDamagedByPlayer(victim);
        }
    }
}
