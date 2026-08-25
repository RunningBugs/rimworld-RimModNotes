# chunk-37 调研发现（Verse 组：S~Z 字母段）

## TL;DR

- **真空系统全家桶就在本片**：`VacuumComponent.cs`（每地图真空模拟：房间分组、真空度均衡、警告格、覆盖层渲染）+ `VacuumUtility.cs`（真空伤害、气密判定、防护装备）+ `TemperatureVacuumSaveLoad.cs`（按房间存温度+真空度网格）。这是"海水淹没/排水"机制最直接的整套模板——Room 级标量场（0~1）、开门/破墙触发 Dirty 重建、按房间容积加权扩散，全部可照抄为"水淹度"。
- **气密建筑的判定链已读清**：`ThingDef.cs` 要求 `building.isAirtight/isStuffableAirtight` 必须 Fillage=Full；`StuffProperties.cs` 有材料级 `isAirtight`（某些材料造的墙才气密）→ 海洋 Mod 的"隔水建材"可直接复用同一属性（水下=气密即隔水）。
- **`TerrainDef.cs` 是海洋改造的核心挂点**：`exposesToVacuum`（地形把房间暴露给真空）、`IsSubstructure`（地基/Substructure 标签，是气密房间在太空的"地板前提"）、`IsOcean/IsWater/IsFlood` 标签、`canFreeze`、`driesTo`（抽干后变成什么地形）、`waterBodyType`、`gravshipReplacementTerrain`。
- **水体与渔业系统**：`WaterBody.cs` 按连通水体追踪鱼群数量/常见鱼/稀有鱼、冻结格数、污染格数；海底殖民地若做"整个地图是一个 Saltwater WaterBody"，渔业、毒鱼（污染曲线）直接可用。
- **真空伤害管线**：`VacuumUtility.PawnVacuumTickInterval` 每 60 tick 按 `真空度×(1-VacuumResistance)` 叠加 `VacuumExposure` hediff；`CanBeVacuumBurnt`/`TryGetVacuumBurnablePart` 处理裸露部位灼伤（基因可免疫）。"溺水/水压"hediff 可照此结构。
- **温度常数**：`TemperatureTuning.cs`——`DeepUndergroundTemperature=15°C`、`DailyTempVariationAmplitude=7`、`DailySunEffect=14`。海底恒温 4°C 需要在 biome/室外温度侧覆盖（本片只有常数表，室外温度计算在 RimWorld 程序集，未在本片）。
- **天气/环境屏蔽点**：`WeatherDef.cs` 的 `temperatureRange`（天气发生的温度门限）、`maxGlow`（天气压暗光照上限）、`doToxicBuildup`；`WindManager.cs` 风速=天气系数+GameCondition 最低风速——海底应压零或改造成"洋流"。
- **战斗侧开关**：`VerbProperties.cs` 有 `useableInVacuum`/`useableInPocketMaps`（默认 true）——"水下禁用武器/能力"的官方先例字段（消费代码在 RimWorld 程序集，本片只见声明，未验证判定位置）。
- **降落流程**：`WorldComponent_GravshipController.cs` 管理引力船起飞/降落到世界格的全流程（含 `GravshipPlacementUtility.ApplyTemperatureVacuumFromBase` 把基地温度/真空状态带到新地图）——海洋格降落的过场与地图生成挂载点参考。

---

## 一、真空模拟（→海水淹没的直接模板）

### VacuumComponent（`Verse/VacuumComponent.cs`，MapComponent）
- 仅当 `ModLister.OdysseyInstalled && map.Biome.inVacuum` 时激活；监听 `RegionsRoomsChanged/DoorOpened/DoorClosed/TerrainChanged/RoofChanged` 事件置 dirty，每 250 tick 重建。
- **房间分组（MergeRoomsIntoGroups）**：通过 `Building.ExchangeVacuum`（门/气闸等）把相邻房间连成组；`HasDirectPathToVacuum` 用 BFS 穿过打开的门找到通外太空的路径，标记 `directWarnings`（要在 UI 上画警告格的边界格）。
- **真空度均衡（ExchangeRoomVacuum）**：组内按房间 CellCount 加权平均真空度；有直通真空路径的房间以 `0.5f/(cellCount/100)` 的速率向 1.0 拉升（即房间越大充真空越慢——"大房间排水慢"的数值模型现成）；`Room.Vacuum` 是 0~1 标量（可为 -0.1 过压？clamp 到 [-0.1,1]）。
- **渲染**：真空房间画两层 overlay（`Map/OxygenOverlay/VacuumOverlay*`）+ 边界过渡材质 + 警告材质；`GetCellExtraColor` 按真空度绿→黄→红。PlaySettings.showVacuumOverlay 控制显隐。
- **类比**：把"真空度"读作"水淹度"，把 ExposedToSpace 读作"连通海水"，整套扩散/警告/覆盖层就是排水系统。注意它挂在 Room 上而不是逐格，性能代价小。

