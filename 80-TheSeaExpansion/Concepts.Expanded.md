# The Sea Expansion — Concepts 扩充稿 v2（穷举调研版）

> 本文档基于第二轮**穷举式**源码调研：RimWorld 1.6.4871 反编译源码 9213 个 .cs 文件 100% 处置（1192 NOTE / 5943 PASS / 2009 SKIP 登记原因 / 69 整目录跳过登记），覆盖明细见 `COVERAGE.md`，分片发现见 `docs/research/chunk-01`~`45.md`。
> 当前仓库只保留第二轮穷举调研；已删除的第一轮关键词式调研不再作为事实源。
> 写法约定：每条结论标注来源分片文档；具体行号以分片文档为准。

---

## 0. 总体架构：两个关键岔路

### 0.1 海水环境的实现：复用真空系统（强烈推荐）

穷举证实 Odyssey 真空系统是**房间级环境模拟的完整基础设施**，且几乎所有挂载点都是 public/Def 驱动：

- `Room.Vacuum`（0~1 房间级数值，setter 广播 "Vacuum" 信号）+ `VacuumComponent`（容积加权扩散、开门交换、覆盖层渲染）+ `VacuumUtility`（60 tick 伤害循环、`IsRoomAirtight`）+ `TemperatureVacuumSaveLoad`（byte 网格存档）——全套可平移为"房间淹水度"。〔chunk-37〕
- 若海洋 BiomeDef 直接设 `inVacuum=true`：警报（Alert_LowOxygen）、能力/武器禁用（`useableInVacuum`）、袭击者自动配发真空装备（`PawnApparelGenerator.NeedVacuumResistance`）、风电禁用、明火熄灭、娱乐屏蔽（`JoyUtility.EnjoyableOutsideNow`）、选床 AI（`RestUtility.CanUseBedNow`）、刷点避让（`MapGenUtility` 的 `ConcernedByVacuum`）等 **30+ 处行为零代码获得**。〔chunk-01/03/15/16/18/21/31〕
- 代价：需换皮 UI 文本；附带真空语义中不适合海水的部分（见 §3 隔热、§5 野生动物抗性）。〔chunk-01/03/15/16/18/21/31/36〕

### 0.2 世界地图层面：新 BiomeDef 而非新 PlanetLayer

- 海洋格 = `SurfaceTile.WaterCovered => elevation <= 0`；负海拔天然就是深度。〔chunk-39〕
- 降落的硬门槛是 `TileFinder.IsValidTileForNewSettlement` 的 `biome.canBuildBase`——新建海底 BiomeDef（BiomeWorker 接管深海格评分）即可解锁全部入口（开局/商队定居/gravship/搬殖民地）。〔chunk-39〕
- PlanetLayer 方案（`PlanetLayerDef` 的 onlyAllowWhitelisted 四连白名单 + isSpace + raidPointsFactor）**备选保留**：事件/任务/袭击过滤更干净（QuestScriptDef/RaidStrategyDef/GameConditionDef 全系层白名单），但改动面大；chunk-39 文档有"海格直建 vs 新建海底层"的完整对比。〔chunk-20/38/39/41〕
- **建议**：主方案走 BiomeDef + TileMutator 白名单；把 PlanetLayer 作为后续"深海层"扩展的预留路径。〔chunk-20/38/39/41〕

## 1. 海洋格出生点

- 门槛确认：`canBuildBase` + `hilliness != Impassable` + 未占用。〔chunk-39〕
- 连带硬编码需处理：营地落点显式排除 IsWater（`SettleInEmptyTileUtility.cs:104`）；商队 `WorldPathGrid` 对 impassable biome 一票否决；离图组队重定位到可通行格。〔chunk-38〕
- **商队对外通道必须设计**：海底基地组商队需合法路径——空投舱/潜艇载具（Gravship 旅行-落图-逐房间携带温度+真空度的打包机制是直接模板，可"携带舱内无水环境"）。〔chunk-10/38〕
- 海洋 feature 是一等公民（OuterOcean/Bay/Island/Archipelago 形态学），可仿写海沟/海山命名；任务选址的 AllowedLandmarks 已支持水体地标。〔chunk-38/41〕

## 2. 地图生成

