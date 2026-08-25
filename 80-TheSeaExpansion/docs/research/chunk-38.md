# chunk-38 调研发现（RimWorld.Planet 组：世界地图/商队/据点/行星层）

## TL;DR

- **Odyssey 行星层系统是本片最大发现**（`PlanetLayer.cs`、`PlanetTile.cs`、`PlanetLayerConnection.cs`）：世界地图已支持多层（地表/轨道），`PlanetLayerDef` 上有 `isSpace`、`SurfaceTiles`、`canFormCaravans`、`SettlementWorldObjectDef`、`DestroyedSettlementWorldObjectDef` 等开关，层间用带 `fuelCost` 的连接通行——"海面层/海底层"或"海底专属层"有现成框架可挂。
- **Gravship 把整张地图的环境状态打包带走**（`Gravship.cs`、`RoomTemperatureVacuum.cs`）：起飞时逐房间记录 `temperature` + `vacuum`，逐格记录气体 `gases`、地形、屋顶、区域——"移动基地携带气密环境"的完整实现，可直接类比"潜水母舰携带舱内无水环境"。
- **海洋 feature 已是一等公民**（`FeatureWorker_OuterOcean/Bay/Island/Archipelago/Peninsula.cs`）：世界生成用 `BiomeDefOf.Ocean/Lake` 判定水域并命名洋、湾、岛、半岛、群岛；`GenPlanetMorphology` 的腐蚀/膨胀形态学可用于生成海沟、海山等自定义 feature。
- **定居/进图链路多处硬编码"非水"假设**（`SettleInEmptyTileUtility.cs`、`CaravanEnterMapUtility.cs`、`MultipleCaravansCellFinder.cs`、`CaravanIncidentUtility.cs`）：海洋格默认不可定居、不可生成伏击地图（`PrimaryBiome.implemented` 检查）、进图 spawn 点要求 `Standable` 且可达地图边缘、营地进入点显式排除 `IsWater` 地形——海底殖民地需要逐点解除或绕开。
- **商队通行性由 `Find.World.Impassable` 一票否决**（`Caravan_PathFollower.cs`、`CaravanExitMapUtility.cs`、`CaravanUtility.cs`、`GenWorldClosest.cs`）：商队不能走进不可通行格，离图组队会被重定位到最近可通行格——海底格若不可通行，"潜艇商队"必须自建通行性规则。
- **商队采集（打鱼）完全由 BiomeDef 驱动**（`ForagedFoodPerDayCalculator.cs`、`Caravan_ForageTracker.cs`）：`biome.foragedFood` + `biome.forageability` + 阵营 `forageabilityFactor`——海底"捕鱼/采集"只需配置 biome，无需改代码。

## 1. 行星层系统（Odyssey 多层世界）

- `PlanetLayer.cs`（1381 行，读关键部分）：一个行星层持有自己的 tile 列表、邻接表（NativeArray）、寻路器（`WorldPathing Pather`）、泛洪器（`WorldFloodFiller Filler`）、`FastTileFinder`。层有 `zoomInToLayer`/`zoomOutToLayer`（缩放切换层）、`Connections`（到其它层的连接）、`IsRootSurface` 标记。`RunWorldGeneration` 各层独立生成。
- `PlanetTile.cs`：tile 标识 = `tileId + layerId`；`layerId < 0` 或 `IsRootSurface` 时按地表处理（哈希/相等把根地表归一到 layer 0）。**跨层引用同一地理格靠这个结构**。
- `PlanetLayerConnection.cs`：`origin`/`target`/`fuelCost`——层间旅行有燃料成本（gravship 用）。
- 散落的层行为开关（本片内实证）：
  - `CaravanMaker.cs:16`：`startingTile.LayerDef.canFormCaravans` 为 false 的层不能组商队（仅警告仍执行）。
  - `CaravanArrivalTimeEstimator.cs:72`：`!from.LayerDef.SurfaceTiles` 时 ETA 直接按 0（非地表层不涉及逐格行走）。
  - `Settlement.cs:54`：`Visitable` 排除 `Tile.LayerDef.isSpace` 的层。
  - `ExpandableWorldObjectsUtility.cs:61-68`：`def.fullyExpandedInSpace && LayerDef.isSpace` 的世界对象在太空层始终展开图标，非当前选中层时半透明（0.3）。
  - `SettlementDefeatUtility.cs:21`：据点被摧毁后生成 `Tile.LayerDef.DestroyedSettlementWorldObjectDef`——每种层有自己的"废墟"对象 def。

