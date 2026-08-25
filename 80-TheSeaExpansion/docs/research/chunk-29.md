# chunk-29 调研发现（RimWorld 组：Transferable*/Transport*/Trigger/Tunnel/Turret/Tutor/UI/Verb/Weather/WorkGiver 等家族）

## TL;DR

- **天气系统全套接口已摸清**（`WeatherManager.cs`/`WeatherDecider.cs`）：海底"无风无光无雨雪"可用自定义 GameCondition 的 `ForcedWeather()` 强制锁定天气实现；恒温 4°C 会让 `WeatherDef.temperatureRange` 过滤自动屏蔽大部分天气；风速为 0 则风力发电自然停转。
- **奥德赛水体/渔业系统是直接模板**（`WaterBodyTracker.cs`/`WorkGiver_Fish.cs`）：洪泛填充聚水体、鱼群 Population 资源、Zone_Fishing 钓鱼工作，但要求 `ModsConfig.OdysseyActive`；全图是水时钓鱼站位逻辑（避开站水里）需要改。
- **刷怪过滤钩子齐全**（`WildAnimalSpawner.cs`/`WildPlantSpawner.cs`）：只刷水生生物=自定义 BiomeDef 只列水生 PawnKindDef；注意恒温海底可能导致 `SeasonAcceptableFor` 把所有动物过滤掉、密度归零。
- **"避光植物+永久黑暗"官方机制已存在**（`WorkGiver_GrowerSow.cs` 的 `diesToLight`/`IsAlwaysDarkOutside`，`WildPlantSpawner.cs` 的 cavePlant 体系）：深海低光照种植可直接借用。
- **奥德赛"地基(Foundation)"地形层是海底建筑平台的官方机制**（`WorkGiver_ConstructRemoveFoundation.cs`）。
- **真空类比点**：`WorkGiver_GrowerSow` 用 `GetVacuum() >= 0.5f` 拒绝播种、`TrashUtility` 用 `GetEffectiveVacuumForFire` 阻止袭击者纵火——"海水淹没度"若做成类真空检查，这些行为链会自动正确。
- **穿梭机(TransportShip)到达体系是"潜水舱降落海底"的直接模板**（`TransportShip*.cs` 家族）。
- **UndercaveMapComponent 提供口袋地图+塌方事件链模板**（`UndercaveMapComponent.cs`），可做海底洞穴/沉船子图与海压塌方事件。
- 其余约 180 个文件（Transferable 交易 UI、Verb 战斗、大量标准 WorkGiver、Tutor、武器特质等）与海洋无关。

## 一、天气系统（海底环境控制的核心）

### WeatherManager.cs（NOTE）
- 每图一个 WeatherManager：`curWeather`/`lastWeather` 两个 WeatherDef，`TransitionTicks=4000` 内做插值过渡（`TransitionLerpFactor`）。
- 对外的环境量全部来自 WeatherDef 字段的插值：`RainRate`、`SnowRate`、`SandRate`（Odyssey 沙尘，非 Odyssey 恒 0）、`CurWindSpeedFactor`、`CurWindSpeedOffset`、`CurMoveSpeedMultiplier`、`CurWeatherAccuracyMultiplier`、`CurWeatherMaxRangeCap`。
- 环境音（ambientSounds）随天气切换，有 Sustainer 管理；`TransitionTo` 触发 `WeatherWorker.OnWeatherStart/End`。
- **类比**：海底可以定义一个"深海"WeatherDef（windSpeedFactor=0、无光、无雨雪）并永久锁定；或锁定 Clear+自定义 GameCondition 压制视觉效果。风力发电失效不需要改任何代码——风速因子归零即可。

