# chunk-21 调研发现（RimWorld 组：RaidStrategy* / RCellFinder / Reading* / Recipe* / Record* / Re* / Reward* / Ritual*）

## TL;DR

1. **袭击策略已有两套现成的"按地图/地格禁用"钩子，可直接用来限制海底袭击**：`RaidStrategyWorker.CanUseWith`（RaidStrategyWorker.cs:66-86）同时检查 `TileMutatorDef.blacklistedRaidStrategies` 和 `RaidStrategyDef.layerWhitelist/layerBlacklist`（按 PlanetLayerDef）。海底图只要挂自定义 TileMutator 或用独立地图层，就能纯 XML 屏蔽不合适的袭击策略。
2. **围城/集结类袭击的选址走 `RCellFinder.FindSiegePositionFrom`，默认要求"可建造地形"（Heavy+Light affordance，周边 35/441 格达标）且默认不许有顶**（RCellFinder.cs:1252-1352）。海床地形若不满足 affordance，围攻会自动找失败或退化——需要决定海底围城的落点语义。
3. **Odyssey 的真空致死已经渗透进通用选格 API**：`RCellFinder.IsGoodDestinationFor` 末尾直接查 `c.VacuumConcernTo(pawn)`（RCellFinder.cs:68），`RestUtility.CanUseBedNow` 查 `sleeper.HarmedByVacuum && GetVacuum >= 0.5f` 拒绝真空格的床（RestUtility.cs:90）。海洋 Mod 的"海水致死"若复用/仿写真空逐格属性，床位、闲逛、仪式目标等大量 AI 检查会自动避开水格。
4. **"屋顶"语义是海底气密性的关键枢纽**：观星（TryFindSkygazeCell 要求 `PsychologicallyOutdoors` + 无顶，RCellFinder.cs:653-671）、天灯仪式（SkyLanterns 要求 `minUnroofedCells` 个无顶格、篝火必须无顶，RitualObligationTargetWorker_SkyLanterns.cs）、仪式按地图层禁用（RitualBehaviorWorker.cs 的 layerWhitelist/layerBlacklist）全都围绕 Roofed/Room/PlanetLayer 判定。海底"室外"如何表征（无顶但充满海水）直接决定这些系统是自动正确还是全部错乱。
5. **仪式奖励会绕过事件系统直接触发动物/虫族事件**：`FarmAnimalsWanderIn`、`Infestation_Jelly`（RitualAttachableOutcomeEffectWorker_FarmAnimalsWanderIn.cs / _InsectJellyTunnels.cs）——"只刷水生生物、只工业以上袭击"的限制必须覆盖这些仪式侧门。
6. **奖励 pawn 以 EdgeWalkIn 从地图边缘走入**（Reward_Pawn.cs），任务物品空投到贸易空投点（Reward_Items.cs）——海底图边缘全是致死海水，walk-in 类到达方式需要拦截（类比 Odyssey 对太空图的处理）。
7. **流浪动物/闲逛 AI 已有"亲水 pawn 只在水边逛"的机制**：`CanWanderToCell` 对 `RaceProps.waterSeeker` 的 pawn 强制目标格 8 格内有水（RCellFinder.cs:468-483），`terrain.avoidWander + IsWater` 组合控制避水——水生生物的行为限制可直接借用。

---

## 一、袭击策略家族（RaidStrategy*）

### RaidStrategyWorker（基类，NOTE）

`RaidStrategyWorker.cs`：

- `CanUseWith`（:52-88）的三层过滤：① `faction.def.disallowedRaidStrategies`；② **遍历 `map.TileInfo.Mutators`，任何 mutator 的 `blacklistedRaidStrategies` 含本 def 即禁用**；③ `def.layerWhitelist/layerBlacklist` 对 `map.Tile.LayerDef`（PlanetLayerDef）白/黑名单。
  - **类比**：海底殖民地若落在挂自定义 TileMutator 的海洋格上，或实现为独立地图层，"只允许工业科技以上/特定类型袭击"可以纯 Def 层配置，不用 patch 代码。这是最干净的入口。
- `SpawnThreats`（:118-140）用 `parms.raidArrivalMode.Worker.Arrive` 投递 pawn；`pawnsCanBringFood` 控制是否带粮。到达方式（边缘走入 vs 空投）在 ArrivalMode 层，不在本文件。

### 围城与集结（NOTE ×3）

