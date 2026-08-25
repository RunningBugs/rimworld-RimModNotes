# chunk-42 调研发现（Verse.AI + Verse.AI.Group）

本片覆盖 AI 基础库（Job/JobDriver/JobGiver/攻击目标搜索/破壁网格）与 Lord 组队 AI（LordJob/LordToil/Trigger/Transition/心灵仪式）。绝大多数是通用机制，与海洋无直接关系；下列是按"类比到海底"筛出的价值点。

## TL;DR

1. **海底 4°C 恒温会触发外来 AI 队伍自动撤离**：`Trigger_PawnExperiencingDangerousTemperatures` 每 197 tick 检查 lord 成员的 Hypothermia/Heatstroke hediff，severity>0.15 即触发（常用于商队/访客撤退）。不防寒的袭击者/访客在海底会快速低体温 → 直接借用做"入侵者被海水逼退"，也要注意别误伤设计为能长期停留的队伍。
2. **破壁 AI 是"打穿气密墙→灌水"剧情的现成底座**：`BreachingGrid` 按墙 HP 计价寻径打穿基地（costBlockedWallBase=20 + 0.015/HP，自然墙+20），并用 Perlin 噪声打散破壁点、回避含休眠 pawn/虫巢的未探明房间（+600 代价）。`LordJob.GetWalkGrid()` 虚方法允许 lord 提供自定义可走格位图（消费方未验证）。
3. **袭击者目标选择 + 砸门 fallback 威胁气密完整性**：`GenAI.RandomRaidDest` 让袭击者优先奔向有人拥有的床 → 非战斗危险建筑 → 殖民者；`JobDriver_AttackMelee.Notify_PatherFailed` 在 `attackDoorIfTargetLost` 时把 6 格内挡路建筑转为攻击目标。海底基地外壳被当作"挡路墙"攻击是默认行为。
4. **离场体系与海底地图设计强耦合**：`JobGiver_ExitMap` 在 `Map.CanEverExit=false` 时只认口袋图 Portal；`JobDriver_ExitMapFlying` 要求无屋顶且 `RaceProps.canLeaveMapFlying`；`Lord.SetJob` 对 `autoFlee` 派系自动追加 `LordToil_PanicFlee` 逃跑支路。海底图若设为不可步行离场，逃跑/撤退/商队离开全链路都要过一遍。
5. **Odyssey 已有游泳渲染管线可直接借用**：`Job.swimming` 字段（默认 false，Clone/Reset 均处理）→ `Pawn.Swimming`（Verse/Pawn.cs:259，读本片 Job）→ 渲染层 `swimmingGraphicData`（半透明 shader）。`JobDriver_GoSwimming`（RimWorld，不在本片）仅在 `TerrainDef.IsWater` 格置位。海底 pawn 水中姿态/半透明显示沿用此机制即可。
6. **心灵仪式框架是干净的自定义仪式扩展点**，但多个 outcome 与海底冲突：`SummonAnimals`→`HerdMigration`/`FrenziedAnimals`（陆生动物群）、`SummonShamblers`、`SummonPitGate`、`SummonFleshbeasts*`（PitBurrow 需要 walkable 且 Player 版要求 `SpawnLocationType.Outdoors`）、`BloodRain`（注册 GameCondition）。海底应屏蔽或替换为水生版本。
7. **AttackTargetFinder 两个可借用 flag**：`NeedNotUnderThickRoof`（厚岩顶下目标不可选——海底"岩层顶"可屏蔽轨道/空投类打击）与 `LOSBlockableByGas`（盲烟挡视线——可做浑浊海水/乌贼墨汁）；玩家派系目标必须不在战争迷雾中。
8. **AvoidGrid**：`building.ai_combatDangerous=true` 的炮塔周围会被袭击者寻径回避（+45 代价，沿视线扩散）。海底 Mod 新增防御建筑要用此 flag 才会被 AI 尊重。
9. **休眠/唤醒信号链**（`Trigger_DormancyWakeup`/`Trigger_OnClamor`/`TransitionAction_WakeAll`/`CompCanBeDormant`）是"沉睡深海巨兽被噪音惊醒"玩法的现成机制。

## 1. 温度/环境对 AI 队伍的影响（冲突+借用）

- `Verse.AI.Group/Trigger_PawnExperiencingDangerousTemperatures.cs`：Tick 信号且 `TicksGame%197==0` 时遍历 lord 成员，检查 `HediffDefOf.Heatstroke`/`HediffDefOf.Hypothermia` severity > 0.15。海底恒定 ~4°C 意味着无防寒装备的外来 humanlike 会持续积累低体温，挂在 lord 状态图上的此 trigger 会让商队/访客/袭击提前撤退。这既是**免费的环境压力机制**（借用），也可能**误伤**希望长期驻留的友方 lord（冲突，需在其 LordJob 中不挂此 trigger 或给水肺装备免疫）。
- `Trigger_PawnExperiencingAnomalousWeather.cs`：同款结构检查 `BloodRage` hediff（Anomaly 血怒天气）。证明"环境 hediff → lord 状态迁移"是官方惯用模式，海底可仿写"水压/缺氧 hediff → 撤退"trigger。
- `Trigger_GameCondition.cs`：每 199 tick 检测 `Map.GameConditionManager` 中指定 GameConditionDef 是否激活。若海底用 GameCondition 实现"深海环境"，AI lord 可原生响应它。

