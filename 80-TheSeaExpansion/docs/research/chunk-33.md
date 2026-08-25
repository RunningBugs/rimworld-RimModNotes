# chunk-33 发现文档（组：Verse，Game/Gas/Gen/Gene/Graphic/Hediff 字头）

本片覆盖 Verse 命名空间下 GameComponent～HediffCompProperties_Randomizer 共 250 个文件。大头是 Graphic_* 渲染家族（约 50 个，全部 PASS）和 HediffComp/HediffCompProperties 通用 hediff 组件家族（约 90 个，绝大多数 PASS/SKIP）。真正的海洋相关发现集中在气体网格、温度、光照、地图生成和水生基因挂钩点。

## TL;DR

- **GasGrid 是"海水淹没"系统的最佳现成模板**（`GasGrid.cs`）：每格 uint 打包 4 种气体密度（0–255），扩散/消散/真空加速消散/建筑阻隔/穿建筑均压一应俱全，类比成"海水格"即可支撑排水玩法。
- **水温/室温系统已有真空特判**（`GenTemperature.cs`）：真空地图降温速率 ×0.1；室外温度统一走 `map.mapTemperature.OutdoorTemp`，海底恒温 4°C 只需接管该值；0°C 以下腐烂率为 0（4°C 海底腐烂率 0.4）。
- **深海黑暗有现成机制**：`GlowGrid.cs` 中 `exposesToVacuum` 地形格强制返回全黑；无屋顶格光照取 `skyManager.CurSkyGlow`——压暗天空光或利用真空地形全黑行为即可做深海低光。
- **水生移动已有基因挂钩**（`GeneDef.cs` 的 `waterCellCost` 字段）：基因/RaceProperties 可直接设定 pawn 在水中格子的移动成本（`Pawn.WaterCellCost`，Biotech 限定基因路径），水生基因/水生种族直接挂这里。
- **地图生成器已认识"水域格"**（`GenStep_Scatterer.cs`）：`allowInWaterBiome` + `map.TileInfo.WaterCovered` 控制散点步骤是否在水域地图执行；海底地图的矿脉/残骸散点直接继承复用。
- **GameConditionDef 自带海洋相关字段**（`GameConditionDef.cs`）：`requireFish`、`fishPopulationOffsetPerDay`、`allowUnderground`、`layerWhitelist/layerBlacklist`（行星层白/黑名单）、`weatherDef`/`temperatureOffset`/`preventRain`——海洋地图要屏蔽或定制游戏条件时这些是主开关。

## 详细发现

### 1. 气体系统——海水淹没的直接模板（强相关）

**GasGrid（`Verse/GasGrid.cs`，553 行，全读）**

- 数据结构：`uint[] gasDensity`，每格一个 uint，按字节打包 4 种气体（BlindSmoke/ToxGas/RotStink/DeadlifeDust，见 `GasType.cs`/`GasTypeMask.cs` 的位偏移 0/8/16/24）。每格每种气体上限 255（`MaxGasPerCell`）。
- 扩散：`TryDiffuseGases` 每 tick 处理 area/32 个随机格，向随机四邻扩散，单格密度差/2、阈值 17（`MinDiffusion`）以下不扩散。
- 消散：`TryDissipateGases` 每 tick 处理 area/64 个随机格；无屋顶格消散速率 ×1、有屋顶 ×0.5，**真空格额外 +vacuum×25**（`VacuumDissipatePerTickFactor`，`intVec.GetVacuum(map)` 来自 `Verse/VacuumUtility.cs`，本片外）。类比：海底地图可把"气密室内残余海水"的排干速率挂在类似结构上。
- 加注与溢出：`AddGas` 超过 255 的部分走 `Overflow`——以 `GasCanMoveTo` 为通行判定的洪泛填充（半径 40 格内）把溢出量摊到邻格。**这套"液体加注+溢出洪泛"就是排水阀/海水倒灌的算法原型**。
- 建筑阻隔：`GasCanMoveTo` 只看 `FillCategory.Full` 的 edifice；门开着则可过。`Notify_ThingSpawned` 在满填充建筑落格时清掉该格气体。`EqualizeGasThroughBuilding(b, twoWay)` 让通风口类建筑两侧气体均压——**气闸/排水泵两侧均压的直接模板**。
- 性能开关：`anyGasEverAdded` 为 false 时整个 Tick 直接跳过；每 600 tick 重扫一次。
- 注意：气体类型是硬编码枚举+位打包，mod 新增"海水"为第五种气体不可行（位已用完），更现实的路径是仿照 GasGrid 写一个独立的 WaterGrid，或复用真空系统（VacuumUtility 在 chunk 外，未验证）。