### WeatherDecider.cs（NOTE）
- `ForcedWeather`：遍历 `gameConditionManager` 所有条件取 `GameCondition.ForcedWeather()`，非空即强制——**这是 Mod 锁天气的官方钩子**。
- `ChooseNextWeather` 按 `BiomeDef.baseWeatherCommonalities` 权重随机；`CurrentWeatherCommonality` 过滤链：
  - 当前天气 `repeatable=false` 不重复；
  - **`weather.temperatureRange` 不含室外温度 → 权重 0**（海底恒温 4°C 会自动屏蔽温度区间不符的雨雪，需注意别把自己想要的"天气"也过滤掉）；
  - 落地前 8 天禁 favorability<2 的坏天气；
  - `rainRate>0.1` 且存在 `preventRain` 条件 → 0；
  - Anomaly 的 monolith 等级门槛；
  - 大火（LargeFireDangerPresent）时雨天权重 ×15（`ChanceFactorRainOnFire`）；
  - `TileMutatorDef.Worker.MutateWeatherCommonalityFor` 钩子可乘算权重（海底 tile mutator 可在这里把一切压成 0）。
- `StartInitialWeather`：教程模式或生物群落允许 Clear 时以 Clear 开局。
- **结论**：屏蔽天气有三条路——自定义 BiomeDef 的 baseWeatherCommonalities 只留一个自定义天气、GameCondition.ForcedWeather 强制、TileMutator 钩子。三条都是官方接口。

### WeatherEvent_LightningFlash / LightningStrike / LightningStrikeDelayed.cs（NOTE，冲突类）
- 雷击落点：`CellFinderLoose.RandomCellWith(sq.Standable && !map.roofGrid.Roofed(sq))`，然后 `GenExplosion.DoExplosion(strikeLoc, map, 1.9f, DamageDefOf.Flame, null)`——**雷暴会在无屋顶格点火**。
- Flash 基类用 `SkyTarget`/`OverrideShadowVector` 做全屏闪光与阴影偏移。
- **类比（冲突）**：海底出现雷暴火灾是出戏点。屏蔽手段：海底天气 Def 不配雷击 WeatherEvent（事件由 WeatherDef.eventMakers 驱动，属 XML 侧）；无需改 C#。

### WeatherOverlay_UnnaturalDarkness.cs + 整个 WeatherOverlay_* 家族（NOTE）
- 所有覆盖层都是 `WeatherOverlayDualPanner`/`SkyOverlay` 子类，仅持有一张世界空间平铺材质 + 双向平移速度/方向（如 `Weather/DarknessOverlayWorld`）。
- `WeatherOverlay_SnowBlizzard`/`SandHard` 额外用 `0.5 + 0.5*GenCelestial.CurCelestialSunGlow(map)` 调制覆盖层颜色——天气视觉与日照联动。
- `WeatherPartPool.cs`：SkyOverlay 实例对象池。
- **类比（直接借用）**：深海"浑浊水体/幽暗"全屏视觉 = 新建一个 SkyOverlay 子类挂自定义材质即可；非自然黑暗的材质和模式（双层异速平移）是现成的深海氛围模板。

## 二、水体与渔业（奥德赛，直接相关）

### WaterBodyTracker.cs（NOTE）
- `ConstructBodies`（**要求 `ModsConfig.OdysseyActive`**）按 `c.GetWaterBodyType(map)` 洪泛填充，把同 `WaterBodyType` 的连通水域聚成 `WaterBody`；`bodiesByCell` 做格→水体索引。
- 每 2500 tick：`map.TileInfo.MaxFishPopulation<=0` 直接跳过；否则按 `gameConditionManager.FishPopulationOffsetFactorPerDay(map, out culprit)` 恢复各水体 `Population`（每天 MaxPopulation×factor）。
- `Notify_TerrainChanged`：地形的水体类型变化时动态重建/拆分水体；`Notify_PollutionChanged` 转发污染。
- `lastRareCatchTick`/`lastNegativeCatchTick` 记录稀有/坏渔获。
- **类比**：海底 Mod 的渔业资源系统整套可借用；世界格海洋 tile 的 `MaxFishPopulation` 决定海底图有没有鱼——若要海底渔业，需确认/修改该 tile 属性（TileInfo 侧在别的分片）。