## 2. 破壁体系（强相关）

- `Verse.AI/BreachingGrid.cs`（510 行，完整读）：
  - `CreateBreachPath(start,end,radius,margin)`：`TraverseMode.PassAllDestroyableThings` 寻径，墙体代价 = `tweakWallCost(20)` + `HitPoints * 0.015`，自然墙再 +20；路径膨胀出 breachGrid（作业区）与 walkGrid（活动区）。
  - Perlin 噪声（threshold 0.5）给部分格 +30 代价，打散每次破壁位置。
  - `DangerousRoomCost`：未探明（Fogged）房间内含不活跃 pawn / Hive / 远古休眠舱 → 整房 +600 代价。**类比：可做"袭击者回避已灌水区域/危险水域"的代价注入点**（CustomTuning 是 PathRequest.IPathGridCustomizer，寻径偏移格的标准注入方式）。
  - `FindBuildingToBreach`：从突破口 flood fill，选可抵达侧面最多的值得破的建筑。
- `Verse.AI.Group/LordJob.cs`：虚方法 `GetWalkGrid(Pawn) → NativeBitArray`，lord 可为成员提供自定义可走格（破壁 lord 使用）。海底若要让袭击者绕开海水格，这是最可能的钩子——**消费方未在本片，未验证**。
- `Verse.AI/GenAI.cs RandomRaidDest`：袭击目标优先级 = 有人拥有的床（按床好坏排序）→ 非 `ai_combatDangerous`/非 inert/非 `ai_neverTrashThis` 的建筑邻格 → 可及殖民者 → 任意可及格。海底基地的卧室照常是磁石。
- `Verse.AI/JobDriver_AttackMelee.cs Notify_PatherFailed`：`job.attackDoorIfTargetLost` 时，寻径失败后把路径上 6 格内首个挡路建筑转为攻击目标（maxNumMeleeAttacks 2–5）。即"够不到人就砸墙"是默认行为，气密外墙会被顺手拆。
- `AvoidGrid.cs`：`ai_combatDangerous` 的 `Building_TurretGun` 射程内（且有视线）格 +45 回避代价，并扩散进相邻建筑格；`LordJob_ExitMapBest` 等逃跑 toil 会 `useAvoidGrid=true`。

## 3. 离场/逃跑体系（海底地图需全局评估）

- `Verse.AI/JobGiver_ExitMap.cs`：`!Map.CanEverExit` 时直接放弃（口袋图则找 `ThingRequestGroup.MapPortal` 走 `EnterPortal`）；`canFlyOutOfMap && RaceProps.canLeaveMapFlying && !Position.Roofed && pawn.flight.CanEverFly` → 改用 `ExitMapFlying`。`forceCanDig/forceCanDigIfCantReachMapEdge` 等让撤退者可以挖穿挡路建筑（`DigUtility.PassBlockerJob`）——**海底撤退者挖穿气密墙逃跑 = 灌水事故**，需要剧本层面约束。
- `JobDriver_ExitMapFlying.cs`：随机等 0–10 tick 后生成 `FlyerLeaving` skyfaller 飞走。海底可仿写"上浮离场"skyfaller。
- `Lord.cs SetJob`：`faction.def.autoFlee && !neverFlee && lordJob.AddFleeToil && Map.CanEverExit` 时给**每个 toil** 自动挂 `Trigger_FractionPawnsLost(attackersDownPercentageRangeForAutoFlee)` → `LordToil_PanicFlee`（useAvoidGrid）。`CanEverExit=false` 的海底图不会挂逃跑支路，袭击 AI 行为树会变。
- `JobDriver_Goto.cs`：`exitMapOnArrival` + `exitMapGrid.IsExitCell` → `ExitMap(allowedToJoinOrCreateCaravan)`；Anomaly 下经 `MetalhorrorUtility.TryPawnExitMap` 拦截。离场格由 exitMapGrid 定义（不在本片）。
- 配套 trigger：`Trigger_PawnCanReachMapEdge`/`Trigger_PawnCannotReachMapEdge`（每 193/197 tick 轮询 `CanReachMapEdge`）、`TriggerFilter_MapExitable`（lord.Map.CanEverExit 过滤）、`TransitionAction_EnsureHaveExitDestination`。海底不可步行离场时这些驱动"困住就换策略"的逻辑会常开/常闭，需逐一确认。

## 4. 游泳/水中渲染（直接相关）

