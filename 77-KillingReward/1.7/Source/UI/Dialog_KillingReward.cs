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
        private enum View { Main, Research, SkillPawn, SkillSkill, ItemCategory, ItemThing, Blessing, TraitPawn, TraitOp, TraitAdd, TraitRemove, GenePawn, GeneType, GeneOp, GeneAdd, GeneRemove }

        private static readonly Color CardBg = new Color(0.14f, 0.14f, 0.16f);
        private static readonly Color DescGrey = new Color(0.75f, 0.75f, 0.75f);

        private View view = View.Main;
        private Vector2 scrollPosition;
        private Pawn selectedPawn;
        private ThingCategoryDef selectedCategory;
        private string itemSearch = "";
        private bool selectedXenogene;
        private string geneSearch = "";
        private string traitSearch = "";

        public Dialog_KillingReward()
        {
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override Vector2 InitialSize => new Vector2(640f, 600f);

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

            // DEV 调试按钮：仅在 DevMode + GodMode 下显示，按一下升一级
            if (Prefs.DevMode && DebugSettings.godMode)
            {
                if (Widgets.ButtonText(new Rect(inRect.x + inRect.width / 2f - 80f, inRect.y + 46f, 160f, 26f), "KR_DevLevelUp".Translate()))
                {
                    tracker.DevLevelUp();
                }
            }

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
            if (view == View.Blessing)
            {
                DoBlessingView(body, tracker);
                return;
            }
            if (view == View.TraitPawn)
            {
                DoTraitPawnView(body);
                return;
            }
            if (view == View.TraitOp && selectedPawn != null)
            {
                DoTraitOpView(body, tracker);
                return;
            }
            if (view == View.TraitAdd && selectedPawn != null)
            {
                DoTraitAddView(body, tracker);
                return;
            }
            if (view == View.TraitRemove && selectedPawn != null)
            {
                DoTraitRemoveView(body, tracker);
                return;
            }
            if (view == View.GenePawn)
            {
                DoGenePawnView(body);
                return;
            }
            if (view == View.GeneType && selectedPawn != null)
            {
                DoGeneTypeView(body, tracker);
                return;
            }
            if (view == View.GeneOp && selectedPawn != null)
            {
                DoGeneOpView(body, tracker);
                return;
            }
            if (view == View.GeneAdd && selectedPawn != null)
            {
                DoGeneAddView(body, tracker);
                return;
            }
            if (view == View.GeneRemove && selectedPawn != null)
            {
                DoGeneRemoveView(body, tracker);
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
            // 卡片列表可滚动，底部预留关闭按钮空间，卡片再多也装得下
            Rect listRect = new Rect(body.x, body.y, body.width, body.height - 40f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, 452f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            DrawRewardCard(new Rect(0f, 0f, viewRect.width, 104f),
                "KR_RewardResearch".Translate(), "KR_RewardResearchDesc".Translate(),
                enabled, () => view = View.Research);
            DrawRewardCard(new Rect(0f, 116f, viewRect.width, 104f),
                "KR_RewardSkill".Translate(), "KR_RewardSkillDesc".Translate(),
                enabled, () => view = View.SkillPawn);
            DrawRewardCard(new Rect(0f, 232f, viewRect.width, 104f),
                "KR_RewardItem".Translate(), "KR_RewardItemDesc".Translate(),
                enabled, () => view = View.ItemCategory);
            DrawRewardCard(new Rect(0f, 348f, viewRect.width, 104f),
                "KR_RewardBlessing".Translate(), "KR_RewardBlessingDesc".Translate(),
                enabled, () => view = View.Blessing);
            Widgets.EndScrollView();
        }

        private static void DrawRewardCard(Rect rect, string title, string desc, bool enabled, Action onClaim)
        {
            Widgets.DrawBoxSolid(rect, CardBg);
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 160f, 22f), title);
            GUI.color = DescGrey;
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 34f, rect.width - 160f, 60f), desc);
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
                    if (!SelectedPawnUnavailable() && tracker.TryConsumeReward())
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

        private void DoBlessingView(Rect inRect, KillRewardTracker tracker)
        {
            List<BlessingReward.BlessingOption> options = BlessingReward.Options.Where(o => o.Visible).ToList();
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KR_PickBlessing".Translate());
            Rect listRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 76f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, options.Count * 104f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (BlessingReward.BlessingOption option in options)
            {
                Rect row = new Rect(0f, y, viewRect.width, 96f);
                Widgets.DrawBoxSolid(row, CardBg);
                Widgets.Label(new Rect(row.x + 12f, row.y + 8f, row.width - 330f, 22f), option.Label);
                GUI.color = DescGrey;
                Text.Anchor = TextAnchor.UpperRight;
                Widgets.Label(new Rect(row.xMax - 300f, row.y + 8f, 156f, 22f), "KR_BlessingCost".Translate(option.Cost(tracker)));
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Label(new Rect(row.x + 12f, row.y + 32f, row.width - 160f, 56f), option.Desc);
                GUI.color = Color.white;
                if (Widgets.ButtonText(new Rect(row.xMax - 132f, row.y + 26f, 120f, 44f), "KR_LetterOpen".Translate()))
                {
                    switch (option.kind)
                    {
                        case BlessingReward.BlessingKind.Regen:
                            if (tracker.TryConsumeReward())
                            {
                                int count = BlessingReward.Apply(option);
                                Messages.Message("KR_BlessingApplied".Translate(option.Label, count), MessageTypeDefOf.PositiveEvent);
                                Close();
                            }
                            break;
                        case BlessingReward.BlessingKind.TraitMod:
                            view = View.TraitPawn;
                            break;
                        case BlessingReward.BlessingKind.GeneMod:
                            view = View.GenePawn;
                            break;
                    }
                }
                y += 104f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = View.Main;
            }
        }

        /// <summary>窗口不暂停游戏，选人后目标可能死亡/被销毁；应用前统一校验，避免对尸体操作。</summary>
        private bool SelectedPawnUnavailable()
        {
            if (selectedPawn != null && !selectedPawn.Destroyed && !selectedPawn.Dead)
            {
                return false;
            }
            Messages.Message("KR_PawnUnavailable".Translate(), MessageTypeDefOf.RejectInput);
            selectedPawn = null;
            view = View.Main;
            return true;
        }

        private void DoPawnListView(Rect inRect, List<Pawn> pawns, string headerKey, Action<Pawn> onPick, View backView)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), headerKey.Translate());
            Rect listRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 76f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, pawns.Count * 32f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (Pawn pawn in pawns)
            {
                Rect row = new Rect(0f, y, viewRect.width, 30f);
                if (Widgets.ButtonText(row, pawn.LabelShortCap))
                {
                    onPick(pawn);
                }
                y += 32f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = backView;
            }
        }

        private void DoTwoChoiceView(Rect inRect, string headerKey, long cost, string choice1Key, string choice2Key, Action on1, Action on2, View backView)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), headerKey.Translate());
            GUI.color = DescGrey;
            Widgets.Label(new Rect(inRect.x, inRect.y + 32f, inRect.width, 22f), "KR_BlessingCost".Translate(cost));
            GUI.color = Color.white;
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y + 64f, inRect.width, 36f), choice1Key.Translate()))
            {
                on1();
            }
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y + 108f, inRect.width, 36f), choice2Key.Translate()))
            {
                on2();
            }
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = backView;
            }
        }

        private void DoTraitPawnView(Rect inRect)
        {
            DoPawnListView(inRect, TraitModReward.Candidates(), "KR_PickPawn", delegate(Pawn pawn)
            {
                selectedPawn = pawn;
                view = View.TraitOp;
            }, View.Blessing);
        }

        private void DoTraitOpView(Rect inRect, KillRewardTracker tracker)
        {
            DoTwoChoiceView(inRect, "KR_PickOperation", tracker.TraitModCost, "KR_TraitOpAdd", "KR_TraitOpRemove",
                delegate { traitSearch = ""; view = View.TraitAdd; }, delegate { traitSearch = ""; view = View.TraitRemove; }, View.TraitPawn);
        }

        private void DoTraitAddView(Rect inRect, KillRewardTracker tracker)
        {
            List<Trait> traits = TraitModReward.AddableTraits(selectedPawn);
            bool affordable = tracker.PendingRewards >= tracker.TraitModCost;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KR_PickTraitAdd".Translate() + " (" + selectedPawn.LabelShortCap + ")");
            // 搜索框：按名称/defName 过滤（忽略大小写，支持中英文）
            Rect searchRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, 30f);
            traitSearch = Widgets.TextField(searchRect, traitSearch);
            List<Trait> filtered = traits.Where(t => TextSearch.Matches(t.LabelCap, traitSearch) || TextSearch.Matches(t.def.LabelCap, traitSearch) || TextSearch.Matches(t.def.defName, traitSearch)).ToList();
            Rect listRect = new Rect(inRect.x, inRect.y + 72f, inRect.width, inRect.height - 112f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, filtered.Count * 32f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (Trait trait in filtered)
            {
                Rect row = new Rect(0f, y, viewRect.width, 30f);
                string label = trait.def.degreeDatas.Count > 1
                    ? trait.def.LabelCap + "：" + trait.LabelCap
                    : trait.LabelCap;
                GUI.color = new Color(1f, 1f, 1f, affordable ? 1f : 0.4f);
                if (Widgets.ButtonText(row, label) && affordable)
                {
                    if (!SelectedPawnUnavailable() && tracker.TryConsumeTraitMod())
                    {
                        TraitModReward.Add(selectedPawn, trait);
                        Messages.Message("KR_TraitAdded".Translate(selectedPawn.LabelShortCap, trait.LabelCap), MessageTypeDefOf.PositiveEvent);
                        Close();
                    }
                }
                GUI.color = Color.white;
                y += 32f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = View.TraitOp;
            }
        }

        private void DoTraitRemoveView(Rect inRect, KillRewardTracker tracker)
        {
            List<Trait> traits = TraitModReward.RemovableTraits(selectedPawn);
            bool affordable = tracker.PendingRewards >= tracker.TraitModCost;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KR_PickTraitRemove".Translate() + " (" + selectedPawn.LabelShortCap + ")");
            // 搜索框：按名称/defName 过滤（忽略大小写，支持中英文）
            Rect searchRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, 30f);
            traitSearch = Widgets.TextField(searchRect, traitSearch);
            List<Trait> filtered = traits.Where(t => TextSearch.Matches(t.LabelCap, traitSearch) || TextSearch.Matches(t.def.LabelCap, traitSearch) || TextSearch.Matches(t.def.defName, traitSearch)).ToList();
            Rect listRect = new Rect(inRect.x, inRect.y + 72f, inRect.width, inRect.height - 112f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, filtered.Count * 32f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (Trait trait in filtered)
            {
                Rect row = new Rect(0f, y, viewRect.width, 30f);
                GUI.color = new Color(1f, 1f, 1f, affordable ? 1f : 0.4f);
                if (Widgets.ButtonText(row, trait.LabelCap) && affordable)
                {
                    if (!SelectedPawnUnavailable() && tracker.TryConsumeTraitMod())
                    {
                        string label = trait.LabelCap;
                        TraitModReward.Remove(selectedPawn, trait);
                        Messages.Message("KR_TraitRemoved".Translate(selectedPawn.LabelShortCap, label), MessageTypeDefOf.PositiveEvent);
                        Close();
                    }
                }
                GUI.color = Color.white;
                y += 32f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = View.TraitOp;
            }
        }

        private void DoGenePawnView(Rect inRect)
        {
            DoPawnListView(inRect, GeneModReward.Candidates(), "KR_PickPawn", delegate(Pawn pawn)
            {
                selectedPawn = pawn;
                view = View.GeneType;
            }, View.Blessing);
        }

        private void DoGeneTypeView(Rect inRect, KillRewardTracker tracker)
        {
            DoTwoChoiceView(inRect, "KR_PickGeneType", tracker.GeneModCost, "KR_GeneTypeGermline", "KR_GeneTypeXeno",
                delegate { selectedXenogene = false; view = View.GeneOp; },
                delegate { selectedXenogene = true; view = View.GeneOp; },
                View.GenePawn);
        }

        private void DoGeneOpView(Rect inRect, KillRewardTracker tracker)
        {
            DoTwoChoiceView(inRect, "KR_PickOperation", tracker.GeneModCost, "KR_GeneOpAdd", "KR_GeneOpRemove",
                delegate { geneSearch = ""; view = View.GeneAdd; },
                delegate { geneSearch = ""; view = View.GeneRemove; },
                View.GeneType);
        }

        private string GeneTypeLabel()
        {
            return (selectedXenogene ? "KR_GeneTypeXeno" : "KR_GeneTypeGermline").Translate();
        }

        private void DoGeneAddView(Rect inRect, KillRewardTracker tracker)
        {
            List<GeneDef> genes = GeneModReward.AddableGenes(selectedPawn);
            bool affordable = tracker.PendingRewards >= tracker.GeneModCost;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KR_PickGeneAdd".Translate() + " (" + selectedPawn.LabelShortCap + " · " + GeneTypeLabel() + ")");
            // 搜索框：按名称/defName 过滤（忽略大小写，支持中英文）
            Rect searchRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, 30f);
            geneSearch = Widgets.TextField(searchRect, geneSearch);
            List<GeneDef> filtered = genes.Where(d => TextSearch.Matches(d.LabelCap, geneSearch) || TextSearch.Matches(d.defName, geneSearch)).ToList();
            Rect listRect = new Rect(inRect.x, inRect.y + 72f, inRect.width, inRect.height - 112f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, filtered.Count * 36f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (GeneDef geneDef in filtered)
            {
                Rect row = new Rect(0f, y, viewRect.width, 34f);
                if (geneDef.Icon != null)
                {
                    GUI.DrawTexture(new Rect(row.x + 2f, row.y + 2f, 30f, 30f), geneDef.Icon);
                }
                GUI.color = new Color(1f, 1f, 1f, affordable ? 1f : 0.4f);
                if (Widgets.ButtonText(new Rect(row.x + 38f, row.y + 1f, row.width - 38f, 32f), geneDef.LabelCap) && affordable)
                {
                    if (!SelectedPawnUnavailable() && tracker.TryConsumeGeneMod())
                    {
                        GeneModReward.Add(selectedPawn, geneDef, selectedXenogene);
                        Messages.Message("KR_GeneAdded".Translate(selectedPawn.LabelShortCap, geneDef.LabelCap), MessageTypeDefOf.PositiveEvent);
                        Close();
                    }
                }
                GUI.color = new Color(DescGrey.r, DescGrey.g, DescGrey.b, affordable ? 1f : 0.4f);
                Widgets.Label(new Rect(row.xMax - 128f, row.y + 6f, 122f, 22f), "KR_GeneStats".Translate(geneDef.biostatMet.ToStringWithSign(), geneDef.biostatCpx.ToStringWithSign()));
                GUI.color = Color.white;
                y += 36f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = View.GeneOp;
            }
        }

        private void DoGeneRemoveView(Rect inRect, KillRewardTracker tracker)
        {
            List<Gene> genes = GeneModReward.RemovableGenes(selectedPawn, selectedXenogene);
            bool affordable = tracker.PendingRewards >= tracker.GeneModCost;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KR_PickGeneRemove".Translate() + " (" + selectedPawn.LabelShortCap + " · " + GeneTypeLabel() + ")");
            Rect searchRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, 30f);
            geneSearch = Widgets.TextField(searchRect, geneSearch);
            List<Gene> filtered = genes.Where(g => TextSearch.Matches(g.def.LabelCap, geneSearch) || TextSearch.Matches(g.def.defName, geneSearch)).ToList();
            Rect listRect = new Rect(inRect.x, inRect.y + 72f, inRect.width, inRect.height - 112f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, filtered.Count * 36f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (Gene gene in filtered)
            {
                Rect row = new Rect(0f, y, viewRect.width, 34f);
                if (gene.def.Icon != null)
                {
                    GUI.DrawTexture(new Rect(row.x + 2f, row.y + 2f, 30f, 30f), gene.def.Icon);
                }
                GUI.color = new Color(1f, 1f, 1f, affordable ? 1f : 0.4f);
                if (Widgets.ButtonText(new Rect(row.x + 38f, row.y + 1f, row.width - 38f, 32f), gene.def.LabelCap) && affordable)
                {
                    if (!SelectedPawnUnavailable() && tracker.TryConsumeGeneMod())
                    {
                        string label = gene.def.LabelCap;
                        GeneModReward.Remove(selectedPawn, gene);
                        Messages.Message("KR_GeneRemoved".Translate(selectedPawn.LabelShortCap, label), MessageTypeDefOf.PositiveEvent);
                        Close();
                    }
                }
                GUI.color = Color.white;
                y += 36f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = View.GeneOp;
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
