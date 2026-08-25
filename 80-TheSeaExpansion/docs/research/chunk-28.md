# chunk-28 调研发现（RimWorld 组：ThoughtWorker T–Z 后段、Threats、TileMutator 全家、TilePicker、Time/Toils、Trade、Trait 家族）

本片 200 个文件：74 个 ThoughtWorker（情绪判定器）、73 个 TileMutator 系统文件（Odyssey 世界格地形变体）、2 个 ThreatsGenerator、TilePicker、Tornado、TorrentialRainFlood，以及 Time/Toils/Trade/Trainable/Trait/Transactor/Transferable 等通用支撑家族。

## TL;DR

- **TileMutatorDef 是"海底地形"最佳挂点**（`TileMutatorDef.cs`）：原生支持 `additionalGameConditions`（永久地图状态）、`blacklistedRaidStrategies`（袭击策略黑名单）、`fishPopulationFactor`、`animalDensityFactor/plantDensityFactor`、`overrideCoastalBeachTerrain/LakeBeachTerrain/RiverbankTerrain/MudTerrain`、`weatherFrequencyFactor`、`biomeWhitelist/Blacklist`、`coastSidesRange`（数邻居 Ocean/Lake 格数）——一个 Def 就能同时改地形、刷怪、天气、袭击。
- **永久 GameCondition 注入入口**（`TileMutatorConditionMapComponent.cs`）：地图生成时把 mutator 的 `additionalGameConditions` 注册为**永久**条件——"深海低光照/恒温 4°C"可以直接做成永久 GameCondition 挂到海底 mutator 上，无需自己写 MapComponent。
- **袭击事件池极简**（`ThreatsGenerator.cs`）：只产出 `RaidEnemy` 和 `MechCluster` 两种 incident，用 `allowedThreats` 位标志过滤；"只有工业科技以上袭击"要靠 faction 侧 techLevel 过滤 + mutator 的 `blacklistedRaidStrategies` 黑名单（如禁掉空降舱直接砸脸的策略），本片没有现成的 techLevel 袭击过滤器。
- **黑暗情绪有现成模板**（`ThoughtWorker_SwallowedByDarkness.cs`）：`MapBrightness < 0.5f` 且 `GroundGlowAt <= 0` 时激活——深海"被黑暗吞没"情绪直接照抄；`ThoughtWorker_ToxicFallout.cs` 的"永久条件 + 无屋顶即生效"结构 = "暴露在海水中"情绪的精确模板。
- **太空栖息地 precept 的检测方式**（`ThoughtWorker_Precept_SpaceHabitat.cs`）：`p.Tile.LayerDef.isSpace`——Odyssey 用 PlanetLayer 判定太空；海底殖民若做成独立 layer 或在海洋 biome 格上，同款一行判定。
- **水体地形生成模板**（`TileMutatorWorker_Coast.cs`/`_Lake.cs`）：噪声阈值分深水/浅水/沙滩三层 + `MapGenUtility.Deep/ShallowOceanWaterTerrainAt`、`Deep/ShallowFreshWaterTerrainAt`；`TileMutatorWorker_Harbor.cs` 演示了在水面上铺 `Bridge` 地形建码头（`terrain.IsOcean` 检测）——海底建筑地基的直接参考。
- **洪水事件 = "海水倒灌"模板**（`TorrentialRainFlood.cs`）：基于 `map.waterBodyTracker.Bodies`（跳过 `WaterBodyType.Saltwater`）找无顶水体边缘，`terrainGrid.SetTempTerrain(cell, sourceTerrain.floodTerrain)` 铺临时洪水地形并定时消退——气密室破洞进水的机制骨架就在这里。
- **洞穴生成四件套**（`TileMutatorWorker_Caves/Cavern/CaveLakes/UndergroundCave.cs`）：`MapGenCavesUtility.GenerateCaves/Dig` + elevation>0.7 判岩层；`UndergroundCave` 生成**全封闭**洞穴（closed: true，至少 2000 格）——海底"气穴/洞穴基地"的现成生成器。
- **轨道贸易船全靠空投舱**（`TradeShip.cs`）：买入的东西 `TradeUtility.SpawnDropPod(DropCellFinder.TradeDropSpot(map))` 直接砸下来，卖出货物需在轨道贸易信标旁——海底殖民地必须决定：空投舱穿透海水？还是禁用轨道贸易改用潜艇商队？这是冲突点。
- **只刷水生动物的挂点**（`TileMutatorWorker_AnimalHabitat.cs`）：`AnimalCommonalityFactorFor` 可把特定 PawnKind 出现率 ×10，且原生读取 `BiomeDef.AllWildAnimals` 和 `CommonalityOfCoastalAnimal`（coastal 动物列表）；植物版是 `TileMutatorWorker_PlantGrove.cs`（`PlantCommonalityFactorFor` ×10）——海底生物群定制走这两个虚方法。
- **龙卷风必须屏蔽**（`Tornado.cs`）：它每 20 tick 拆除非厚顶非天然屋顶（`RoofCollapserImmediate.DropRoofInCells`），厚岩顶/自然岩免疫——海底建筑被"掀顶"等于进水团灭，海底地图应通过天气/beauty 屏蔽链禁用，或反向借用为"海底风暴卷走未固定物"。

