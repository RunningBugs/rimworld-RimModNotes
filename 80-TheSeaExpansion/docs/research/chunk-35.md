# chunk-35 发现文档（Verse 组：LookTargets ~ PawnRenderTree）

本片覆盖 Verse 命名空间中段：地图核心（Map/MapGenerator/MapTemperature）、寻路（PathFinder 家族）、Pawn 本体与渲染树、Mote 特效、Mod 加载基础设施、XML Patch 操作等。

## TL;DR

- **海底恒温 4°C 有现成钩子**：`Biome.constantOutdoorTemperature` 和 pocket map 的 `pocketMapProperties.temperature` 都会让 `MapTemperature.OutdoorTemp/SeasonalTemp` 短路返回恒定值（源：MapTemperature.cs）。
- **游泳/涉水机制已内建且可 def 驱动**：`Pawn.WaterCellCost`（基因 > race def > 游泳时=1）、`PathFinderCostTuning.costWater`、`PathGridJob` 的 water 位图 + `TraverseMode.NoPassClosedDoorsOrWater`/`PassAllDestroyableThingsNotWater`（源：Pawn.cs、PathFinderCostTuning.cs、PathGridJob.cs）。
- **真空抗性体系在 Pawn 侧的入口**：`HarmedByVacuum`/`ConcernedByVacuum` 只看 `StatDefOf.VacuumResistance` 阈值（1.0/0.75），机械与不呼吸变种豁免（源：Pawn.cs）——"潜水服=海水抗性"可直接抄这套 stat 阈值模式。
- **游泳渲染链完整**：`PawnKindLifeStage.swimmingGraphicData`、`GraphicStateDefOf.Swimming`、`PawnRenderer` 游泳时剥掉衣物渲染并禁用缓存（源：PawnKindLifeStage.cs、PawnRenderNode_AnimalPart_Body.cs、PawnRenderer.cs）。
- **地图生成完全 def 化**：`MapGeneratorDef` 有 `customMapComponents`、`pocketMapProperties`、`isUnderground`+`roofDef`（全图铺顶）、`mapClipperTexturePath/mapClipperShader`（地图边缘视觉）、`disableShadows`；genSteps 可被 Biome/TileMutator 增删（源：MapGeneratorDef.cs、MapGenerator.cs）。
- **植物生长 = 温度 × 光照 × 肥力，光照取自天体日光**：海底低光下野外放牧链（WildGrazingPlants → 牧场营养）整体归零，水生植物体系必须自建（源：MapPlantGrowthRateCalculator.cs、MapPastureNutritionCalculator.cs）。
- **UI 真空读数可类比**：`MouseoverReadout` 在 `Biome.inVacuum` 时显示每格真空度百分比（源：MouseoverReadout.cs）。

## 一、温度与气候（海底恒温）

### MapTemperature.cs（NOTE）
- `OutdoorTemp`/`SeasonalTemp` 的求值顺序：`Biome.constantOutdoorTemperature`（nullable）→ pocket map 的 `map.generatorDef.pocketMapProperties.temperature` → 世界格温度。**两条都是纯 def/XML 可配的恒温钩子**，海底"室外恒温约 4°C"不需要任何 C# patch。
- `SeasonAcceptableFor`/`OutdoorTemperatureAcceptableFor(animalRace, buffer)`：用 `ComfyTemperatureMin/Max` 判定动物是否能在本图出现——这是"只刷水生生物"筛选链会经过的入口之一（野生生物生成在 RimWorld 侧，本片只有判定函数）。
- 温度覆盖层 `GetCellBool` 里对 `TerrainDefOf.Space` 特判（Odyssey）：真空地形不进温度 overlay。海底若做自定义"海水地形"，overlay 行为需要留意。
- 房间温度均衡走 `Room.TempTracker.EqualizeTemperature()`（每 120 tick），气密房间与"室外恒温"的隔离天然成立。

### 相关冲突点
- 热浪/寒流等事件会偏移世界格温度，但 `constantOutdoorTemperature` 短路后事件偏移无效——恒温海底自动免疫此类事件，无需额外屏蔽（此结论基于本文件求值顺序，事件侧代码在 RimWorld，未验证）。

## 二、地图生成与地图级组件

