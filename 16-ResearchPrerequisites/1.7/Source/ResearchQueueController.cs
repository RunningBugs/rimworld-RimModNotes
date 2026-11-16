using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.Sound;

namespace ResearchPrerequisites
{
    public static class ResearchQueueController
    {
        private static bool ColonistsHaveResearchBench
        {
            get
            {
                bool result = false;
                List<Map> maps = Find.Maps;
                for (int i = 0; i < maps.Count; i++)
                {
                    if (maps[i].listerBuildings.ColonistsHaveResearchBench())
                    {
                        result = true;
                        break;
                    }
                }
                return result;
            }
        }

        private static List<(BuildableDef, List<string>)> ComputeUnlockedDefsThatHaveMissingMemes(ResearchProjectDef project)
        {
            List<(BuildableDef, List<string>)> cachedDefsWithMissingMemes = new List<(BuildableDef, List<string>)>();
            if (project == null)
            {
                return cachedDefsWithMissingMemes;
            }
            if (!ModsConfig.IdeologyActive)
            {
                return cachedDefsWithMissingMemes;
            }
            if (Find.World == null || Faction.OfPlayerSilentFail?.ideos?.PrimaryIdeo == null)
            {
                return cachedDefsWithMissingMemes;
            }
            foreach (Def unlockedDef in project.UnlockedDefs)
            {
                if (!(unlockedDef is BuildableDef { canGenerateDefaultDesignator: false } buildableDef))
                {
                    continue;
                }
                List<string> list = null;
                foreach (MemeDef item in DefDatabase<MemeDef>.AllDefsListForReading)
                {
                    if (!Faction.OfPlayer.ideos.HasAnyIdeoWithMeme(item) && item.AllDesignatorBuildables.Contains(buildableDef))
                    {
                        if (list == null)
                        {
                            list = new List<string>();
                        }
                        list.Add(item.LabelCap);
                    }
                }
                if (list != null)
                {
                    cachedDefsWithMissingMemes.Add((buildableDef, list));
                }
            }
            return cachedDefsWithMissingMemes;
        }

        private static void DoBeginResearch(ResearchProjectDef projectToStart, bool playStartSound)
        {
            if (playStartSound)
            {
                SoundDefOf.ResearchStart.PlayOneShotOnCamera();
            }
            Find.ResearchManager.SetCurrentProject(projectToStart);
            TutorSystem.Notify_Event("StartResearchProject");
            if ((!ModsConfig.AnomalyActive || ResearchCategories.Of(projectToStart) == ResearchCategory.Normal) && !ColonistsHaveResearchBench)
            {
                Messages.Message("MessageResearchMenuWithoutBench".Translate(), MessageTypeDefOf.CautionInput);
            }
        }

        public static void AttemptBeginResearch(
            ResearchProjectDef projectToStart,
            Action beforeBegin = null,
            bool playStartSound = false,
            ResearchCategory? expectedAutoStartCategory = null)
        {
            if (projectToStart == null || projectToStart.IsFinished || !projectToStart.CanStartNow)
            {
                return;
            }
            List<(BuildableDef, List<string>)> list = ComputeUnlockedDefsThatHaveMissingMemes(projectToStart);
            if (!list.Any())
            {
                beforeBegin?.Invoke();
                DoBeginResearch(projectToStart, playStartSound);
                return;
            }
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("ResearchProjectHasDefsWithMissingMemes".Translate(projectToStart.LabelCap)).Append(":");
            stringBuilder.AppendLine();
            foreach (var (buildableDef, items) in list)
            {
                stringBuilder.AppendLine();
                stringBuilder.Append("  - ").Append(buildableDef.LabelCap.Colorize(ColoredText.NameColor)).Append(" (")
                    .Append(items.ToCommaList())
                    .Append(")");
            }
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(stringBuilder.ToString(), delegate
            {
                if (projectToStart.IsFinished || !projectToStart.CanStartNow)
                {
                    return;
                }
                if (expectedAutoStartCategory.HasValue)
                {
                    ResearchQueue q = ResearchQueue.Instance;
                    ResearchCategory expectedCat = expectedAutoStartCategory.Value;
                    if (q == null || q.PeekHead(expectedCat) != projectToStart)
                    {
                        return;
                    }
                    if (Find.ResearchManager.GetProject(ResearchCategories.ToVanillaDef(expectedCat)) != null)
                    {
                        return;
                    }
                }
                beforeBegin?.Invoke();
                DoBeginResearch(projectToStart, playStartSound);
            }));
        }

