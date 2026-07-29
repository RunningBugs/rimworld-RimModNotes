# Research Queue 重构实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 Research Prerequisites Mod(仅 1.6)的 Harmony patch 从"整段替换原版方法"重构为最小侵入 postfix,新增 Ctrl 插队到队首功能,并把研究队列扩展到异象(Anomaly)研究页(Basic/Advanced 两条独立队列)。

**Architecture:** 数据层 `ResearchQueue`(GameComponent,三条队列 + 按 knowledgeCategory 路由);表现层三个互不拦截的 patch:`DrawStartButton`(prefix 微调 rect + postfix 画队列按钮)、`DrawContentSource`(postfix 在滚动区末尾画队列清单)、`ResearchManager.FinishProject`(postfix 自动补位)。`ResearchQueueController` 保留现有自包含的 `AttemptBeginResearch`(meme 确认弹窗)。

**Tech Stack:** C# net472,Harmony 2.3.1.1,Krafs.Rimworld.Ref 1.6.4488-beta,`dotnet build`。

**Spec:** `docs/superpowers/specs/2026-07-29-research-queue-redesign-design.md`

## Global Constraints

- **只改 `1.6/Source/` 内文件、新增 `Common/Languages/`;`1.4/`、`1.5/`、`About/` 一律不动**(冻结的历史版本)。
- 所有 patch 不得 `return false` 拦截原版方法;只允许 postfix,以及仅微调参数的 prefix。
- 不整段复制原版绘制代码;私有成员访问仅限缓存的 `AccessTools.FieldRef` 读 `selectedProject`。
- 异象相关路径保持守卫:未激活 Anomaly DLC 时行为与旧版一致、不报错。
- 无自动化测试框架;每个任务的验收 = `dotnet build` 成功;最后任务含游戏内手动验证清单。
- 编译命令(在仓库根目录执行):`dotnet build 1.6/Source/mod.csproj`,预期 `Build succeeded`,输出到 `1.6/Assemblies/ResearchPrerequisites.dll`。
- 语言版本 C# 11(`LangVersion 11.0`)。

---

### Task 1: 数据层重写 — ResearchQueue + ResearchQueueController + 清空 Main.cs

**Files:**
- Modify: `1.6/Source/Main.cs`(删到只剩 Harmony 初始化)
- Create: `1.6/Source/ResearchQueue.cs`
- Create: `1.6/Source/ResearchQueueController.cs`

**Interfaces:**
- Consumes: 无(第一个任务)。
- Produces(后续任务依赖这些签名):
  - `class ResearchQueue : GameComponent`
    - `static ResearchQueue Instance { get; }`(`Current.Game?.GetComponent<ResearchQueue>()`,游戏外可能为 null)
    - `List<ResearchProjectDef> QueueFor(ResearchProjectDef project)`
    - `List<ResearchProjectDef> QueueFor(KnowledgeCategoryDef category)`(null → 普通队列;`KnowledgeCategoryDefOf.Basic` → Basic 队列;其他非 null → Advanced 队列)
    - `void Enqueue(ResearchProjectDef project)`(递归拉前置入队尾,保序去重)
    - `void JumpToFront(ResearchProjectDef project)`
    - `ResearchProjectDef NextStartable(KnowledgeCategoryDef category)`(清理已完成项后返回第一个 `CanStartNow`,可能为 null;阻塞项保留在队列中)
    - `void ClearQueue(KnowledgeCategoryDef category)`
  - `static class ResearchQueueController`
    - `static void AttemptBeginResearch(ResearchProjectDef projectToStart)`(含 meme 缺失确认弹窗,逻辑从旧 Main.cs 原样搬移)
    - `static void TryStartNext(KnowledgeCategoryDef category)`(槽位空则取 `NextStartable` 开始,并从队列移除该项)

- [ ] **Step 1: 重写 `1.6/Source/Main.cs` 为以下内容(整文件替换)**

```csharp
using HarmonyLib;
using Verse;

namespace ResearchPrerequisites
{
    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            new Harmony("com.runningbugs.researchprerequisites").PatchAll();
        }
    }
}
```

- [ ] **Step 2: 创建 `1.6/Source/ResearchQueue.cs`**

```csharp
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
```

- [ ] **Step 3: 创建 `1.6/Source/ResearchQueueController.cs`**

meme 相关逻辑从旧 Main.cs 原样搬移(基于公开 API,无反射),仅删除 `ResearchButtonMode`/`mode` 状态机,新增 `TryStartNext`:

```csharp
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
    }
}
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build 1.6/Source/mod.csproj`
Expected: `Build succeeded`,0 errors。`RPModSettings.cs`、`Logger.cs` 未改动,应照常编译通过。

