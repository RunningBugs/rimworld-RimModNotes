using RimWorld;
using UnityEngine;
using Verse;

namespace KillingReward
{
    public class Dialog_KillingReward : Window
    {
        public Dialog_KillingReward()
        {
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override Vector2 InitialSize => new Vector2(640f, 480f);

        public override void DoWindowContents(Rect inRect)
        {
            KillRewardTracker tracker = KillRewardTracker.Instance;
            if (tracker == null)
            {
                return;
            }
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            Text.Font = GameFont.Medium;
            listing.Label("KR_WindowTitle".Translate());
            Text.Font = GameFont.Small;
            listing.Label("KR_Tier".Translate() + ": " + tracker.Level);
            listing.Label("KR_Pending".Translate() + ": " + tracker.PendingRewards);
            listing.Label("KR_Progress".Translate() + ": " + tracker.Progress + " / " + tracker.RequiredForCurrentLevel);
            if (tracker.PendingRewards <= 0)
            {
                listing.Label("KR_NoPending".Translate());
            }
            listing.End();
        }
    }
}
