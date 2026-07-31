using System;
using System.Collections.Generic;
using System.Linq;
using KillingReward.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace KillingReward
{
    public class Dialog_KillingReward : Window
    {
        private enum View { Main, Research, SkillPawn, SkillSkill, ItemCategory, ItemThing }

        private static readonly Color CardBg = new Color(0.14f, 0.14f, 0.16f);
        private static readonly Color DescGrey = new Color(0.75f, 0.75f, 0.75f);

        private View view = View.Main;
        private Vector2 scrollPosition;
        private Pawn selectedPawn;
        private ThingCategoryDef selectedCategory;
        private string itemSearch = "";

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

            // 标题（居中）
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "KR_WindowTitle".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // 等阶（左）/ 待领取（右）
            Widgets.Label(new Rect(inRect.x, inRect.y + 48f, 300f, 22f), "KR_Tier".Translate() + ": " + tracker.Level);
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inRect.xMax - 300f, inRect.y + 48f, 300f, 22f), "KR_Pending".Translate() + ": " + tracker.PendingRewards);
            Text.Anchor = TextAnchor.UpperLeft;

            // 血祭进度条
            Rect barRect = new Rect(inRect.x, inRect.y + 74f, inRect.width, 22f);
            float fill = tracker.RequiredForCurrentLevel > 0
                ? Mathf.Clamp01((float)tracker.Progress / tracker.RequiredForCurrentLevel)
                : 0f;
            Widgets.FillableBar(barRect, fill);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.red;
            Widgets.Label(barRect, "KR_Progress".Translate() + " " + tracker.Progress + " / " + tracker.RequiredForCurrentLevel);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect body = new Rect(inRect.x, inRect.y + 112f, inRect.width, inRect.height - 112f);
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
                GUI.color = DescGrey;
                Widgets.Label(body, "KR_NoPending".Translate());
                GUI.color = Color.white;
                return;
            }
            DoMainCards(body, tracker);
        }

        private void DoMainCards(Rect body, KillRewardTracker tracker)
        {
            bool enabled = tracker.PendingRewards > 0;
            DrawRewardCard(new Rect(body.x, body.y, body.width, 104f),
                "KR_RewardResearch".Translate(), "KR_RewardResearchDesc".Translate(),
                enabled, () => view = View.Research);
            DrawRewardCard(new Rect(body.x, body.y + 116f, body.width, 104f),
                "KR_RewardSkill".Translate(), "KR_RewardSkillDesc".Translate(),
                enabled, () => view = View.SkillPawn);
            DrawRewardCard(new Rect(body.x, body.y + 232f, body.width, 104f),
                "KR_RewardItem".Translate(), "KR_RewardItemDesc".Translate(),
                enabled, () => view = View.ItemCategory);
        }

        private static void DrawRewardCard(Rect rect, string title, string desc, bool enabled, Action onClaim)
        {
            Widgets.DrawBoxSolid(rect, CardBg);
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 10f, 400f, 22f), title);
            GUI.color = DescGrey;
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 34f, 420f, 60f), desc);
            GUI.color = Color.white;
            Rect buttonRect = new Rect(rect.xMax - 144f, rect.y + 30f, 120f, 44f);
            GUI.color = new Color(1f, 1f, 1f, enabled ? 1f : 0.4f);
            if (Widgets.ButtonText(buttonRect, "KR_LetterOpen".Translate()) && enabled)
            {
                onClaim();
            }
            GUI.color = Color.white;
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

        private void DoItemCategoryView(Rect inRect)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KR_PickItem".Translate());
            float y = inRect.y + 40f;
            foreach (ThingCategoryDef category in ItemReward.RootCategories)
            {
                if (Widgets.ButtonText(new Rect(inRect.x, y, inRect.width, 32f), category.LabelCap))
                {
                    selectedCategory = category;
                    itemSearch = "";
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
            // 搜索框：按名称过滤当前类别（忽略大小写，支持中英文）
            Rect searchRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, 30f);
            itemSearch = Widgets.TextField(searchRect, itemSearch);
            List<ThingDef> filtered = things.Where(d => TextSearch.Matches(d.LabelCap, itemSearch) || TextSearch.Matches(d.defName, itemSearch)).ToList();
            Rect listRect = new Rect(inRect.x, inRect.y + 72f, inRect.width, inRect.height - 112f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, filtered.Count * 36f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (ThingDef thingDef in filtered)
            {
                Rect row = new Rect(0f, y, viewRect.width, 34f);
                ThingDef stuff = thingDef.MadeFromStuff ? GenStuff.DefaultStuffFor(thingDef) : null;
                Widgets.ThingIcon(new Rect(row.x + 2f, row.y + 2f, 30f, 30f), thingDef, stuff);
                if (Widgets.ButtonText(new Rect(row.x + 38f, row.y + 1f, row.width - 38f, 32f), thingDef.LabelCap + " ×" + StackMath.FullStackCount(thingDef.stackLimit)))
                {
                    BeginItemTargeting(thingDef, tracker);
                }
                y += 36f;
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
            Messages.Message("KR_PickCell".Translate(), MessageTypeDefOf.NeutralEvent);
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
            });
        }
    }
}