- `Verse.AI/Job.cs:45`：`public bool swimming;`（与 `flying` 并列），Reset 置 false（:280）、Clone 保留（:431）。
- `Verse/Pawn.cs:259`（跨片确认）：`Pawn.Swimming => CurJob?.swimming ?? false`，渲染层读它切换 `swimmingGraphicData`（PawnKindDef 生命阶段，强制 Transparent shader）。
- 本片外唯二置位点：`RimWorld/JobDriver_GoSwimming.cs:103`，条件只是 `pawn.Position.GetTerrain(map).IsWater`。**海底 pawn 入水姿态 = 给自己的 JobDriver 在 IsWater 格置 `job.swimming=true` 即可**；也可给水生 PawnKind 配 swimmingGraphicData 获得半透明游泳外观。

## 5. 心灵仪式框架与冲突点

框架（可扩展点，做"唤潮/深渊仪式"可整套复用）：`PsychicRitual`（力量/取消/成员丢失处理）、`PsychicRitualGraph`（顺序 toil 状态机，MaxJumpsPerTick=1000）、`PsychicRitualToil`（基类，Start/Tick/UpdateAllDuties/End）、`PsychicRitualToil_Goto`（JobSyncTracker 双阶段集合同步）、`PsychicRitualToil_Multiplex`（按角色分派子 toil）、`LordToil_PsychicRitual`、`PsychicRitualRoleAssignments`、`PsychicRitualGizmo`（UI 入口，`PsychicRitualDef_InvocationCircle`）、`JobSyncTracker`。

冲突点（outcome 直发 incident/刷怪，海底内容不适宜）：
- `PsychicRitualToil_SummonAnimals`：按品质概率发 `FrenziedAnimals`（失败）或 `HerdMigration`（成功）incident——陆生动物群。
- `PsychicRitualToil_SummonShamblers`：发 `ShamblerSwarm`（延迟 2500–7500 tick）。
- `PsychicRitualToil_SummonPitGate`：发 `PitGate` incident。
- `PsychicRitualToil_SummonFleshbeastsCultist`/`_Player`：`PitBurrow` 需要 walkable 格；Player 版 `BurrowSpawnParms` 明确 `attemptSpawnLocationType = SpawnLocationType.Outdoors`、`attemptNotUnderBuildings`——海底"室外=海水"时能否刷出取决于地形 walkable 定义，未验证。
- `PsychicRitualToil_BloodRain`：注册 `GameConditionDefOf.BloodRain`。

## 6. 攻击目标搜索（借用点）

- `Verse.AI/AttackTargetFinder.cs`（734 行，完整读）：
  - `TargetScanFlags.NeedNotUnderThickRoof`：目标格是 `roof.isThickRoof` 则排除——海底"厚岩层顶/超深水域"可借此免疫特定打击。
  - `LOSBlockableByGas`：盲烟（GasType.BlindSmoke）格挡视线校验（`losValidator`）；且目标搜索时 `CanSee` 走 `GenSight.LineOfSight`+探头格。浑浊海水/墨汁云做气体即可干扰瞄准。
  - 玩家派系目标 1×1 格需非雾、多格需至少一格非雾才被 AI 锁定。
  - `IsAutoTargetable`：`CompCanBeDormant` 未醒 / `CompInitiatable` 未启动 / 机械族睡眠 → 不被自动索敌。休眠深海生物天然隐身于自动索敌。
  - 射击评分：距离(封顶40)、目标正瞄准自己+10、5 秒内打过+40、掩体-、倒地-50、非战斗员-50、友军误伤半径/锥形惩罚。
- `AttackTargetsCache.cs`/`AttackTargetReservationManager.cs`：阵营敌意缓存与近战目标预约（上限=目标周围可站格数），通用，无海洋特异性。

## 7. 休眠/唤醒（借用）

- `Trigger_DormancyWakeup`/`Trigger_DormancyWakeupOrClamor`/`Trigger_OnClamor`（ClamorDef 噪音事件）+ `TransitionAction_WakeAll`（同时唤醒 `CompCanBeDormant` 建筑）。整套"沉睡→被噪音/伤害惊醒→进入战斗"链路，可直接驱动"深海巨兽"遭遇。

## 覆盖说明

- 本片 204 个文件全部处置：NOTE 50 / PASS 128 / SKIP 26（SKIP 为纯数据类/枚举/接口/抽象基类/极 trivial 文件）。
- 大文件读法：BreachingGrid、AttackTargetFinder 全文；Lord、JobDriver、Job、PsychicRitualRoleAssignments 读类定义/字段/核心方法；`PsychicRitualToil_InvokeHorax`（515 行）只完整读了头部 120 行（仪式编排），尾部未逐行读（未验证，但属 Anomaly 仪式表现层）。
- JobDriver 其余部分（JobDriver.cs 260–699 的 toil 执行机）为通用调度代码，未发现海洋相关钩子。
