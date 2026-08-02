# 队首即当前研究(队列算法回摆)实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 1.6 研究队列回摆为 1.5 式设计:队列包含当前研究、队首即当前研究、tick 驱动严格按队首推进。

**Architecture:** `ResearchQueue`(GameComponent)持有的三条队列(普通/异象 Basic/异象 Advanced)语义改为包含当前研究,`queue[0]` 即进行中项目;`GameComponentTick` 每 tick 自愈不变量并推进队首;新增 `SetCurrentProject` postfix 让任何启动路径都同步队首;绘制改为单段队列列表。

**Tech Stack:** C# 11 / .NET Framework 4.7.2 / Harmony 2.3 / Krafs.Rimworld.Ref 1.6。

## Global Constraints

- 只改 `16-ResearchPrerequisites/1.6/` 目录;`1.4/`、`1.5/` 是冻结的历史版本,一律不动。
- 设计依据:`16-ResearchPrerequisites/docs/superpowers/specs/2026-08-02-queue-head-is-current-design.md`,语义以它为准。
- 本仓库无 RimWorld 单元测试设施,每个任务的验证手段是 `dotnet build -c Release` 0 错误;功能验证靠末尾的游戏内清单。
- 按环境策略**不执行任何 git 提交**,由用户自行提交。
- 所有 patch 保持最小侵入:不 `return false` 拦截任何原版方法。
- 队首卡住(`!CanStartNow`)时**保留队列等待**,不照搬 1.5 的清队行为。
- `RPModSettings.cs` 里的 `Patch_FinishProject`(完成发 Letter 功能)与本计划无关,保持不动。

---

### Task 1: ResearchQueue 数据层 — 队首操作三方法

**Files:**
- Modify: `16-ResearchPrerequisites/1.6/Source/ResearchQueue.cs`

**Interfaces:**
- Consumes: 现有 `QueueFor(KnowledgeCategoryDef)`、`DistinctInPlace`。
- Produces(后续任务依赖这三个签名):
  - `public void PopFinishedHeads(KnowledgeCategoryDef category)`
  - `public ResearchProjectDef PeekHead(KnowledgeCategoryDef category)` — 弹出已完成队首后返回新队首,不启动、不移除
  - `public void MoveToHead(ResearchProjectDef project)` — 把项目挪到对应队列队首,不在队列则插入

本任务纯增量:只加方法,不删不改任何现有成员,保证编译通过。

- [ ] **Step 1: 在 `ClearQueue` 方法之后添加三个方法**

在 `16-ResearchPrerequisites/1.6/Source/ResearchQueue.cs` 的 `ClearQueue` 方法后插入:

```csharp
        /// <summary>
        /// 弹出队首所有已完成/为 null 的项。
        /// </summary>
        public void PopFinishedHeads(KnowledgeCategoryDef category)
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
        public ResearchProjectDef PeekHead(KnowledgeCategoryDef category)
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
            List<ResearchProjectDef> queue = QueueFor(project);
            queue.Remove(project);
            queue.Insert(0, project);
        }
```

- [ ] **Step 2: 编译验证**

Run: `cd 16-ResearchPrerequisites/1.6/Source && dotnet build -c Release`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

### Task 2: ResearchQueueController — tick 推进核心 + 链式插队合并 + 更新全部调用方

**Files:**
- Modify: `16-ResearchPrerequisites/1.6/Source/ResearchQueueController.cs`
- Modify: `16-ResearchPrerequisites/1.6/Source/Patches/DrawStartButtonPatch.cs`
- Delete: `16-ResearchPrerequisites/1.6/Source/Patches/FinishProjectPatch.cs`

**Interfaces:**
- Consumes: Task 1 的 `PeekHead`、`MoveToHead`;现有 `AttemptBeginResearch`。
- Produces:
  - `public static void AdvanceCategory(KnowledgeCategoryDef category)` — Task 3 的 tick 与 UI 入队都调用它
  - `public static void JumpChainToFrontAndStart(ResearchProjectDef project)` — 合并后的唯一插队入口
- 删除 `TryStartNext`、`JumpToFrontAndStart`(无残留引用)。

