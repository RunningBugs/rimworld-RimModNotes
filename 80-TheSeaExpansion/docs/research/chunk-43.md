# chunk-43 调研发现（Verse.AI：JobGiver / MentalState / ThinkNode / 寻路基础）

本片覆盖 Verse.AI 命名空间的核心 AI 基础设施：游荡类 JobGiver、全部精神崩溃/精神状态的 Worker 与状态类、ThinkNode 条件/优先级框架、Toil/Toils 工具库、PathGrid/Pathing/PathFollower 寻路栈、ReservationManager。绝大部分与海洋无关，但**寻路栈里藏着 Odyssey 的游泳系统入口**，是本片最大收获。

## TL;DR

1. **水面移动成本已有官方钩子**：`Pawn_PathFollower.GetPawnCellBaseCostOverride`——地形 `IsWater` 且 pawn 有 `WaterCellCost` 时整体替换寻路基础成本（Odyssey 游泳）。水生 pawn 走水域地形可以又快又便宜，非水生 pawn 走不动，这正是"海底只刷水生生物"在寻路层的现成实现。（Verse.AI/Pawn_PathFollower.cs:737-744）
2. **游泳后 7500 tick 免疫"全身湿透"心情惩罚**：`Pawn_MindState.lastSwamTick` + `SwimSoakingWetImmuneDuration = 7500`，在 `CanGainGainThoughtNow` 里拦截 `ThoughtDefOf.SoakingWet`。海底殖民地里 pawn 永远在"水"中，湿透思想的刷屏/免疫逻辑是必须处理的冲突点。（Verse.AI/Pawn_MindState.cs:151,185,583）
3. **PathGrid 体系按 Def 可扩展**：`Pathing` 从 `DefDatabase<PathGridDef>` 构建多个 PathingContext（Normal/FenceBlocked/Flying），`PathGridDef.workerType` 可挂自定义 PathGrid 子类，pawn 通过 `pawn.GetPathContext()` 选用自己的寻路上下文。Mod 可以加一个"Swimming/水生" PathGridDef 做独立通行规则，无需 patch 核心寻路。（Verse.AI/Pathing.cs:45-92）
4. **PathGrid 成本公式**：地形 passability + pathCost + 建筑 pathCost + **积雪/（Odyssey）积沙移动附加**（`WeatherBuildupUtility.MovementTicksAddOn`），火焰邻格加罚。类比：海底"淤泥/沉积物"若做成 buildup 类网格（snowGrid/sandGrid 同款），即可免费获得减速+随时间堆积的行为。（Verse.AI/PathGrid.cs:120-206）
5. **"室外致死"对精神崩溃的冲突已有先例**：`MentalBreakWorker_RunWild.BreakCanOccur` 硬编码检查 ToxicFallout / NoxiousHaze / `mapTemperature.SeasonAcceptableFor`，环境不宜居时禁止"跑野"。海底海水致死=同类问题：需要在海水条件下同样屏蔽 RunWild（以及任何把 pawn 送出水密区的崩溃），但该检查是硬编码 def 引用，**只能 Harmony patch，不能 XML 扩展**。（Verse.AI/MentalBreakWorker_RunWild.cs:7-22）
6. **精神崩溃可按地图层白名单过滤**：`MentalBreakWorker.BreakCanOccur` 检查 `def.layerWhitelist` 是否包含 `pawn.MapHeld.Tile.LayerDef`（Odyssey 地图层概念）。若海底做成独立 layer，可用纯 Def 方式禁用不合海底语境的崩溃类型。（Verse.AI/MentalBreakWorker.cs:39-42）
7. **恶劣天气驱赶圈养动物找屋顶的现成机制**：`JobGiver_WanderInRoofedCellsInPen` 扫描 `GameCondition.def.pennedAnimalsSeekShelter`，为圈养动物找棚顶格。类比：海底可直接复用该字段做"洋流风暴/高压事件驱兽入舱"。（Verse.AI/JobGiver_WanderInRoofedCellsInPen.cs:59-69）
8. **LOS 可按介质阻挡**：`TargetScanFlags.LOSBlockableByGas` 表明攻击目标扫描的视线可以被"气体"这类环境介质阻断。类比：浑浊海水/黑暗深海若要让武器射程打折或视线受阻，有先例可循（次要）。（Verse.AI/TargetScanFlags.cs:17）

