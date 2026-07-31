using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

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
            if (Faction.OfPlayer.ideos?.PrimaryIdeo == null)
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

        private static void DoBeginResearch(ResearchProjectDef projectToStart)
        {
            Find.ResearchManager.SetCurrentProject(projectToStart);
            TutorSystem.Notify_Event("StartResearchProject");
            if ((!ModsConfig.AnomalyActive || projectToStart.knowledgeCategory == null) && !ColonistsHaveResearchBench)
            {
                Messages.Message("MessageResearchMenuWithoutBench".Translate(), MessageTypeDefOf.CautionInput);
            }
        }

        public static void AttemptBeginResearch(ResearchProjectDef projectToStart)
        {
            if (projectToStart == null)
            {
                return;
            }
            List<(BuildableDef, List<string>)> list = ComputeUnlockedDefsThatHaveMissingMemes(projectToStart);
            if (!list.Any())
            {
                DoBeginResearch(projectToStart);
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
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(stringBuilder.ToString(), delegate
            {
                DoBeginResearch(projectToStart);
            }));
        }

        /// <summary>
        /// 槽位空闲时,从对应队列取下一个可立即开始的项目开始研究,并将其移出队列。
        /// 注意:若 meme 确认弹窗被玩家取消,该项目仍已移出队列(可接受的边界行为)。
        /// </summary>
        public static void TryStartNext(KnowledgeCategoryDef category)
        {
            if (Find.ResearchManager.GetProject(category) != null)
            {
                return;
            }
            ResearchQueue queue = ResearchQueue.Instance;
            ResearchProjectDef next = queue?.NextStartable(category);
            if (next == null)
            {
                return;
            }
            AttemptBeginResearch(next);
            queue.QueueFor(category).Remove(next);
        }

        /// <summary>
        /// 插队到队首并立即开始研究。被挤下的当前研究放回队列第 1 位
        /// (进度由原版按项目保存,不会丢),插队项目完成后会自动继续。
        /// </summary>
        public static void JumpToFrontAndStart(ResearchProjectDef project)
        {
            ResearchQueue queue = ResearchQueue.Instance;
            if (queue == null || project == null)
            {
                return;
            }
            KnowledgeCategoryDef category = project.knowledgeCategory;
            queue.JumpToFront(project);
            ResearchProjectDef current = Find.ResearchManager.GetProject(category);
            if (current == project)
            {
                return;
            }
            if (current != null)
            {
                queue.QueueFor(category).Insert(1, current);
            }
            AttemptBeginResearch(project);
            queue.QueueFor(category).Remove(project);
        }

        /// <summary>
        /// 不能立即开始的项目:将其与未完成的前置整体插队到队首,并立即开始
        /// 链上第一个可开始的项目。被挤下的当前研究放回整条链之后
        /// (进度由原版按项目保存,不会丢),链上项目完成后按顺序自动继续。
        /// </summary>
        public static void JumpChainToFrontAndStart(ResearchProjectDef project)
        {
            ResearchQueue queue = ResearchQueue.Instance;
            if (queue == null || project == null)
            {
                return;
            }
            KnowledgeCategoryDef category = project.knowledgeCategory;
            int chainLength = queue.JumpChainToFront(project);
            List<ResearchProjectDef> list = queue.QueueFor(category);
            ResearchProjectDef first = list.FirstOrDefault(p => p.CanStartNow);
            if (first == null)
            {
                // 链上暂无可开始项目(如缺科技印花/研究台),链已排在队首待命。
                return;
            }
            ResearchProjectDef current = Find.ResearchManager.GetProject(category);
            if (current == first)
            {
                return;
            }
            if (current != null && !list.Contains(current))
            {
                // 必须放在整条链之后,否则当前研究会插队到链中间,
                // 链首完成后又抢先恢复,违背"最快启动目标项目"的意图。
                list.Insert(Math.Min(chainLength, list.Count), current);
            }
            AttemptBeginResearch(first);
            list.Remove(first);
        }
    }
}
