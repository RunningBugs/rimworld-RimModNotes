# 研究队列算法回摆:队首即当前研究 — 设计文档

日期:2026-08-02
目标版本:RimWorld 1.6。**只改 `1.6/` 目录;`1.4/`、`1.5/` 是冻结的历史版本,保持不动。**

## 背景

2026-07-29 的重构(见 `2026-07-29-research-queue-redesign-design.md`)把队列设计为
"队列不含当前研究,当前研究由原版 ResearchManager 单独跟踪,补位时从队列中找第一个
`CanStartNow` 的项目(允许跳过卡住的队首)"。该设计暴露了两类问题:

1. **重复显示 bug**(已临时修复):插队操作会把当前研究留在队列里,绘制时
   "当前研究"段与队列段各画一次,同一项目出现两行。根因是"当前研究"与"队列"
   两份状态需要手工保持同步,任何一条路径漏掉就出问题。
2. **顺序不严格**:`NextStartable` 跳过卡住的队首去启动后面的项目,队列展示顺序
   与实际执行顺序不一致,观感上"队列不准"。

用户决定回摆到 1.4/1.5 的核心设计:**精确计算研究队列,队首即当前研究**。
本文档取代上一份设计中"出队/补位规则"与"当前研究跟踪"部分;其余(三条队列、
最小侵入 patch、递归入队语义等)继续有效。

与 1.5 原算法的一处有意偏离(经用户批准):1.5 在队首 `!CanStartNow` 时清空整个
队列;本设计卡住时**保留队列等待**,因为 1.6 的插队功能明确支持"链首暂时开不了、
排好待命"的场景,照搬清队会把插队排好的链立刻清空。

## 核心不变量

> 对每个知识类别:`ResearchManager.GetProject(category)` 非空时,当前研究
> **就是**对应队列的 `queue[0]`;研究完成后才从队首弹出。

队列是唯一事实源,"当前研究"不再是需要单独维护的第二份状态。

## 目标设计

### 数据层(ResearchQueue.cs)

三条队列不变(普通 / 异象 Basic / 异象 Advanced),语义变为**包含当前研究**:

- `Enqueue(project)`:递归追加未完成前置链到队尾,保序去重(逻辑不变)。
- `JumpChainToFront(project)`:构建前置链、去重、从队列移除链中项、整体插到
  index 0(逻辑不变)。若当前研究本身在链中(如进行中的前置),它被挪到队首——
  恰好符合不变量,此前的重复显示 bug 从结构上不再可能。
- 新增队首操作:
  - `PopFinishedHeads(category)`:从队首起弹出已完成/为 null 的项。
  - `PeekHead(category)`:弹出已完成队首后返回新队首(不启动、不移除)。
- 删除 `NextStartable`(跳过队首找可开始项的旧语义)与 `JumpToFront`
  (被 `JumpChainToFront` 涵盖)。

### 推进机制(tick 驱动,取代 FinishProject 补位)

`ResearchQueue.GameComponentTick` 每 tick 对三个类别执行:

1. 槽位被占用 → 校验不变量:当前研究不在队首则挪到队首(自愈,同时覆盖旧存档
   迁移:旧存档队列不含当前研究,加载后首个 tick 自动插到队首)。
2. 槽位空且队列非空 → `PopFinishedHeads` 后取队首:
   - 队首 `CanStartNow` → `AttemptBeginResearch`(队首**留在队列中**);
   - 队首不可开始 → **什么都不做,队列保留等待**(偏离 1.5 的清队行为)。
3. 防 meme 弹窗刷屏:仅当"槽位由占用变为空"或"队首发生变化"时才尝试启动;
   玩家在 meme 确认弹窗上取消后,同一队首不会每 tick 重复弹窗,直到队列或
   当前研究发生变化。

`FinishProjectPatch` 删除(tick 最多延迟 1 tick,无感知)。注意 `RPModSettings.cs`
另有 `FinishProject` 的 Letter postfix,与本改动无关,保持不动。

### 操作语义(ResearchQueueController.cs)

- **加入队列按钮**:`Enqueue` + 立即对该类别执行一次推进(不等下一 tick)。
- **Ctrl 插队**:`JumpChainToFront` 后,链首可开始则立即 `AttemptBeginResearch`;
  被挤下的当前研究留在队列中链之后,进度由原版按项目保存,之后按队列顺序
  自动恢复。链首不可开始则链排在队首待命(现有行为保留)。
- **原版"研究"按钮手动开始 X**:新增 `ResearchManager.SetCurrentProject` postfix,
  把 X 挪到对应队列队首(先移除再插入 index 0),**保留**队列其余内容。
  本 Mod 自己的启动路径也走 `SetCurrentProject`,此同步为幂等 no-op。
  (1.5 是清空队列重建;经用户批准改为保留。)
- **停止研究**:`StopProjectPatch` 清空对应类别队列(维持现状)。
- **面板清空按钮**:清队列 + 停止当前研究(维持现状)。

### 显示(DrawContentSourcePatch)

- 不再分"当前研究 + 队列"两段,直接遍历绘制队列;`p == 当前研究` 的项加粗 +
  `→` 与"(进行中)"标记。
- 清空按钮显示条件:队列非空或当前研究非空(维持现状)。

### 存档兼容

序列化 key 不变,无需迁移代码:旧存档读入的队列不含当前研究,由 tick 自愈逻辑
(推进机制第 1 条)在加载后自动把当前研究插到队首。

## 改动文件清单

- `1.6/Source/ResearchQueue.cs`:加 `GameComponentTick`、`PopFinishedHeads`、
  `PeekHead`;删 `NextStartable`、`JumpToFront`。
- `1.6/Source/ResearchQueueController.cs`:重写推进与插队逻辑(tick 推进 +
  防刷屏状态);`JumpToFrontAndStart` 与 `JumpChainToFrontAndStart` 合并为单一
  链式插队入口(可立即开始的项目其链就是自身,`JumpChainToFront` 天然涵盖),
  `DrawStartButtonPatch` 的 Ctrl 分支统一调用它。
- `1.6/Source/Patches/FinishProjectPatch.cs`:删除文件。
- `1.6/Source/Patches/SetCurrentProjectPatch.cs`:新增,同步队首。
- `1.6/Source/Patches/DrawContentSourcePatch.cs`:改为单段队列绘制。
- `1.6/Source/Patches/DrawStartButtonPatch.cs`:`TryStartNext` 调用换成新的推进入口。
- `1.6/Source/Patches/StopProjectPatch.cs`:不变。

## 不做的事(YAGNI)

- 不碰 `1.4/`、`1.5/` 目录(冻结的历史版本)。
- 不做队列拖拽排序、右键菜单等额外 UI。
- 不改动递归入队的现有语义。
- 不恢复 1.5"队首卡死清空队列"的行为(用户已批准保留等待)。