**海洋类比**：若做"海面↔海底"两层地图，框架（分层、层间连接成本、层级 def 开关、跨层 PlanetTile 相等性）全是现成的；若不分层而是同层海洋格降落，则这些开关用不上，但 `isSpace` 系列的"特殊层行为挂点"模式值得照抄。

## 2. Gravship：打包整张地图（含环境状态）搬迁

- `Gravship.cs`（867 行，读字段与关键方法）：起飞时把引擎范围内的建筑、pawns、地形（`terrains`/`foundations`/`terrainColors`/`terrainDesignations`）、屋顶 `roofs`、**逐格气体 `gases: Dictionary<IntVec3, uint>`**、**逐房间 `roomTemperatures: List<RoomTemperatureVacuum>`**、区域（`MoveableAreas`：储物区/种植区/允许区/畜棚等）全部序列化进一个 WorldObject，落地时按旋转还原。`IIncidentTarget` 实现（`PlayerWealthForStoryteller`、`GameConditionManager`）让飞行中的船可被叙事者当目标。
- `RoomTemperatureVacuum.cs`：纯数据类，`roomCell + temperature + vacuum`——**房间级温度与真空度随船携带的最小记录单元**。
- `MoveableArea.cs`/`MoveableAreas.cs`/`MoveableArea_Allowed.cs`/`MoveableGrowZone.cs`/`MoveableStockpile.cs`/`MoveableStorageGroup.cs`：区域以"相对坐标"存储，落地时按 `gravship.Rotation` 旋转还原（`PrefabUtility.GetAdjustedLocalPosition`）。
- `PositionData.cs`：单个 Thing 的 `position + relativeRotation + drafted` 记录。
- `GravshipLaunch.cs`：起飞点遗留的标记性 WorldObject（只显示起飞时间）。

**海洋类比**：这是"非气密区域致死环境 + 气密建筑隔绝"的【搬家版本】。海底若允许"潜水母舰"玩法（整基地上浮转移），房间海水淹没状态、舱内温度可以同样打包；`gases` 字典证明官方已经在逐格存气体量，海水淹没度可以同构存储。

## 3. 世界生成：海洋 feature 与形态学

- `FeatureWorker_OuterOcean.cs`：从世界边缘的海洋/湖泊格（`BiomeDefOf.Ocean/Lake`）泛洪，命名整片外洋；组大小须在 `def.minSize/maxSize` 内。
- `FeatureWorker_Bay.cs` / `FeatureWorker_Peninsula.cs`：继承 `FeatureWorker_Protrusion`，分别以水域/陆地为 root，用 `GenPlanetMorphology.Open`（先腐蚀后膨胀）消除窄通道后提取"突出部"——湾=水伸入陆地，半岛=陆地伸入海。
- `FeatureWorker_Island.cs`：`FeatureWorker_FloodFill` 派生，root=非海洋/非湖泊，`IsPossiblyAllowed`=Lake（湖中岛也算岛）。
- `FeatureWorker_Archipelago.cs`：`FeatureWorker_Cluster` 派生，把邻近小岛聚成群岛（`GenPlanetMorphology.Close` 先膨胀后腐蚀搭桥）。
- `FeatureWorker_MountainRange.cs`：root=非平地，`CanTraverse` 显式排除 `BiomeDefOf.Ocean`——山脉不跨洋。**同类写法可用于"海沟/海岭"：root=海洋格+某条件，CanTraverse 排除陆地**。
- `FeatureWorker_FloodFill.cs`/`FeatureWorker_Cluster.cs`/`FeatureWorker_Protrusion.cs`/`FeatureWorker_Biome.cs`：上述的通用机制（min/maxSize、possiblyAllowed 吞并、`canTouchWorldEdge`）；`FeatureWorker_Biome` 用 def 配置的 `rootBiomes/acceptableBiomes` 提取 biome 区域——**纯 def 驱动即可给一片自定义"深海 biome"命名**。
- `GenPlanetMorphology.cs`：Erode/Dilate/Open/Close 四个形态度学原语，作用于任意 `PlanetLayer` 的 tile 集合。
- `GameSetupStep_TileElementsCore.cs`/`GameSetupStep_TileElementsOdyssey.cs`/`GameSetupStep_Grids.cs`/`GameSetupStep_Components.cs`：世界生成管线步骤（features/landmarks/grid/components 的初始化顺序），加自定义生成步骤照此注册。

