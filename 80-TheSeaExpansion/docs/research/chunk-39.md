# chunk-39 调研发现：RimWorld.Planet（世界地图层）

本片覆盖 `RimWorld.Planet` 命名空间的世界地图部分：世界格数据（Tile/SurfaceTile）、世界生成管线（WorldGenStep_*）、行星层系统（PlanetLayer/WorldGrid）、世界寻路（WorldPathGrid/WorldPathing/WorldReachability）、运输舱到达动作（TransportersArrivalAction_*）、据点/站点（Site/SitePart）以及大量世界渲染与 UI 代码。

## TL;DR

1. **海洋格 = `elevation <= 0` 的 SurfaceTile**（`SurfaceTile.cs:28` `WaterCovered => elevation <= 0f`）。整个世界层只有一个"地表"，海洋不是独立层，只是海拔为负的格子。海底殖民地 Mod 的第一入口就是 `Tile.WaterCovered`。
2. **在海洋格定居/降落的硬门槛是 `BiomeDef.canBuildBase`**（`TileFinder.cs:65-144` `IsValidTileForNewSettlement`）：`!canBuildBase` 直接拒绝（理由串 "CannotLandBiome"），另有 `hilliness == Impassable`、相邻已有基地等检查。Mod 要么 Harmony patch 此方法对海洋格放行，要么提供一个 `canBuildBase=true` 的海底 biome/mutator。
3. **商队无法穿越海洋**：`WorldPathGrid.CalculatedMovementDifficultyAt`（`WorldPathGrid.cs:118-140`）对 `biome.impassable` 或 `Hilliness.Impassable` 返回 1000（不可通行），`TransportersArrivalAction_FormCaravan.CanFormCaravanAt`（:75-82）要求目标格 `!Impassable` 且 `LayerDef.canFormCaravans`。海底殖民地的对外交通只能走运输舱/穿梭机，或者 Mod 自己造"潜艇商队"。
4. **PlanetLayer 系统（Odyssey）是"海底=另一层"备选架构**：`WorldGrid.RegisterPlanetLayer` 是 public（`WorldGrid.cs:187-206`），层由场景部件 `ScenPart_PlanetLayer` 声明、经 `LayerConnection`（含 fuelCost、zoomIn/zoomOut）互连；每层的生成步骤由 `PlanetLayerDef.GenStepsInOrder`（WorldGenStepDef 列表）驱动（`WorldGenerator.cs:83-104`）。类比：可以像 Orbit 层一样做一个"海底层"，也可以不造层、直接在 Surface 的海洋格上生成地图（更接近"世界地图海洋格降落"的需求，改动面更小）。
5. **TileMutator 是官方给世界格加"风味机制"的扩展点**（`Tile.cs:100-211`、`WorldGenStep_Mutators.cs`）：mutator 可改 hilliness 标签、动物密度、**鱼类密度（fishPopulationFactor）**、是否允许屋顶边缘走入等，经 `TileMutatorDef.chanceOnNonLandmarkTile` 或 Landmark 附加（`WorldLandmarks.cs:52-67`）。"深海热泉""海沟"等可以直接做成 TileMutatorDef/LandmarkDef。
6. **Odyssey 太空站点模板可整套借用为"海底考察站"**：`SpaceMapParent`（`MapGeneratorDefOf.Space` 兜底生成器、`GravShipCanLandOn`、地图移除即世界对象移除）+ `TransportersArrivalAction_VisitSpace/VisitSite` 的 "OrbitalWarning" 二次确认弹窗（目标在 Orbit 层时弹出可破坏性确认框）→ 类比成"深海下潜警告"几乎一比一。
7. **世界格温度管线**：`TileTemperaturesComp`（每格缓存室外温度= tile.temperature + 季节偏移 + 每日 Perlin 随机 ±7°C + 地图 GameCondition 聚合偏移）。海底恒温 4°C 的需求需要在地图侧（GenTemperature/GameCondition）处理，本片结论：世界格级温度对海洋格照常计算，没有水体温差特例。
8. **袭击压力模板**：`TimedDetectionRaids`（站点被发现后定时向站点地图发 `IncidentDefOf.RaidEnemy`，威胁点×1.5，派系取站点派系否则机械族）——可借用为海底基地的"声呐探测→袭击"机制。
9. **世界生成把 ≤15 格的海洋块改写成 Lake**（`WorldGenStep_Lakes.cs`）：小片"海"其实是湖 biome。Mod 若按 `BiomeDefOf.Ocean` 判定海底格，湖格不会命中，需决定是否把 Lake 也算作可潜水格。
10. **渲染层证实海洋视觉由 `BiomeDef.DrawMaterial` + WorldOcean shader 承担**（`WorldDrawLayer_Terrain.cs:228-231`）：非 ocean shader 且 elevation<0 时高度通道被钳到 0；海岸过渡顶点的 elevation 也做特殊插值。改海洋外观要换 biome 的 DrawMaterial 或 patch 此层。

