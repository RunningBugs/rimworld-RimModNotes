using System.Collections.Generic;
using KillingReward.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace KillingReward
{
    public class Dialog_KillingReward : Window
    {
        private enum View { Main, Research, SkillPawn, SkillSkill, ItemCategory, ItemThing }

        private View view = View.Main;
        private Vector2 scrollPosition;
        private Pawn selectedPawn;
        private ThingCategoryDef selectedCategory;

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
            if (view == View.SkillPawn)
            {
                DoSkillPawnView(body, tracker);
                return;
            }
            if (view == View.SkillSkill && selectedPawn != null)
            {
                DoSkillSkillView(body, tracker);
                return;
            }
            if (view == View.ItemCategory)
            {
                DoItemCategoryView(body);
                return;
            }
            if (view == View.ItemThing && selectedCategory != null)
            {
                DoItemThingView(body, tracker);
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
            GUI.color = new Color(1f, 1f, 1f, hasPending ? 1f : 0.4f);
            if (listing.ButtonTextLabeled("KR_RewardSkill".Translate(), "KR_PickPawn".Translate()) && hasPending)
            {
                view = View.SkillPawn;
            }
            GUI.color = Color.white;
            listing.Label("KR_RewardSkillDesc".Translate());
            GUI.color = new Color(1f, 1f, 1f, hasPending ? 1f : 0.4f);
            if (listing.ButtonTextLabeled("KR_RewardItem".Translate(), "KR_PickItem".Translate()) && hasPending)
            {
                view = View.ItemCategory;
            }
            GUI.color = Color.white;
            listing.Label("KR_RewardItemDesc".Translate());
        }

        private void DoItemCategoryView(Rect inRect)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KR_PickItem".Translate());
            float y = inRect.y + 40f;
            foreach (ThingCategoryDef category in ItemReward.RootCategories)
            {
                if (Widgets.ButtonText(new Rect(inRect.x, y, inRect.width, 32f), category.LabelCap))
                {
                    selectedCategory = category;
                    view = View.ItemThing;
                }
                y += 36f;
            }
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = View.Main;
            }
        }

        private void DoItemThingView(Rect inRect, KillRewardTracker tracker)
        {
            List<ThingDef> things = ItemReward.ThingsIn(selectedCategory);
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KR_PickItem".Translate() + " (" + selectedCategory.LabelCap + ")");
            Rect listRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 76f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, things.Count * 32f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (ThingDef thingDef in things)
            {
                Rect row = new Rect(0f, y, viewRect.width, 30f);
                if (Widgets.ButtonText(row, thingDef.LabelCap + " ×" + StackMath.FullStackCount(thingDef.stackLimit)))
                {
                    BeginItemTargeting(thingDef, tracker);
                }
                y += 32f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = View.ItemCategory;
            }
        }

        private void BeginItemTargeting(ThingDef thingDef, KillRewardTracker tracker)
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            Close();
            TargetingParameters parameters = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetSelf = false,
                validator = ti => ti.Cell.InBounds(map) && ti.Cell.Walkable(map) && !ti.Cell.Fogged(map)
            };
            Find.Targeter.BeginTargeting(parameters, delegate(LocalTargetInfo target)
            {
                if (tracker.TryConsumeReward())
                {
                    ItemReward.Deliver(thingDef, target.Cell, map);
                    Messages.Message("KR_Claimed".Translate(), MessageTypeDefOf.PositiveEvent);
                }
            }, null, null, null, "KR_PickCell".Translate());
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

        private void DoSkillPawnView(Rect inRect, KillRewardTracker tracker)
        {
            List<Pawn> pawns = SkillReward.Candidates();
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KR_PickPawn".Translate());
            Rect listRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 76f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, pawns.Count * 32f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (Pawn pawn in pawns)
            {
                Rect row = new Rect(0f, y, viewRect.width, 30f);
                if (Widgets.ButtonText(row, pawn.LabelShortCap))
                {
                    selectedPawn = pawn;
                    view = View.SkillSkill;
                }
                y += 32f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = View.Main;
            }
        }

        private void DoSkillSkillView(Rect inRect, KillRewardTracker tracker)
        {
            List<SkillRecord> skills = SkillReward.AvailableSkills(selectedPawn);
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KR_PickSkill".Translate() + " (" + selectedPawn.LabelShortCap + ")");
            Rect listRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 76f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, skills.Count * 32f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (SkillRecord skill in skills)
            {
                Rect row = new Rect(0f, y, viewRect.width, 30f);
                string label = skill.def.LabelCap + " " + skill.Level + " → " + SkillMath.ClampedAdd(skill.Level, 3, SkillRecord.MaxLevel);
                if (Widgets.ButtonText(row, label))
                {
                    if (tracker.TryConsumeReward())
                    {
                        SkillReward.Apply(skill);
                        Messages.Message("KR_Claimed".Translate(), MessageTypeDefOf.PositiveEvent);
                        Close();
                    }
                }
                y += 32f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = View.SkillPawn;
            }
        }
    }
}