- **WaterCovered 瘫痪问题确认**：`GenStep_ElevationFertility` 全图海拔压 0、`GenStep_RocksFromGrid`/`GenStep_RockChunks` 整体跳过——海底资源必须 Mod 自补。〔chunk-10〕
- **头号生成模板**：`GenStep_Space`+`GenStep_Asteroid`（全图填致死地形+噪声孤岛+主岛洪水填充）；`SpaceGenUtility.GenerateHole`（矩形变 Space+掀屋顶）="海床上挖海水空洞"直接模板。〔chunk-10/24〕
- 恒温初始化官方做法：`MapGenUtility.SetMapRoomTemperature` 整图设温（OrbitalPlatform 先例）；`BiomeDef.constantOutdoorTemperature`=4 是 Def 级银弹。〔chunk-02/10/35〕
- 永久黑暗注入：`BiomeConditionMapComponent`（biomeMapConditions）/ `TileMutatorConditionMapComponent`（additionalGameConditions）零代码挂永久 GameCondition。〔chunk-02/28〕
- TileMutator 五阶段挂载管线（`GenStep_Mutator*`）+ `blacklistedRaidStrategies` + `fishPopulationFactor` + `forceRockTypes` 确认齐全。〔chunk-10/28〕
- 冲突点：`GenStep_Power` 据点生成 `ForceSetCurSkyGlow(1f)` 强制满亮度；`SymbolResolver_RemoveDangerousTerrain` 会把 dangerous 海水格自动排干；`EnsureCanReachMapEdge` 会拆气密墙保连通。〔chunk-10/44〕
- BaseGen 是 XML RuleDef 驱动，可注册自定义 SymbolResolver 生成海底 POI；沉船模板齐全（Ship_Pregen/SketchResolver_Gravship/RoomLayoutGenerator 走廊门位管线）。〔chunk-22/44〕

## 3. 温度与天气

- 恒温 4°C：`constantOutdoorTemperature` 一行解决，`AggregateTemperatureOffset`（热浪/寒流/气候循环）天然失效。〔chunk-09/35〕
- 替代/补充路径：`Comp_AtmosphericHeater` 把目标温度存进永久 GameCondition——反过来用可做"海底热泉暖域"。〔chunk-03〕
- 天气：锁单一自定义"深海天气"即控全套（`WeatherDecider.ForcedWeather`；`WeatherDef.temperatureRange` 在 4°C 自动滤掉雨雪）；任务强制天气走 `GameCondition.CanApplyOnMap`（layer 白名单/allowUnderground/requireFish 三重过滤先例）兜底。〔chunk-09/29〕
- 冲突确认：寒流门槛 0~15°C 在 4°C 仍触发需屏蔽；厚顶房间向硬编码 **15°C** 深部均衡（`RoomTempTracker`）需拦截；`GenTemperature` 查不到 Room 时兜底 21°C 是隐患。〔chunk-11/33/36〕
- **隔热语义**：`RoomTempTracker` 的 inVacuum 分支把墙体导热压到 5E-05——复用 inVacuum 会附带"海水隔热"的错误语义，需 patch 或接受。〔chunk-36〕
- 附带：4°C 下户外食物几乎不腐（CompRottable 环境温度驱动，海底天然冷藏）；4°C 冰会缓慢融化（CompMelter）；FreezeManager 的 -7°C 结冰不触发。〔chunk-06/19/32〕

## 4. 光照

- 三层压暗手段齐备：天气 `maxGlow` 截断 / `CompAffectsSky` 建筑级天空覆盖 / 永久 GameCondition（`QuestPart_DarkenMap` 的 `SetTargetBrightness` 是官方压光管道；`GameConditionManager.IsAlwaysDarkOutside`/`MapBrightness` 现成开关）。〔chunk-05/09/31/41〕
- `BiomeDef.disableSkyLighting` 有 SkyManager 特判（关天光）+ `SkyTarget.LerpDarken`（只变暗）两条路线。〔chunk-36〕
- 随深度变化：世界格的负海拔可保存海深，永久 GameCondition 可通过现有亮度管道按该值压低 glow；具体曲线仍由 Mod 定义。〔chunk-09/25/39/41〕
- 关键坑确认：`exposesToVacuum` 地形格灯光强制归零（GlowGrid）——海水地形若复用此字段则海底开灯无效，需铺地板（Odyssey 原生解法）或自建标志。〔chunk-33〕
- 玩法联动：黑暗战斗修正（DarknessCombatUtility，precept 驱动）、血族不晒伤、虫害黑暗偏好、`EdgeWalkInDarkness` 袭击刷点按 PsychGlow.Dark 选位——永暗海底全部命中。〔chunk-12/18/45〕
- 发光体系白捡：`TerrainDef.glowRadius/glowColor` 地形自发光 + cavePlant 发光植物 + Darklight（darklightToggle）+ Anomaly SectionLayer_Darkness 渲染模板——海底生物荧光/发光热泉零新代码。〔chunk-05/32/45〕