## 主题一：海洋格的本质与世界生成

### SurfaceTile.WaterCovered（SurfaceTile.cs）
- `public override bool WaterCovered => elevation <= 0f;`（基类 `Tile.WaterCovered` 恒 false，只有 SurfaceTile 覆写）。
- SurfaceTile 还持有 `potentialRoads/potentialRivers`，但 `Roads/Rivers` 属性受 `PrimaryBiome.allowRoads/allowRivers` 门控——海洋 biome 自然不生成道路/河流。
- `Tile.IsCoastal => Find.World.CoastDirectionAt(tile) != Rot4.Invalid`；而 `World.CoastDirectionAt`（`World.cs:316-349`）要求**本格 biome.canBuildBase** 且邻居是 `BiomeDefOf.Ocean`——即海洋格自身永远不算 coastal，海岸是"陆地格看海"的概念。Mod 若要"海底格靠近海沟/浅滩"之类概念需自己写邻接判定。

### WorldGenStep_Terrain（WorldGenStep_Terrain.cs）
- 海拔噪声 `ElevationRange = (-500, 5000)`，`elevation <= 0` 即海；`hilliness` 在 elevation<=0 时直接 Flat（:221 `value > 0.235f || surfaceTile.elevation <= 0f` 分支）。
- biome 不是硬编码选择的：`BiomeFrom` 遍历所有 `implemented && generatesNaturally` 的 BiomeDef，取 `biome.Worker.GetScore(biome, ws, tile)` 最高分（:288-307）。**Mod 可以注册自己的 BiomeDef+BiomeWorker 让世界生成出"浅海/深海"等不同海洋 biome**，或干脆不动生成、只 patch 判定。
- 温度 = 纬度曲线(30°C@赤道 → -37°C@极) − 海拔削减 + Perlin 偏移，再经 `overallTemperature` 曲线。海洋格没有任何温度特例。

### WorldGenStep_Lakes / WorldGenStep_Rivers
- Lakes：flood fill 连片 Ocean biome 格，≤15 格整块改写成 `BiomeDefOf.Lake`。
- Rivers：以"沿海海洋格"为汇做反向 flood（impassable 谓词即 `WaterCovered`），沿海拔梯度回溯生成河流；纯生成期逻辑，与海洋玩法无交集，但证实 `WaterCovered` 是世界生成期的标准"水体"谓词。

### WorldGenStep_Pollution / TilePollutionComp
- 污染按 `biome.allowPollution` 门控生成；TilePollutionComp 每 60000 tick 全图污染衰减 0.01%。若想要"海洋自净/污染扩散到海"可从这里挂，但与核心玩法关系弱（PASS 级）。

## 主题二：降落、定居与站点

### TileFinder（TileFinder.cs）
- `IsValidTileForNewSettlement`：依次检查 `canBuildBase` → `implemented` → `Impassable` 山地 → 已有定居点/相邻基地 → `SettleInEmptyTileUtility.CanCreateMapAt`。这是"允许在海洋格降落"的核心 patch 点。
- 注意 `TryFillFindTile.BackupValidTile`（:383-398）：找站点格的兜底分支**已经允许不可通行格，只要它 `WaterCovered`**——原版任务站点理论上可以刷在海洋格上（实际上 ValidTile 前置的 `IsValidTileForNewSettlement` 会先挡掉，只有兜底路径例外）。说明 Ludeon 已经给"水格上的站点"留了一个缝隙。
- `TryFindNewSiteTile`（:320-365）是 Odyssey 时代统一的站点选址：landmark 偏好、`canBeSpace` 参数、层间回落（非 space 层）。Mod 的海底站点选址可直接复用，加 `validator` 限定 WaterCovered。