## 一、寻路与移动（本片核心收获）

### Pawn_PathFollower.cs — NOTE（游泳成本钩子）
- `GetPawnCellBaseCostOverride(Pawn, IntVec3)`：`if (c.GetTerrain(pawn.Map).IsWater && pawn.WaterCellCost.HasValue) return pawn.WaterCellCost;`（:737-744）。即 pawn 层面有个可空的水格基础成本，Odyssey 游泳系统由此注入；`CostToMoveIntoCell` 里它作为 `baseCostOverride` 传入 `PathGrid.CalculatedCostAt`，**整体替换**地形+建筑的常规成本。
- 同方法还有 `pawn.kindDef.moveSpeedFactorByTerrainTag`（:756-766）：按地形 tag 给特定 pawnKind 加/减速——给水生生物的 kindDef 配 `Water` tag 的加速因子即可做出"鱼在水里游得快"，纯 XML（未验证 XML 字段名，但 kindDef 层面可读）。
- `CostToPayThisTick` 里 `pawn.Flying` 时乘 `RaceProps.flightSpeedFactor`（:819-822）——飞行是另一种"无视地形"的移动方式，PathGrid 里 `def.flying` + `terrainDef.forcePassableByFlyingPawns` 配合。
- 旁证（不在本片、未读透）：`Verse.AI/Job.cs:45` 有 `public bool swimming;` 字段，`JobGiver_ExitMap.cs:57` 有 `ExitMapFlying`。说明游泳是 Job 级状态。

### Pathing.cs — NOTE（可扩展的寻路上下文）
- 构造时遍历 `DefDatabase<PathGridDef>`，凡 `workerType != null` 就 `Activator.CreateInstance(workerType, map, def)` 建 PathGrid（:48-68）→ **Mod 可以定义自己的 PathGridDef + PathGrid 子类**，比如"水生寻路"（水格可走、干地对陆生封锁反过来用）。
- `For(Pawn)` 走 `pawn.GetPathContext(this)`（:85-92），pawn 可声明自己用哪个上下文。

### PathGrid.cs — NOTE（移动成本公式）
- `CalculatedCostAt`（:120-206）：地形不可通行→10000（flying 网格且 `forcePassableByFlyingPawns` 例外）；否则取 `terrainDef.pathCost` 与格上 Thing 的 `pathCost` 最大值；叠加 `WeatherBuildupUtility.MovementTicksAddOn(snowGrid.GetCategory(c))`，Odyssey 激活时再叠加 sandGrid（:162-174）；感知模式下邻格有火焰 +150/+1000。
- 类比结论：原版用"积雪/积沙 buildup 网格"实现"随事件堆积、踩上去减速"。海底的"淤泥沉积""矿渣覆盖"可以照抄这个模式；`WeatherBuildupUtility` 在别的分片应重点查。

### RegionCostCalculator.cs / RegionCostCalculatorWrapper.cs / PawnPath.cs / PawnPathUtility.cs / PathingContext.cs / TouchPathEndModeUtility.cs — PASS
纯 A*/区域图/路径池/触达判定数学，无环境语义。RegionCostCalculator 只依赖 `Region.Allows(traverseParms)` 和 pathGrid 中位数成本，海洋化不需要动它。

## 二、海洋环境下的 AI 行为冲突与借用

### Pawn_MindState.cs — NOTE（湿透思想免疫）
- 字段 `lastSwamTick`（:151）、常量 `SwimSoakingWetImmuneDuration = 7500`（:185）。
- `CanGainGainThoughtNow`（:577-588）：`thought == ThoughtDefOf.SoakingWet && TicksGame - lastSwamTick < 7500` 时**不给**湿透思想。即官方设定"刚游过泳的人不在乎湿"。
- 冲突：海底 pawn 长期暴露海水会反复刷 SoakingWet；要么给海底 pawn 持续更新 lastSwamTick，要么给海水环境换专用思想。`WeatherThoughInterval = 120`（:183）也在此文件（天气思想检查间隔，名字是原文拼写）。