## 5. 海水环境与气密（门墙比对终版）

- 真空系统全套在 Verse 层读完：`VacuumComponent`/`VacuumUtility`/`TemperatureVacuumSaveLoad`（详见 chunk-37，本篇不再重复）。〔chunk-37〕
- 气密判定：`Building.IsAirtight`（`isAirtight`/`isStuffableAirtight` + `StuffProperties.isAirtight`）；门"开着才交换真空"= 水密门现成语义。〔chunk-02/31/37〕
- 门墙家族终版对照表：Building_Door（关门隔绝）/ Building_Vent（flick 控制交换）/ **Building_VacBarrier（通电气密+可通行力场门=水闸门模板）**/ AnimalFlap（常漏）/ 天然岩壁（气密）。〔chunk-02/03〕
- **材质陷阱终版确认**：只有金属+活铁 `isAirtight`，石/木墙全漏——海底建材气密规则是必做决策。〔chunk-31/37〕
- `IsRoomAirtight` 要求气密边界 + **Substructure 地基**——气密建筑强制地基是原版规则；`Building_GravEngine:600` 的全船气密自检可整套借用为"耐压完整性自检"。〔chunk-02/37〕
- 伤害链三层设计确认可复刻：`HediffGiver_VacuumBurn`（直伤，5~20s/次）+ `HediffComp_VacuumExposure`（暴露累积/脱离恢复）+ `HediffStage.preventVacuumBurns`（阶段免疫）+ `BodyPartDef.canBeVacuumBurnt`（部位开关）。〔chunk-34〕
- 防护 = `VacuumResistance` stat（`HarmedByVacuum` 阈值 1.0/0.75；`breathesAir=false` 直接 1.0）；装备侧 `ThingStuffPair` 已缓存该 stat；`CompCauseHediff_Apparel` 是"潜水服供氧"模板。〔chunk-25/26/34/35〕
- **进/排水动态**：`Flood` 抽象基类（四邻扩散+`floodTerrain` 地形替换+地基阻挡）+ `SeasonalFlood`（SetTempTerrain 临时地形+定时消退）+ `LavaEmergence`（扩张+poolSize 上限+QueueRemoveTerrain）——"破洞进水→修复排干"全链模板；`GasGrid`（密度扩散/加注溢出洪泛/真空 25 倍消散）是另一套可参考的网格模拟。〔chunk-09/15/23/33〕
- **排水阀终版**：两个官方模板——`CompOxygenPusher`（24 行，`room.Vacuum = UnsanitizedVacuum - 排量`，房间级）与 `CompTerrainPumpDry`（潮湿泵，半径渐进+`driesTo` 链，格级；`GenStep_EscapeShip` 有实证调用）。〔chunk-04/07/16〕

## 6. 淡蓝 overlay 与视觉

- 真空覆盖层：房间级 instanced 着色（绿→红），换淡蓝材质即得"房间进水度"显示。〔chunk-37〕
- 逐格方案：`SectionLayer_Darkness`（Anomaly 逐格黑暗遮罩，需自写因硬编码 Anomaly）/ `SectionLayer_BridgeProps` 模板。〔chunk-36〕
- 氛围白捡：`FishShadowComponent`（水体鱼影）、`MoteWater`/`TerrainWater` shader、`_WaterReflectionTex`、`MapDrawLayer_OrbitalDebris` 视差背景（→海洋雪）、`MapGeneratorDef.mapClipperShader`（→无尽黑水边缘）、`FogOfWarColor` 定制。〔chunk-10/32/35/36〕
- **音效**：`SoundParamSource_Underground`（17 行）+ `SoundFilterLowPass` ——照抄为 Underwater 参数源挂低通滤波，水下闷音近零代码。〔chunk-45〕
- 呼吸白气（PawnBreathMoteMaker 逐格 GetVacuum 抑制）可换气泡特效。〔chunk-17〕

## 7. 生物、植物与食物

