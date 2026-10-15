namespace KillingReward.Core
{
    public static class KillEligibility
    {
        public static bool ShouldCount(bool victimHasFaction, bool victimHostileToPlayer, bool instigatorIsPlayerPawn)
        {
            return victimHasFaction && victimHostileToPlayer && instigatorIsPlayerPawn;
        }
    }
}