### MentalBreakWorker_RunWild.cs — NOTE（"跑野"的环境门控，硬编码）
- `BreakCanOccur`（:7-22）： ToxicFallout、NoxiousHaze（Biotech）激活时不跑野；`mapTemperature.SeasonAcceptableFor(pawn.def, 7f)` 不过也不跑。这就是"室外致命→禁止把 pawn 扔进野外"的官方实现位置。
- 海洋类比：**冲突类**——海水致死环境下 RunWild 会把殖民者变成 WildMan 放进海里等死。检查是硬编码 def 引用，无法 XML 扩展，需要 Harmony 前置补丁注入"海水条件"。
- 同文件 `TryStart` 里 `pawn.ChangeKind(PawnKindDefOf.WildMan)` + `SetFaction(null)`，跑野的完整流程。

### MentalBreakWorker.cs — NOTE（layerWhitelist 地图层过滤）
- `BreakCanOccur`（:39-42）：`def.layerWhitelist` 非空时要求 `pawn.MapHeld.Tile.LayerDef` 在白名单内。Odyssey 引入的 Tile.LayerDef 体系在 AI 层的落点之一。海底若注册为独立 layer，MentalBreakDef 层面即可做"只允许/禁止某类崩溃"，纯 Def。
- 另注：`commonalityFactorPerPopulationCurve`（人口越多某些崩溃越常见）与基因 `AggroMentalBreakSelectionChanceFactor` 也在此，与海洋无关但说明崩溃率可 Def 驱动。

### JobGiver_WanderInRoofedCellsInPen.cs — NOTE（天气驱兽入棚）
- `ShouldSeekRoofedCells`（:59-69）：遍历 `GameConditionManager.ActiveConditions`，任一 condition 的 `def.pennedAnimalsSeekShelter == true` 即触发圈养动物寻找棚顶格游荡。GameConditionDef 的一个 bool 字段就能接入整套"躲恶劣天气"AI，海底做"强洋流/剧毒上涌"事件时直接置位即可借用。

### MentalStateWorker_Roaming.cs / MentalState_Roaming.cs / JobGiver_WanderMapEdge.cs / JobGiver_ExitMap*  — PASS（带一句提醒）
漫游离场要求 `pawn.CanReachMapEdge()`，出口点由 RCellFinder 在地图边缘选取。海底图边缘=开阔海水，只要水生生物的 PathGrid 允许走到边缘，离场逻辑天然成立；陆生 pawn 在海底图的"逃跑离场"语义需要设计（未验证 RCellFinder 选点细节，属别的分片）。

## 三、确认无关的大家族（PASS/SKIP 概览）

- **精神状态类全家**（MentalState_* 约 40 个文件）：狂暴/暴食/侮辱/发脾气/杀俘虏/挖尸/茧怒/机械族/婴儿哭等，全是社交与战斗目标选择逻辑，与海洋零交集。
- **MentalStateHandler / MentalStateUtility / MentalFitGenerator / MentalBreaker**：崩溃调度框架本身无环境语义（除上文 layerWhitelist/RunWild 两处）。
- **ThinkNode 全家**（约 45 个文件）：条件节点（能力/职务/精神状态/能量/机械族模式）、优先级排序、子树插入（`ThinkNode_SubtreesByTag` 按 insertTag 插树——Mod 向原版思维树注入节点的标准口子，虽与海洋无直接语义，但做"海底专用 JobGiver 注入"时会用到）、ChancePerHour 系列。`ThinkNode_ChancePerHour_Forage` 用 `Map.Biome.forgeability`，海底生物群系的 forageability 为 0 时动物不自主觅食（顺手结论，不影响 STATUS）。
- **Toil/Toils_***：Goto/Haul/Reserve/Recipe/Jump/效果器/音效等纯作业原语，无环境语义。
- **ReservationManager / ReservationUtility / JobQueue / QueuedJob / Pawn_JobTracker / Pawn_Thinker / PawnDuty**：预定与作业调度框架，无环境语义。
- **Verb_CastTargetEffect 系列**：精神错乱枪/生物质变异枪的目标校验，无关。
- **JobTag / LocomotionUrgency / PathEndMode / TargetIndex / ToilCompleteMode / ThinkTreeDutyHook / JobIssueParams**：枚举/小结构体，SKIP。
