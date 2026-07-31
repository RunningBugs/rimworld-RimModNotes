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
            return KillEligibility.ShouldCount(victimHasFaction, victimHostileToPlayer, instigatorIsPlayerPawn);
        }
    }
}