### WorkGiver_Fish.cs（NOTE）
- 需 Odyssey；Ideo 的 `HistoryEventDefOf.SlaughteredFish` 可禁止 pawn 钓鱼。
- 优先用紧邻的 Zone_Fishing 可钓格；否则找最近钓鱼区，`TriesToFindSpotThatIsntInWater=60` 次尝试，用 `BestStandSpotFor` 给钓手选站位——**优先非水格**（`avoidWander` 地形权重 0.5，普通格 1.0），两轮 60 次找不到才允许站水里。
- **类比（需改）**：全图是水的海底，"避免站水里钓鱼"逻辑失效，要么允许站平台/浅水，要么重写成"在潜水状态下捕捞"。Zone_Fishing/`IsFishable` 框架本身可直接复用。

### WaterSplash.cs（PASS→弱相关）
- 投射物命中时销毁命中格的 `ThingDefOf.Fire`（灭火水弹）。水下火焰本不成立，无需处理；仅说明"水灭火"在物品层有先例。

## 三、动植物刷新（只刷水生生物的实现路径）

### WildAnimalSpawner.cs（NOTE）
- 每 1213 tick 一次，`Rand.Chance(0.026955556 * DesiredAnimalDensity)`。
- `DesiredAnimalDensity` = `TileInfo.AnimalDensity` × 季节可用动物占比 × `gameConditionManager.AggregateAnimalDensityFactor` × 污染曲线（Biotech）。季节占比：`map.mapTemperature.SeasonAcceptableFor(allWildAnimal.race)`——**海底恒温下若动物 RaceProps 的温度区间不含 4°C，该动物权重为 0；全部不含则密度 0、永不刷怪**。做水生动物时必须把其 comfyTemperature 区间覆盖海底恒温。
- 入场格：`RCellFinder.TryFindRandomPawnEntryCell` + `CanReachMapEdge(NoPassClosedDoors, forceFenceblocked)`；`canFlyIntoMap` 的种族走 `FlyerArrival` 空降（要求无屋顶、非雾格）。
- `waterSeeker` 种族（如水獭类）要求图内有水格才刷新（`AnyWaterCells`）；IsCoastal 时 `CommonalityOfCoastalAnimal` 加权、且海岸动物专门刷在 `WaterOceanShallow` 边缘格。
- **类比**：海底图自定义 BiomeDef 只列水生 PawnKindDef 即可实现"只刷水生生物"；刷怪点"从图边缘走入"在海底解释为从远处游来，逻辑兼容；空降类要排除。

### WildPlantSpawner.cs（NOTE）
- **cavePlant 体系（深海植物直接模板）**：`plant.cavePlant` + `cavePlantWeight` 的植物只长在有**自然屋顶**的格（`GoodRoofForCavePlant`），regrow MTB 130 天（普通植物用 biome.wildPlantRegrowDays），密度 ×0.5。
- **水生植物**：`WaterPlantsDensityFactor=0.1`；`completelyIgnoreFertility` 植物可在水格生长（`GetBaseDesiredPlantsCountAt`：水格+无视肥力 → 0.1 基础密度）；`wildPlantUseDistanceToShore` 的植物用 `GetWaterPlantDistanceToShoreWeight`（洪泛到最近非水格，距离/4 线性衰减，4 格外不长）——**海底没有"岸"，这类植物在海底永不刷新，定义海底水生植物时不要勾这个标记**。
- TileMutator 钩子：`AdditionalWildPlants`、`PlantCommonalityFactorFor`、`overrideDensityForFertilityCurve`——海底 tile mutator 可整套替换植物谱系。
- 肥力：`fertilityGrid.FertilityAt` 为 0 且无无视肥力植物时不刷；`CheckSpawnWildPlantAt` 还要求 `PlantUtility.SnowAllowsPlanting`/`SandAllowsPlanting`。

## 四、种植与黑暗（深海低光照的官方接口）