### MapGeneratorDef.cs（NOTE）
关键字段（全部 XML 可配）：
- `isUnderground` + `roofDef`：生成时给全图铺屋顶（默认 RoofRockThick）——"海底全图盖顶遮光"的现成开关。
- `pocketMapProperties`：pocket map 的 biome/温度/tileMutators（配合 MapTemperature 的恒温短路）。
- `customMapComponents: List<Type>`：**mod 注入自定义 MapComponent 的官方入口**，海水格 tracker 类组件可从这里挂。
- `disableShadows`、`disableMapClippers`、`mapClipperTexturePath`/`mapClipperShader`：控制太阳光影与地图边缘裁剪材质（见 MapEdgeClipDrawer）。
- `defaultUnderGridTerrain`、`renderWorld`、`defeatRequiresCantReachUnfogged` 等。

### MapGenerator.cs（NOTE）
- 生成流程：种子 = 世界种子 ⊕ 地图格哈希；`ConstructComponents()` 后按 `order` 依次跑 genSteps；**Biome 和每个 TileMutator 都能用 `extraGenSteps` 加步骤、`preventGenSteps` 删步骤**——海底 biome/mutator 可借此替换掉地表专属步骤（如蒸汽间歇泉、动物散布）。
- `isUnderground` 时在 genSteps 之前全图 `SetRoof`。
- `MapGenFloatGrid`（Elevation/Fertility/Caves）是 genStep 间共享的浮点网格（MapGenFloatGrid.cs，PASS 但值得知道名字）。
- 生成结束若 `map.TileInfo.Layer.Def.isSpace` 会教 Orbital 概念课——Layer 体系是 Odyssey 太空的实现层，海底若走"独立 layer"方案会经过这里。

### Map.cs（NOTE）
- 组件清单里与海洋强相关的：`waterBodyTracker`（WaterBodyTracker，**仅 Odyssey 激活时创建**，Map.cs:609-615）、`TemperatureVacuumCache`、`skyManager`、`glowGrid`、`waterInfo`（WaterInfo，MapComponent，`waterInfo.SetTextures()` 每帧设置水体 shader 贴图，Map.cs:1170）。
- `VacuumComponent`（Odyssey）在 FinalizeInit 时 `SetDrawerDirty()`（Map.cs:832）——真空的"气密/非气密"区域绘制组件，海水淹没区绘制可对位参考（实现在 RimWorld 侧，本片只见调用点）。
- `DrawMapClippers`/`MapEdgeMaterial`（Map.cs:272,310）：地图边缘材质优先取 WorldObjectDef.MapEdgeMaterial，其次 `generatorDef.mapClipperShader + mapClipperTexturePath`——**海底图边缘可以做"无尽黑水"视觉**，纯 def 可配。

### MapEdgeClipDrawer.cs（NOTE）
- 实际绘制地图四边 500 格宽的裁剪带，用 `Map.MapEdgeMaterial`；内置 MetalHell 特例。证实边缘材质管线每条地图生成器独立。

### MapInfo.cs（NOTE）
- `isPocketMap`、`disableSunShadows` 两个字段是上述行为的数据载体，存档序列化。

### MapEvents.cs（NOTE）
- 地图事件总线：`TerrainChanged`、`RoofChanged`、`GlowChanged`、`CellFogChanged`、`RegionsRoomsChanged`、`DoorOpened/Closed` 等。**做"海水淹没/排出"系统时，这些是现成的事件源**（例如地形变为水 → 触发淹没扩散重算），mod 组件可直接订阅。

### MapComponentUtility.cs / MapComponent.cs（PASS）
- 组件生命周期分发（Tick/Update/OnGUI/Draw/MapGenerated/MapRemoved），每个调用都有独立 try-catch。配合 `MapGeneratorDef.customMapComponents` 使用。

## 三、寻路与涉水移动（强相关）

### Pawn.cs 的涉水部分（NOTE）
- `Swimming => CurJob?.swimming ?? false`：游泳是 Job 级状态。
- `WaterCellCost`（Pawn.cs:494-516）：飞行=1 → `genes.WaterCellCost`（Biotech 基因！）→ `def.race.waterCellCost` → 非游泳 null / 游泳 1。**水生生物=低水成本、潜水员基因改造，官方已铺好两级 def 钩子**。
- `HarmedByVacuum`/`ConcernedByVacuum`（Pawn.cs:896-926）：仅 Odyssey；机械与不呼吸变种（`mutant.Def.breathesAir=false`）豁免；否则按 `VacuumResistance` stat < 1.0 / < 0.75 判定。每 tick 走 `VacuumUtility.PawnVacuumTickInterval`（Pawn.cs:1716）。**"海水致死+潜水服抗性"整套可直接复刻此 stat 阈值模式**。
- `DrawNonHumanlikeSwimmingGraphic`（Pawn.cs:422-436）：非人形、有 WaterCellCost、当前地形 `IsWater`、且 CurKindLifeStage 有 `swimmingGraphicData` 时切游泳图。