---

## 1. TileMutator 系统（本片最大金矿，73 文件）

### 1.1 核心三件套

- `TileMutatorDef.cs`（274 行，完整读）：世界格"地形变体"定义。关键字段（全部读自源码）：
  - `workerClass` → `TileMutatorWorker` 实例，`genOrder`/`priority`/`categories`/`overrideCategories` 控制互斥（同类高优先级互斥）。
  - `extraGenSteps`/`preventGenSteps`：往地图生成管线里插/删 GenStep。
  - `overrideCoastalBeachTerrain`/`overrideLakeBeachTerrain`/`overrideRiverbankTerrain`/`overrideMudTerrain`、`preventsPondGeneration`、`preventNaturalElevation`、`preventPatches`。
  - `animalDensityFactor`、`plantDensityFactor`、`junkDensityFactor`、`geyserCountFactor`、`chunkDensityFactor`、`fishPopulationFactor`（**渔业密度**，海洋直接可用）、`additionalWildPlants`。
  - **`blacklistedRaidStrategies`**：袭击策略黑名单（海底可禁空降/破墙等不合理策略）。
  - **`additionalGameConditions`**：见 §2。
  - `hillinessForElevationGen/hillinessForOreGeneration/hillinessLabel`、`biomeWhitelist/biomeBlacklist`、`animalDensityRange/plantDensityRange`、`minHilliness/maxHilliness`、`coastSidesRange`（数邻居中 Ocean/Lake biome 格数来判定是否可生成）、`canSpawnOnRiver/Road/Landmark`、`pollutionRange`、`averageTemperatureRange`。
  - `weatherFrequencyFactor/weatherFrequencyOffset/weathersToAffect`（配合 `TileMutatorWorker_IncreaseWeatherFrequency` 改天气频率）。
  - `terrainPatchMakers`、`plantKinds`、`structureGenParms`、`resourceBlacklist`。
  - `IsValidTile` 的完整校验链：同类互斥 → 密度/污染/温度/坡度/biome 白黑名单 → 海岸线数 → 河流/道路/地标。
- `TileMutatorWorker.cs`（81 行，完整读）：生命周期钩子全集——`IsValidTile`、`OnAddedToTile`（世界格上）、`Init(map)`、`Tick(map)`（**每 tick 调用**，可驱动周期事件）、`GeneratePostElevationFertility`/`GeneratePostTerrain`/`GenerateCriticalStructures`/`GenerateNonCriticalStructures`/`GeneratePostFog`（地图生成五阶段）、`MutateWeatherCommonalityFor`、`AnimalCommonalityFactorFor`、`PlantCommonalityFactorFor`、`AdditionalWildPlants`。
- `TileMutatorDefOf.cs`：DefOf，列出 Mountain/Coast/River/Caves 及 Odyssey 的 Lakeshore/RiverConfluence/Headwater/RiverIsland/RiverDelta/Fjord/AnimalHabitat/Crevasse/MixedBiome。（SKIP 登记）

### 1.2 永久地图状态注入 —— 深海环境的主入口

`TileMutatorConditionMapComponent.cs`（30 行，完整读）：`MapGenerated()` 时遍历 `map.TileInfo.Mutators`，把每个 mutator 的 `additionalGameConditions` 用 `GameConditionMaker.MakeConditionPermanent` 注册成**永久条件**。