- 水生生物移动/渲染全现成：`Pawn.WaterCellCost`（基因>race>默认）+ `SwimPathFinder`（12 步水中游走）+ `PawnKindLifeStage.swimmingGraphicData` + `GraphicStateDefOf.Swimming` 游泳渲染管线 + `RaceProperties.waterSeeker/waterCellCost`（筛水生袭击者现成字段）+ `TraverseMode.*Water` 水感知寻路。〔chunk-26/35/36/42/43〕
- 钓鱼系统确认可整体白捡：`Zone_Fishing`+`WaterBodyTracker`（洪泛聚水体+鱼群恢复）+`FishingUtility`+`NegativeFishingOutcomeDef`+`WorkTypeDefOf.Fishing`（MayRequireOdyssey 独立工种）+`Difficulty.fishingYieldFactor`；海底 biome 配 `fishTypes.saltwater_*`+`maxFishPopulation` 即可。注意 `WorkGiver_Fish` 站位逻辑优先非水格——全海图需改。〔chunk-12/13/24/29/30〕
- 动物事件拦截：ManhunterPack/FarmAnimalsWanderIn 兜底扫全表；ThrumboPasses 固定种；流浪者事件无环境检查会走进海水淹死；仪式奖励直接触发 FarmAnimalsWanderIn/Infestation_Jelly 绕过事件过滤；`Pawn_GuestTracker:617` 救援加入判定引 SafeTemperatureRange。〔chunk-12/17/21〕
- **虫害终版结论**：`InfestationCellFinder` 硬要求厚岩顶+山体得分——**海底无山则天然免疫**；但任务虫害 `QuestPart_Infestation` 有 `ignoreRoofedRequirement:true` 需外层拦截；想做"海床巢穴"需自写 cell finder。〔chunk-12/20/40〕
- 植物钩子全家确认：`DyingBecauseExposedToVacuum`+`vacuumResistant`+`destroyedByFlooding`+`dieIfNoSunlight`+`mustBePermanentDarknessToSow`+`growMinGlow`+`diesToLight`；Nutrifungus/Archean 树/cavePlant 发光体系现成。水培断电即死惩罚内置；海底光照≈0 使放牧/野生植物链整体归零（水产饲料需自建）。〔chunk-06/19/29/35〕
- 觅食：`BiomeDef.foragedFood/forageability` 驱动商队采集与动物觅食，海底可配。〔chunk-38〕

## 8. 袭击与事件

- "工业+袭击"三条现成路径确认：①`PawnsArrivalModeDef.minTechLevel`+`onlyAllowWhitelistedArrivalModes`（biome 级）；②`FactionDef` 的 techLevel/allowedArrivalTemperatureRange/6 个层名单/arrivalModeBlacklist/disallowedRaidStrategies；③`SitePartDef.minFactionTechLevel`（据点级）。`TechLevelUtility.CanSpawnWithEquipmentFrom` 明确 Industrial 只穿 Industrial。〔chunk-08/18/24/26〕
- 袭击者装备：方案 A 下 `PawnApparelGenerator` 真空服免费补发管线自动生效（逐部位补 VacuumResistance）。〔chunk-17〕
- **水中袭击模板终版**：`PawnsArrivalModeWorker_EmergeFromWater`（要求 >50 格连通水域且可达殖民地，分批+眩晕+水雾特效）+ `IncidentWorker_DevourerWaterAssault` 完整事件——换 `raidArrivalMode` 即可水生化任意袭击。〔chunk-11/18〕
- 事件过滤正道：`IncidentWorker.CanFireNow` 内建 biome 黑白名单 + GameCondition 层过滤 + `QuestPart_GameCondition.CanApplyOnMap` 跨图回退。〔chunk-11/20〕
- **空投冲突终版**：`DropCellFinder.CanPhysicallyDropInto` 硬排斥 IsWater+exposesToVacuum——贸易/袭击/难民/资源舱全图无落点会崩，必须自定义落点（着陆台/浮筒）或 patch；`Skyfaller.HitRoof` 砸穿屋顶管线可嫁接"海面沉降物砸壳漏水"叙事。任务人物到达有 `canBeBackup` 回退链，需自定义 backup 否则静默丢失。〔chunk-08/20/24〕
-  storyteller 层：Odyssey 已有按层调控先例（isSpace 时 MTB×spaceMtbDayFactor）——海洋层照抄。〔chunk-25〕
- 屏蔽/保留清单：屏蔽——寒流（误触发）、热浪/极光（天然免疫但弹信）、Tornado（拆非厚顶）、Bombardment（轨道轰炸穿海+点火）、Flashstorm、SeasonalFlooding、敲击兽/Gauranlen/Anima 树；保留——轨道商船（只需通讯台，海底贸易主通道）、MechCluster（落点避室内，全密闭基地天然排斥——注意这是双刃剑）、GillRot、EmergeFromWater、HerdMigration。〔chunk-03/11/16/28/40〕
- 新增冲突：访客/商队的 `Trigger_PawnExperiencingDangerousTemperatures`（Hypothermia>0.15 即撤退）在 4°C 海底会赶走无防寒队伍；`LordJob_VisitColony` 危险温度撤离同理。〔chunk-16/42〕