### PathFinderCostTuning.cs（NOTE）
- `costWater` 默认值 -1（走普通地形成本）；`For(pawn)` 用 `pawn.WaterCellCost` 覆盖——每个 pawn 的涉水成本在寻路调参处注入。

### PathGridJob.cs（NOTE，核心）
- Burst 并行的每格成本计算。持有 `water`（NativeBitArray）、`darknessDanger`、`persistentDanger` 等位图。
- `passWater` 由 `TraverseMode` 决定：`NoPassClosedDoorsOrWater` 和 `PassAllDestroyableThingsNotWater` 时水格不可通过（成本 10000）。
- `CostForCell`：水格且 `tuning.costWater >= 0` 时用 costWater 替代地形成本；水格跳过 `perceptualCost`。
- `darknessDanger` 位图 + `traverseParams.avoidDarknessDanger` → +`costDanger`(300)：**"黑暗=危险"的寻路规避已存在**（Anomaly 的黑暗兽机制），深海黑暗区让 AI 避走可白嫖。

### PathFinderJob.cs（NOTE）
- 实际 A*（Burst job）；成本 = max(grid, providerCost) + blocked(175)；`providerCost` 来自 `IPathFindCostProvider`——mod 可用该接口给任意格加寻路成本（例如"未穿潜水服的角色避开海水格"）。

### PathFinderMapData.cs（NOTE）
- 寻路数据源架构：`WaterSource`、`DarknessSource`、`PerceptualSource`、`FenceSource` 等 `IPathFinderDataSource` 列表，增量化收集 cellDelta。**水格位图有独立数据源**，海底扩展（如"强水流区域"成本层）可照此加新 Source。

### PathUtility.cs / PathGridDef.cs / PathFinder.cs / PathRequest.cs / PathGridDoorsBlockedJob.cs
- PathUtility.GetDoorCost：门成本同样按 `TraverseMode` 的水模式分支（NOTE，已并入寻路主题）。
- PathGridDef：`workerType` 可指定 PathGrid 子类 + `fencePassable`/`flying` 标志——游戏本身用多份寻路网格 def（普通/放牧/飞行），"游泳网格"有同型先例（NOTE）。
- PathFinder.cs 本体是 job 调度器，无涉水温逻辑（PASS）。PathRequest/PathGridDoorsBlockedJob 为数据/阻挡 job（PASS）。

## 四、游泳渲染链（水生生物外观）

涉及文件均为 NOTE，链条如下（全部已读源码验证）：
1. `PawnKindLifeStage.swimmingGraphicData`/`femaleSwimmingGraphicData`（PawnKindLifeStage.cs:45-47）——def 里给每个生命阶段配游泳图。
2. `PawnKindDef.ResolveReferences` 给游泳图补默认 `ShaderTypeDefOf.Transparent`（PawnKindDef.cs:390-397）；`PawnKindDef.moveSpeedFactorByTerrainTag`（:69）——**按地形标签调移速，水生生物"水中加速"直接可用**。
3. `PawnRenderNode_AnimalPart_Body.GetSwimmingGraphic` 把游泳图注册为 `GraphicStateDefOf.Swimming` 状态图（含雌性变体、AlternateGraphic 变体）。
4. `PawnRenderNodeWorker_AnimalBody.GetGraphicState`：`pawn.DrawNonHumanlikeSwimmingGraphic` 时返回 Swimming 状态。
5. `PawnRenderer`（:189,324-340）：游泳时不画阴影、清掉 Headgear/Clothes 等 flag 并加 `NoBody`（人形只露头），游泳 pawn 禁用渲染缓存。
6. `PawnRenderNodeWorker_Head`（OffsetFor）：游泳时头部 z -0.5（下沉半格，只露头顶的效果）。
7. `PawnDrawParms.swimming` 贯穿整个渲染参数结构。

**结论：给海洋 Mod 加"鱼形生物只露鳍/头"的外观，只需要 def 级 swimmingGraphicData + race.waterCellCost，无需新渲染代码。**

## 五、光照与黑暗