- [ ] **Step 5: Commit**

```bash
git add 1.6/Source/Main.cs 1.6/Source/ResearchQueue.cs 1.6/Source/ResearchQueueController.cs
git commit -m "refactor: three-lane research queue data layer, strip Main.cs to harmony init"
```

---

### Task 2: FinishProject postfix — 研究完成后自动补位

**Files:**
- Create: `1.6/Source/Patches/FinishProjectPatch.cs`

**Interfaces:**
- Consumes: `ResearchQueueController.TryStartNext(KnowledgeCategoryDef)`(Task 1)。
- Produces: 无新接口。

说明:`RPModSettings.cs` 中已有同一方法的 prefix/postfix(完成发 Letter 功能),本 patch 与其共存,Harmony 会自动串联,互不影响。

- [ ] **Step 1: 创建 `1.6/Source/Patches/FinishProjectPatch.cs`**

```csharp
using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchPrerequisites
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    public static class FinishProjectPatch
    {
        public static void Postfix(ResearchProjectDef proj)
        {
            if (proj == null || Scribe.mode != LoadSaveMode.Inactive)
            {
                return;
            }
            ResearchQueueController.TryStartNext(proj.knowledgeCategory);
        }
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build 1.6/Source/mod.csproj`
Expected: `Build succeeded`,0 errors。

- [ ] **Step 3: Commit**

```bash
git add 1.6/Source/Patches/FinishProjectPatch.cs
git commit -m "feat: auto-start next queued research on project completion"
```

---

### Task 3: DrawStartButton patch — 队列按钮 + Ctrl 插队

**Files:**
- Create: `1.6/Source/Patches/DrawStartButtonPatch.cs`

**Interfaces:**
- Consumes: `ResearchQueue.Instance`、`Enqueue` / `JumpToFront`(Task 1)、`ResearchQueueController.TryStartNext`(Task 1)。
- Produces: 无新接口。

行为:原版按钮不动(prefix 仅在有队列按钮可显示时把它压缩到左侧 58%);postfix 在右侧 40% 画队列按钮。无修饰键 = "加入队列"(队尾);按住 Ctrl 且 `CanStartNow` = "插队到队首"。点击后调用 `TryStartNext` 踢一下槽位(空闲则立即开始)。翻译 key `ResearchJumpQueueFront`、`ResearchQueueButtonTip` 在 Task 5 才添加,此阶段游戏内会显示原始 key 文本,属预期。

- [ ] **Step 1: 创建 `1.6/Source/Patches/DrawStartButtonPatch.cs`**

```csharp
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ResearchPrerequisites
{
    [HarmonyPatch(typeof(MainTabWindow_Research), "DrawStartButton")]
    public static class DrawStartButtonPatch
    {
        private static readonly AccessTools.FieldRef<MainTabWindow_Research, ResearchProjectDef> SelectedProject =
            AccessTools.FieldRefAccess<MainTabWindow_Research, ResearchProjectDef>("selectedProject");

        private static Rect queueButtonRect;

        private static bool ShouldShowQueueButton(ResearchProjectDef project)
        {
            return project != null && !project.IsFinished && !project.IsHidden
                && !Find.ResearchManager.IsCurrentProject(project);
        }

        public static void Prefix(ref Rect startButRect, MainTabWindow_Research __instance)
        {
            if (!ShouldShowQueueButton(SelectedProject(__instance)))
            {
                return;
            }
            queueButtonRect = startButRect.RightPart(0.4f);
            startButRect = startButRect.LeftPart(0.58f);
        }

        public static void Postfix(MainTabWindow_Research __instance)
        {
            ResearchProjectDef project = SelectedProject(__instance);
            if (!ShouldShowQueueButton(project))
            {
                return;
            }
            bool jump = project.CanStartNow
                && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
            string label = jump ? "ResearchJumpQueueFront".Translate() : "ResearchAddToQueue".Translate();
            TooltipHandler.TipRegion(queueButtonRect, "ResearchQueueButtonTip".Translate());
            if (Widgets.ButtonText(queueButtonRect, label))
            {
                ResearchQueue queue = ResearchQueue.Instance;
                if (queue == null)
                {
                    return;
                }
                if (jump)
                {
                    queue.JumpToFront(project);
                }
                else
                {
                    queue.Enqueue(project);
                }
                ResearchQueueController.TryStartNext(project.knowledgeCategory);
            }
        }
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build 1.6/Source/mod.csproj`
Expected: `Build succeeded`,0 errors。

