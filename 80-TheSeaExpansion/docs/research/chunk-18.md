# chunk-18 调研发现（RimWorld 组：Pawn* 与 PlaceWorker* 家族）

本片 200 个文件，两大块：`Pawn*`（pawn 追踪器/生成/关系/袭击到达方式，约 150 个）与 `PlaceWorker*`（放置限制，45 个），外加 Pit 系列（坑洞/口袋地图事件）。多数是与海洋无关的通用 pawn 管理代码，但袭击到达方式和 Pit 家族里挖出了本片最有价值的东西。

## TL;DR

- **原版已有"从水中钻出"袭击到达方式**：`PawnsArrivalModeWorker_EmergeFromWater.cs` —— 敌人直接从大片水体（河/海，>50 格连通水域）中现身，带眩晕、水雾特效和音效。海洋 Mod 的水生袭击可直接复用该 worker 类或整个 `EmergeFromWater` Def。
- **袭击到达方式自带生物群系/星球图层/科技三重过滤**：`PawnsArrivalModeDef.cs` 有 `biomeWhitelist/biomeBlacklist/layerWhitelist/layerBlacklist/minTechLevel`；`PawnsArrivalModeWorker.CanUseOnMap/CanUseOnTile` 还支持 biome 与 PlanetLayer 的 `onlyAllowWhitelistedArrivalModes` 白名单锁定 —— "海洋格只刷工业以上袭击、只允许指定到达方式"纯 XML 即可实现。
- **黑暗袭击已有现成模板**：`PawnsArrivalModeWorker_EdgeWalkInDarkness.cs` 用 `map.glowGrid.PsychGlowAt(x) == PsychGlow.Dark` 选刷怪点 —— 深海低光照环境直接命中该机制。
- **PitGate/PitBurrow = "海沟深渊之门"的完整模板**：口袋地图（MapPortal→pocketMap）+ MTB 计时喷发事件 + 怪物从坑中跳出（PawnFlyer 眩晕飞行）+ 定时坍塌杀死口袋地图内所有 pawn（`PitGate.cs`）。整套可换皮为海底热泉口/海沟巢穴。
- **基因系统已有水栖钩子**：`Pawn_GeneTracker.cs` 的 `WaterCellCost`（GeneDef.waterCellCost，水中移动消耗修正，取最小值）和 `AffectedByDarkness`（GeneDef.ignoreDarkness）—— 做"水栖改造基因"不需要新机制，Def 级即可。
- **Odyssey 的真空/飞行互斥模式可类比**：`Pawn_FlightTracker.cs` 中 `RaceProps.canFlyInVacuum` + `BiomeDef.inVacuum` 禁飞 —— 海洋可同构做"水下不能飞/必须会游"；且飞行 pawn 落水（降落点不可行走且 downed）直接处死，是"落水即死"语义的现成先例。
- **Substructure（gravship 地基）系列 PlaceWorker** 展示"建筑必须坐落在特殊地基地形上"的完整放置校验链（`terrainGrid.FoundationAt` + `TerrainDef.IsSubstructure`），可类比"海底设施必须建在耐压地基上"。注意 `PlaceWorker_OnSubstructure` 已标 `[Obsolete("Use Substructure affordance instead.")]`，新代码应走 Substructure affordance。
- **普通电缆放置逻辑不含防水规则**：`PlaceWorker_Conduit.cs:8-26` 只阻止电缆与其它输电建筑重叠，没有检查 `IsWater` 或 terrain affordance；源码中也没有 `WaterproofConduit` C# 类。防水电缆若存在，应在 XML Def/affordance 侧核对，当前**未验证**。
- **被救者加入判定受环境温度影响**：`Pawn_GuestTracker.cs:617` —— 若 `SafeTemperatureRange` 不覆盖室外温度（或有毒尘/毒霾），救援加入概率从 0.5 升到 1.0 的反向逻辑（环境恶劣反而不易触发 SetFaction 加入分支）。海底恒温 4°C 会让多数人类 pawn 的 SafeTemperatureRange 不覆盖 → 属于需要评估的**冲突点**。