        public static void AdvanceAllActiveCategories()
        {
            AdvanceCategory(ResearchCategory.Normal);
            if (ModsConfig.AnomalyActive)
            {
                AdvanceCategory(ResearchCategory.AnomalyBasic);
                AdvanceCategory(ResearchCategory.AnomalyAdvanced);
            }
        }

        /// <summary>
        /// 推进一个类别:槽位被占用则自愈"当前研究 = 队首"不变量;
        /// 槽位空闲则严格按队首推进,队首卡住则保留队列等待。
        /// 由 GameComponentTick 每 tick 调用,也可在入队后立即调用。
        /// </summary>
        public static void AdvanceCategory(ResearchCategory category)
        {
            if (!ResearchCategories.IsActive(category))
            {
                return;
            }
            ResearchQueue queue = ResearchQueue.Instance;
            if (queue == null)
            {
                return;
            }
            ResearchProjectDef current = Find.ResearchManager.GetProject(ResearchCategories.ToVanillaDef(category));
            if (current != null)
            {
                // 自愈:当前研究必须在队首(同时覆盖旧存档迁移与外部启动路径)。
                if (queue.PeekHead(category) != current)
                {
                    queue.MoveToHead(current);
                }
                queue.SetLastAttemptedHead(category, current);
                return;
            }
            ResearchProjectDef head = queue.PeekHead(category);
            if (head == null)
            {
                queue.SetLastAttemptedHead(category, null);
                return;
            }
            if (!head.CanStartNow)
            {
                // 队首卡住:保留队列等待,不记录,解锁后自动启动。
                return;
            }
            if (queue.GetLastAttemptedHead(category) == head)
            {
                // 已尝试过且被玩家取消,等待队列变化或显式操作。
                return;
            }
            queue.SetLastAttemptedHead(category, head);
            AttemptBeginResearch(head, playStartSound: false, expectedAutoStartCategory: category);
        }

        /// <summary>
        /// 将项目及其未完成前置按类别加入各自队尾，并立即尝试推进空闲类别。
        /// 显式点击入队时清除对应空闲类别的弹窗取消屏蔽，以便玩家重新确认。
        /// </summary>
        public static void EnqueueAndAdvance(ResearchProjectDef project)
        {
            ResearchQueue queue = ResearchQueue.Instance;
            if (queue == null || project == null)
            {
                return;
            }
            List<ResearchProjectDef> chain = ResearchQueue.CollectDependencyChain(project);
            if (chain.Count == 0)
            {
                return;
            }
            queue.Enqueue(project);
            for (int c = 0; c < ResearchCategories.Count; c++)
            {
                ResearchCategory cat = (ResearchCategory)c;
                if (!ResearchCategories.IsActive(cat))
                {
                    continue;
                }
                if (Find.ResearchManager.GetProject(ResearchCategories.ToVanillaDef(cat)) == null)
                {
                    queue.ClearLastAttemptedHead(cat);
                }
            }
            AdvanceAllActiveCategories();
        }

        public static void RemoveFromQueueAndAdvance(ResearchProjectDef project)
        {
            ResearchQueue queue = ResearchQueue.Instance;
            if (queue == null || project == null)
            {
                return;
            }
            if (queue.RemoveFromQueue(project))
            {
                AdvanceAllActiveCategories();
            }
        }