→ 海洋类比：**"深海黑暗"、"海水恒温"、"高压"全部可以做成永久 GameCondition**，挂在自定义的 Seafloor mutator（或 Ocean biome 配套的 mutator）上，零代码（纯 XML + 已有 GameCondition 类）或极少代码即可生效。

### 1.3 水体地形生成家族

- `TileMutatorWorker_Coast.cs`（完整读）：海岸生成基类。`Init` 用 `Find.World.CoastAngleAt(tile, BiomeDefOf.Ocean)` 得海岸角，`MapNoiseUtility.FalloffAtAngle` 造噪声；`GeneratePostElevationFertility` 把深水区 elevation 置 0；`GeneratePostTerrain` 按噪声值分三档：`MaxForDeepWater`(0.4)→`MapGenUtility.DeepOceanWaterTerrainAt`、`MaxForShallowWater`(0.5)→`ShallowOceanWaterTerrainAt`、`MaxForSand`(0.6)→`BeachTerrainAt`。**写"全图皆海"的海底地图时，这是最直接的模板**（甚至可反过来：海床 = 全图深水 + 局部海山）。
- `TileMutatorWorker_Lake.cs`（完整读）：淡水湖版，阈值 DeepWater 0.75/Water 0.5/Beach 0.45，设 `map.waterInfo.lakeCenter`。
- 变体（均为噪声参数/地形替换级修改，读过确认无独立机制）：Bay、Cove、Fjord、Peninsula（Coast 子类）；Lakeshore（淡水版 Coast）、CoastalAtoll、CoastalIsland、Archipelago、Iceberg；LakeWithIsland(s)、Pond、DryLake、Oasis、Basin、ToxicLake（换 `ToxicWaterDeep/Shallow`）、LavaLake/LavaCrater（换 `LavaDeep`/`VolcanicRock`）、Headwater（River+Lake 混合）。
- `TileMutatorWorker_Wetland.cs`（完整读）：`RidgedMultifractal` 噪声 >0.35 时岩层→泥（`MapGenUtility.MudTerrainAt`）、其他→浅水，>0.015 时岸边→泥。**海床泥地/软泥区的模板**。
- `TileMutatorWorker_Harbor.cs`（251 行，完整读）：在海岸生成废弃派系前哨站 + 伸向海里的栈桥码头。关键 API：`c.GetTerrain(map).IsOcean` 判定海水格；`SetDockAround` 把水格设为 `TerrainDefOf.Bridge`、陆格设为 `PackedDirt`，自然岩直接 `Destroy()` 并掀顶。**"在纯水域上建造可行走地面"的原生范例**——海底基地外部走道/平台可复用 Bridge 思路。
- `TileMutatorWorker_River.cs`（364 行，完整读）：河流图（`map.waterInfo.riverGraph`，RiverNode 起止点+宽度）、深度图采样、河岸地形、`GenerateRiverLookupTexture` 生成 `riverFlowMap`（每格二维流向量）。→ 海底"洋流"效果（推 pawn/漂浮物）可借用 riverFlowMap 的数据结构。

### 1.4 洞穴生成家族（海底气穴/洞穴基地）

- `TileMutatorWorker_Caves.cs`（完整读）：`MapGenCavesUtility.GenerateCaves` 按 `directionNoise` 挖隧道，`ShouldCarve` = elevation>0.7 的岩层；后处理在洞内按 Perlin 撒浅水（>0.93）和 Gravel（>0.55）。
- `TileMutatorWorker_Cavern.cs`：露天大洞窟 + 4–6 条隧道，海岸角影响开口方向，`RoofCollapseCellsFinder.CheckAndRemoveCollpsingRoofs` 防塌。
- `TileMutatorWorker_UndergroundCave.cs`：**closed:true 的全封闭洞穴**，反复挖直到连通区域 ≥2000 格——海底"天然气穴避难所"的现成生成器。
- `TileMutatorWorker_CaveLakes.cs`：洞内随机 0.005/格概率炸出 10–15 格半径的深/浅淡水湖（会 `edifice.Destroy()`）。
- IceCaves/LavaCaves 为换材质变体（SolidIce / LavaDeep+CooledLava）。

### 1.5 事件/天气驱动型 mutator