## 9. 建筑、电力与物理

- 能源终版：风电（inVacuum 禁用）/太阳能（CurSkyGlow 线性）自动失效；**地热=海底热泉**（`Building_SteamGeyser`+`geyserCountFactor`+`PlaceWorker_OnSteamGeyser` 两段式）；**洋流发电机**（水车 `PlaceWorker_WatermillGenerator`：MovingFluid affordance+`waterInfo.GetWaterMovement` 流向）；深层钻井（`hasBedrock`+DeepResourceGrid 海底矿床直接复用，注意 CompDeepScanner 排除深水格——需可通行"海床"基底地形）；`CompLowPowerInSpace` 示范按环境切功耗档。〔chunk-04/18/32/33/35〕
- 建造地基：Odyssey Foundation/Substructure 体系（terrainGrid 三层、`FoundationAt/IsSubstructure`、IndoorMask shader、`Designator_RemoveFoundation` 支撑检查）是海床平台官方机制。〔chunk-08/23/29/32〕
- 水下电力规则尚未定稿：第二轮 C# 只确认 `PlaceWorker_Conduit` 负责电缆不可重叠，未找到 `WaterproofConduit` 的 C# 证据；其是否由 XML Def/affordance 驱动需在实际 Mod 的 Def 调研阶段核对，当前标记为**未验证**。〔chunk-18〕
- 水下物理白捡：`extinguishesFire` 地形 flag + 真空比例灭火（`GetEffectiveVacuumForFire`）+ `IsWater` 武器失效 + Filth 拒生成 + `postExplosionSpawnThingDefWater` 水格爆炸变体 + `takeSplashes` 水花。〔chunk-09/26/33〕
- 新增冲突：`CompScanner`/着陆信标查屋顶（若"头顶海水"实现为屋顶则全瘫——海水实现方式决策）；`ShortCircuitUtility.TryShortCircuitInRain`（进水舱电器短路的现成框架）。〔chunk-06/23〕
- 腐坏：`StatPart_EnvironmentalEffects` 地形 extraDeteriorationFactor 直接叠加——海水加速腐坏零代码。〔chunk-24〕

## 10. 角色生存、心情与 AI

- 生存链终版：三层 hediff（§5）+ `HediffGiver_Hypothermia` 全公式（4°C 常驻威胁原生支持）+ `HediffComp_RemoveIfApparelDropped`（脱装备即断供氧模板）。〔chunk-34〕
- 自救 AI：`JobGiver_FindOxygen` 完整模板（hediff 阶段+格子判定→BFS Region 找安全房间）；动物无自救。〔chunk-14〕
- 新增发现：`JobGiver_OptimizeApparel` 在 inVacuum biome 绝不自动脱真空服（"海水中不脱潜水服"先例），但不会主动穿（评分不看抗性）。〔chunk-14〕
- 心情压力（新增）：`Need_Outdoors` 掉到底=幽闭症 Entombed——**海底全室内基地的常驻心情压力，是核心玩法张力点**；`ThoughtWorker_Cold` 常驻（戒律可减档）、`ThoughtWorker_Dark` 常驻、`SwallowedByDarkness`（MapBrightness<0.5）直接命中、阳光/极光想法永不触发；`ThoughtWorker_Precept_SpaceHabitat`（isSpace 一行判定）是"海底栖息地戒律"模板。〔chunk-17/27/28〕
- 湿透思想：游泳后 7500 tick 免疫 SoakingWet（`lastSwamTick`）——海底常驻水体需处理。〔chunk-43〕
- 危险地形 AI：`dangerous=true` +300 感知成本；`ConcernedByVacuum` 系谓词全套（刷点避让/选床/娱乐/仪式安全检查 `MeditationUtility.SafeEnvironmentalConditions`）——海水致死挂同型谓词即全链路正确。〔chunk-13/16/21/31〕
- 精神崩溃门控先例：`MentalBreakWorker_RunWild` 硬编码屏蔽致死环境、`layerWhitelist` 按层过滤。〔chunk-43〕
- 仪式/冥想：户外仪式被 SafeTemperatureRange/ExtremeTemperature 挡（`ignoreExtremeTemperatures` 可豁免）；儿童户外学习项失效。〔chunk-15/19/22〕