- [ ] **Step 3: Commit**

```bash
git add 1.6/Source/Patches/DrawStartButtonPatch.cs
git commit -m "feat: queue button on research panel, ctrl+click jumps to queue front"
```

---

### Task 4: DrawContentSource patch — 滚动区末尾的队列清单

**Files:**
- Create: `1.6/Source/Patches/DrawContentSourcePatch.cs`

**Interfaces:**
- Consumes: `ResearchQueue.Instance`、`QueueFor(ResearchProjectDef)`、`ClearQueue(KnowledgeCategoryDef)`(Task 1)。
- Produces: 无新接口。

说明:`DrawContentSource(Rect, ResearchProjectDef)` 是 `DrawProjectScrollView` 滚动区内最后一个绘制调用,返回高度累加进布局 y;postfix 在 `rect.y + __result` 处接着画,并把总高度写回 `__result`,普通页与异象页自动同时生效,与其他 Mod 的同类 patch 通过 `__result` 累加天然兼容。

- [ ] **Step 1: 创建 `1.6/Source/Patches/DrawContentSourcePatch.cs`**

```csharp
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ResearchPrerequisites
{
    [HarmonyPatch(typeof(MainTabWindow_Research), "DrawContentSource")]
    public static class DrawContentSourcePatch
    {
        public static void Postfix(Rect rect, ResearchProjectDef project, ref float __result)
        {
            if (project == null)
            {
                return;
            }
            ResearchQueue queue = ResearchQueue.Instance;
            if (queue == null)
            {
                return;
            }
            List<ResearchProjectDef> list = queue.QueueFor(project);
            if (list.Count == 0)
            {
                return;
            }

            float y = rect.y + __result;
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, Text.LineHeight), "ClearResearchQueue".Translate()))
            {
                queue.ClearQueue(project.knowledgeCategory);
                __result = y - rect.y + Text.LineHeight;
                return;
            }
            y += Text.LineHeight;
            Widgets.Label(new Rect(rect.x, y, rect.width, Text.LineHeight), "CurrentResearchQueue".Translate());
            y += Text.LineHeight;
            foreach (ResearchProjectDef p in list)
            {
                Widgets.Label(new Rect(rect.x + 10f, y, rect.width - 10f, Text.LineHeight), p.LabelCap);
                y += Text.LineHeight;
            }
            __result = y - rect.y;
        }
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build 1.6/Source/mod.csproj`
Expected: `Build succeeded`,0 errors。

- [ ] **Step 3: Commit**

```bash
git add 1.6/Source/Patches/DrawContentSourcePatch.cs
git commit -m "feat: draw research queue list below project details, works on anomaly tab"
```

---

### Task 5: 翻译文件(Common/Languages)+ 游戏内手动验证

**Files:**
- Create: `Common/Languages/ChineseSimplified/Keyed/Keys.xml`
- Create: `Common/Languages/English/Keyed/Keys.xml`

说明:`1.6/` 原本没有 Languages 目录,旧 key 放在 `1.4/Languages`(1.6 不加载,游戏内显示原始 key)。`Common/Languages` 对所有版本生效;版本目录下同名 key 优先,不影响冻结的 1.4/1.5。

- [ ] **Step 1: 创建 `Common/Languages/ChineseSimplified/Keyed/Keys.xml`**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<LanguageData>
    <ResearchAddToQueue>加入研究队列</ResearchAddToQueue>
    <ResearchJumpQueueFront>插队到队首</ResearchJumpQueueFront>
    <ResearchQueueButtonTip>点击加入队尾;按住 Ctrl 点击插队到队首</ResearchQueueButtonTip>
    <CurrentResearchQueue>当前研究队列:</CurrentResearchQueue>
    <ClearResearchQueue>清空研究队列</ClearResearchQueue>
</LanguageData>
```

- [ ] **Step 2: 创建 `Common/Languages/English/Keyed/Keys.xml`**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<LanguageData>
    <ResearchAddToQueue>Add to research queue</ResearchAddToQueue>
    <ResearchJumpQueueFront>Jump to queue front</ResearchJumpQueueFront>
    <ResearchQueueButtonTip>Click to append to queue; hold Ctrl and click to jump to the front</ResearchQueueButtonTip>
    <CurrentResearchQueue>Current research queue:</CurrentResearchQueue>
    <ClearResearchQueue>Clear research queue</ClearResearchQueue>
</LanguageData>
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build 1.6/Source/mod.csproj`
Expected: `Build succeeded`,0 errors。

- [ ] **Step 4: 游戏内手动验证清单(启动 RimWorld 1.6 + 本 Mod)**