## 4. 定居与进图：需要解除的"非水"硬编码

- `SettleInEmptyTileUtility.cs`：
  - `SettleCommand` 的可用性由 `TileFinder.IsValidTileForNewSettlement`（不在本片，在 TileFinder.cs）裁决——**海洋格默认在这里被拒**，海底降落需 patch 或绕行。
  - `SetupCamp`（临时营地）进图落点要求 `x.GetRoom(map).CellCount >= 600` **且 `!x.GetTerrain(map).IsWater`**——海底全水地图上此条件永远不满足，会回退或失败。
  - `CanCreateMapAt` 检查同格所有 WorldObject 的 `def.canHaveMap`。
- `SettleInExistingMapUtility.cs`：在已有地图上定居走 `map.Parent.CanBeSettled`（`MapParent.cs:25` 虚属性，默认 true，`Settlement.cs:66` 返回 false）——**自定义海底 MapParent 可以用这个钩子控制能否转定居点**。
- `CaravanEnterMapUtility.cs`：商队进图 Edge 模式要求 `Standable` 且能 `CanReachFactionBase`/`CanReachBiggestMapEdgeDistrict`；Center 模式要求 `Standable` 且 `CanReachMapEdge`。海底地图若海水格不可站立，需要保证有可达的可站立区，否则回退 `CellFinder.RandomCell`（可能把人刷进海水）。
- `MultipleCaravansCellFinder.cs`：伏击图两组出生点要求互相 `Standable`+可达+不处于雾中；海底伏击图同样受限。
- `CaravanIncidentUtility.cs`：`CanFireIncidentWhichWantsToGenerateMapAt` 要求 `PrimaryBiome.implemented`——**原版 Ocean biome 若 `implemented=false`，海上商队不会刷伏击**；海底 biome 必须 `implemented=true` 才会有地图事件，反之可利用此 flag 屏蔽。
- `Camp.cs`：临时营地 MapParent，拆图时若 tile 有 Landmark 变 `AbandonedLandmark`，否则变 `AbandonedCamp`（30 天 TimeoutComp）。**"海底临时作业点→遗留废弃点"的直接模板**。
- `Site.cs`（558 行，读签名）：任务站点基类，`SitePart` 组合、`HandlesConditionCausers`、`GravShipCanLandOn`、`CanBeSettled` 可覆写——海底兴趣点（沉船、热泉）走 Site+SitePart 机制。
- `MapParent.cs`（406 行，读签名）：所有可开图世界对象基类，关键钩子：`MapGeneratorDef`、`ExtraGenStepDefs`（追加生成步骤）、`CanBeSettled`、`GravShipCanLandOn`、`Abandon(wasGravshipLaunch)`、`ShouldRemoveMapNow`。

## 5. 商队移动与通行性