### VacuumUtility（`Verse/VacuumUtility.cs`）
- `PawnVacuumTickInterval`：每 60 tick，要求 `pawn.Map.Biome.inVacuum && pawn.HarmedByVacuum`；所在格真空度 ≥0.5 才受伤，severity 增速 = `0.02 × vacuum × max(1-VacuumResistance, 0)`（StatDefOf.VacuumResistance 来自装备）。
- `GetVacuum(cell)`：无房间=1.0；`EverInVacuum` 逐格判断——不可通行且全填充的建筑格不算真空；`building.canExchangeVacuum/alwaysExchangeVacuum` 或 `edifice.ExchangeVacuum` 的格算真空。
- `IsRoomAirtight(room)`：气密判定——不能 TouchMapEdge、不能 OpenRoofCount>0、**每个房间格脚下必须有 `IsSubstructure` 地基**（太空站地板前提！）；边界 edifice 要么 `IsAirtight` 要么继续向外查相邻房间是否直通外界（穿非气密建筑会泄漏判定）。
- `TryGetVacuumBurnablePart`：随机选未被防护装备覆盖的皮肤覆盖部位灼伤；`IsProtectiveApparel` = 装备提供 ≥0.1 VacuumResistance；基因 `immuneToVacuumBurns` 与 hediff `PreventVacuumBurns` 可免疫。
- **类比**：潜水服=VacuumResistance 装备；"气密+地基"双重要求 = 海底建筑"防水墙+防水地板"的直接范本。

### ThingComp_VacuumAware（`Verse/ThingComp_VacuumAware.cs`）
- 抽象 comp：`InVacuum` + `FunctionsInVacuum` 两个钩子，真空中不能工作就显示 "CannotFunctionInVacuum" 警告。→ 水下设备（火把、发电机）禁用提示的模板。

### TemperatureVacuumCache / TemperatureVacuumSaveLoad
- 缓存每格所属房间的 (roomID, cellCount, temperature, vacuum)；存档时温度编码为 ushort（×16 精度，-273.15~1000°C），真空度编码为 byte（×255）；**vacuumGrid 只在 `biome.inVacuum` 时存在**。读档后 `ApplyLoadedDataToRegions` 按房间回填。
- **注意**：海洋 Mod 若加"水淹度"场，存档侧需要同样一套网格序列化；这里是现成参照。

## 二、地形系统（海洋改造的挂点）

### TerrainDef（`Verse/TerrainDef.cs`）关键字段
- `exposesToVacuum`：该地形把所在格视为暴露真空（类比：某种地形"透水"）。
- `IsSubstructure` = Odyssey 激活且有 "Substructure" 标签——气密房间的地板前提（见 VacuumUtility.IsRoomAirtight）。
- 标签系：`IsOcean`("Ocean")、`IsWater`("Water")、`IsRiver`、`IsFlood`("Flood")、`IsIce`、`IsSoil` 等，全是 `tags` 字符串匹配——海洋地形 def 只需打标签即接入现有判定。
- `waterBodyType`（Freshwater/Saltwater）→ 决定 `WaterBody.SetFishTypes` 用哪组鱼。
- `driesTo`（干涸后地形）、`canFreeze`（可冻成薄冰）、`floodTerrain`、`gravshipReplacementTerrain`（引力船降落时替换）、`destroyedByFlooding` 相关逻辑在 TerrainGrid。
- 渲染：`waterDepthShader` + `waterDepthShaderParameters`（水深渐变材质）、`TerrainEdgeType.Water` 用 `ShaderDatabase.TerrainWater`；`takeSplashes`、`extinguishesFire`（地形灭火！水下当然全灭）、`pollutionShaderType` 等。

