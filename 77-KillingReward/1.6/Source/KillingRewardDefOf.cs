using RimWorld;
using Verse;

namespace KillingReward
{
    [DefOf]
    public static class KillingRewardDefOf
    {
        public static LetterDef KillingRewardBoon;
        public static HediffDef KillingReward_BlessingRegen;

        static KillingRewardDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(KillingRewardDefOf));
        }
    }
}
