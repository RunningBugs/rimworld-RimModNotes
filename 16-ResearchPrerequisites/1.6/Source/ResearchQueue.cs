using System.Collections.Generic;
using System.Linq;
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

        public List<ResearchProjectDef> QueueFor(ResearchProjectDef project)
        {
            return QueueFor(project?.knowledgeCategory);
        }

        public List<ResearchProjectDef> QueueFor(KnowledgeCategoryDef category)
        {
            if (category == null)
            {
                return normalQueue;
            }
            if (category == KnowledgeCategoryDefOf.Basic)
            {
                return anomalyBasicQueue;
            }
            return anomalyAdvancedQueue;
        }

        public void Enqueue(ResearchProjectDef project)
        {
            if (project == null || project.IsFinished || project.IsHidden)
            {
                return;
            }
            List<ResearchProjectDef> queue = QueueFor(project);
            AddWithPrerequisites(queue, project);
            DistinctInPlace(queue);
        }

        public void JumpToFront(ResearchProjectDef project)
        {
            List<ResearchProjectDef> queue = QueueFor(project);
            queue.Remove(project);
            queue.Insert(0, project);
        }

        /// <summary>
        /// 将项目及其所有未完成的前置(含隐藏前置,按依赖顺序)整体插入队首,
        /// 队列中已有的同项会被提前。用于不能立即开始的项目尽快启动。
        /// </summary>
        public void JumpChainToFront(ResearchProjectDef project)
        {
            if (project == null)
            {
                return;
            }
            List<ResearchProjectDef> queue = QueueFor(project);
            List<ResearchProjectDef> chain = new List<ResearchProjectDef>();
            AddWithPrerequisites(chain, project);
            queue.RemoveAll(chain.Contains);
            queue.InsertRange(0, chain);
        }

        public ResearchProjectDef NextStartable(KnowledgeCategoryDef category)
        {
            List<ResearchProjectDef> queue = QueueFor(category);
            queue.RemoveAll(p => p == null || p.IsFinished);
            return queue.FirstOrDefault(p => p.CanStartNow);
        }

        public void ClearQueue(KnowledgeCategoryDef category)
        {
            QueueFor(category).Clear();
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