普通研究页:
- 选中一个前置未完成的项目 → 右侧出现"加入队列"按钮,点击后其未完成前置 + 该项目按序进入队列,空闲槽位立即开始第一项。
- 队列清单显示在项目详情滚动区末尾,顺序正确、无重叠。
- 选中一个可立即研究的项目,按住 Ctrl → 按钮变为"插队到队首",点击后该项目位于队首。
- 一项研究自然完成 → 自动开始队列中下一个 `CanStartNow` 的项目;队首被卡住时跳过看下一个,队列不被清空。
- 点击"停止研究" → 对应队列被清空,不自动开始下一项。
- "清空研究队列"按钮只清队列,不停止当前研究。

异象研究页(需 Anomaly DLC):
- Basic / Advanced 各有独立队列与清单;两条槽位并行完成时各自补位,互不干扰。
- Ctrl 插队在异象页同样生效。

兼容性:
- 读旧存档:原 `researchQueue` 内容作为普通队列恢复,无报错。
- 不激活 Anomaly DLC 开档:无红字,普通队列功能正常。
- Mod 设置里"FinishProjectWithLetter"功能仍然生效(与自动补位共存)。

- [ ] **Step 5: Commit**

```bash
git add Common/Languages
git commit -m "feat: add zh/en translations for queue UI via Common/Languages"
```

---

### Task 6: StopProject postfix — 手动停止时清空对应队列

**Files:**
- Create: `1.6/Source/Patches/StopProjectPatch.cs`

**Interfaces:**
- Consumes: `ResearchQueue.Instance`、`ClearQueue(KnowledgeCategoryDef)`(Task 1)。
- Produces: 无新接口。

说明:spec 要求"手动停止研究 → 清空该槽位对应的队列",但原版停止按钮只调 `StopProject`,本身不清队列(旧 Mod 是在整段替换里塞了 `Clear()`)。因此补此 patch:`ResearchManager.StopProject(ResearchProjectDef)` postfix 清空该项目 `knowledgeCategory` 对应的队列,保留"停止 = 全部取消"语义;异象页停 Basic 只清 Basic 队列。已核实原版 `FinishProject` 内部直接置空槽位字段、不调 `StopProject`,自然完成不会误触清队。

- [ ] **Step 1: 创建 `1.6/Source/Patches/StopProjectPatch.cs`**

```csharp
using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchPrerequisites
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.StopProject))]
    public static class StopProjectPatch
    {
        public static void Postfix(ResearchProjectDef proj)
        {
            if (proj == null)
            {
                return;
            }
            ResearchQueue.Instance?.ClearQueue(proj.knowledgeCategory);
        }
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build 1.6/Source/mod.csproj`
Expected: `Build succeeded`,0 errors。

- [ ] **Step 3: Commit**

```bash
git add 1.6/Source/Patches/StopProjectPatch.cs
git commit -m "feat: manual stop clears the corresponding research queue"
```

---

## Self-Review 记录

- Spec 覆盖:三队列(Task 1)、入队/Ctrl 插队(Task 1+3)、自然完成自动补位(Task 2)、队列按钮(Task 3)、队列清单 + 清空按钮(Task 4)、翻译(Task 5)、手动停止清队(Task 6)、异象页队列(Task 2/3/4/6 按 `knowledgeCategory` 路由自动覆盖)、最小侵入 patch(全部任务均无 `return false`)、旧存档兼容(Task 1 ExposeData 沿用旧 key)。无遗漏。
- 类型一致性:`TryStartNext(KnowledgeCategoryDef)` 在 Task 1 定义,Task 2/3 按此签名调用;`QueueFor` 两个重载在 Task 1 定义,Task 3/4/6 分别使用 `QueueFor(ResearchProjectDef)`、`ClearQueue(KnowledgeCategoryDef)`、`NextStartable(KnowledgeCategoryDef)`,签名一致;`Instance` 的可空性在所有调用点均有判空。
- 占位符:无 TBD/TODO;所有代码步骤均含完整代码。
- 已核实的外部事实:`ResearchProjectDef.CanStartNow` 为原版公开属性(1.6);`GenUI.LeftPart/RightPart(Rect, float)` 存在于 Verse;`KnowledgeCategoryDefOf.Basic/Advanced` 存在于 RimWorld;`ResearchManager.GetProject(KnowledgeCategoryDef = null)` / `StopProject` / `FinishProject` 均为 public;`DrawContentSource(Rect, ResearchProjectDef)` 返回 float;`RPModSettings.cs` 已 patch `FinishProject`,与本计划 Task 2 的 postfix 共存不冲突。