        /// <summary>
        /// 插队到队首并尽快启动:将项目及其未完成前置按各自类别拆链插队。
        /// 链首可开始时直接抢占并启动(若有缺失模因确认弹窗，则先确认再抢占队列与当前研究，取消时不破坏原队列)；
        /// 链首暂不可开始且当前研究正在进行时，把插队链插在当前研究之后立即待命。
        /// </summary>
        public static void JumpChainToFrontAndStart(ResearchProjectDef project)
        {
            ResearchQueue queue = ResearchQueue.Instance;
            if (queue == null || project == null)
            {
                return;
            }
            List<ResearchProjectDef> chain = ResearchQueue.CollectDependencyChain(project);
            if (chain.Count == 0)
            {
                return;
            }
            for (int c = 0; c < ResearchCategories.Count; c++)
            {
                ResearchCategory cat = (ResearchCategory)c;
                if (!ResearchCategories.IsActive(cat))
                {
                    continue;
                }
                List<ResearchProjectDef> slice = ResearchQueue.ExtractCategorySlice(chain, cat);
                if (slice.Count == 0)
                {
                    continue;
                }
                ResearchProjectDef head = slice[0];
                ResearchProjectDef current = Find.ResearchManager.GetProject(ResearchCategories.ToVanillaDef(cat));
                if (current == head)
                {
                    queue.JumpCategorySliceToFront(cat, slice);
                    queue.SetLastAttemptedHead(cat, head);
                    continue;
                }
                if (head.CanStartNow)
                {
                    List<ResearchProjectDef> capturedSlice = slice;
                    ResearchCategory capturedCat = cat;
                    ResearchProjectDef capturedHead = head;
                    AttemptBeginResearch(
                        capturedHead,
                        beforeBegin: delegate
                        {
                            queue.JumpCategorySliceToFront(capturedCat, capturedSlice);
                            queue.SetLastAttemptedHead(capturedCat, capturedHead);
                        },
                        playStartSound: true);
                }
                else if (current == null)
                {
                    queue.JumpCategorySliceToFront(cat, slice);
                    queue.ClearLastAttemptedHead(cat);
                }
                else
                {
                    queue.InsertCategorySliceAfterCurrent(cat, current, slice);
                }
            }
        }

        public static string GetWaitingReason(ResearchProjectDef project)
        {
            if (project == null)
            {
                return string.Empty;
            }
            if (!project.PrerequisitesCompleted)
            {
                List<string> missing = new List<string>();
                if (project.prerequisites != null)
                {
                    for (int i = 0; i < project.prerequisites.Count; i++)
                    {
                        ResearchProjectDef prereq = project.prerequisites[i];
                        if (prereq != null && !prereq.IsFinished)
                        {
                            missing.Add(prereq.LabelCap);
                        }
                    }
                }
                if (project.hiddenPrerequisites != null)
                {
                    for (int i = 0; i < project.hiddenPrerequisites.Count; i++)
                    {
                        ResearchProjectDef prereq = project.hiddenPrerequisites[i];
                        if (prereq != null && !prereq.IsFinished && !prereq.IsHidden)
                        {
                            missing.Add(prereq.LabelCap);
                        }
                    }
                }
                return missing.Count > 0
                    ? "PrerequisitesNotCompleted".Translate() + " (" + missing.ToCommaList() + ")"
                    : "PrerequisitesNotCompleted".Translate().ToString();
            }
            if (!project.TechprintRequirementMet)
            {
                return "InsufficientTechprintsApplied".Translate(project.TechprintsApplied, project.TechprintCount);
            }
            if ((!ModsConfig.AnomalyActive || project.knowledgeCategory == null) && !project.PlayerHasAnyAppropriateResearchBench)
            {
                return "MissingRequiredResearchFacilities".Translate();
            }
            if (project.requiredResearchBuilding != null && !project.PlayerHasAnyAppropriateResearchBench)
            {
                return "MissingRequiredResearchFacilities".Translate();
            }
            if (!project.PlayerMechanitorRequirementMet)
            {
                return "MissingRequiredMechanitor".Translate();
            }
            if (!project.InspectionRequirementsMet)
            {
                return "MissingGravEngineInspection".Translate();
            }
            if (!project.AnalyzedThingsRequirementsMet && project.requiredAnalyzed != null && project.requiredAnalyzed.Count > 0)
            {
                return "NotStudied".Translate(project.requiredAnalyzed[0].LabelCap);
            }
            if (project.IsHidden)
            {
                return "UnknownResearch".Translate();
            }
            return "ResearchQueueWaitingGeneric".Translate();
        }
    }
}