- `RaidStrategyWorker_Siege.cs`：`MakeLordJob` 调 `RCellFinder.FindSiegePositionFrom(spawnCenter, map)`（默认 `allowRoofed: false`、`requireBuildableTerrain: true`），围攻弹药点数为 `points * Rand(0.2,0.3)` 下限 60。要求 `faction.def.canSiege`。
- `RaidStrategyWorker_StageThenAttack.cs`：同样用 `FindSiegePositionFrom(..., allowRoofed: true)` 找集结点，找不到退化为 pawn 当前位置；要求 `faction.def.canStageAttacks`。
- `RaidStrategyWorker_PsychicRitualSiege.cs`（Anomaly）：`FindSiegePositionFrom(..., allowRoofed: true, requireBuildableTerrain: false)`，自带 Validator 要求仪式中心 6 格内 Walkable 且无可清除建筑。要求 `ModsConfig.AnomalyActive` + `faction.def.canPsychicRitualSiege`。
- **关键结论**：`FindSiegePositionFrom`/`TryFindSiegePosition`（RCellFinder.cs:1290-1352）要求候选格 `SupportsStructureType(Heavy)` 且 `SupportsStructureType(Light)`（围攻要造沙袋和迫击炮），并在 10 格半径内数 35 个达标格；`allowRoofed=false` 时排除有顶格。**海底语境**：海床地形的 TerrainAffordance 若不给 Heavy/Light，原版围攻永远找不到落点（报错后退化为入口格）；海底"室外"若有顶语义混乱同样影响。要么调海床地形 affordance，要么用 mutator 黑名单禁围攻。

### RaidStrategyWorker_SiegeMechanoid（NOTE）

`RaidStrategyWorker_SiegeMechanoid.cs`：不走 pawn 生成，而是 `MechClusterGenerator.GenerateClusterSketch` + `MechClusterUtility.SpawnCluster(spawnCenter, map, sketch, dropInPods: true)`——**机械集群以空投方式落在 spawnCenter**。仅机械族 + Royalty。**类比**：海底机械集群空投 = 空投仓穿水落到海床，是"工业科技以上袭击"在海底最自然的形式；落点 spawnCenter 由 incident 层决定，本文件无水体检查。

### 其余 RaidStrategy 文件（PASS）

- `ImmediateAttack`/`ImmediateAttackFriendly`/`ImmediateAttackSmart`/`ImmediateAttackSappers`/`ImmediateAttackBreaching`/`BreachingSmart`/`ShamblerAssault`：只是组装不同的 `LordJob_AssaultColony` 参数（sappers/breachers/useAvoidGridSmart）或 pawn kind 过滤（`isGoodBreacher`、`canBeSapper`），无地图环境逻辑。
- `RaidStrategyWorker_WithRequiredPawnKinds`：按 pawnGroupMakers 里是否有匹配 pawnkind 决定策略可用性，纯生成层。
- `RaidStrategyDefOf`：DefOf。

## 二、RCellFinder（NOTE，本片最重要文件之一）

`RCellFinder.cs`（1807 行）是全图选格工具库，读完关键方法后的海洋相关结论：

