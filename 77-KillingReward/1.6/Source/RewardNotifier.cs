using RimWorld;
using Verse;

namespace KillingReward
{
    public static class RewardNotifier
    {
        public static void NotifyLevelUp()
        {
            ChoiceLetter letter = LetterMaker.MakeLetter(
                "KR_LetterTitle".Translate(),
                "KR_LetterText".Translate(),
                KillingRewardDefOf.KillingRewardBoon);
            Find.LetterStack.ReceiveLetter(letter);
        }
    }
}