## 一、袭击到达方式（PawnsArrivalMode*）——本片最大富矿

### EmergeFromWater：原版水生袭击（直接相关，最强发现）
`PawnsArrivalModeWorker_EmergeFromWater.cs`（100 行，全读）：
- `CanUseWith`：要求地图 Tile 是 SurfaceTile 且**有河流或沿海**（`casted.Rivers.NullOrEmpty()` 或 `map.TileInfo.IsCoastal`）。
- `TryResolveRaidSpawnCenter`：随机找 `(terrain.IsRiver || terrain.IsOcean) && Walkable && CanReachColony` 的格，以它为种子 flood-fill 连通水域（`terrain.IsWater`，半径 30），**连通水体 >50 格**才算有效（`LargeBodyOfWaterThreshold = 50`）。
- `Arrive`：每个 pawn 从水体集合随机取格（不重复），先 StunFor 2~4 秒，再用 `map.deferredSpawner`（SpawnRequest）分批入场：初始延迟 40 tick，总时长 8 秒；体型 >1.5 的大个体走单独的慢速 SpawnRequest（间隔 ×2.7）。特效链：`EffecterDefOf.WaterMist`（预置 -40 tick）→ `PawnEmergeFromWater`/`PawnEmergeFromWaterLarge` + `SoundDefOf.EmergeFromWater`。
- 海洋 Mod 结论：全海洋地图上"整图皆水"，此 worker 的 >50 格水域条件天然满足；`CanReachColony` 保证怪刷在能游到殖民地的连通水域。水栖袭击几乎零代码可用；若要"从深海浮上"的演出，改 preSpawnEffect/spawnSound 即可。

### 到达方式过滤框架（"只刷工业以上袭击"的实现点）
`PawnsArrivalModeDef.cs`（全读）：`minTechLevel`、`biomeWhitelist/Blacklist`、`layerWhitelist/Blacklist`、`walkIn`、`selectionWeightCurve`、`pointsFactorCurve`、`selectionWeightCurvesPerFaction`。
`PawnsArrivalModeWorker.cs`（全读）：
- `CanUseWith`：`faction.def.techLevel < def.minTechLevel` 直接否决该到达方式；派系级 `arrivalModeWhitelist/Blacklist` 也可控。
- `CanUseOnMap`：biome 黑白名单 + `BiomeDef.onlyAllowWhitelistedArrivalModes`（海洋 biome 设为 true 即可锁死到达方式白名单）；`walkIn` 模式要求 `map.CanEverExit`。
- `CanUseOnTile`：PlanetLayer 黑白名单 + 图层级 `onlyAllowWhitelistedArrivalModes`（若海洋实现为独立星球图层，可在图层维度锁）。
- 注意：`minTechLevel` 是"该到达方式要求的最低派系科技"（如下载舱空投需要科技派系），**不是**"该地图只接受高科技派系袭击"。后者要在袭击事件/派系选择层做，不在本片。

### EdgeWalkInDarkness：黑暗刷怪（深海低光照直接命中）
`PawnsArrivalModeWorker_EdgeWalkInDarkness.cs`（全读）：刷怪点优先选 `map.glowGrid.PsychGlowAt(x) == PsychGlow.Dark` 的边缘可进入格，找不到则退回普通入口格。深海图如果全域 PsychGlow=Dark，该到达方式会自然"从黑暗中走来"。DefOf 里它标了 `[MayRequireAnomaly]`（`PawnsArrivalModeDefOf.cs`），原版绑定 Anomaly 内容，Mod 需注意 DLC 门控。