### 运输舱到达动作族（TransportersArrivalAction*）
- 族谱：`TransportersArrivalAction` 抽象基类（GeneratesMap/StillValid/Arrived）；`VisitSite`/`VisitSpace` 生成地图并空投；`FormCaravan` 在世界格直接组商队；`LandInSpecificCell` 落到已有地图指定格。
- **OrbitalWarning 模式**（`TransportersArrivalAction_VisitSpace.cs:88-102`、`TransportersArrivalAction_VisitSite.cs:103-117`）：目标格 `LayerDef == PlanetLayerDefOf.Orbit` 时弹 `Dialog_MessageBox`（buttonADestructive）警告"轨道危险"。→ 海底版本：目标格 WaterCovered 时弹"深海下潜"警告，实现就是抄这个 UIConfirmationCallback。
- `FormCaravan.CanFormCaravanAt`：`!Find.World.Impassable(tile) && tile.LayerDef.canFormCaravans`——海洋格两条都不满足，空投到海格不会形成商队，只会走 `TravellingTransporters` 兜底（banish/物资丢失，TravellingTransporters.cs:274-326）。Mod 若允许海格降落，必须提供一个 GeneratesMap 的到达动作（生成海底地图），否则玩家物资会被"lost"。

### SpaceMapParent（SpaceMapParent.cs）
- 完整的"一次性危险环境站点"模板：`MapGeneratorDef => def.mapGenerator ?? MapGeneratorDefOf.Space`；`GravShipCanLandOn => !HasMap`；`ShouldRemoveMapNow` 在无阻挡时连世界对象一起删（alsoRemoveWorldObject=true）。海底考察站/沉船点直接照抄此结构。
- `IResourceWorldObject.PreciousResource`：站点绑定一种贵重资源（`WorldComponent_LocationGenerator` 生成时从 `GeneratedLocationDef.preciousResources` 随机）——"海底矿脉"站点可直接用这套。

### Site/SitePart/SiteMaker（SiteMaker.cs 等）
- SitePartWorker 子类族（AbandonedSettlement 撒尸体+Gravlite 面板、DistressCall_Fleshbeasts 生成三方混战场面等）是 `PostMapGenerate` 阶段往已生成地图上撒内容的标准范式——"沉没遗迹"站点可照此写 `SitePartWorker_SunkenRuin`。
- `SiteTuning.ThreatPointsToSiteThreatPointsCurve`、TimedDetectionRaids（见 TL;DR 8）、TimeoutComp（站点限时，`Passed && !ParentHasMap` 即销毁世界对象）、TimedMakeFactionHostile（进图倒计时结束后派系翻脸）都是站点叙事件，与海洋无冲突可直接复用。
- `TimedForcedExit` 已标 `[Obsolete]`，不要参考它。

## 主题三：PlanetLayer——"海底作为一层"的备选架构

### WorldGrid（WorldGrid.cs）
- 层生命周期：`ScenPart_PlanetLayer`（场景声明）→ `RegisterPlanetLayer(PlanetLayerDef, PlanetLayerSettings, ...)`（public，:187）→ `LayerConnection`（tag 寻层、fuelCost、zoomIn/zoomOut 模式，:140-157）。Odyssey 的 Orbit 层就是这么挂的。
- `RemovePlanetLayer` 会销毁该层所有世界对象（:208-227）。
- 结论：造一个"海底层"在技术上完全可行且有先例；但需求是"世界地图海洋格降落"，海洋格本来就在 Surface 上，**直接在海格上生成地图（SettleInEmptyTileUtility 路径）比新建一层改动小得多**。层的方案只在你想让"海面/海底"成为两套可切换视图时才值得。

### WorldGenerator / WorldGenStep_Tiles（WorldGenerator.cs、WorldGenStep_Tiles.cs）
- 每层生成 = `layer.Def.GenStepsInOrder` 里 WorldGenStepDef 依次跑（:83-104），WorldGenStepDef 是 def 驱动——**Mod 可以纯 XML 往 Surface 层追加自己的 WorldGenStep**（比如给深海格打 mutator）。
- 基类 `WorldGenStep_Tiles.GenerateTileFor` 的默认实现：`PrimaryBiome = LayerDef.DefaultBiome`——Orbit 层就是靠这个全是 Space biome。侧面印证 biome 是层属性的可换件。

### TileFinder 的层处理（TileFinder.cs:275-340）
- `TryFindRandomPlayerTile/TryFindNewSiteTile` 都有 `canBeSpace` + `layer` 参数，非 space 站点会从 space 层回落到 Surface 邻接层。这套"层回落"逻辑若做海底层需要同样处理。

## 主题四：世界格数据与温度

