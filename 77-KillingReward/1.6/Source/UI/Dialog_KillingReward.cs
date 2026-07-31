using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace KillingReward
{
    public class Dialog_KillingReward : Window
    {
        private enum View { Main, Research }

        private View view = View.Main;
        private Vector2 scrollPosition;

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
            listing.End();

            Rect body = new Rect(inRect.x, inRect.y + 130f, inRect.width, inRect.height - 130f);
            if (view == View.Research)
            {
                DoResearchView(body, tracker);
                return;
            }
            if (tracker.PendingRewards <= 0)
            {
                Widgets.Label(body, "KR_NoPending".Translate());
                return;
            }
            Listing_Standard main = new Listing_Standard();
            main.Begin(body);
            DoMainView(main, tracker);
            main.End();
        }

        private void DoMainView(Listing_Standard listing, KillRewardTracker tracker)
        {
            bool hasPending = tracker.PendingRewards > 0;
            GUI.color = new Color(1f, 1f, 1f, hasPending ? 1f : 0.4f);
            if (listing.ButtonTextLabeled("KR_RewardResearch".Translate(), "KR_PickProject".Translate()) && hasPending)
            {
                view = View.Research;
            }
            GUI.color = Color.white;
            listing.Label("KR_RewardResearchDesc".Translate());
        }

        private void DoResearchView(Rect inRect, KillRewardTracker tracker)
        {
            List<ResearchProjectDef> projects = ResearchReward.Available();
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KR_PickProject".Translate());
            Rect listRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 76f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, projects.Count * 32f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (ResearchProjectDef project in projects)
            {
                Rect row = new Rect(0f, y, viewRect.width, 30f);
                if (Widgets.ButtonText(row, project.LabelCap + " (" + project.baseCost + ")"))
                {
                    if (tracker.TryConsumeReward())
                    {
                        ResearchReward.Complete(project);
                        Messages.Message("KR_Claimed".Translate(), MessageTypeDefOf.PositiveEvent);
                        Close();
                    }
                }
                y += 32f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = View.Main;
            }
        }
    }
}
