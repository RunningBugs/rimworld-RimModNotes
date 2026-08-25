# chunk-45 调研发现（顶层杂项 + Verse.Glow + Verse.Noise + Verse.Sound + LudeonTK + Verse.Grammar）

本片覆盖：源码根目录散文件（Bossgroup 召唤、黑暗战斗、布局生成器 LayoutWorker 家族、机械族工具等）、Verse.Glow 光照系统、Verse.Noise 噪声库（libnoise 移植）、Verse.Sound 音频引擎、LudeonTK 开发工具、Verse.Grammar 文本语法引擎。共 220 个文件。

## TL;DR

1. **地形可以直接发光**（`Verse.Glow/GlowLight.cs`）：`TerrainDef.glowRadius` + `TerrainDef.glowColor` 让任意地形格成为光源，发光植物（`cavePlant`/`CompGlower`）同理 → 海底发光热泉、生物荧光地衣/植物的官方实现路径，零新系统。
2. **黑暗战斗判定依赖地图级天空光**（`DarknessCombatUtility.cs`）：室外亮/暗由 `map.skyManager.CurSkyGlow > 0.35f` 一刀切，文化 precept 给射击/近战命中/闪避挂 Lit/Dark 修正 → 海底永暗地图里这套加成永久生效，是做"深海暗猎"玩法或需要平衡屏蔽的直接挂点。
3. **环境声"Underground"参数源是水下闷音的完美模板**（`Verse.Sound/SoundParamSource_Underground.cs`）：地图 `generatorDef.isUnderground` 为真即输出 1，驱动 SubSoundDef 里的低通滤波/音量映射；配合 `SoundFilterLowPass.cs`（纯 Def 挂 Unity AudioLowPassFilter）→ "Underwater 声音参数源 + 全场低通闷音"几乎可以纯 Def + 一个小类实现。
4. **召唤 Boss 袭击机制全套 Def 驱动**（`Command_CallBossgroup.cs` + `CallBossgroupUtility.cs`）：建筑 comp（`CompProperties_Useable_CallBossgroup`）召唤 `BossgroupDef`，2 天冷却，自动做可达/通电检查 → 类比"声呐信标召唤深渊巨兽袭击"，直接复用。
5. **Odyssey 轨道平台布局生成器 = 海底设施/沉船遗迹模板**（`LayoutWorker_OrbitalPlatform.cs`、`LayoutWorker_Structure.cs`、`LayoutWorker.cs`、`LayoutWorkerComplex.cs`）：`StructureLayoutDef` 声明墙/门/地板/壁灯，`RoomLayoutGenerator.GenerateRandomLayout` 生成房间+走廊，Complex 版还带威胁点数生成守卫（机械族/虫族）→ 海底沉船、废弃海底站、遗迹+守卫威胁全可用。

---

## 1. 光照系统（Verse.Glow）——深海低光照的核心底层

### GlowLight.cs（NOTE，强相关）
- 光源结构体，两个构造函数揭示两种光源来源：
  - **CompGlower（Thing 光源）**：读 `Props.overlightRadius`、`GlowRadius`、`GlowColor`；`isCavePlant = Props.overrideIsCavePlant || parent.def.plant?.cavePlant` —— 植物 def 标 `cavePlant=true` 即成为"洞穴发光植物"。
  - **地形光源**：`TerrainDef.glowRadius` + `TerrainDef.glowColor` 非零即为光源，`isTerrain=true`。
- 海洋类比：**海床发光地形**（热泉口、发光菌毯）只需自定义 TerrainDef 填 glowRadius/glowColor；**发光水生植物**只需 plant def 标 cavePlant。深海"微光生态"不需要写新光照代码。

### ComputeGlowGridsJob.cs（NOTE）
- Burst 并行 job，对每个 dirty 光源做优先队列洪水填充：8 方向、直走代价 100/对角 141，`lightBlockers`（遮光格）阻挡；衰减公式为 `Lerp(1 - d/r, 1/d², 0.4)`。
- 海洋类比：光照穿墙/衰减行为全在这里；若想做"海水中光衰减更快/探照灯锥形照明"，需要 hook 或替换这套 job（未验证可否不打补丁扩展——光源半径/颜色走 Def，衰减曲线是硬编码常量 0.4 混合比）。

### GlowCell/GlowCellStatus/GlowCellComparer/GlowUniqueState/LocalGlowArea.cs（SKIP）
- 洪水填充的配套数据结构（距离场、状态枚举、比较器、每线程堆/缓冲区池），纯算法 plumbing。

## 2. 黑暗战斗（DarknessCombatUtility.cs，NOTE，强相关）