### 其余到达方式（均读过，PASS）
CenterDrop/EdgeDrop/ClusterDrop/RandomDrop/SpecificLocationDrop/EdgeDropGroups 是空投舱系（`DropPodUtility.DropThingsNear` / `DropCellFinder` 选点，`canRoofPunch` 与派系敌对性挂钩）；EdgeWalkIn/Groups/Distributed/DistributedGroups/HateChanters 是边缘走入系（`RCellFinder.TryFindRandomPawnEntryCell`）。`PawnsArrivalModeWorkerUtility` 提供分组（1~3 组）与落点工具。海洋语境下的注意点：空投舱系在海底语义上需要换成"潜航器/深水炸弹舱"，否则穿帮；EdgeWalkIn 有 `(map.TileInfo.AllowRoofedEdgeWalkIn || !map.roofGrid.Roofed(p))` 的屋顶检查，海底图若全图覆"海水顶"会影响走入刷怪 —— 需在地图生成时保证边缘可走。

## 二、Pit 家族：坑洞 + 口袋地图事件（"海沟巢穴"模板）

- `PitGate.cs`（351 行全读）：`MapPortal` 子类，通向 pocketMap（UndercaveMapComponent）。开启后 450 tick 才允许进入；事件系统：MTB 2 天触发一次"EmergenceWarning"威胁信，再延迟 1800~3600 tick 后 `FireRandomIncidentByWeight` 从 `PitGateIncidentDef` 按 `baseChance` 抽事件；事件间冷却 12 天。坍塌两阶段演出（特效+屏幕震动+音效），`Collapse()` 时对口袋地图内**所有 pawn 造成 Crush 99999 伤害并 Kill**，然后 `PocketMapUtility.DestroyPocketMap`。事件点数用 `StorytellerUtility.DefaultThreatPointsNow` × `pointsMultiplier`。
- `PitGateIncidentDef.cs`/`PitGateIncidentWorker.cs`：Def+Worker 模式，`usesThreatPoints`、`disableEnteringTicks`（事件进行中禁止进入）。
- `PitGateIncidentWorker_Fleshbeast.cs`（全读）：按点数生成 fleshbeast，在 PitGate 占地内先 Spawn，再用 `PawnFlyer.MakeFlyer(ThingDefOf.PawnFlyer_Stun, ...)` 做出"从坑里跳出来"的眩晕飞行演出，挂 `LordJob_FleshbeastAssault`。
- `PitBurrow.cs`（全读）：`Crater` 子类，自然出现的兽穴版；12~20 天后自行坍塌填平；同样有"跳出"演出队列（`FlushSpawnQueue`）。
- 海洋类比：整套 = "海底热泉口/海沟深渊"：地面出现一个通往口袋地图（洞穴→深海裂缝）的入口，周期性涌出深海怪物，长时间后坍塌。复用点：MapPortal/pocketMap 框架、MTB 事件驱动、PawnFlyer 眩晕出场演出、坍塌清场逻辑。
- `PawnGroundSpawner.cs`（全读）：通用的"延迟从地下钻出"生成器（GroundSpawner 基类 + 尘土/污迹 MTB 演出），也是同类出场方式的可复用件。

## 三、环境交互的 pawn 级钩子

