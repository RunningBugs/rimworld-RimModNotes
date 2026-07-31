using RimWorld;
using Verse;

namespace KillingReward
{
    [DefOf]
    public static class KillingRewardDefOf
    {
        public static LetterDef KillingRewardBoon;

        static KillingRewardDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(KillingRewardDefOf));
        }
    }
}