- `TileMutatorWorker_LavaFlow.cs`（完整读）：`Tick(map)` 里 `Rand.MTBEventOccurs(6f, ...)` 平均 6 天触发一次 `IncidentDefOf.LavaFlow` 事件并入 storyteller 队列——**mutator 驱动周期性地图事件的模板**（海底可做"海底地震/浊流"）。
- `TileMutatorWorker_IncreaseWeatherFrequency.cs`（完整读）：`MutateWeatherCommonalityFor` 对 `weathersToAffect` 里的天气做 `+offset ×factor`——海底**屏蔽雨/雪/雷暴**（commonality 清零）的官方通道。
- `TileMutatorWorker_IncreasedPollution.cs`：`OnAddedToTile` 直接改世界格 `tile.pollution`（+0.1~0.2）——mutator 在世界格层面改属性的范例。

### 1.6 生物群定制

- `TileMutatorWorker_AnimalHabitat.cs`（完整读）：按格（`Rand.Seed = tile.GetHashCode()`）从 `PrimaryBiome.AllWildAnimals`（commonality>0.1）和**沿海动物的 `CommonalityOfCoastalAnimal`**（要求 `tile.Tile.IsCoastal`）选一种动物，`AnimalCommonalityFactorFor` 返回 ×10。→ BiomeDef 原生区分"沿海动物"，海底 biome 刷水生动物有现成数据通道。
- `TileMutatorWorker_PlantGrove.cs`：`PlantCommonalityFactorFor` 对 `def.plantKinds` 里选中的植物 ×10（海藻林模板）。`TileMutatorWorker_WildPlants.cs`：`AdditionalWildPlants` 额外注入一种植物 commonality 0.1。

### 1.7 遗迹/废墟生成（海底沉船/遗迹）

- `TileMutatorWorker_AbandonedColony.cs`：调 `GenStep_Settlement` 生成无 pawn 的废弃殖民地，损坏建筑、老化尸体、禁取所有物品（`ForbidAllItems`）、$500 战利品——**海底废弃基地/沉船点模板**；Outlander/Tribal 子类只是 faction techLevel 过滤（Medieval~Spacer / Animal~Neolithic）。
- `TileMutatorWorker_AncientStructure.cs`：`structureGenParms`（LayoutWorker 生成结构草图 + 周界墙/地形/散兵坑）——结构化海底遗迹（研究站废墟）模板。
- `TileMutatorWorker_AncientRuins.cs`：`GenStep_AncientRuins.GenerateRuins`，MapFill 0.3~0.5；`_Frozen` 变体把建筑封进冰（`EncaseStructuresInIce`）——"封进沉积物/珊瑚"的海底版可直接类比。
- `TileMutatorWorker_AncientVent.cs`（抽象）+ Heat/Smoke/Tox 子类：5–9 个远古排放口 + 周围 8.9 格铺 `AncientMegastructure` 地形，且**剔除 8.9 格内含水的位置**（`TerrainAt(c).IsWater`）。→ 海底黑烟囱/热泉生态位的直接类比物。
- `TileMutatorWorker_HotSprings.cs`：噪声 >0.85 铺 `TerrainDefOf.HotSpring` 地形——海底热泉地形版。
- `TileMutatorWorker_Stockpile.cs`：`SketchGen` 生成 `AncientHatch` 舱室藏宝（药品/化合燃料/零部件/武器/毒品）——海底密封舱宝藏模板。
- `TileMutatorWorker_InsectMegahive.cs`：虫巢入口生成，优先找**有顶**格——海底洞穴虫巢（深海虫群）可用；陆地图才会刷的逻辑需注意与"只刷水生生物"冲突。
- `TileMutatorWorker_ArcheanTrees.cs`：始祖树 + `CompTerraformer.CanEverConvertCell` 把周围改造为 `SoilRich`——海底"造礁生物改善海床"的模板。
- `TileMutatorWorker_MixedBiome.cs`：与相邻不同 biome 格按噪声分界混合（`MixedBiomeMapComponent.biomeGrid`）——海陆过渡带/海沟-平原过渡的参考。

### 1.8 海拔造型的关键细节

