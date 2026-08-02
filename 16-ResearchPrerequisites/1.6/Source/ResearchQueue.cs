using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ResearchPrerequisites
{
    public class ResearchQueue : GameComponent
    {
        private List<ResearchProjectDef> normalQueue = new List<ResearchProjectDef>();
        private List<ResearchProjectDef> anomalyBasicQueue = new List<ResearchProjectDef>();
        private List<ResearchProjectDef> anomalyAdvancedQueue = new List<ResearchProjectDef>();

        public ResearchQueue(Game game)
        {
        }

        public static ResearchQueue Instance => Current.Game?.GetComponent<ResearchQueue>();

        public List<ResearchProjectDef> QueueFor(ResearchCategory category)
        {
            switch (category)
            {
                case ResearchCategory.AnomalyBasic:
                    return anomalyBasicQueue;
                case ResearchCategory.AnomalyAdvanced:
                    return anomalyAdvancedQueue;
                default:
                    return normalQueue;
            }
        }

        public void Enqueue(ResearchProjectDef project)
        {
            if (project == null || project.IsFinished || project.IsHidden)
            {
                return;
            }
            List<ResearchProjectDef> queue = QueueFor(ResearchCategories.Of(project));
            AddWithPrerequisites(queue, project);
            DistinctInPlace(queue);
        }

        /// <summary>
        /// 将项目及其所有未完成的前置(含隐藏前置,按依赖顺序)整体插入队首,
        /// 队列中已有的同项会被提前。用于不能立即开始的项目尽快启动。
        /// 返回插入链的长度。
        /// </summary>
        public int JumpChainToFront(ResearchProjectDef project)
        {
            if (project == null)
            {
                return 0;
            }
            List<ResearchProjectDef> queue = QueueFor(ResearchCategories.Of(project));
            List<ResearchProjectDef> chain = new List<ResearchProjectDef>();
            AddWithPrerequisites(chain, project);
            // 菱形依赖(同一前置被多条路径引用)会在递归中重复加入,先去重;
            // DistinctInPlace 保留首次出现的位置,不破坏依赖顺序。
            DistinctInPlace(chain);
            queue.RemoveAll(chain.Contains);
            queue.InsertRange(0, chain);
            return chain.Count;
        }

        public void ClearQueue(ResearchCategory category)
        {
            QueueFor(category).Clear();
        }

        /// <summary>
        /// 弹出队首所有已完成/为 null 的项。
        /// </summary>
        public void PopFinishedHeads(ResearchCategory category)
        {
            List<ResearchProjectDef> queue = QueueFor(category);
            while (queue.Count > 0 && (queue[0] == null || queue[0].IsFinished))
            {
                queue.RemoveAt(0);
            }
        }

        /// <summary>
        /// 弹出已完成队首后返回新队首。只查看,不启动、不移除。
        /// </summary>
        public ResearchProjectDef PeekHead(ResearchCategory category)
        {
            PopFinishedHeads(category);
            List<ResearchProjectDef> queue = QueueFor(category);
            return queue.Count > 0 ? queue[0] : null;
        }

        /// <summary>
        /// 把项目挪到对应队列的队首;不在队列中则插入队首。
        /// 用于维持"当前研究 = 队首"的不变量。
        /// </summary>
        public void MoveToHead(ResearchProjectDef project)
        {
            if (project == null)
            {
                return;
            }
            List<ResearchProjectDef> queue = QueueFor(ResearchCategories.Of(project));
            queue.Remove(project);
            queue.Insert(0, project);
        }

        private static void AddWithPrerequisites(List<ResearchProjectDef> queue, ResearchProjectDef project)
        {
            if (project == null || project.IsFinished)
            {
                return;
            }
            if (project.PrerequisitesCompleted && project.TechprintRequirementMet)
            {
                queue.Add(project);
                return;
            }
            if (project.prerequisites != null)
            {
                foreach (ResearchProjectDef prereq in project.prerequisites)
                {
                    AddWithPrerequisites(queue, prereq);
                }
            }
            if (project.hiddenPrerequisites != null)
            {
                foreach (ResearchProjectDef prereq in project.hiddenPrerequisites)
                {
                    AddWithPrerequisites(queue, prereq);
                }
            }
            queue.Add(project);
        }

        private static void DistinctInPlace(List<ResearchProjectDef> queue)
        {
            HashSet<ResearchProjectDef> seen = new HashSet<ResearchProjectDef>();
            queue.RemoveAll(p => !seen.Add(p));
        }

        /// <summary>
        /// 每 tick 推进各类别队列:自愈"当前研究 = 队首"不变量,
        /// 槽位空闲时严格按队首启动下一项。
        /// </summary>
        public override void GameComponentTick()
        {
            ResearchQueueController.AdvanceCategory(ResearchCategory.Normal);
            if (ModsConfig.AnomalyActive)
            {
                ResearchQueueController.AdvanceCategory(ResearchCategory.AnomalyBasic);
                ResearchQueueController.AdvanceCategory(ResearchCategory.AnomalyAdvanced);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            // 旧存档兼容:沿用原有 key 作为普通队列
            Scribe_Collections.Look(ref normalQueue, "ResearchPrerequisites.ResearchQueue.researchQueue", LookMode.Def);
            Scribe_Collections.Look(ref anomalyBasicQueue, "ResearchPrerequisites.ResearchQueue.anomalyBasicQueue", LookMode.Def);
            Scribe_Collections.Look(ref anomalyAdvancedQueue, "ResearchPrerequisites.ResearchQueue.anomalyAdvancedQueue", LookMode.Def);
            normalQueue ??= new List<ResearchProjectDef>();
            anomalyBasicQueue ??= new List<ResearchProjectDef>();
            anomalyAdvancedQueue ??= new List<ResearchProjectDef>();
        }
    }
}