`AdvanceCategory` 语义:槽位被占用 → 自愈"当前研究 = 队首";槽位空闲 → 严格按队首推进,卡住则保留队列等待;`LastAttemptedHead` 防止 meme 弹窗取消后每 tick 重弹(只在真正调用 `AttemptBeginResearch` 时记录,卡住不记录,解锁后即可自动启动)。

- [ ] **Step 1: 重写 `ResearchQueueController.cs` 的推进与插队部分**

删除 `TryStartNext`、`JumpToFrontAndStart`、`JumpChainToFrontAndStart` 三个方法,替换为:

```csharp
        /// <summary>
        /// 每个知识类别上次实际尝试启动的队首。
        /// 玩家在 meme 确认弹窗取消后,同一队首不再每 tick 重复弹窗,
        /// 直到队首变化(完成/入队/插队/清空)才恢复自动尝试。
        /// </summary>
        private static readonly Dictionary<KnowledgeCategoryDef, ResearchProjectDef> LastAttemptedHead =
            new Dictionary<KnowledgeCategoryDef, ResearchProjectDef>();

        /// <summary>
        /// 推进一个类别:槽位被占用则自愈"当前研究 = 队首"不变量;
        /// 槽位空闲则严格按队首推进,队首卡住则保留队列等待。
        /// 由 GameComponentTick 每 tick 调用,也可在入队后立即调用。
        /// </summary>
        public static void AdvanceCategory(KnowledgeCategoryDef category)
        {
            if (category != null && !ModsConfig.AnomalyActive)
            {
                return;
            }
            ResearchQueue queue = ResearchQueue.Instance;
            if (queue == null)
            {
                return;
            }
            ResearchProjectDef current = Find.ResearchManager.GetProject(category);
            if (current != null)
            {
                // 自愈:当前研究必须在队首(同时覆盖旧存档迁移与外部启动路径)。
                if (queue.PeekHead(category) != current)
                {
                    queue.MoveToHead(current);
                }
                LastAttemptedHead[category] = current;
                return;
            }
            ResearchProjectDef head = queue.PeekHead(category);
            if (head == null)
            {
                LastAttemptedHead.Remove(category);
                return;
            }
            if (!head.CanStartNow)
            {
                // 队首卡住:保留队列等待,不记录,解锁后自动启动。
                return;
            }
            if (LastAttemptedHead.TryGetValue(category, out ResearchProjectDef last) && last == head)
            {
                // 已尝试过且被玩家取消,等待队首变化。
                return;
            }
            LastAttemptedHead[category] = head;
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
            KnowledgeCategoryDef category = project.knowledgeCategory;
            queue.JumpChainToFront(project);
            ResearchProjectDef head = queue.PeekHead(category);
            if (head == null || !head.CanStartNow)
            {
                // 链上暂无可开始项目(如缺科技印花/研究台),链已排在队首待命。
                return;
            }
            if (Find.ResearchManager.GetProject(category) == head)
            {
                // 链首已在研究中,无需动作。
                return;
            }
            LastAttemptedHead[category] = head;
            AttemptBeginResearch(head);
        }
```

- [ ] **Step 2: 更新 `DrawStartButtonPatch.cs` 的按钮点击分支**

把 `16-ResearchPrerequisites/1.6/Source/Patches/DrawStartButtonPatch.cs` 中 Postfix 的分支:

```csharp
                if (!jump)
                {
                    queue.Enqueue(project);
                    ResearchQueueController.TryStartNext(project.knowledgeCategory);
                }
                else if (project.CanStartNow)
                {
                    ResearchQueueController.JumpToFrontAndStart(project);
                }
                else
                {
                    // 不能立即开始:连同未完成的前置整体插队,尽快启动
                    ResearchQueueController.JumpChainToFrontAndStart(project);
                }
```

改为:

```csharp
                if (!jump)
                {
                    queue.Enqueue(project);
                    ResearchQueueController.AdvanceCategory(project.knowledgeCategory);
                }
                else
                {
                    // 可立即开始的项目其前置链就是自身,统一走链式插队
                    ResearchQueueController.JumpChainToFrontAndStart(project);
                }
```

- [ ] **Step 3: 删除 `FinishProjectPatch.cs`**

```bash
rm 16-ResearchPrerequisites/1.6/Source/Patches/FinishProjectPatch.cs
```