`TileMutatorWorker_Mountain.cs`（第 33 行）、`_Valley.cs`、`_TerraformingScar.cs` 都有同一行：`float b = (map.TileInfo.WaterCovered ? 0f : float.MaxValue); elevation[allCell] = Mathf.Min(..., b);`——**世界格 `WaterCovered`（被水覆盖）时地图海拔被强制钳到 0**，即海洋格生成的地图默认无山岩。海底 mod 若想要海山/海沟地形，需要注意这个钳制的存在（这些 mutator 是地表变体，对纯海图的影响未验证——需看 GenStep_ElevationFertility 本体确认海图基线海拔）。

其余纯海拔/外观造型（Chasm/Cliffs/Crevasse/Hollow/Plateau/Dunes/IceDunes/MineralRich/ObsidianDeposits/Patches/AncientQuarry/AncientUplink）已读，机制均为噪声塑形或矿脉散射，无海洋特异交互，登记 PASS。

## 2. 袭击生成（ThreatsGenerator）

- `ThreatsGenerator.cs`（完整读）：`MakeIntervalIncidents` 按难度系数（`threatScale` 曲线 0.1~2.0）在 on/off 天数窗口内排 N 个威胁；`GetPossibleIncidents` 只有两项：`AllowedThreatsGeneratorThreats.Raids`→`IncidentDefOf.RaidEnemy`，`MechClusters`→`IncidentDefOf.MechCluster`。**"只有工业科技以上袭击"在本片无直接开关**：RaidEnemy 的 faction 挑选在别处（IncidentWorker_RaidEnemy），但 mutator 的 `blacklistedRaidStrategies` 可以在海底禁掉具体策略（如中心空降）。
- `ThreatsGeneratorParams.cs`：纯参数数据类（allowedThreats/onDays/offDays/minSpacingDays/numIncidentsRange/threatPoints/faction），SKIP。

## 3. 情绪（ThoughtWorker）相关发现

多数 ThoughtWorker 是通用心情/意识形态/社交判定，与海洋无类比价值（PASS）。有价值的：

- `ThoughtWorker_Precept_SpaceHabitat.cs`（完整读）：Odyssey 的"太空栖息地"戒律情绪——`p.Tile.LayerDef.isSpace` 一行判定当前是否在太空层，在太空 stage1/否则 stage0。**海底版本（"深海栖息"戒律）直接照抄，把 isSpace 换成海洋 biome/layer 判定**。
- `ThoughtWorker_SwallowedByDarkness.cs`（完整读）：Anomaly"被黑暗吞没"——`MapBrightness < 0.5f && glowGrid.GroundGlowAt(pos) <= 0f`。`GameConditionManager.MapBrightness` 是全局亮度通道（永久 GameCondition 可压暗全图），深海低光照情绪照抄。
- `ThoughtWorker_UnnaturalDarkness.cs`：`ThoughtWorker_GameCondition` 子类，条件激活 + `weatherManager.curWeather == UnnaturalDarkness_Stage2` 双门——"条件+天气"叠加情绪的结构参考。
- `ThoughtWorker_ToxicFallout.cs`（完整读）：`ConditionIsActive(ToxicFallout) && !PositionHeld.Roofed`——**"永久环境危害 + 无顶即暴露"的情绪判定**，类比"暴露在海水中（未气密）"的心情惩罚，结构完全可搬。
- `ThoughtWorker_PsychicDrone.cs`（完整读）：GameCondition 驱动的全图/商队 AOE 情绪（含 `CompCauseGameCondition_PsychicEmanation.InAoE(tile)` 的世界格范围判定）——"深海低语"类全图情绪的参考。
- `ThoughtWorker_Precept_OutdoorBlindingLight.cs`：`glowGrid.PsychGlowAt == PsychGlow.Overlit && !Roofed`——光照查询 API 参考（海底一般用不上 Overlit，但 glowGrid 是黑暗判定的配套）。
- `ThoughtWorker_TreesDesired.cs`（完整读）：树爱好者戒律按 `p.surroundings.NumSightingsInRange(TreeCategory...)` 分 15 档，**海底无树会恒掉最低档（stage 14）**——冲突点：海底殖民者若带此戒律会永久惩罚，需屏蔽或替换为"海藻/珊瑚"。
- `ThoughtWorker_Precept_TreeDensity.cs`（完整读）：按 `map.treeDestructionTracker.PlayerResponsibleTreeDestructionCount` 阈值 [2,5,8,10,13,16,20] 分档；海底无树可砍 → 恒为 stage 0（该档正负未验证，取决于 XML），同样需注意与"伐木"类戒律的交互。
- `ThoughtWorker_VoidFascination.cs`：Anomaly 虚空迷恋（附近有收容实体即满足）——"深海迷恋"特性情绪的类比参考（弱相关）。
- `ThoughtWorker_PsychicEmanatorSoothe.cs` / `ThoughtWorker_SleepSuppressor.cs` / `ThoughtWorker_PsychicArchotechEmanator.cs`：建筑光环情绪模板——`listerThings.ThingsOfDef(X)` + `InHorDistOf` 半径 + （可选）`CompPowerTrader.PowerOn`/`CompNoiseSource.Active` 检查。**"声呐安抚"/"加压舱"类建筑光环心情的标准写法**。