### TerrainGrid（`Verse/TerrainGrid.cs`）
- 四层网格：`topGrid`（面层）、`underGrid`（垫层）、`foundationGrid`（地基/isFoundation）、`tempGrid`（临时地形，如潮水/洪水）；`waterCells` 集合快速回答 WaterAt。
- `SetTerrain` 对 layerable 地形自动把旧地形压入 underGrid；`SetFoundation`/`RemoveFoundation` 走独立层。
- `DoTerrainChangedEffects`：地形变化会杀掉肥力不足或 WildTerrainTags 不匹配的植物；**`newTerr.IsFlood && !plant.destroyedByFlooding` 时洪水地形不杀植物**（水下植物先例！）；terrain 变更会通知 `waterBodyTracker.Notify_TerrainChanged`（鱼类水体追踪，Odyssey）。
- 存档 fallback：缺失地形 def 回退 Soil。

### TempTerrainProps / TraverseMode / WaterSource
- `TempTerrainProps`：`terrainOnRemoved`（临时地形消失后变成什么）、`destroysFloors`、`removedByExplosions`、`replaceableByBridge`——"临时水淹层"机制（涨潮？）的载体。
- `TraverseMode` 有 `NoPassClosedDoorsOrWater`、`PassAllDestroyableThingsNotWater`——寻路层已内建"避水"模式。
- `WaterSource`：SimpleBoolPathFinderDataSource，把 IsWater 格喂给路径系统。

## 三、水体、渔业与河流渲染

### WaterBody（`Verse/WaterBody.cs`）
- 连通水体对象：cells、waterBodyType、鱼群 population（上限 `map.TileInfo.MaxFishPopulation × 尺寸曲线`，最低 20）、common/uncommon 鱼种（按 biome.fishTypes 分淡水/咸水两组抽取）。
- 追踪 `numCellsFrozen`（薄冰格）、`numCellsPolluted`；污染比例高时 Biotech 的 Toxfish 进常见鱼池（`FishingUtility.PollutionToxfishChanceCurve`）。
- **类比**：海底图整个水体可以是一个 Saltwater WaterBody；鱼群恢复/耗尽、污染影响渔获都是现成的。

### WaterInfo（`Verse/WaterInfo.cs`，MapComponent）
- 持有 `riverFlowMap`（每格水流方向向量，存档 deflate+base64）、`riverGraph`、`lakeCenter`。
- `SetTextures` 把 **WaterDepth 子相机**（SubcameraDefOf.WaterDepth）的 targetTexture 设为全局 shader 纹理（WaterOutputTex），配合 `Other/WaterReflection` 天空反射——水面渲染管线；`GetWaterMovement` 给漂物流向。
- SubcameraDef/SubcameraDriver（本片另两文件）：子相机系统按 def 建正交相机渲到 RenderTexture——海面/水下分层渲染可以照此加自己的子相机。

## 四、温度、天气、风（海底恒温/低光照的冲突与借用点）

### TemperatureTuning（`Verse/TemperatureTuning.cs`）
- 关键常数：默认 21°C、地下深处恒温 15°C（DeepUndergroundTemperature，"深处恒温"的官方先例）、日变化振幅 ±7、阳光效果 14、季节变化曲线 3→28、食物冷藏 10/冷冻 0。
- 房间温度均衡 120 tick、开门 34 tick/关门 375 tick、通风口速率 14。
- **注意**：海底 4°C 恒温要改的是室外温度源（不在本片）；但这些常数会被冷藏/腐败等系统引用，4°C 介于冷藏 10 与冷冻 0 之间——室外即"慢速冷藏"，需设计考量。

### WeatherDef / WeatherWorker / WeatherWorker_TorrentialRain / WeatherBuildupUtility
- `WeatherDef.temperatureRange`：天气只在该温度区间发生——海底 4°C 可直接挡掉热浪/寒流类天气（冲突类问题的解决点）。`maxGlow` 限制天气下天空光照上限（低光照可借）；`doToxicBuildup`（毒尘）；`preventsShuttleLaunch`；`moveSpeedMultiplier/accuracyMultiplier`。
- `WeatherWorker.WeatherTick`：doToxicBuildup 时对未防空的 pawn 调 `ToxicUtility.DoAirbornePawnToxicDamage`（室内有顶免疫）——"环境伤害+室内豁免"管线先例。
- `WeatherWorker_TorrentialRain`：暴雨开始生成 `TorrentialRainFlood` Thing、结束销毁——洪水事件实体挂法。
- `WeatherBuildupUtility`：雪/沙深度分级与移动惩罚（Thin=4/Medium=8/Thick=12 tick 附加）——若做"海底沉积物"可类比。