- `Pawn_FlightTracker.cs`（全读）：`CanEverFly` = 非禁飞突变体 &&（`RaceProps.canFlyInVacuum` || 当前 biome 非 `inVacuum`）&& `StatDefOf.MaxFlightTime > 0`。**BiomeDef.inVacuum 禁飞**是"环境禁用移动方式"的现成范式 → 海洋可做 `inDeepWater` 同构字段或直接用 biome 钩子；`FlightTick` 落地分支：downed 且落点 `!WalkableBy` → 直接 `Kill` 并销毁尸体 —— "落水即死"已有先例语义。起飞/降落各 50 tick 插值动画。
- `PawnFootprintMaker.cs`（全读）：`TerrainDef.takeSplashes` → 走过时 `FleckMaker.WaterSplash`（大小随 BodySize 开方）；`takeFootprints` + 雪深 ≥0.4 → 脚印。海洋水地形设 `takeSplashes=true` 即获得涉水水花；海底软泥可考虑自定义 fleck。
- `Pawn_MeleeVerbs.cs` + `Pawn_MeleeVerbs_TerrainSource.cs`（全读）：站在某地形上的 pawn 获得 `TerrainDef.tools` 定义的临时近战 verb（4% 选用率、1200 tick 冷却、换地形即失效）。海洋可在水域地形上挂"水流冲击/尾击"类地形攻击，纯 Def 可配。
- `PawnRecentMemory.cs`（全读）：`lastLightTick` 由 `PsychGlowAt != Dark` 刷新（驱动"在黑暗中太久"类思想/需求）；`lastOutdoorTick` 由 `Room.PsychologicallyOutdoors` 刷新。深海低光照会让"黑暗中"类思想常驻 —— 设计情绪系统时要知道这两个计时器的判定源。
- `Pawn_GeneTracker.cs`（关键点读：245-263、445-468）：`WaterCellCost`（遍历激活基因取 `GeneDef.waterCellCost` 最小值，带缓存）——水栖基因减水移耗的现成字段；`AffectedByDarkness`（`GeneDef.ignoreDarkness`）。水栖/深潜基因可直接走这两个 Def 字段。
- `Pawn_GuestTracker.cs:606-631`（读）：被救起的无派系 pawn，若环境舒适（SafeTemperatureRange 覆盖室外温且无毒尘/毒霾）有 50% 概率直接加入，环境恶劣则 100% 走另一随机分支。海底 4°C 恒温下人类 SafeTemperatureRange（约 0~32°C？具体值未在本片验证）大概率不覆盖 → **冲突点**，需测试救援加入率在海底的偏移。
- `Pawn_FilthTracker.cs`（全读）：`TerrainDef.generatedFilth` 让 pawn 走过地形带上污垢（如泥），进屋后按概率掉落；`GameConditionDef.spreadsFilth` 让天气/事件在室外 pawn 身上积污。海洋类比：海底软泥地形 → 带泥入室（直接可用）；"赤潮/浮渣"事件 → spreadsFilth。
- `Pawn_SurroundingsTracker.cs`（全读）：意识形态"树木渴望"的目击计数模板：2500 tick 间隔扫描、每类最多 15 条 TreeSighting、按 TreeCategory 分桶。若做"深海幽闭/海洋亲近"类情境思想，这是采集端模板。

## 四、PlaceWorker 家族（放置限制）

与海洋有价值的：
- `PlaceWorker_DeepDrill.cs`（全读）：`DeepDrillUtility.GetNextResource(loc, map) == null` 则拒绝 —— 深钻必须压深层资源。海底矿产（锰结核/热泉矿）可直接复用深层资源系统。
- `PlaceWorker_OnSteamGeyser.cs`（全读）：建筑必须放在 SteamGeyser thing 正上方（地热发电机的限制）。海底热泉发电 = 同样的"泉口 thing + 压顶建筑"两段式，零新机制。
- `Placeworker_OnlyUnderThickRoof.cs` / `PlaceWorker_NotUnderRoof.cs` / `PlaceWorker_MustBeIndoors.cs`（全读）：分别是"必须在厚顶下"（`roofDef.isThickRoof`）、"必须无顶"、"必须在不触边的房间内"（`room.TouchesMapEdge`）。海底"耐压舱"建筑若要强制建在承重组顶/完全气密室内，这三个是直接的校验范式。
- Substructure 系列（全读）：`PlaceWorker_InSubstructureFootprint`（必须在 gravship 引擎/扩展器脚印范围内，且不能在自然岩顶下）；`PlaceWorker_OnSubstructure`（**已 Obsolete**，校验 `terrainGrid.FoundationAt(cell).IsSubstructure`）；`PlaceWorker_InvalidOverSubstructure`（禁止压在地基上）；`PlaceWorker_BuildingsValidOverSubstructure`（放地基时检查上方既有建筑合法性）。这是 Odyssey"船体地基"的完整校验链，类比"海底耐压地基"：海底建筑可要求先铺"加固地基"地形。
- `PlaceWorker_Conduit.cs:8-26`（重读）：遍历目标格 Thing，只在不能替换且既有 Thing（或其 `entityDefToBuild`）会输电时拒绝；没有 `IsWater`、affordance 或防水判定。故 C# 层只能确认"电缆不可重叠"，不能确认防水电缆规则；后者需查游戏 XML Def，当前**未验证**。
- 其余 30+ 个（引力引擎范围 `PlaceWorker_InRangeOfGravEngine`、推进器 `GravshipThruster`、半径显示类 `GlowRadius`/`PollutionPump`/`NoiseSource`、邻接限制类 `NeverAdjacent*`、连线显示类 `DrawLinesTo*` 等）均读过，为通用放置辅助，与海洋无独特关联（PASS）。`PlaceWorker_PollutionPump` 本体只是画半径环，真正的抽污逻辑在 `CompProperties_PollutionPump`（本片外）。