- 判定：`SkyGlowDarkThreshold = 0.35f`；`IsOutdoorsLit(map) = map.skyManager.CurSkyGlow > 0.35f`。
- "室外"定义：`roof.isNatural || roof.isThickRoof` 底下不算室外，否则看房间的 `PsychologicallyOutdoors`。
- 玩法挂钩：文化 precept 的 statOffsets 里有 8 个 stat（`ShootingAccuracyOutdoorsLitOffset`/`OutdoorsDarkOffset`/`IndoorsDarkOffset`/`IndoorsLitOffset`，近战命中/闪避同四件套），本类只负责在属性面板汇总显示；实际判定函数 `IsOutdoorsAndDark/IsIndoorsAndDark` 等是公共 API。
- 海洋类比：海底地图若 CurSkyGlow 恒低于 0.35（永暗），则"室外黑暗"战斗修正永久生效——既是特色（夜视装备/发光地形/照明弹价值放大），也是冲突点（暗处惩罚类 precept 永久惩罚，需平衡）；室内黑暗判定还联动 `DarklightUtility.IsDarklightAt`（暗光，Ideology）。

## 3. 音频系统（Verse.Sound）——水下氛围的实现路径

### SoundParamSource_Underground.cs（NOTE，强相关，直接模板）
- 17 行完整实现：`Find.CurrentMap.generatorDef?.isUnderground == true` 则返回 1 否则 0。该参数可在 SubSoundDef 的 `SoundParameterMapping` 里映射到音量/低通 cutoff 等目标。
- 海洋类比：**照抄一个 `SoundParamSource_Underwater`**（判断海底 MapGeneratorDef 标记），即可让全部/选定环境音在水下地图自动切换混音。

### SoundFilterLowPass.cs + ReverbSetup.cs + SoundFilterUtility.cs（NOTE）
- `SoundFilterLowPass` 是纯 Def 式滤波器：`cutoffFrequency`（50–20000）+ `lowpassResonaceQ`，`SetupOn` 给 AudioSource 挂 `AudioLowPassFilter` —— **水下闷音效果（高频衰减）零代码**，只需在 SoundDef 上挂 filters。
- `ReverbSetup` 封装 Unity AudioReverbFilter 全部参数（decayTime/density/diffusion 等），支持预设和 Lerp 混合。
- 类比：海底地图环境音 = 低通（闷）+ 定制混响（水下空间感），全部走现有 SoundDef 机制。

### SoundParamSource_OutdoorTemperature.cs（NOTE，冲突类）
- 环境音参数直接取 `map.mapTemperature.OutdoorTemp`。海底恒温 4°C 下，凡是按温度变化的环境音（如寒带风声）会恒定 → 需确认海底地图想呈现的温度参数是"水温"还是假温度。

### 其余 Verse.Sound 文件（SKIP/PASS）
- 音频引擎 plumbing：Sample/Sustainer/AudioSourcePool/SoundSlotManager/SubSoundDef 等，都是通用播放管线，与海洋无机制级类比。`SoundParamSource` 抽象基类（`ValueFor(Sample)` 单方法）已在上面条目覆盖。

## 4. 召唤 Boss 袭击（Command_CallBossgroup.cs + CallBossgroupUtility.cs，NOTE）

- 机制：机械师 gizmo → 列出所有 `BossgroupDef` → `BossgroupEverCallable` 检查：地图上存在对应"召唤器"建筑（带 `CompProperties_Useable_CallBossgroup` 且 bossgroupDef 匹配的 ThingDef）、可达、可预定、（若有 CompPowerTrader）通电；召唤走 `CompUsable.TryStartUseJob`。
- 冷却：`Find.BossgroupManager.lastBossgroupCalled`，120000 tick（=2 游戏日）内禁用；已有未生成 Boss 的 `QuestPart_Bossgroup` 时也禁用。
- 海洋类比：**"声呐信标/诱饵召唤深海巨兽（或海盗舰队）袭击"**可直接复用 BossgroupDef 管线，只需自定义召唤器建筑和 Boss 组成。与"只刷工业科技以上袭击"的设定兼容（Bossgroup 是任务袭击，不受常规袭击派系过滤影响——未验证常规过滤器细节，本片不含）。

## 5. 布局生成器（LayoutWorker 家族）——海底遗迹/沉船/设施生成

### LayoutWorker.cs（NOTE，基类管线）
- `GenerateStructureSketch` 管线：`GenerateSketch`（子类）→ ResolveRoomDefs → MergeAdjacentRooms → RemoveBorderDoors（按 `layoutDef.borderDoorRemoveChance`）→ FlushLayoutToSketch → 可选 `shouldDamage` 随机损坏。生成用 `MapGenerator.mapBeingGenerated?.NextGenSeed` 做随机种子。