- **`IsGoodDestinationFor`（:37-73）末尾硬编码 `c.VacuumConcernTo(pawn)`** —— Odyssey 真空已被织入"通用好目的地"判定：怕真空的 pawn 的所有散步/集合/仪式选格自动避开真空格。**海水致死若实现为类似逐格属性并接入 `GetDangerFor`/类似的通用判定，绝大多数 AI 自动正确**；否则需要逐点 patch。
- **`TryFindRandomPawnEntryCell`（:525-528）**：袭击/访客入图格 = 地图边缘 + `Standable` + 非危险地形 + `map.TileInfo.AllowRoofedEdgeWalkIn || !Roofed` + `CanReachColony`。**`TileInfo.AllowRoofedEdgeWalkIn` 是"允许从有顶边缘格走入"的地格级开关**（Odyssey 用于有全图屋顶的地图）。海底图边缘若是海水：若水格不可 Standable/Walkable，则自然找不到入口格（袭击会失败或退化）；若可行走但致死，则人类袭击者会走进水里送死——需要 arrival mode 层的拦截。
- **`CanWanderToCell`（:325-485）**：闲逛目的地过滤，含 ① `careAboutSunlight` 时排除 `InSunlight` 格（怕光生物，**深海黑暗环境的反向参照**）；② `terrain.avoidWander && (!IsWater || !waterSeeker)` 避水；③ **`waterSeeker` pawn 强制 8 格内有水**（`terrainGrid.AnyWaterCells` + 随机采样 10 次找水）——水生生物"只在水中/水边活动"的现成行为框架；④ `terrain.dangerous` 排除。**水生生物限制可直接复用 `waterSeeker`/`avoidWander` 体系**。
- **`TryFindSkygazeCell`（:653-671）**：观星要求 `Room.PsychologicallyOutdoors` + 无顶 + 无雾。海底永远看不到天空——观星娱乐/冥想类行为在海底的语义需要处理（全图无"心理室外"？）。
- **`RandomAnimalSpawnCell_MapGen`（:598-651）**：地图生成时动物刷格要求 Standable、非 avoidWander、非 dangerous、District 触图边。海底全图水格若 avoidWander/dangerous，陆生动物无格可刷（配合生物过滤即为"只刷水生"）。
- **`TryFindAllowedUnroofedSpotOutsideColony`（:1621-1678）**：显式找"殖民地外无顶格"（距殖民者建筑 35 格以上）——又一处以 Roofed 表达"室外"的用法。
- **`TryFindNearbyDarkCellFor`（:1757-1780）**：BFS 找 `glowGrid.GroundGlowAt <= 0` 的格（怕光/黑暗需求生物）。**深海低光照下全图 GroundGlow≈0，此机制直接成立**，无需改动。
- 其余（出口格、咀嚼站位、结婚点、机械自毁点等）均为通用 Standable/reachability 逻辑，海水格设为不可走/危险后自动正确。

## 三、奥德赛真空机制的触点（RestUtility，NOTE）

`RestUtility.cs`：

- `CanUseBedNow`（:90）：`if (sleeper.HarmedByVacuum && building_Bed.Position.GetVacuum(bedThing.Map) >= 0.5f) return false;` —— 怕真空 pawn 不能睡真空格的床。**这是"逐格环境致死属性 + 家具可用性挂钩"的最小范例**：海洋版即"床在被淹格不可用"，判定 API（`Pawn.HarmedByVacuum`、`Position.GetVacuum(map)`）若海水复用真空通道则免费获得。
- `FindBedFor`/`FindPatientBedFor`（:285/320/351）：两轮 `ClosestThingReachable`，先 `Danger.None` 后放宽，并用 `b.Position.GetDangerFor(sleeper, map)`。**只要海水格进入 Danger 体系，选床 AI 自动避水**，无需 patch。

## 四、奖励与仪式：海水/屋顶/生物限制的侧门

### 奖励（NOTE ×2）

- `Reward_Pawn.cs:68`：奖励殖民者用 `PawnsArrivalModeDefOf.EdgeWalkIn` 或 `CenterDrop` 抵达，pawn kind 是 `SpaceRefugee`（无防水装备）。**EdgeWalkIn 在海底 = 从致死海水里走进来**，必须拦截（禁用 walk-in 到达方式或强制空投/配装备）。
- `Reward_Items.cs:163-181`：物品奖励挂 `QuestPart_DropPods { useTradeDropSpot = true }`——空投到贸易信标点。**空投仓穿越水层的落点/表现需要设计**（Odyssey 太空图有空投先例）。

### 仪式奖励侧门（NOTE ×4）

- `RitualAttachableOutcomeEffectWorker_FarmAnimalsWanderIn.cs`：仪式奖励直接 `TryExecute` `IncidentDefOf.FarmAnimalsWanderIn`——**陆生农场动物会被仪式直接送进海底殖民地**，"只刷水生生物"必须堵这个口。
- `RitualAttachableOutcomeEffectWorker_InsectJellyTunnels.cs`：直接触发 `IncidentDefOf.Infestation_Jelly`（虫巢）——与"只工业科技以上袭击"冲突，需屏蔽或替换。
- `RitualAttachableOutcomeEffectWorker_GauranlenPosSprout.cs`：示范了"仪式奖励按 biome 排除"模式——`IncidentWorker_GauranlenPodSpawn.IsGoodBiome(map.Biome)` 不通过则奖励不可用。自定义海底 biome 可借此模式自动排除不适用的仪式奖励。
- `RitualAttachableOutcomeEffectWorker_NearbyFactionGoodwill.cs`：用 `Find.WorldObjects.AllSettlementsOnLayer(tile.Layer)` 找同层定居点——海底若为独立 PlanetLayer 且无派系定居点，该奖励静默无效。
- `RitualAttachableOutcomeEffectWorker_RandomRecruit.cs`：生成 WandererJoins 任务（`tile = jobRitual.Map.Tile`），流浪者入图方式同 Reward_Pawn 问题。