(tick 推进取代完成事件补位;`RPModSettings.cs` 的 `Patch_FinishProject` 是独立的 Letter 功能,不受影响。)

- [ ] **Step 4: 编译验证 + 残留引用检查**

Run: `cd 16-ResearchPrerequisites/1.6/Source && dotnet build -c Release && grep -rn "TryStartNext\|JumpToFrontAndStart" --include="*.cs" . | grep -v obj/ || echo "no stale references"`
Expected: `Build succeeded. 0 Error(s)` + `no stale references`

---

### Task 3: ResearchQueue.GameComponentTick — 接线 tick 驱动

**Files:**
- Modify: `16-ResearchPrerequisites/1.6/Source/ResearchQueue.cs`

**Interfaces:**
- Consumes: Task 2 的 `ResearchQueueController.AdvanceCategory(KnowledgeCategoryDef)`。
- Produces: 每 tick 对三个类别推进;无新成员供外部调用。

- [ ] **Step 1: 在 `ResearchQueue` 类中添加 `GameComponentTick`**

在 `16-ResearchPrerequisites/1.6/Source/ResearchQueue.cs` 的 `ExposeData` 方法之前插入:

```csharp
        /// <summary>
        /// 每 tick 推进各类别队列:自愈"当前研究 = 队首"不变量,
        /// 槽位空闲时严格按队首启动下一项。
        /// </summary>
        public override void GameComponentTick()
        {
            ResearchQueueController.AdvanceCategory(null);
            if (ModsConfig.AnomalyActive)
            {
                ResearchQueueController.AdvanceCategory(KnowledgeCategoryDefOf.Basic);
                ResearchQueueController.AdvanceCategory(KnowledgeCategoryDefOf.Advanced);
            }
        }
```

注:异象高级知识类别在 1.6 的 DefOf 字段名是 `Advanced`(`KnowledgeCategoryDefOf.Advanced`,
已编译探针确认;1.5 时期名为 `Anomaly`,1.6 已改名),与
`QueueFor` 中"非 Basic 即 Advanced"的路由一致。

- [ ] **Step 2: 编译验证**

Run: `cd 16-ResearchPrerequisites/1.6/Source && dotnet build -c Release`
Expected: `Build succeeded. 0 Error(s)`

---

### Task 4: SetCurrentProjectPatch — 任何启动路径同步队首

**Files:**
- Create: `16-ResearchPrerequisites/1.6/Source/Patches/SetCurrentProjectPatch.cs`

**Interfaces:**
- Consumes: Task 1 的 `ResearchQueue.MoveToHead(ResearchProjectDef)`。
- Produces: 原版"研究"按钮、本 Mod、其他 Mod 使项目成为当前研究后,该项目被挪到对应队列队首(不在队列则插入),队列其余内容保留。

- [ ] **Step 1: 新建补丁文件**

创建 `16-ResearchPrerequisites/1.6/Source/Patches/SetCurrentProjectPatch.cs`:

```csharp
using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchPrerequisites
{
    /// <summary>
    /// 任何路径(原版按钮、本 Mod、其他 Mod)使项目成为当前研究后,
    /// 把它挪到对应队列队首,维持"当前研究 = 队首"的不变量。
    /// 本 Mod 自己的启动路径也走 SetCurrentProject,此同步为幂等 no-op。
    /// </summary>
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.SetCurrentProject))]
    public static class SetCurrentProjectPatch
    {
        public static void Postfix(ResearchProjectDef proj)
        {
            if (proj == null || Scribe.mode != LoadSaveMode.Inactive)
            {
                return;
            }
            ResearchQueue.Instance?.MoveToHead(proj);
        }
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `cd 16-ResearchPrerequisites/1.6/Source && dotnet build -c Release`
Expected: `Build succeeded. 0 Error(s)`

---

### Task 5: DrawContentSourcePatch — 单段队列绘制

**Files:**
- Modify: `16-ResearchPrerequisites/1.6/Source/Patches/DrawContentSourcePatch.cs`

**Interfaces:**
- Consumes: 现有 `QueueFor`、`ClearQueue`;语义变为队列含当前研究。
- Produces: 无新成员;绘制行为:当前研究在队列列表内加粗标记,不再单独成段。

- [ ] **Step 1: 替换绘制循环**

把 `16-ResearchPrerequisites/1.6/Source/Patches/DrawContentSourcePatch.cs` 中从
`y += Text.LineHeight;`(清空按钮之后)到方法末尾的绘制代码:

```csharp
            y += Text.LineHeight;
            Widgets.Label(new Rect(rect.x, y, rect.width, Text.LineHeight), "CurrentResearchQueue".Translate());
            y += Text.LineHeight;
            if (current != null)
            {
                // 当前正在研究的项目显示为队首,加粗 + 箭头标记
                Widgets.Label(new Rect(rect.x + 10f, y, rect.width - 10f, Text.LineHeight),
                    "<b>→ " + current.LabelCap + " (" + "InProgress".Translate() + ")</b>");
                y += Text.LineHeight;
            }
            foreach (ResearchProjectDef p in list)
            {
                // 防线:队列理论上不含当前研究,即使有也不再重复绘制。
                if (p == null || p == current)
                {
                    continue;
                }
                Widgets.Label(new Rect(rect.x + 10f, y, rect.width - 10f, Text.LineHeight), p.LabelCap);
                y += Text.LineHeight;
            }
            __result = y - rect.y;