- `Caravan_PathFollower.cs`：`Find.World.Impassable(tile)` 是硬门槛（`IsNextTilePassable`、进格时若 Impassable 直接 Log.Error）；每格成本 `CostToMove = TicksPerMove × WorldPathGrid.CalculatedMovementDifficultyAt(目标格) × 道路系数`，钳制 [1, 30000]。
- `CaravanTicksPerMoveUtility.cs`：基础速度由人形 `MoveSpeed` 换算（`CellToTilesConversionRatio=340`），骑乘动物/负载系数修正；不含任何地形判断——地形全在 `WorldPathGrid`（本片外）。
- `CaravanExitMapUtility.cs`：离图组商队时 `GenWorldClosest.TryFindClosestPassableTile` 把出生格强制改到最近可通行格——**海底基地（不可通行格）组商队会被瞬移到陆地，必须处理**。
- `CaravanUtility.cs`：`BestGotoDestNear` 同样过滤 `Find.World.Impassable` + `CanReach`。
- `GenWorldClosest.cs`：泛洪找最近满足条件的格，`canSearchThroughImpassable` 可穿透搜索但落点仍要可通行。
- `FastTileFinder.cs`（653 行，读结构与查询部分）：Burst 编译的全图 tile 查询器，`TileQueryParams` 支持距离环、landmark 模式、`reachable`、`checkBiome`（按 biome shortHash 过滤）、`validSettlement`、hilliness 范围；缓存每格 `passable`、`validForSettlement`、`biomeHash` 等。**任务/事件在海底选址（如"找一片深海格"）用这个，支持按 biome 过滤**。
- `CaravanArrivalTimeEstimator.cs`：ETA 模拟逐格行走+夜间休息（22:00–6:00，`CaravanNightRestUtility.cs`，按经度算当地时区），每格成本复用 `Caravan_PathFollower.CostToMove`。

## 6. 商队生存经济（食物/腐烂/可见性）

- `ForagedFoodPerDayCalculator.cs` + `Caravan_ForageTracker.cs`：采集产出 = 成员 `ForagedNutritionPerDay` 合计 ÷6 × `biome.forageability` × 阵营 `forageabilityFactor`，产出物 = `biome.foragedFood`；静止采集效率 ×2（`NotMovingProgressFactor`）。**海底打鱼 = 给深海 biome 配 foragedFood（鱼）和 forageability 即可，零代码**。
- `DaysUntilRotCalculator.cs`：商队食物腐烂沿路径逐格按 `CompRottable.ApproxTicksUntilRotWhenAtTempOfTile` 估算——**世界格温度直接决定腐烂速度**；海底低温（~4°C）下商队食物几乎不腐，机制原生支持，只需格温正确。
- `DaysWorthOfFoodCalculator.cs`（448 行，签名级）：食物天数 = 库存+采集的加权中位数，调用上面的腐烂估算。
- `CaravanVisibilityCalculator.cs`：商队被发现的可见度 = 体型和查曲线，静止 ×0.3；与光照无关——海底"黑暗隐蔽"若想体现，需自己接这个系数。
- `CaravanNightRestUtility.cs`：夜间强制休息 22:00–6:00（按经度时区）；海底无光但该系统只认世界时钟，无需改动也不会冲突。

## 7. 可借用的杂项模式

- `OrbitalScannerWorldComponent.cs`：世界组件 MTB（60000 tick 均时，18 天冷却）随机让一台工作中的扫描仪 `ReceiveSignal()`——**"声呐阵列定期发现海底兴趣点"的现成节奏模板**。
- `BasicAsteroidMapParent.cs`/`ResourceAsteroidMapParent.cs`：Odyssey 太空层的资源点 MapParent，`preciousResource` 驱动检查字符串和图标颜色——**"海底矿脉/资源点"的同构物**。
- `EnterCooldownComp.cs`/`EnterCooldownCompUtility.cs`：站点进入冷却（拆图后自动重启冷却），防止反复刷——海底打捞点防刷可直接挂此 comp。
- `SettlementProximityGoodwillUtility.cs`（210 行，签名级）：定居点邻近好感惩罚 + `CheckConfirmSettle` 确认对话框（含 gravEngine 参数，gravship 降落定居也走这）；海底定居点离 NPC 远时天然无惩罚。
- `CaravansBattlefield.cs`：伏击战场 MapParent，胜利后 `TimedDetectionRaids` 静默——商队战斗地图的生命周期模板。
- `AbandonComp.cs`/`SettlementAbandonUtility.cs`：放弃据点的确认流程（含殖民者/情绪警示）；`ArchotechSettlement.cs`/`AbandonedArchotechStructures.cs` 展示"放弃后保留特殊建筑到废弃点"的做法。
- `FastTileFinder.cs` 的 `LandmarkMode`（Any/Required/Forbidden）+ `ExpandableLandmarksUtility.cs` 的 landmark 图标：Odyssey landmark（地表兴趣点）体系，海底版可直接镜像。
- `WorldMaterials.cs`（本片外，grep 所见）有 `WorldOcean` 材质与 `OverlayModeMatOcean`；世界渲染层在其它分片。