### 仪式与屋顶/地图层（NOTE ×4）

- `RitualBehaviorWorker.cs:31-37`：`CanStartRitualNow` 开头检查 `ritual.layerWhitelist/layerBlacklist` 对 `Map.Tile.LayerDef`——**仪式可按行星层禁用**（翻译键 "CantStartRitualLayer"）。海底若做成独立层，天灯等露天仪式可纯 XML 禁用。
- `RitualObligationTargetWorker_SkyLanterns.cs`：**原版最明确的"露天仪式"判定**——目标格 `unroofedCellSearchRadius` 半径内数 `!Roofed && 同房间` 的格子，不足 `minUnroofedCells` 拒绝（"RitualTargetNeedUnroofedCells"）；篝火目标必须自身无顶（"RitualTargetCampfireMustBeUnroofed"）。**海底"室外"若是无顶海水区，天灯仪式会在海里判定可办（荒谬）；若全图视为有顶，则自动被禁（合理）**——屋顶表征决策的直接受影响方。
- `RitualObligationTargetFilterDef.cs`：字段 `minUnroofedCells`/`unroofedCellSearchRadius` 是 Def 层通用的"需要 N 个无顶格"表达，任何仪式 filter 都能挂。
- `RitualObligationTargetFilter.cs`：`mustBeAbleToReachTarget` 的角色用 `pawn.CanReach(target, Touch, pawn.NormalMaxDanger())` 检查仪式目标可达——海水格进 Danger 体系后自动正确。

### 其余仪式文件（NOTE 弱 / PASS）

- `RitualBehaviorWorker_GravshipLaunch.cs`：`PawnCanFillRole` 用 `CanReach(..., Danger.Deadly)`——仪式角色填充内建避险寻路的又一例证。
- `RitualBehaviorWorker_Duel.cs`：决斗场地按 `Standable && 同房间` 格数校验——海底室外海水区不 Standable 则露天决斗自然不可用。
- `RitualOutcomeComp_BuildingsPresent.cs`：聚集区域与 Room 系统耦合（整房间 vs 18 格半径）——理解气密房间如何影响仪式质量计算即可，无需 patch。
- 其余 ~60 个 Ritual* 文件（触发器、目标枚举、结果 comp、行为 worker）均为纯仪式逻辑/数据，与海洋无交集。

## 五、医疗/阅读/记录家族（整体无发现，一处样板）

- **全部 25 个 Recipe_\* 手术 worker**：`AvailableOnNow`/失败判定不含任何环境检查（不查屋顶/温度/真空）——**原版机制层面水下可以做手术**。若想让"手术必须气密环境"，需自行在 Bill/Job 层加判定，Recipe 层无现成钩子。
- **RecordWorker_TimeUnderRoof（NOTE）**：`ShouldMeasureTimeNow` = `pawn.Spawned && pawn.Position.Roofed(pawn.Map)`——"逐 pawn 逐 tick 按格查屋顶"的最小用法样板；做"水下暴露时间"类统计可照抄换成自定义水淹判定。
- Reading*/Record* 其余全部为 UI/统计/DefOf，PASS/SKIP。

## 六、其余工具类（PASS 要点）

- `ResearchManager`/`ResearchPrerequisitesUtility`：纯研究系统；`ResearchProjectDefOf` 里有 `[MayRequireOdyssey] Fishing`——钓鱼研究本身已是 Odyssey 内容，可作海洋 Mod 的研究扩展参考。
- `ResurrectionUtility`：复活在尸体原格 GenSpawn，无环境检查（海底复活边缘情况，不处理）。
- `RevenantUtility`：幽魂找格走通用 Standable/reachability。
- `RelationsUtility`/`RecruitUtility`/`RefuelWorkGiverUtility`/`RepairUtility`/`ResourceCounter`/`RestraintsUtility` 等：纯社交/工作/统计，通用 reachability 生效即可。

## 覆盖统计

共 200 文件：NOTE 22 / PASS 133 / SKIP 45。
