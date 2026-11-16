using System;
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

        // 每个知识类别上次实际尝试启动的队首(不存档，跟随每局 GameComponent 生命周期，避免跨存档泄漏)
        private readonly ResearchProjectDef[] lastAttemptedHead = new ResearchProjectDef[ResearchCategories.Count];

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

        public ResearchProjectDef GetLastAttemptedHead(ResearchCategory category)
        {
            return lastAttemptedHead[(int)category];
        }

        public void SetLastAttemptedHead(ResearchCategory category, ResearchProjectDef project)
        {
            lastAttemptedHead[(int)category] = project;
        }

        public void ClearLastAttemptedHead(ResearchCategory category)
        {
            lastAttemptedHead[(int)category] = null;
        }

        public bool Contains(ResearchProjectDef project)
        {
            if (project == null)
            {
                return false;
            }
            ResearchCategory category = ResearchCategories.Of(project);
            SanitizeQueue(category);
            return QueueFor(category).Contains(project);
        }

        /// <summary>
        /// 按拓扑依赖顺序收集 project 及其所有未完成的前置(含隐藏前置)。
        /// 使用 visited 集合防护环状依赖，保证不会栈溢出且同一前置只出现一次。
        /// </summary>
        public static List<ResearchProjectDef> CollectDependencyChain(ResearchProjectDef project)
        {
            List<ResearchProjectDef> chain = new List<ResearchProjectDef>();
            if (project == null || project.IsFinished || project.IsHidden)
            {
                return chain;
            }
            HashSet<ResearchProjectDef> visited = new HashSet<ResearchProjectDef>();
            CollectChainDfs(project, visited, chain);
            return chain;
        }

        private static void CollectChainDfs(ResearchProjectDef project, HashSet<ResearchProjectDef> visited, List<ResearchProjectDef> chain)
        {
            if (project == null || project.IsFinished || !visited.Add(project))
            {
                return;
            }
            if (project.prerequisites != null)
            {
                for (int i = 0; i < project.prerequisites.Count; i++)
                {
                    CollectChainDfs(project.prerequisites[i], visited, chain);
                }
            }
            if (project.hiddenPrerequisites != null)
            {
                for (int i = 0; i < project.hiddenPrerequisites.Count; i++)
                {
                    CollectChainDfs(project.hiddenPrerequisites[i], visited, chain);
                }
            }
            chain.Add(project);
        }

        /// <summary>
        /// 按类别拆分前置链后，将每个未完成项目追加到其自身所属类别的队列尾部。
        /// 跨类别前置(如异象科技依赖普通电力)会正确路由到各自槽位队列。
        /// </summary>
        public void Enqueue(ResearchProjectDef project)
        {
            List<ResearchProjectDef> chain = CollectDependencyChain(project);
            if (chain.Count == 0)
            {
                return;
            }
            for (int i = 0; i < chain.Count; i++)
            {
                ResearchProjectDef item = chain[i];
                ResearchCategory cat = ResearchCategories.Of(item);
                if (!ResearchCategories.IsActive(cat))
                {
                    continue;
                }
                SanitizeQueue(cat);
                List<ResearchProjectDef> queue = QueueFor(cat);
                if (!queue.Contains(item))
                {
                    queue.Add(item);
                }
            }
        }

        /// <summary>
        /// 将项目及其所有未完成的前置(含隐藏前置)按依赖顺序插入各自所属类别队列的队首。
        /// 队列中已有的同项会被提前。返回依赖链总长度。
        /// </summary>
        public int JumpChainToFront(ResearchProjectDef project)
        {
            List<ResearchProjectDef> chain = CollectDependencyChain(project);
            if (chain.Count == 0)
            {
                return 0;
            }
            for (int c = 0; c < ResearchCategories.Count; c++)
            {
                ResearchCategory cat = (ResearchCategory)c;
                if (!ResearchCategories.IsActive(cat))
                {
                    continue;
                }
                List<ResearchProjectDef> slice = ExtractCategorySlice(chain, cat);
                if (slice.Count > 0)
                {
                    JumpCategorySliceToFront(cat, slice);
                }
            }
            return chain.Count;
        }

        public static List<ResearchProjectDef> ExtractCategorySlice(List<ResearchProjectDef> chain, ResearchCategory category)
        {
            List<ResearchProjectDef> slice = new List<ResearchProjectDef>();
            for (int i = 0; i < chain.Count; i++)
            {
                if (ResearchCategories.Of(chain[i]) == category)
                {
                    slice.Add(chain[i]);
                }
            }
            return slice;
        }

        public void JumpCategorySliceToFront(ResearchCategory category, List<ResearchProjectDef> slice)
        {
            if (slice == null || slice.Count == 0)
            {
                return;
            }
            SanitizeQueue(category);
            List<ResearchProjectDef> queue = QueueFor(category);
            for (int i = 0; i < slice.Count; i++)
            {
                queue.Remove(slice[i]);
            }
            queue.InsertRange(0, slice);
        }

        /// <summary>
        /// 当某类别当前研究正在进行且插队链首暂时无法开始时，保持当前研究在队首(index 0)，
        /// 把插队链放到 index 1 紧随其后，既不中断当前研究也不违反"当前研究 = 队首"不变量。
        /// </summary>
        public void InsertCategorySliceAfterCurrent(ResearchCategory category, ResearchProjectDef current, List<ResearchProjectDef> slice)
        {
            if (slice == null || slice.Count == 0)
            {
                return;
            }
            SanitizeQueue(category);
            List<ResearchProjectDef> queue = QueueFor(category);
            if (current == null || current.IsFinished)
            {
                JumpCategorySliceToFront(category, slice);
                return;
            }
            queue.Remove(current);
            List<ResearchProjectDef> tailSlice = new List<ResearchProjectDef>(slice.Count);
            for (int i = 0; i < slice.Count; i++)
            {
                ResearchProjectDef item = slice[i];
                queue.Remove(item);
                if (item != current)
                {
                    tailSlice.Add(item);
                }
            }
            queue.Insert(0, current);
            queue.InsertRange(1, tailSlice);
        }

        /// <summary>
        /// 从队列中移除指定项目(不移除当前正在研究的项目)，同时移除所有排队中直接或间接依赖它的项目，
        /// 避免留下永远无法满足前置的孤立阻塞项。返回是否有队列发生变动。
        /// </summary>
        public bool RemoveFromQueue(ResearchProjectDef project)
        {
            if (project == null)
            {
                return false;
            }
            bool anyRemoved = false;
            for (int c = 0; c < ResearchCategories.Count; c++)
            {
                ResearchCategory cat = (ResearchCategory)c;
                if (!ResearchCategories.IsActive(cat))
                {
                    continue;
                }
                SanitizeQueue(cat);
                List<ResearchProjectDef> queue = QueueFor(cat);
                ResearchProjectDef current = Find.ResearchManager.GetProject(ResearchCategories.ToVanillaDef(cat));
                int removed = queue.RemoveAll(p => p != current && TransitivelyDependsOn(p, project));
                if (removed > 0)
                {
                    anyRemoved = true;
                    if (current == null)
                    {
                        lastAttemptedHead[c] = null;
                    }
                }
            }
            return anyRemoved;
        }

        private static bool TransitivelyDependsOn(ResearchProjectDef candidate, ResearchProjectDef target)
        {
            if (candidate == null || target == null)
            {
                return false;
            }
            if (candidate == target)
            {
                return true;
            }
            HashSet<ResearchProjectDef> visited = new HashSet<ResearchProjectDef>();
            return DependsOnDfs(candidate, target, visited);
        }

        private static bool DependsOnDfs(ResearchProjectDef node, ResearchProjectDef target, HashSet<ResearchProjectDef> visited)
        {
            if (node == null || !visited.Add(node))
            {
                return false;
            }
            if (node == target)
            {
                return true;
            }
            if (node.prerequisites != null)
            {
                for (int i = 0; i < node.prerequisites.Count; i++)
                {
                    if (DependsOnDfs(node.prerequisites[i], target, visited))
                    {
                        return true;
                    }
                }
            }
            if (node.hiddenPrerequisites != null)
            {
                for (int i = 0; i < node.hiddenPrerequisites.Count; i++)
                {
                    if (DependsOnDfs(node.hiddenPrerequisites[i], target, visited))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void ClearQueue(ResearchCategory category)
        {
            QueueFor(category).Clear();
            lastAttemptedHead[(int)category] = null;
        }

        public void ClearAllQueues()
        {
            for (int c = 0; c < ResearchCategories.Count; c++)
            {
                ClearQueue((ResearchCategory)c);
            }
        }

        /// <summary>
        /// 清理队列中的 null、已完成、跨类别错位项与重复项。
        /// </summary>
        public void SanitizeQueue(ResearchCategory category)
        {
            List<ResearchProjectDef> queue = QueueFor(category);
            queue.RemoveAll(p => p == null || p.IsFinished || ResearchCategories.Of(p) != category);
            DistinctInPlace(queue);
        }

        /// <summary>
        /// 清理并弹出已完成/无效项，同时兼容旧版本遗留在错误类别队列中的项。
        /// </summary>
        private void SanitizeAndMigrateAll()
        {
            List<ResearchProjectDef>[] buckets =
            {
                new List<ResearchProjectDef>(),
                new List<ResearchProjectDef>(),
                new List<ResearchProjectDef>()
            };
            for (int c = 0; c < ResearchCategories.Count; c++)
            {
                List<ResearchProjectDef> source = QueueFor((ResearchCategory)c);
                for (int i = 0; i < source.Count; i++)
                {
                    ResearchProjectDef p = source[i];
                    if (p == null || p.IsFinished)
                    {
                        continue;
                    }
                    ResearchCategory realCat = ResearchCategories.Of(p);
                    if (ResearchCategories.IsActive(realCat))
                    {
                        buckets[(int)realCat].Add(p);
                    }
                }
            }
            for (int c = 0; c < ResearchCategories.Count; c++)
            {
                List<ResearchProjectDef> target = QueueFor((ResearchCategory)c);
                target.Clear();
                target.AddRange(buckets[c]);
                DistinctInPlace(target);
            }
        }

        /// <summary>
        /// 清理队列中所有已完成/为 null/非本类别的项。
        /// </summary>
        public void PopFinishedHeads(ResearchCategory category)
        {
            SanitizeQueue(category);
        }

        /// <summary>
        /// 清理无效项后返回队首。只查看,不启动、不移除。
        /// </summary>
        public ResearchProjectDef PeekHead(ResearchCategory category)
        {
            SanitizeQueue(category);
            List<ResearchProjectDef> queue = QueueFor(category);
            return queue.Count > 0 ? queue[0] : null;
        }

        /// <summary>
        /// 把项目挪到对应队列的队首;不在队列中则插入队首。
        /// 用于维持"当前研究 = 队首"的不变量。
        /// </summary>
        public void MoveToHead(ResearchProjectDef project)
        {
            if (project == null || project.IsFinished)
            {
                return;
            }
            ResearchCategory category = ResearchCategories.Of(project);
            if (!ResearchCategories.IsActive(category))
            {
                return;
            }
            SanitizeQueue(category);
            List<ResearchProjectDef> queue = QueueFor(category);
            queue.Remove(project);
            queue.Insert(0, project);
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
            ResearchQueueController.AdvanceAllActiveCategories();
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
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                SanitizeAndMigrateAll();
                Array.Clear(lastAttemptedHead, 0, lastAttemptedHead.Length);
            }
        }
    }
}