### WorkGiver_GrowerSow.cs（NOTE）
- 播种前检查 **`c.GetVacuum(pawn.Map) >= 0.5f` → 直接返回 null**（Odyssey 真空格不可种）。**这是"环境致死区域禁种"的官方检查点——海底若把"海水淹没"实现为类真空网格（或在此处 Harmony 加同类检查），种植限制自动生效。**
- `wantedPlantDef.plant.diesToLight`（避光植物）：要求 `c.Roofed` **或 `map.GameConditionManager.IsAlwaysDarkOutside`**，且 `glowGrid.GroundGlowAt(c, ignoreCavePlants:true) == 0`。**`IsAlwaysDarkOutside` 这个 GameCondition 属性就是"深海室外永久黑暗"的官方开关——挂上后避光植物可在无顶室外种植，同时它理应也驱动光照变暗（GameCondition 侧在别的分片）。**
- 另有生长季 `PlantUtility.GrowthSeasonNow`、`interferesWithRoof`、sowMinSkill 等常规检查。

### WorkGiver_Grower / GrowerHarvest（PASS）
- 常规种植区/栽培盆扫描与收获判定，无环境特例（收获不看光照/水），海底无需改。

## 五、建筑与地形（海底基地建造）

### WorkGiver_ConstructRemoveFoundation.cs / ConstructRemoveFloor.cs（NOTE）
- Odyssey 引入**三层地形**：`terrainGrid.UnderTerrainAt`（底层）/`TopTerrainAt`（表层地板）/`FoundationAt`（地基），`CanRemoveFoundationAt`/`CanRemoveTopLayerAt`。
- 拆除地板/地基前校验 `terrainAffordanceNeeded` 与下层地形 affordances——**地基(Foundation)就是在水/不可建地形上造可建平台的官方机制（奥德赛用来在水面架桥/在真空铺船体）。海底殖民地的"海床建筑平台"应直接复用 Foundation 地形层，而不是自造一套。**

### WorkGiver_ClearSnowOrSand.cs（NOTE）
- 清雪与清沙合一：`snowGrid.GetDepth(c) < 0.2 && c.GetSandDepth(map) < 0.2` 跳过；区域 `areaManager.SnowOrSandClear`。
- **类比（直接借用）**：Odyssey 的"sand depth"堆积网格换个皮肤就是海底"泥沙/沉积物掩埋"——沙尘暴天气堆积（WeatherDef.sandRate，见 WeatherManager）→ 掩埋建筑门口 → 清理工作，全套链条已存在。

### WorkGiver_BuildRoof.cs（PASS，弱相关）
- 建屋顶要求 `RoofCollapseUtility.WithinRangeOfRoofHolder`+`ConnectedToRoofHolder`。海底若用"屋顶"模拟耐压壳顶盖，支撑规则可复用；无海洋特例。

## 六、穿梭机/运输船体系（海底降落的到达表现模板）

### TransportShip.cs / TransportShipManager.cs / TransportShipMaker.cs / TransportShipDef.cs / TransportersArrivalAction_TransportShip.cs（NOTE）
- `TransportShip` 是 ShipJob 队列状态机：`shipJobs` 队列 + `curJob`，`ArriveAt(cell, mapParent)` 插入 `ShipJobDefOf.Arrive`；`Dispose` 时玩家穿梭机保留、任务船销毁。
- `TransportShipDef` 字段：`shipThing`/`arrivingSkyfaller`/`leavingSkyfaller`/`worldObject`/`maxLaunchDistance`/`playerShuttle`——**到达/离开表现完全由 skyfaller ThingDef 决定**。
- 到达动作 `TransportersArrivalAction_TransportShip.Arrived`：`GetOrGenerateMapUtility.GetOrGenerateMap`（目标格无地图时现场生成）→ `DropCellFinder.GetBestShuttleLandingSpot` 选落点 → 货物从运输舱转移到船内 → 玩家穿梭机追加 `Unload(PawnsOnly)` job；落进敌对聚落会发信并影响关系。
- **类比（直接借用）**："玩家在世界图海洋格降落"的到达流程 = 新 TransportShipDef（潜水舱/深潜器）+ 自定义 arrivingSkyfaller（从海面下潜的动画表现）+ 复用 GetOrGenerateMap 生成海底图。降落点选择器 `GetBestShuttleLandingSpot` 需要检查它是否会选到水格（该选择器本体在别的分片，**未验证**）。
- `TransporterUtility.IncomingTransporterPreventingMapRemoval` 提到 GravshipController 的 LandingAreaConfirmationInProgress（奥德赛着陆区确认流程），可参考但本体不在本片。