**GasUtility（`Verse/GasUtility.cs`，227 行，全读）**

- `PawnGasEffectsTickInterval`：每 50 tick 检查 pawn 脚下气体密度，RotStink→LungRotExposure hediff，ToxGas→ToxGasExposure + ToxicBuildup 伤害（按密度 0–1 缩放）。**水下"溺水/缺氧"按环境密度累积 hediff 的现成范式**。
- 免疫判定 `IsAffectedByExposure`：遍历 `apparel.immuneToToxGasExposure` 和 `gene.immuneToToxGasExposure`——**潜水服/水下呼吸基因可以用完全同款的布尔字段模式**。
- `CorpseGasEffectsTickRare`：腐烂尸体按体型产 RotStink（52/体型单位，人形 ×1.15），海底低温下 `GenTemperature.RotRateAtTemperature` ≤0 时不产气。

### 2. 温度——海底恒温的接管点（直接相关）

**GenTemperature（`Verse/GenTemperature.cs`，533 行，全读）**

- 室外温度唯一来源：`map.mapTemperature.OutdoorTemp`（无图时回退世界格季节温度 `GetTemperatureFromSeasonAtTile`）。海底恒温 4°C = 接管 OutdoorTemp 即可，日循环/季节循环（`OffsetFromSunCycle` ±7°C、`OffsetFromSeasonCycle`）都加在世界格温度上，海底地图应令两者归零。
- 格温度查询 `GetTemperatureForCell` → 房间温度 `room.Temperature`；查不到时**默认回退 21°C**（`TryGetAirTemperatureAroundThing` 也是 21）——若海底"室外"格不属于任何 Room，要小心这个 21°C 兜底破坏恒温设定（未验证：需确认海底室外格是否总能拿到 Room/UsesOutdoorTemperature 房间）。
- **真空特判**：`EqualizeTemperaturesThroughBuilding`（换热建筑，如气闸/通风口的温度版）中 `b.Map.Biome.inVacuum && num8 < 0` 时降温速率 ×0.1（`VacuumTemperatureEqualizationFactor`）——`BiomeDef.inVacuum` 是"这张地图环境特殊"的官方开关位，海洋生物群系可设同类 flag（或直接用 inVacuum，需评估副作用，未验证）。
- `ControlTemperatureTempChange`：房间 `UsesOutdoorTemperature` 时空调/加热器无效——海底"室外"海水区加热无效是原生行为，契合设定。
- `RotRateAtTemperature`：<0°C 为 0，0–10°C 线性到 1。4°C 海底 = 0.4 倍腐烂。
- `FactionOwnsPassableRoomInTemperatureRange`：袭击/事件判定玩家是否有可用温度房间——海底殖民地恒温下基本恒真，需注意事件触发条件变化。

### 3. 光照——深海黑暗（相关）

**GlowGrid（`Verse/GlowGrid.cs`，508 行，全读）**

- 无屋顶格地面光照 = `map.skyManager.CurSkyGlow`（日食/非自然黑暗就是通过压它实现全暗）；海底地图把 CurSkyGlow 压到 ~0 即是深海黑暗。
- **`exposesToVacuum` 地形格在 `GetAccumulatedGlowAt` 直接返回全黑**（光源照不亮真空地形）——海底"深渊"地形可复用同款行为：地形设 `exposesToVacuum` 或仿写一个 flag，让灯光照不进未排水的海水格。
- 照明阈值：`GameGlowLitThreshold` 0.3 / `GameGlowOverlitThreshold` 0.9；`PsychGlowAt` 据此判 Lit/Dark（影响遮光心情、植物生长等）。
- `RegisterTerrain(cell)` 支持发光地形——发光珊瑚/热泉地形照明的挂点。
- 实现是 BurstCompile Job（ComputeGlowGridsJob + CombineColorsJob），性能上不用操心。