## 明确无价值的家族（已读，PASS）

- 商队 UI 全家：`CaravanUIUtility`、`CaravanItemsTabUtility`、`CaravanNeedsTabUtility`、`CaravanThingsTabUtility`、`Gizmo_CaravanInfo`、`Dialog_SplitCaravan`、`Caravan_GotoMoteRenderer`、`Caravan_Tweener`、`CaravanTweenerUtility`。
- 商队人事/库存/需求逻辑：`CaravanInventoryUtility`、`Caravan_NeedsTracker`、`CaravanPawnsNeedsUtility`、`CaravanBedUtility`、`Caravan_BedsTracker`、`Caravan_CarryTracker`、`CaravanCarryUtility`、`Caravan_BabyTracker`、`CaravanDrugPolicyUtility`、`CaravanTendUtility`、`CaravanMergeUtility`、`CaravanFormingUtility`、`CaravanShuttleUtility`、`CaravanBonusUtility`、`CaravanRideableUtility`、`BestCaravanPawnUtility`、`CaravanAbandonOrBanishUtility`、`CaravanNameGenerator`。
- 贸易/外交：`Settlement_TraderTracker`、`FactionGiftUtility`、`PeaceTalks`、`CaravanArrivalAction_OfferGifts/Trade/VisitSettlement/VisitPeaceTalks/VisitEscapeShip/AttackSettlement`（均为通用到达动作，无环境逻辑）。
- 杂项 debug/渲染：`DebugTile`、`DebugWorldLine`、`DebugArena`、`GlobalDrawLayer_Stars/Sun`、`GlobalRendererUtility`、`CompassWidget`、`GenWorldUI`、`FloatMenuMakerWorld`、`GlobalTargetInfo`、`EscapeShip`、`EscapeShipComp`、`CaravanVisitUtility`、`SettlementUtility`、`SettlementNameGenerator`、`DestroyedSettlement`、`AbandonedSettlement`、`GravshipLaunch`、`HillinessUtility`、`OverallTemperatureUtility`、`OverallRainfallUtility`、`OverallPopulationUtility`、`LandmarkDensityUtility`、`ImportantPawnComp`、`DownedRefugeeComp`、`PrisonerWillingToJoinComp`、`ItemStashContentsComp`、`DefeatAllEnemiesQuestComp`、`FormCaravanComp`、`ExpandableWorldObjectsUtility` 之外的纯 UI。
- 纯数据/枚举（SKIP）：`Hilliness`、`OverallTemperature`、`OverallRainfall`、`OverallPopulation`、`LandmarkDensity`、`CaravanDropInventoryMode`、`CaravanEnterMode`、`PawnDiscardDecideMode`、`PackedListOfLists`、`PositionData`、`SiteCoreBackCompat`。

## 备注

- `Tile.cs`（`FishPopulationFactor`/`MaxFishPopulation`/`WaterCovered`）、`TileFinder.cs`（定居合法性判定）、`WorldGenStep_Rivers/Lakes.cs`、`SurfaceTile.cs` 在 grep 中高度相关但**不属于本分片**，留给对应分片深挖。
- `Caravan.cs`（1148 行）、`PlanetLayer.cs`、`Settlement.cs`、`Site.cs`、`FastTileFinder.cs` 为签名/关键段阅读，未逐行读全；上述结论基于已读部分。