### Tile.cs 的 mutator 衍生量
- `AnimalDensity`、`PlantDensityFactor`、`FishPopulationFactor`（:159-196）全是"biome 基值 × 各 mutator 系数"。**`MaxFishPopulation => PrimaryBiome.maxFishPopulation * FishPopulationFactor`**——海洋/渔业相关数值已有官方通道，Mod 做"渔场密度"只需加 mutator 或 biome 值。
- `AddMutator` 有 category 冲突解决与 priority（:248-282），`mutator.Worker.OnAddedToTile(tile)` 钩子可在附加时改格数据。

### TileTemperaturesComp（TileTemperaturesComp.cs）
- 世界格室外温度 = `tile.temperature + GenTemperature.OffsetFromSeasonCycle + (每日 Perlin×7°C + OffsetFromSunCycle)`；若该格有玩家地图，再加 `map.gameConditionManager.AggregateTemperatureOffset()`（:63-76）。
- 海底恒温的实现含义：世界格这层不用动；地图内的"室外 4°C"应在地图/GameCondition 层实现（类比 Odyssey 真空的地图级处理），热浪/寒流等事件正是通过 AggregateTemperatureOffset 起效的，在海底地图上屏蔽对应 GameCondition 即可。

### VirtualPlantsUtility（VirtualPlantsUtility.cs）
- 世界格放牧（商队动物吃"虚拟草"）条件：`biome.hasVirtualPlants && 温度>=0`（:35-46）。海洋 biome 显然 hasVirtualPlants=false——若 Mod 允许"潜艇商队"，食物补给要另想办法；反之"鲸群牧场"之类可以做一个 hasVirtualPlants 的海洋变种 biome。

## 主题五：寻路与可达性

- `WorldPathGrid.CalculatedMovementDifficultyAt`（:118）：`biome.impassable || hilliness==Impassable → 1000`；否则 biome.movementDifficulty + 丘陵偏移 + 冬季偏移（温度<5°C 时 +2）。冬季偏移对世界格水温恒定背景无意义但无害。
- `WorldPathing.FindPath`（:56-163）：A* 限制在单层内（`startTile.Layer != destTile.Layer → NotFound`），跳过 `world.Impassable` 格。
- `WorldReachability`：按层 flood fill 连通域缓存，跨层永远不可达。
- 结论：原版体系里"海上通行"完全不存在（没有船），海洋格是硬隔断。海底殖民地与世界其它部分的联系 100% 走空运（运输舱/穿梭机/引力船），这反而简化了 Mod——不需要做水面路径，只需保证"生成地图→空投进入"这条链通畅。

## 主题六：UI 与其他

- `WITab_Terrain`（仅 `SelPlanetTile.Tile.OnSurface` 时可见）显示 biome 描述、移动难度、mutator 列表、landmark、石材类型（`canBuildBase` 才显示石材）等。海底格的检查面板可以直接复用此 tab（它本来就是 Surface 格面板），注意石材/放牧等条目受 biome 标志门控。
- `WITab_Orbit` 是层专属 tab 的先例（仅 Orbit 层可见）——若走"海底层"方案，照抄一个 WITab_SeaFloor。
- `WorldSelector.SelectedLayer` 切换层时清空选中；space 层会触发 ConceptDefOf.Orbit 教学——层方案的 UI 先例。
- `WorldComponent_LocationGenerator`：按层维持 `GeneratedLocationDef` 生成的世界对象数量（地表约 8~20 个，按 planetCoverage 分档），选址 `TryFindSiteTile` 只排除"已有世界对象"，不查 biome——若 Mod 加海洋类 GeneratedLocationDef，需要自己 patch/验证选址落海（**此点为推断，未验证原版是否有海洋限定的 GeneratedLocationDef**）。
- 大量 WorldDrawLayer_*/WorldCamera*/WITab_Caravan_* 为纯渲染/纯 UI，与海洋无机制交集（PASS）。
- `WorldRendererUtility.UpdateGlobalShadersParams`：世界地图昼夜光照 shader 参数（`usePlanetDayNightSystem`）——深海低光照是世界格地图内的事，不在此，PASS。

## 与其它分片的衔接

- `BiomeDef`/`BiomeWorker`（canBuildBase、impassable、maxFishPopulation、hasVirtualPlants 等字段的定义）在 Verse/RimWorld 核心分片，本片只用到字段。
- `GenTemperature`、`SettleInEmptyTileUtility`、`GetOrGenerateMapUtility`、`MapGeneratorDefOf.Space` 的实际实现在其它分片，本片只确认调用点。
- 真空/气密机制本身不在本片（本片只有世界层），但 `SpaceMapParent` 这层"世界对象↔地图"的桥接模式已确认可整套借用。
