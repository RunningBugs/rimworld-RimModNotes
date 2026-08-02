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
            if ((!ModsConfig.AnomalyActive || ResearchCategories.Of(projectToStart) == ResearchCategory.Normal) && !ColonistsHaveResearchBench)
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
        /// 每个知识类别上次实际尝试启动的队首,按 ResearchCategory 枚举值索引。
        /// 玩家在 meme 确认弹窗取消后,同一队首不再每 tick 重复弹窗,
        /// 直到队首变化(完成/入队/插队/清空)才恢复自动尝试。
        /// </summary>
        private static readonly ResearchProjectDef[] LastAttemptedHead = new ResearchProjectDef[ResearchCategories.Count];

        /// <summary>
        /// 推进一个类别:槽位被占用则自愈"当前研究 = 队首"不变量;
        /// 槽位空闲则严格按队首推进,队首卡住则保留队列等待。
        /// 由 GameComponentTick 每 tick 调用,也可在入队后立即调用。
        /// </summary>
        public static void AdvanceCategory(ResearchCategory category)
        {
            if (category != ResearchCategory.Normal && !ModsConfig.AnomalyActive)
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
                LastAttemptedHead[(int)category] = current;
                return;
            }
            ResearchProjectDef head = queue.PeekHead(category);
            if (head == null)
            {
                LastAttemptedHead[(int)category] = null;
                return;
            }
            if (!head.CanStartNow)
            {
                // 队首卡住:保留队列等待,不记录,解锁后自动启动。
                return;
            }
            if (LastAttemptedHead[(int)category] == head)
            {
                // 已尝试过且被玩家取消,等待队首变化。
                return;
            }
            LastAttemptedHead[(int)category] = head;
            AttemptBeginResearch(head);
        }

        /// <summary>
        /// 插队到队首并尽快启动:将项目及其未完成的前置整体插到队首,
        /// 链首可开始则立即开始。被挤下的当前研究留在队列中
        /// (进度由原版按项目保存),之后按队列顺序自动恢复。
        /// 可立即开始的项目其前置链就是自身,本入口涵盖全部插队场景。
        /// </summary>
        public static void JumpChainToFrontAndStart(ResearchProjectDef project)
        {
            ResearchQueue queue = ResearchQueue.Instance;
            if (queue == null || project == null)
            {
                return;
            }
            ResearchCategory category = ResearchCategories.Of(project);
            queue.JumpChainToFront(project);
            ResearchProjectDef head = queue.PeekHead(category);
            if (head == null || !head.CanStartNow)
            {
                // 链上暂无可开始项目(如缺科技印花/研究台),链已排在队首待命。
                return;
            }
            if (Find.ResearchManager.GetProject(ResearchCategories.ToVanillaDef(category)) == head)
            {
                // 链首已在研究中,无需动作。
                return;
            }
            LastAttemptedHead[(int)category] = head;
            AttemptBeginResearch(head);
        }
    }
}