### 4. 游戏条件/事件——海洋环境的条件开关（相关/冲突类）

**GameConditionDef（`Verse/GameConditionDef.cs`，115 行，全读）**

关键字段（都是 Def 数据，XML 可直接用）：
- `allowUnderground`（默认 true）：条件能否在地下/洞窟生效——海底地图多半应把天气类条件关掉。
- `requireFish`、`fishPopulationOffsetPerDay`：**原生已有"需要鱼"和"每日鱼群增减"字段**（1.6 钓鱼系统的挂钩），海洋地图钓鱼/鱼群事件直接受益。
- `layerWhitelist`/`layerBlacklist`（PlanetLayerDef 白/黑名单）+ `canAffectAllPlanetLayers`：按行星层过滤条件——若海洋做成独立行星层，这是总开关。
- `weatherDef`、`temperatureOffset`（默认 -10）、`preventRain`：热浪/寒流类条件的载体；海底恒温下需在条件生成侧屏蔽（本片未含 GameConditionManager，屏蔽点未验证）。
- `silencedByConditions`、`exclusiveConditions`、`CanCoexistWith`（UnnaturalDarkness 与一切互斥的硬编码）：自定义"深海永暗"条件时可借互斥表避免与 vanilla 黑暗叠加。
- `pennedAnimalsSeekShelter`（毒尘暴类让圈养动物找 shelter）：海洋版"危险洋流/污染羽流"条件可复用此行为开关。

### 5. 地图生成——海底地形与散点（直接相关）

**GenStep_Scatterer（`Verse/GenStep_Scatterer.cs`，290 行，全读）**

- 散点基类，所有矿脉/残骸/战利品散点的祖宗。`ShouldSkipMap`：`!allowInWaterBiome && map.TileInfo.WaterCovered` 跳过——**`TileInfo.WaterCovered` 是官方"水域格"判定**，海底地图这个值必为 true，vanilla 散点默认 `allowInWaterBiome=true` 会照常生成，需要评估哪些散点该在海里关掉（直接改 def 字段即可，无需代码）。
- 选点约束齐全：minSpacing、minEdgeDist(Pct)、minDistToPlayerStart(Pct)、spotMustBeStandable、allowFoggedPositions、allowRoofed、`validators`/`fallbackValidators`（ScattererValidator 列表）。
- `GetPlacementFactor`：`isJunk` 散点按 `TileMutatorDef.junkDensityFactor` 缩放——TileMutator（1.6 地块变异器）是定制海底地块特征（沉船残骸密度等）的官方入口。

**GenStep_TerrainPatches（35 行，全读）**：按每万格数量+半径在随机点刷圆形地形补丁——海底沙地/淤泥斑块的最简现成步骤，纯 XML 可用。

**GenStep_ScatterThings（311 行，全读）**：单物散点，支持 `terrainValidationRadius`+`terrainValidationAllowed/Disallowed`（按地形标签约束散点位置）、clusterSize 成簇、filthDef 配套污渍——沉箱/矿瘤散在特定海底地形上可直接用。

**GenMorphology（`Verse/GenMorphology.cs`，195 行，全读）**：`GenerateNaturalPatch` 用双层 Perlin 噪声（macro 0.05/micro 0.08 频率）+ 距离衰减在矩形内生成自然地形/岩壁（edifice）/薄岩顶斑块，阈值可调；另有 Erode/Dilate/Open/Close 形态学操作。海底丘陵/海山/礁石生成的现成工具。

**GetOrGenerateMapUtility（35 行，全读）**：`GetOrGenerateMap(tile, size, mapParentDef, extraGenStepDefs)`——世界格→Map 的统一入口，无 MapParent 时现场造一个；`extraGenStepDefs` 参数允许调用方追加生成步骤。海洋格降落时的地图生成挂点。

**GenStep_RandomSelector / GenStep_ScatterGroup / GenStep_ScatterGroupPrefabs（全读/读关键部分）**：随机选一子步骤、按权重组撒战利品/预制体（废墟用）。通用，海底沉船 POI 可直接配置使用，无新机制。PASS 级。