### LayoutWorker_Structure.cs（NOTE，基类）
- `StructureLayoutDef` 全 Def 驱动：墙/门/壁灯/地板/默认 affordance 地形/外墙材料（`RandomCheapWallStuff`，notVeryFlammable=true）。`GenerateStructure` = GetStructureLayout + 图生成 + 重要房间 + CreateDoors。

### LayoutWorker_OrbitalPlatform.cs（NOTE，Odyssey 直接模板）
- 15 行：`RoomLayoutGenerator.GenerateRandomLayout(rect, 10, 10, 0.1f, canRemoveRooms: true, generateDoors: false, maxMergeRoomsRange: 2~4, corridorShapes, corridor, corridorExpansion: 2, canDisconnectRooms: false)`。
- 类比：太空站 = 海底站的结构孪生（都是"孤立人工密闭设施"），房间布局参数直接可抄。

### LayoutWorkerComplex.cs / _Ancient / _Mechanitor（NOTE）
- Complex 版在 Spawn 时按威胁点数（factor 0.25~0.35）从 `Def.threats`（ComplexThreatDef 列表）生成守卫；`_Ancient` 固定敌对阵营在虫族/机械族间随机，`_Mechanitor` 固定机械族并预置机械师尸体棺材（信号驱动）。
- 类比：海底遗迹综合体 + 沉睡机械守卫/虫巢，整套威胁布置管线可复用；威胁阵营可换自定义（如"沉眠的深海护卫机械"）。

### LayoutWorker_Labyrinth.cs（NOTE，弱相关）
- Anomaly 迷宫：整体换材质（GrayWall/GraySurface/LabyrinthMatter），Delaunator 三角剖分 + A* 连走廊，含 19×19 方尖碑房。
- 类比：海底洞穴迷宫可照抄生成算法、换材质 def。

### LayoutWorker_SimpleRuin / LayoutWorker_AncientStockpile（PASS）
- 简单废墟（无走廊无外门）、远古仓储（50% 门换成防爆门）——变体示例，确认机制同上，无新增海洋价值。

## 6. 其他

- **ResearchUtility.cs（NOTE，弱相关）**：`ApplyPlayerStartingResearch` 按玩家派系 def 的 `startingResearchTags`/`startingTechprintsResearchTags` 和主文化 meme 的 `startingResearchProjects` 送起始科技 → 海底 scenario 的自定义派系 def 可用 tag 直接送"潜水/气密"起始科技，纯 Def。
- **MechanitorUtility.cs / MechWorkUtility.cs（PASS）**：机械师带宽/控制组/工作类型工具，与海洋无机制类比（仅与"机械族袭击"题材沾边）。
- **Verse.Noise 全家 50 文件（SKIP）**：libnoise 移植（Perlin/RidgedMultifractal/Voronoi + Add/Blend/Select 等组合模块 + DistFromAxis 等星球工具），世界生成噪声纯数学库；海底地貌噪声理论上可用这些模块组合，但属通用工具，无机制发现。
- **Verse.Grammar 9 文件（PASS）**：规则文本生成引擎（RulePack/GrammarResolver），叙事文本用，与海洋机制无关。
- **LudeonTK 49 文件（SKIP/PASS）**：Dev 模式工具（DebugAction 属性、DevGUI、调试窗口、度量工具、NativePriorityQueue 等基础设施），运行时玩法无关。
- **HistoryEventUtility.cs（PASS）**：杀"无辜动物"判定（文化 history event 用），逻辑含 Scaria/捕猎豁免，无海洋类比。
- **Thought_FoodEaten.cs（PASS）**、**JobDriver_Breastfeed*（PASS）**、**ScenarioUtility.cs（PASS）**、**Screen_ArchonexusSettlementCinematics.cs（PASS，Archonexus 结局过场黑屏+文字）**、**Delay.cs（PASS，协程延迟工具）**、**FleckUtility.cs（PASS，Fleck 并行池）**、**PerformanceBenchmarkUtility.cs（PASS）**、**DevWindowDrawing.cs（PASS）**。
- **生成代码（SKIP）**：`-BurstDirectCallInitializer.cs`（Burst 直接调用初始化）、`__JobReflectionRegistrationOutput__*.cs`、`UnitySourceGeneratedAssemblyMonoScriptTypes_v1.cs`（80994 行，Unity 生成）、`WeaponClassDef/WeaponClassPairDef`（空/近空 Def 类）。