## 七、袭击与 AI 行为（冲突/借用）

### TrashUtility.cs（NOTE）
- 袭击者纵火决策：`TrashJob` 里只有 `Rand.Value<0.35 && t.FlammableNow && !(t is Building_Door) && FireUtility.GetEffectiveVacuumForFire(t.Position, t.Map) <= 0f` 才选 Ignite——**真空格上袭击者 AI 不会放火**；`ShouldTrashPlant` 还要求雨率 ≤0.25。
- **类比**：海底"海水"若用类真空值实现（`GetEffectiveVacuumForFire` 同源检查），袭击者纵火 AI 自动失效，无需额外补丁。反之若海水是自造机制，这里是要补丁的点之一。

### TunnelHiveSpawner.cs / TunnelJellySpawner.cs（PASS，弱相关）
- 虫巢事件刷怪器（GroundSpawner 子类）：按 insectsPoints 刷虫族 + LordJob_AssaultColony。海底若保留虫巢事件需想"海底虫巢"的包装；机制本身通用，无海洋耦合。

### Trigger_* 家族（PASS）
- LordJob 触发器（殖民地承伤比例、游戏结束、高价值物品、绑架目标、伤员），纯袭击 AI 通用件，与海洋无关。

### Verb 家族（全部 PASS）
- `Verb_BeatFire`/`Verb_Ignite`（拍火/点火，点火走 `FireUtility.TryStartFireIn`——水下是否可行取决于 FireUtility，在别的分片）、`Verb_SmokePop`/`Verb_DeployToxPack`/`Verb_DeployDeadlifePack`（气体类，水下气体扩散问题归 Gas 网格，不在本片）、`Verb_Bombardment`/`Verb_PowerBeam`/`Verb_MechCluster`（轨道打击类直接 GenSpawn 天气式打击物，海底若屏蔽需查 Bombardment 本体）、`Verb_WaterSplash` 同类。均无需海底特判或特判点在别处。

## 八、其他确认无关的家族（PASS/SKIP 摘要）

- **Transferable\***（16 个文件）：交易/货运/远行队 UI 的"可转移物"抽象、排序器、单双向面板绘制。纯 UI，无海洋点。
- **Tutor\*/TutorialState/UI_BackgroundMain/UIRoot_Play/WidgetsWork**：教学与 UI 框架。
- **WeaponTrait\*/WeaponCategory\***：武器特质数据与 worker。
- **WealthWatcher/WealthUtility**：财富统计（地板、物品、pawn、建筑），袭击点数输入，海底通用无需改。
- **Twelfth/TwelfthUtility**：日历（一年 12 等分→象限），海底若搞"无季节"需注意的是上层温度/生长季逻辑而非本文件。
- **TreeSighting/TreeDestructionTracker/TreeCategory**：树木相关记录（Ideology 树精体系用），海底无树即不触发。
- **UniqueIDsManager/VersionControl/VersionUpdateDialogMaker/VirtualPawnRelation/WickMessage/VoidMonolithPyramid/VisitorGiftForPlayerUtility/VoluntarilyJoinable\*/WardenFeedUtility/WatchBuildingUtility/UsedRectPathGridCustomizer/UnloadCarriersJobGiverUtility/WalkPathFinder**：基础设施/通用 AI，其中 `WalkPathFinder` 散步避 `avoidWander` 地形（水格天然回避，水生动物漫游需另设计，弱相关已记 PASS）。
- **大量标准 WorkGiver**（喂养、搬运、建造送料、医疗、基因、机械族等约 60 个）：全部为通用作业扫描器，无环境/水相关分支。
