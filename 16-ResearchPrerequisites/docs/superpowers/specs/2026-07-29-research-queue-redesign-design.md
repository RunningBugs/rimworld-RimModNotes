# Research Prerequisites 重构 + 研究队列增强 — 设计文档

日期:2026-07-29
目标版本:RimWorld 1.6。**只改 `1.6/` 目录;`1.4/`、`1.5/` 是有意冻结的历史版本
(代码相同、各放一份以免老版本出错),保持原样,一律不动。**

## 背景与问题

当前 Mod(1.6/Source/Main.cs)用 Harmony Prefix **整段替换**了原版
`MainTabWindow_Research.DrawProjectScrollView` 和 `DrawStartButton`(`return false`
阻止原方法执行),并复制了大段原版绘制代码、用反射访问私有成员。问题:

1. Prefix 整段替换会拦截/冲突其他 Mod 对同一方法的 patch,等同于"写新类 + XML 替换"
   的兼容性后果。
2. `GetNextResearchCanStart` 在队首 `!CanStartNow` 时 `researchQueue.Clear()`,
   一个前置卡住会丢整条队列。
3. 队列通过 `GameComponentTick` 轮询补位,语义粗糙。
4. 异象(Anomaly)研究完全被排除在队列功能外。

注:`1.4/`、`1.5/`、`1.6/` 三份代码相同是有意为之(冻结历史版本,避免老版本
出错),不是需要清理的问题。异象研究页是 1.5 才加入的原版功能,因此历史上
1.4 版本必须不报错、1.5 起自动启用异象相关功能——本次重构在 1.6 代码内继续
保持这一防护(所有异象路径都以 `ModsConfig.AnomalyActive` /
`knowledgeCategory == null` 等条件守卫,未激活 Anomaly DLC 时行为与旧版一致)。

## 关键原版机制

- `ResearchManager` 有三条并行研究槽位:
  - `currentProj`:普通研究一条;
  - `currentAnomalyKnowledgeProjects`:异象研究按 `KnowledgeCategory`(Basic /
    Advanced)各一条,可同时进行。
- 异象页与普通页共用 `DrawStartButton` / `DrawProjectScrollView` /
  `DrawContentSource` 绘制路径。
- `DrawContentSource(Rect, ResearchProjectDef)` 是 `DrawProjectScrollView` 滚动区内
  最后一个绘制调用,返回高度并累加到布局 y。
- `ResearchManager.FinishProject` 是研究自然完成的统一入口。

## 目标设计

### 数据层:三条队列

`ResearchQueue`(GameComponent)持有三条独立列表,均随存档序列化:

- 普通队列 × 1
- 异象 Basic 队列 × 1
- 异象 Advanced 队列 × 1

按项目/当前页签的 `knowledgeCategory` 路由:`null` → 普通队列;
`KnowledgeCategoryDefOf.Basic` / `Advanced` → 对应异象队列。

旧存档兼容:`ExposeData` 继续读取原有 `researchQueue` 字段作为普通队列;
异象两条队列缺失时初始化为空列表。

### 入队规则

- **加入队尾**:任何未完成、未隐藏的项目。前置未完成时,递归先将未完成前置
  (含 hiddenPrerequisites)入队,再入队该项目,最后保序去重(`Distinct`)。
- **Ctrl + 点击 = 插队到队首**:仅当项目 `CanStartNow`(前置、techprint、机械师、
  实体分析等条件全部满足)时可用;插入到对应队列 index 0。
  不满足条件时不提供插队(按钮显示普通"加入队列")。
- 插队到队首并**立即开始研究**:若槽位正被占用,被挤下的当前研究保留进度
  (原版按项目保存)并放回队列第 1 位,插队项目完成后自动继续。

### 出队 / 补位规则

- 研究**自然完成** → `ResearchManager.FinishProject` postfix:从该槽位对应队列的
  队首起,找第一个 `CanStartNow` 的项目并开始(`AttemptBeginResearch`,保留
  Ideology meme 缺失确认弹窗逻辑)。队首阻塞则跳过看下一个;全部不可开始则停,
  **队列保留,不再清空**。
- 手动**停止研究** → 清空**该槽位对应的**队列(保留"停止 = 全部取消"语义),
  不自动开始下一项。异象页停止 Basic 项目只清 Basic 队列。
- 面板"清空队列"按钮清当前页签对应的队列,并同时停止该槽位的当前研究
  (恢复旧版"清空 = 全部取消"的顺手行为)。

### 表现层(全部最小侵入 patch,不整段替换任何原版方法)

1. **`MainTabWindow_Research.DrawStartButton` postfix**
   - 原版"研究/停止/锁定"按钮完全保留。
   - 在其旁(prefix 微调 rect 或在右侧空余处)追加队列按钮:
     - 无修饰键:"加入队列"(追加到队尾);
     - 按住 Ctrl 且 `CanStartNow`:"插队到队首"。
   - 移除原 Shift 模式切换逻辑及 `ResearchQueueController.mode` 状态机,
     修饰键状态直接在读 Event/Input 得到,不再需要 `PostClose` patch 重置。
2. **`MainTabWindow_Research.DrawContentSource` postfix**
   - 在滚动区内容末尾(`rect.y + __result` 处)绘制当前页签对应队列的清单
     (含清空按钮),并把队列区高度累加进 `__result`。
   - 普通页与异象页自动同时生效;与其他 patch 此处的 Mod 通过 `__result`
     累加天然兼容。
3. **`ResearchManager.FinishProject` postfix**
   - 触发对应队列的自动补位。
   - 删除 `GameComponentTick` 轮询。
4. `ResearchQueueController.AttemptBeginResearch` 保留现有自包含实现(meme 确认
   弹窗等,全部基于公开 API):原版对应方法是研究窗口的私有实例方法,而
   `FinishProject` 触发自动补位时窗口可能并未打开,无法复用。

### 兼容性目标

- 不再 `return false` 拦截任何原版方法;所有 patch 均为 postfix(或仅微调参数的
  prefix),其他 Mod 可共存。
- 不再整段复制原版绘制代码,也不再反射读写 `leftScrollViewHeight` /
  `leftScrollPosition` / `lockedReasons`;仅通过缓存的
  `AccessTools.FieldRef` 读取 `selectedProject` 一个私有字段。
- 注意:`ResearchManager.FinishProject` 已被本 Mod 的 `RPModSettings.cs`
  (完成时发 Letter 功能)patch 过,新增的自动补位 postfix 与其共存,互不影响。

## 不做的事(YAGNI)

- 不碰 `1.4/`、`1.5/` 目录(冻结的历史版本,保持原样)。
- 不做队列拖拽排序、右键菜单等额外 UI。
- 不改动研究项目递归入队的现有语义。