## 4. 天气/灾害事件

- `TorrentialRainFlood.cs`（109 行，完整读，Odyssey 洪水）：`GetInitialCells` 遍历 `map.waterBodyTracker.Bodies`，**跳过 `WaterBodyType.Saltwater`**（咸水体不泛滥），取有露天格的水体边缘格；`SpreadFlood` 用 `terrainGrid.SetTempTerrain(cell, sourceTerrain.floodTerrain)` 铺临时地形，`Destroy` 时用 `tempTerrain.QueueRemoveTerrain` 延时 2000 tick 错峰消退。→ **"海水涌入/舱室进水"的完整实现骨架**：TerrainDef 上的 `floodTerrain` 字段 + 临时地形系统。
- `Tornado.cs`（364 行，完整读）：移动天气实体，近距离 4.2 格高伤、远距离 10 格随机伤害，每 20 tick `DestroyRoofs` 拆除非厚顶非天然屋顶（`RoofCollapserImmediate.DropRoofInCells`）；`CellImmuneToDamage` = 厚岩顶或自然岩/无派系墙。→ 海底必须屏蔽（否则掀顶=进水）；其"厚顶免疫"判定也提示海底建筑的强度分级可以复用屋顶体系。

## 5. 贸易

- `TradeShip.cs`（246 行，完整读）：轨道贸易商船。`GiveSoldThingToPlayer` → `TradeUtility.SpawnDropPod(DropCellFinder.TradeDropSpot(map))`——**买入物资全部空投舱投递**；`ColonyThingsWillingToBuy` → `TradeUtility.AllLaunchableThingsForTrade`（需在轨道贸易信标范围内）。通讯走 `Dialog_Trade`（远程，不需要商队靠岸）。→ 海底冲突点：空投舱落海、信标泡水。可类比方案：通讯保留（无线电穿水无碍？）、交付换成"浮筒上浮/潜艇货运"。
- Tradeable/TradeDeal/TradeSession/TradeUI/TradeUtility/TraderKindDef 等为通用贸易数据与 UI（TraderKindDef 有 `orbital` 标志可区分轨道商），与海洋无直接交互，PASS。TrainableDefOf 注意到 Odyssey 新增动物训练 `Forage`/`Dig`/`EggSpew`/`SludgeSpew` 等——水生动物技能扩展可参考（弱）。

## 6. 其余家族（快速结论）

- `TilePicker.cs`（194 行，完整读）：选格 UI，核心是一个 `Func<PlanetTile,bool> validator` + `Find.WorldInterface.SelectedTile`；降落海洋格只需提供允许 Ocean biome 的 validator。有 `forGravship` 模式。NOTE 级（入口点）。
- Time 家族（TimeAssignmentDef/Selector/TimeControls/TimetableUtility/TipSetDefOf）：时间表与加速 UI，纯界面/数据，PASS/SKIP。
- Toils 家族（Bed/Construct/Ingest/Interpersonal/LayDown/Misc/Refuel/Tend）：AI 工作步骤积木，无环境交互，PASS（大文件读关键段落确认）。
- Trait 家族（Trait/TraitDef/TraitSet/TraitDegreeData 等）：特性数据与抑制逻辑；`TraitDefOf` 里有 `Undergrounder`（穴居者——深海暗环境可直接获益的现有特性）和 Anomaly 的 `VoidFascination`，新增"深海适应"特性走标准 TraitDef+degreeDatas 即可。数据类 SKIP/逻辑 PASS。
- Transactor/TransferableComparer*/Tradeability*/TradeAction/TradeCurrency/TraderCaravanRole：枚举与排序器，SKIP/PASS。