### 6. 水生基因/种族挂钩（直接相关）

**GeneDef（`Verse/GeneDef.cs`，816 行，读关键字段）**

- 第 91 行 `public int? waterCellCost;`——跨文件验证：`RimWorld/Pawn_GeneTracker.cs` 的 `WaterCellCost` 取所有激活基因中该值的最小者并缓存；`Verse/Pawn.cs` 的 `Pawn.WaterCellCost`（约 506 行）优先级：Flying→1，Biotech 基因值，`def.race.waterCellCost`（`RaceProperties.cs:122` 同名字段），否则 Swimming→1。**结论：水生基因（降低水中移动成本）和水生种族都有原生字段，写 XML 即可，无需新代码。**

**Gene_PollutionRush（全读）**：基因移除时摘掉 PollutionStimulus hediff——"污染环境给增益"基因的骨架；"水下给增益"基因可仿写（环境判定在 RimWorld 侧，本片未含）。

### 7. 环境暴露 hediff 模板（相关）

- **HediffComp_GiveHediffLungRot（38 行，全读）**：pawn 处在 RotStink 气体中、severity 过阈值后按 `mtbOverRotGasExposureCurve`（SimpleCurve，见 `HediffCompProperties_GiveHediffLungRot.cs`）MTB 判定给肺部疾病并发信。**"长期暴露于 X 环境→MTB 给疾病"的完整范式**，水下减压病/低温症可直接套用此 comp 结构。
- **HediffComp_Pollution（22 行，全读）**：每 interval tick 按脚下格 `IsPolluted` 增减 severity（`HediffCompProperties_Pollution`：pollutedSeverity/unpollutedSeverity/interval）。**最简环境暴露 comp**，"泡在海水里缓慢累积 X"的最小模板。
- HediffComp_Invisibility（234 行，读关键部分）：Anomaly 隐形 comp，含 RevealCurve（接近度→显形概率）、受 disruptor 干扰、fade/recover 时长字段。若做深海生物光学迷彩可参考，弱相关。

### 8. 其他单点发现

- **GenClamor（55 行，全读）**：声音按 Region BFS（关门阻断、15 区上限）传播，听者按 Hearing 能力缩放半径。气密门天然隔音；水下声呐/噪音吸引海洋生物的机制可在此基础上做（水的传声更好，可加大 radius 或改用区域泛洪）。弱相关。
- **GenExplosion（199 行，全读）**：`DoExplosion` 有 `postExplosionSpawnThingDefWater`（水格上爆炸后生成不同事物，vanilla 用于灭火/蒸汽）和 `postExplosionGasType/Amount`（爆炸产气）——水下爆炸不点火的官方处理先例。
- **GridsUtility（551 行，读关键部分）**：`GetWaterBodyType`（地形 `waterBodyType`，**有地基 `FoundationAt` 时取基底地形的**——铺地基改变水体判定）；`GetAffordances` 同样地基优先——海底建造承重/地基系统与这两个方法强绑定；`UsesOutdoorTemperature` 委托 Room。
- **Graphic_FleckSplash（24 行，全读）**：水花 fleck 渲染到 `SubcameraDefOf.WaterDepth.LayerId`——**原生有"水深"子相机渲染层**，水下气泡/扰动特效往这层画。
- **GenSpawn / GenPlace（全读）**：通用生成/放置/擦除（SpawningWipes 规则表），`CanSpawnAt` 走 `GenConstruct.CanBuildOnTerrain`（地形 affordance 检查）——海底建造合法性校验链条的一环，无海洋特判。PASS 级。

## 未覆盖/未验证事项

- `VacuumUtility.cs`、`MapTemperature.cs`、`SkyManager.cs`、`TerrainDef.cs`（`exposesToVacuum`/`waterBodyType` 定义侧）均不在本片，真空系统的完整机制、室外温度写入点、天空光控制点需由对应分片确认。
- 海底室外格拿不到 Room 时 `GenTemperature` 的 21°C 兜底是否触发，未验证。
- 将海洋生物群系设 `inVacuum=true` 借用真空行为的副作用清单，未验证。