```

改为:

```csharp
            y += Text.LineHeight;
            Widgets.Label(new Rect(rect.x, y, rect.width, Text.LineHeight), "CurrentResearchQueue".Translate());
            y += Text.LineHeight;
            // 队列包含当前研究(队首):单段绘制,进行中的项加粗 + 箭头标记。
            foreach (ResearchProjectDef p in list)
            {
                if (p == null)
                {
                    continue;
                }
                string text = p == current
                    ? "<b>→ " + p.LabelCap + " (" + "InProgress".Translate() + ")</b>"
                    : p.LabelCap;
                Widgets.Label(new Rect(rect.x + 10f, y, rect.width - 10f, Text.LineHeight), text);
                y += Text.LineHeight;
            }
            __result = y - rect.y;
```

- [ ] **Step 2: 编译验证**

Run: `cd 16-ResearchPrerequisites/1.6/Source && dotnet build -c Release`
Expected: `Build succeeded. 0 Error(s)`

---

### Task 6: 终验 — 全量编译 + 游戏内功能清单

**Files:**
- 无改动;仅验证。

- [ ] **Step 1: 清理全量重编译**

Run: `cd 16-ResearchPrerequisites/1.6/Source && dotnet build -c Release --no-incremental`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`,DLL 输出到 `16-ResearchPrerequisites/1.6/Assemblies/ResearchPrerequisites.dll`

- [ ] **Step 2: 游戏内功能清单(交用户验证)**

1. 普通入队:槽位空时队首立即开始,显示在列表首行并加粗"(进行中)"。
2. 研究完成后自动按队列顺序补位下一项。
3. Ctrl 插队:链排全队首并启动链首;被挤下的当前研究留在队列中,之后自动恢复。
4. 回归验证(原 bug):对缺前置的研究 Ctrl+"插队到队首"两次 → 无重复行,进行中的前置只有一行加粗标记。
5. 原版"研究"按钮手动开始队列外的项目 X → X 出现在队首,队列其余内容保留。
6. 停止研究 → 对应类别队列清空。
7. 队首卡住(如缺科技印花)→ 队列保留等待,不跳过启动后面项目;印花补齐后自动启动。
8. meme 确认弹窗点取消 → 不再反复弹窗。
9. 旧存档加载 → 当前研究自动出现在队首,无需手动干预。

---

## Self-Review 记录

- Spec 覆盖:数据层三方法(T1)、tick 推进+防刷屏(T2/T3)、链式插队合并(T2)、
  SetCurrentProject 同步(T4)、单段绘制(T5)、删除 FinishProjectPatch(T2)、
  存档免迁移(tick 自愈,T3)——均有对应任务。
- 类型一致性:`AdvanceCategory` / `JumpChainToFrontAndStart` / `PeekHead` /
  `MoveToHead` / `PopFinishedHeads` 签名跨任务一致;`LastAttemptedHead` 仅在
  T2 内部使用。
- 无占位符;每个代码步骤均给出完整代码。