## 11. DLC 内容向系统（本轮新增覆盖）

- **Royalty**：头衔援助有温度门槛（`RoyalAid.overrideAcceptableTemperatureRange` 可按 permit 豁免）；心灵仪式框架是干净的扩展点，但 SummonAnimals/BloodRain 等 outcome 会刷陆生内容需过滤。〔chunk-22/23/42〕
- **Ideology**：戒律/模因全套 ThoughtWorker 模板（§10）；`Precept_Ritual.ignoreExtremeTemperatures`；`ThoughtWorker_Precept_NoFish` 对接 waterBodyTracker——海洋戒律纯 XML。〔chunk-27/28〕
- **Biotech**：`GeneDef.waterCellCost`+`ignoreDarkness` 水栖/深潜基因现成钩子；Xenotype 三件套做"水生人种"纯 Def；机械族免呼吸确认。〔chunk-18/30/33〕
- **Anomaly**：实体全带 VacuumResistance=1 天然兼容；`Hediff_DarknessExposure` 黑暗撕咬（硬门控需自实现）；PitGate/PitBurrow 口袋地图+伏击=海沟深渊巢穴模板；`Command_CallBossgroup` 召唤 Boss="声呐信标召唤深海巨兽"直接复用。〔chunk-12/34/41/45〕
- **Odyssey**：除真空全家桶外——MapPortal 三件套（下潜舱/沉船入口）、`CompSealable` 弃舱封门流程、PocketMapProperties（独立 biome+温度的口袋小图=海底基地备选实现）、Gravship 打包搬迁（潜水母舰）、LandingOutcome 框架（潜水器降落事故）、Layout 布局家族（海底遗迹生成）。〔chunk-06/13/16/19/35/44〕

## 12. 待决策清单（v2）

已解决项：虫害（海底天然免疫，除非自写）；heater 语义（4°C 恒满效率，建议保留为玩法核心）。
剩余必须决策：

1. **方案 A/B 终选**：inVacuum 复用 vs 自建含水性系统（§0.1）——建议 A，接受换皮工作量。
2. **海格直建 vs 海底层**（§0.2）——建议直建，层方案留作深海扩展。
3. **建材气密与水下电力规则**：石墙/木墙是否隔水，以及普通电缆能否在海水中工作（前期开局关键；防水电缆 Def 仍待核对）。
4. **商队/访客对外通道**：潜艇载具 vs 纯空投。
5. **空投/贸易落点系统**：着陆台/浮筒/ patch DropCellFinder。
6. **海水地形标志组合**：是否带 `Water` tag（会接入游泳、钓鱼、避水寻路和灭火，也会被空投/大型建筑/路径生成排斥）以及是否设 `exposesToVacuum`（会接入真空暴露、强制黑暗和禁止 Filth）；两者必须分别决策，不能再依赖已删除的旧矩阵。〔chunk-08/09/15/30/32/33/37/43〕
7. **隔热语义**：接受真空的 5E-05 墙体导热还是 patch 成海水高导热。
8. **野生动物 VacuumResistance** 批量注入（方案 A 必做）。
9. **UI 文本换皮**范围（Vacuum→海水 全术语表）。
10. **幽闭症/黑暗心情**是保留为核心压力还是提供缓解手段（发光植物/观景窗？）。

## 13. 架构扩展性（终版）

穷举确认的 Def 驱动注入点全集：BiomeDef（20+ 环境字段）/ TileMutatorDef（五阶段管线+袭击黑名单+游戏条件）/ MapGeneratorDef（customMapComponents/isUnderground/mapClipperShader）/ BaseGen RuleDef（自定义 SymbolResolver）/ LayoutDef+LayoutWorker（POI 布局）/ PlanetLayerDef（六类白名单）/ IncidentDef+ArrivalModeDef（biome/层/科技过滤）/ GameConditionDef（层过滤+requireFish）/ ScenPart（ForcedMap/PermaGameCondition/DisableIncident）/ Pathing（PathGridDef.workerType 自定义寻路）/ ThoughtWorker/Precept（XML 心情）/ GeneDef（waterCellCost/ignoreDarkness）/ SoundParamSource（环境音效）。
本 Mod 的自定义部分（海水地形表、气密材质表、排水阀 comp、含水性 MapComponent）应同样参数化，避免硬编码 biome/地形名。