## 五、袭击/pawn 生成链（与"只刷工业以上"相关的中性结论）

- `PawnGroupMakerUtility.cs`（读 1-200 行，其余为例示生成与 DebugOutput，海洋关键词零命中）：按 points 预算从派系 `pawnGroupMakers` 选组，`PawnGenOptionValid` 过滤条件为 generateFightersOnly、单次火箭筒、raidStrategy、年龄限制、Bossgroup 占用等 —— **没有 techLevel 过滤**；科技门槛在派系定义层（哪些派系会被叙事者选中），不在组生成层。
- `PawnWeaponGenerator.cs`（全读）：武器按 `kindDef.weaponTags` + `weaponMoney` 预算过滤，无科技检查（科技差异体现在 pawnkind 的 weaponMoney/tags 配置上）。
- `PawnInventoryGenerator.cs`（全读）：成瘾药/战斗药按 `x.techLevel > faction.def.techLevel` 过滤 —— 这是本片唯一"按派系科技过滤物品"的实例。
- `PawnTechHediffsGenerator.cs`（全读）：义体袭击者生成入口：`kindDef.techHediffsMoney/techHediffsMaxAmount/techHediffsChance/techHediffsTags/techHediffsRequired`，按 tag 匹配 `ThingDef.isTechHediff && techHediffsTags`、按 BaseMarketValue 加权选取并安装。海洋 Mod 的"工业以上袭击者带义体"可直接配 pawnkind 的这些字段。
- `PawnGroupKindWorker_Trader.cs`（关键点读）：商队驮兽受 `Find.WorldGrid[tile].PrimaryBiome.IsPackAnimalAllowed(race)` 过滤 —— 海洋 biome 可用此机制禁掉陆生驮兽（甚至原版 BiomeDef 已有此钩子）。
- `PawnsFinder.cs`（grep+扫读）：pawn 枚举工具，含 `Find.CurrentGravship.Pawns` 分支（Odyssey 引力船上的 pawn 也计入全图搜索）——若海洋 Mod 引入"潜艇载具"类移动基地，pawn 枚举要记得挂同样的分支。

## 六、本片无独特海洋价值的主要家族（均已读/扫读登记）

- 27 个 `PawnRelationWorker_*` + `PawnRelationDef/Utility`：亲属关系判定与生成概率（血缘/姻亲/前任/机械师-机械体），纯社会关系数学。
- `PawnTable*`（8 个）：主界面 pawn 表格 UI。
- 各 `Pawn_*Tracker`（需求/技能/工作/毒品/服装/阅读/风格/意识形态/互动/王权/机械师/突变体/爬行加入者等）：通用殖民者状态机，关键词扫描 + 类定义扫读确认无海洋钩子。
- 渲染类（`PawnWoundDrawer`/`PawnScarDrawer`/`PawnFirefoamDrawer`/`PawnOverlayDrawer`/`PawnPortraitIconsDrawer`/`PawnDrawUtility` 等）：overlay/伤口/图标绘制。
- 命名/颜色/姿态等小枚举与工具类。

未验证项：`PawnUtility.cs`（1654 行）与 `PawnGroupMakerUtility.cs` 尾部仅做关键词扫描 + 部分阅读，未逐行读；两者对 Water/Ocean/Vacuum/Terrain/Biome 等关键词零命中，结论可信度高。