### WindManager（`Verse/WindManager.cs`）
- 风速 = Perlin 基础风 × 天气系数，GameCondition 可设最低风速，`CompWindSource`（风力发电机类）取 max。植物摆动 shader 也吃这个。
- **冲突**：海底应无风——风力发电、植物摇摆都需要处理（要么压 0，要么改造为洋流）。

### ToxicUtility（`Verse/ToxicUtility.cs`）
- 每 3451 tick 按 `terrain.toxicBuildupFactor + (污染格?1:0)` 叠 ToxicBuildup hediff，受 ToxicResistance/ToxicEnvironmentResistance 减免。
- **类比**：地形自带"环境伤害系数"的先例——海底可以给"海水地形"配一个持续伤害/湿冷系数，或水压伤害。

## 五、世界层与降落

### WorldComponent_GravshipController（`Verse/WorldComponent_GravshipController.cs`）
- 管理引力船起飞→世界旅行→降落全流程：捕获船体渲染、着陆标记确认 UI、降落过场、`PlaceGravship` 调 `GravshipPlacementUtility.PlaceGravshipInMap` + **`ApplyTemperatureVacuumFromBase`**（把原基地房间温度/真空带入新图）。
- `BiomeHasTransparency`：Space/Orbit biome 用透明天空盒背景——海洋 biome 若要做"透过海水看上方"效果可参考此分支。
- 降落失败按 `LandingOutcomeDef` 加权随机负面结果。
- **类比**：玩家选海洋格降落的过场/地图生成挂载点；LandingOutcome 可扩展"深海着陆事故"。

### WorldGenStep/WorldGenStepDef / WorldFloodFiller / WorldPollutionUtility
- WorldGenStep 抽象：`GenerateFresh(seed, PlanetLayer)`——Odyssey 已支持多行星层（PlanetLayer），海洋世界格生成走这里。
- WorldFloodFiller：世界格 BFS（带距离）——用于污染扩散等；海洋连通域计算可复用。
- WorldPollutionUtility：`Tile.PrimaryBiome.allowPollution` 决定可否污染，污染会 MapPollute 到对应地图——海洋 biome 通常应 allowPollution=false 或特殊处理（本片未含 biome def，结论仅限机制）。

## 六、建筑气密与装备防护（隔水对应物）

- `ThingDef.cs`（ConfigErrors/SpecialDisplayStats）：`building.isAirtight || isStuffableAirtight` 必须 Fillage=Full；信息卡显示 Stat_Airtight（stuffable 时看 `stuffProps.isAirtight`）。
- `StuffProperties.cs`：`isAirtight` 材料级字段——"只有金属/塑钢墙才隔水"的官方机制。
- 装备防护先例见 VacuumUtility.IsProtectiveApparel（stat 偏移 ≥0.1 即算防护服）。

## 七、其余有小价值的点

- `VerbProperties.useableInVacuum` / `useableInPocketMaps`（默认 true）：武器/能力环境禁用字段；判定消费在 RimWorld 程序集（本片未验证具体位置）。
- `TraverseParms.avoidDarknessDanger`：由 `GameCondition_UnnaturalDarkness.AffectedByDarkness(pawn)` 驱动——非自然黑暗影响寻路的接线点；深海低光照若要"吓人"可类比。
- `ThingRequestGroup` 枚举含 `Fish`、`WindSource`、`SubstructureFootprint`——listerThings 分组，鱼类/水源物查询走这里。
- `TexGame.RippleTex`（Other/Ripples 全局 shader 纹理）：涟漪贴图，水特效用。
- `ThingCategory.Gas`：Thing 分类有 Gas——气体实体类别（水下气泡？）。
- `Zone` 子类含钓鱼区（ZoneColorUtility 有 FishingZoneColors 五色）。
- `WeatherOverlayDualPanner`：双层滚动材质天空覆盖层——"海面波光/水下光柱"屏幕效果可借用此 overlay 机制。

## 本片覆盖家族说明

本片为 Verse 命名空间 S~Z 字母段：Stance/SubEffecter/Thing*/UI/Verb/Weather/Window/Xml/Zone 等。大量文件为 UI 控件（Widgets/Window*/Tab*/Tooltip*）、翻译系统（TKey*/Translat*）、XML 加载（Xml*）、战斗 Verb 族、Thing 容器/过滤器族——这些与海洋机制无关，已逐文件浏览登记为 PASS/SKIP。真空+水体+地形三个子系统恰好落在本片，是全 chunk 中最肥的一片之一。