- MatBases.cs（NOTE）：全局材质清单含 `Darkness`（Misc/Darkness）、`LightOverlay`、`SunShadow`、`RoofedOutdoorMask`、`FogOfWar`——深海低光照/黑暗区的渲染素材管线现成。
- MapDrawLayer_ExteriorLightingOverlay.cs（PASS）：debug 用光照覆盖层，挂在 "GravshipExclude" 渲染层，无玩法意义。
- 植物光照：`MapPlantGrowthRateCalculator.SimulateGrowthRateForDay` 用 `GenCelestial.CelestialSunGlow(tile, ticks)` 逐小时模拟（NOTE 文件）——海底"日照=0"会让所有依赖日光的野生植物生长率为 0；`MapPastureNutritionCalculator`（NOTE）的放牧营养又完全建立在 WildGrazingPlants 上，所以**"海底放牧/养鱼饲料"必须绕开或重写这条链**（水培类建筑在 RimWorld 侧，不在本片）。
- MapPlantGrowthRateCalculator 同时证实：温度项走 `PlantUtility.GrowthRateFactorFor_Temperature`，恒温 4°C 下多数植物温度项也极低——双重锁死地表农业，符合设计预期。

## 六、视觉效果（水下氛围）

- Mote.cs / MoteProperties.cs（NOTE）：mote 支持 `acceleration`（可给上升加速度做气泡）、`growthRate`（放大）、`archHeight/archDuration`（抛物线）、`Attach(target)` 跟随、`needsMaintenance`+`fadeOutUnmaintained`（持续型特效，如不维持就消散的"水流"）。**水下气泡、漂浮颗粒、光柱全部可以用现成 mote 属性拼装，无需新代码**。
- Pawn_DrawTracker.cs（NOTE）：内置 `PawnWaterRippleMaker`（涉水涟漪）和 `PawnBreathMoteMaker`（冷空气呼吸雾）两个 per-pawn 特效器——"水下呼吸气泡"可对位参考 BreathMote（实现在 RimWorld 侧，本片只见挂载点）。

## 七、UI 与其他钩子

- MouseoverReadout.cs（NOTE）：`if (Find.CurrentMap.Biome.inVacuum)` 时鼠标读数显示该格真空度（`intVec.GetVacuum(map)` 百分比）。**海底版可对位做"淹没度/水深"读数**；也证实 `BiomeDef.inVacuum` 是整个真空体系的总开关。
- MentalBreakDef.cs（NOTE）：`layerWhitelist: List<PlanetLayerDef>`——精神崩溃可按星球层白名单。若海底实现为独立 planet layer（Odyssey 轨道层式），可控制哪些崩溃在海底发生；地面 layer 的崩溃不会泄漏过来。
- MouseoverUtility.cs（PASS）：辉光等级文本缓存。

## 八、确认无关的家族（PASS/SKIP 概览）

- **Mod 加载基础设施**（Mod.cs、ModContentPack、ModLister、ModsConfig、ModMetaData、ModAssemblyHandler、ModContentLoader、ModDdsLoader 等）：通用 mod 管线，无海洋特异性。
- **PatchOperation 家族**（16 个文件）：XML patch 操作实现，mod 必备工具但与海洋主题无机制关联。
- **渲染节点大批量子类**（PawnRenderNode*/PawnRenderNodeWorker* 约 60 个文件）：除上面游泳链点名者外，均为发型/纹身/伤口/孢囊等具体外观逻辑。
- **序列化/压缩**（MapFileCompressor、MapSerializeUtility、MapExposeUtility、MemoryMappedFileSpanWrapper）：存档 IO。
- **MathEvaluator 家族**：XML 内嵌数学表达式求值（min/max/randInt 等自定义函数），通用工具。
- **Pawn 杂项 tracker**（Age/Carry/Equipment/Inventory/Health/InfectionVector/Call）：Pawn_HealthTracker 全文 grep 无 drowning/vacuum/oxygen 关键词——**环境致死伤害（真空灼伤）不在 HealthTracker，在 VacuumUtility/hediff 体系（RimWorld 侧）**；PawnGenerator 同样无水/泳相关逻辑（均已 grep 验证）。
- MechShield.cs：机械护卫护盾的跟随绘制 Thing，纯视觉。
- OrbitalDebrisDef.cs：轨道碎屑视差背景图 def（含 `parallaxPer10Cells`）——数据类；"海中漂浮微粒背景"只是美术类比，本片无实现。

## 未验证事项

- `VacuumUtility.PawnVacuumTickInterval` 的伤害细节（在 RimWorld 侧，不在本片）。
- `WaterBodyTracker`/`WaterInfo`/`FreezeManager` 实现（Odyssey 类，本片只见 Map.cs 中的挂载点）。
- 野生生物生成如何调用 `SeasonAcceptableFor`（RimWorld 侧）。
- `PawnGenerationRequest.ForceAddFreeWarmLayerIfNeeded` 字段存在（已读），但"袭击者按温度强制添衣"逻辑在 RimWorld 侧——海底 4°C 恒温对袭击者装备的影响未验证。
