# chunk-07 调研发现（组：RimWorld，Comp S–W / 杂项 C–D 家族）

## TL;DR

1. **排水泵直接模板**（`CompTerrainPump.cs` / `CompTerrainPumpDry.cs`）：半径随天数扩张（`daysToRadius`）、`CompTickRare` 逐格 `AffectCell`、地形沿 `TerrainDef.driesTo` 链逐级抽干，SeaIce 生物群系特例直接转 Ice。海洋 Mod 的"排水阀/抽干海水格"可整套借用。
2. **潜艇降落验证模板**（`Designator_MoveGravship.cs`）：Odyssey 重力船落点校验——至少一格不得是 `TerrainDefOf.Space`（"必须与陆地相连"）、屋顶/迷雾/landingBlockers 阻挡、地形须能通过 `GenConstruct.CanBuildOnTerrain(TerrainDefOf.Substructure, …)`。改成"对接海床/不得落在深渊"即是潜水器降落逻辑。
3. **黑暗隐身猎手与光照联动**（`CompSightstealer.cs`）：侦测半径 = 14 × `Lerp(0.33, 1, glowGrid.GroundGlowAt)` × 视力能力——黑暗中侦测半径压到 1/3。深海低光照环境下"隐身水生掠食者"的现成公式。
4. **海床深层采矿**（`DeepDrillUtility.cs`）：`deepResourceGrid` 21 格扫描找矿，无矿时回落 `GetBaseResource`：`map.Biome.hasBedrock` 为假则无基岩资源；`RockForTerrain` 用地形反查岩石 def。海床生物群系要设好 `hasBedrock` 与基岩地形→矿石映射。
5. **地基/足迹网格**（`CompSubstructureFootprint.cs`）：Odyssey 新增 `map.substructureGrid`，建筑 spawn/despawn 时 `MarkDirty()`，可叠加显示地基范围。海底"耐压壳地基"可仿此做独立网格。
6. **地形转化泵**（`CompTerraformer.cs`）：按 `secondsPerConvert` 节奏把半径内地形转成目标地形，受 `terrain.canEverTerraform` 门槛限制。海床改造（如把海水格固化成可建地基）的另一条路径，比 TerrainPump 的 driesTo 链更直接。
7. **4°C 恒温下的食物腐败**（`CompTemperatureRuinable.cs`）：腐败按"超出安全温区的度数 × 每度速率"累计；4°C 恰在冷藏区，户外散落食物几乎不腐——需在设计上决定是否接受"海水即冰箱"。
8. **温度目标控制**（`CompTempControl.cs`）：暖炉/冷气的目标温度 gizmo 与高低功率状态字段；海底殖民地取暖全靠它，机制可直接复用无需改动。
9. **虫巢繁殖的屋顶要求**（`CompSpawnerHives.cs`）：子巢选址强制 `c.Roofed(map)`、全图巢上限 30、繁殖速率随邻近巢数曲线衰减。类比：海底生物巢穴"只在有遮蔽（气密/岩顶）处繁殖"。

## 主题分类

### 排水 / 地形改造（强相关）

- **`CompTerrainPump`**（抽象基类，106 行）：`progressTicks` 每 `CompTickRare` +250，`CurrentRadius = min(radius, ProgressDays/daysToRadius * radius)`，半径内每格调抽象 `AffectCell`。选中时画半径环。这就是潮湿泵的引擎，海洋 Mod 的排水建筑几乎可以原样继承。
- **`CompTerrainPumpDry`**（74 行）：`AffectCell` 同时处理顶层与底层地形（`TopTerrainAt`/`UnderTerrainAt`），`GetTerrainToDryTo` 读 `terrainDef.driesTo`；**SeaIce 生物群系特例一律转 `TerrainDefOf.Ice`**——说明"某生物群系下排水结果不同"是官方已有写法，海洋群系可以照此定义自己的 driesTo 终点。
- **`CompTerraformer`**（130 行）：一次性转化型（`secondsPerConvert` 后逐格 `convertTerrainDef`），`CanEverConvertCell` 拒绝有 edifice 的格与 `terrain.canEverTerraform == false` 的地形。若想让"海水格"可被转化成海床地基，给水地形 def 置 `canEverTerraform` 即可走这条路。
- **`CompSnowExpand`**（96 行）：虽然不是水，但它是"网格数值扩散"的完整范本——室外温度 >10°C 清零、半径分段减速增长（8/17/30 格分段）、FloodFill 限制在被墙围住的连通域内、Perlin 噪声决定 `snowGrid.AddDepth` 深度。海底"淤泥/藤壶/珊瑚沉积扩散"可照抄这个结构（含 `reachableCells` 静态集合复用的性能写法）。

### 降落 / 地基（Odyssey 类比）

- **`Designator_MoveGravship`**（263 行）：落点校验逻辑完整可读——逐格 `IsValidCell`（出界、no-build 边缘、`map.landingBlockers`、屋顶、迷雾、`thing.def.preventGravshipLandingOn`、地形须可建 Substructure），外加全局规则"至少一格非 `TerrainDefOf.Space`"否则报 `GravshipMustBeConnectedToLand`。潜水器/着陆舱在海床降落的所有校验点这里都有对应物。
- **`CompSubstructureFootprint`**（76 行）：维护 `map.substructureGrid` 脏标记 + 可选地基范围 overlay 开关 gizmo，与 `CompGravshipFacility.CanBeActive` 联动判 `Valid`。海底基地的"压力壳地基网格"需要同样的 spawn/despawn 脏标记模式。

### 光照 / 隐蔽（深海低光照）

- **`CompSightstealer`**（146 行）：隐身通过 `HediffComp_Invisibility` 实现；`GetPawnSightRadius` 是基础 14 × 光照插值（黑暗降到 0.33 倍）× `PawnCapacityDefOf.Sight` 等级；`genes.AffectedByDarkness` 为假的 pawn 不受黑暗惩罚。被发现后 1200 tick 内无法再隐身。深海图整体 `GroundGlowAt` 偏低时，此公式天然让"黑暗猎手"更难被发现——直接可用于深海袭击生物；反过来玩家的灯/照明弹成为防御刚需。

### 温度（4°C 恒温）

- **`CompTempControl`**（143 行）：目标温度 gizmo（±1/±10/重置 21°C）、`operatingAtHighPower` 区分功耗档位。海底场景下户外恒 4°C，室内取暖全靠此类建筑，机制无需改。
- **`CompTemperatureRuinable`**（100 行）：`AmbientTemperature` 超出 `[minSafeTemperature, maxSafeTemperature]` 才累计腐败度，速率与超出的度数成正比；合并堆叠时按数量 lerp 腐败度。4°C 户外 = 天然冷藏：设计决策点——要么接受，要么给"浸泡在海水中的物品"另做变质途径。

### 刷怪 / 巢穴（只刷水生生物、袭击生态）

- **`CompSpawnerHives`**（216 行）：`MaxHivesPerMap = 30`；子巢选址 `CanSpawnHiveAt` 强制 `Roofed`、Walkable、距母巢最小距离、邻近 8 格无其他巢；繁殖间隔按 `ReproduceRateFactorFromNearbyHiveCountCurve` 与难度 `enemyReproductionRateFactor` 缩放。海底版"巢穴"（如深渊虫巢）可借用，屋顶要求可换成"气密/岩层遮蔽"。
- **`DeepDrillUtility`**（84 行）：深钻井找矿——`NumCellsToScan = 21` 径向扫 `deepResourceGrid`，无矿格时用 `GetBaseResource`：`biome.hasBedrock == false` 直接返回 null（无基岩无保底矿）；否则 `RockForTerrain(BaseTerrainAt)` 或按格哈希从 `NaturalRockTypesIn(tile)` 随机。海底群系必须决定 `hasBedrock` 取值与基岩地形，否则深钻保底矿不工作。
- **`DeepDrillInfestationIncidentUtility`**（21 行）：收集玩家拥有的 `CompCreatesInfestations.CanCreateInfestationNow` 建筑供虫灾事件选用——海床钻井要不要触发"深渊生物涌出"事件，挂这个 group 即可。
- **`CompSpawner` / `CompSpawnerPawn` / `CompSpawnerItems`**：通用周期刷物/刷 pawn 组件，`spawnMaxAdjacent`、`maxSpawnedPawnsPoints`、`enemyReproductionRateFactor` 缩放等字段齐全，做"海底资源喷口/鱼群刷新点"的通用底座。无特殊海洋语义，登记为 PASS 但列出备查。

### 污染（如海洋 Mod 涉及）

- **`CompToxifier`**（190 行）：`pollutionIntervalTicks` 到点后在半径内找 `CanPollute` 的格批量 `Pollute`。若海水格被污染系统视为不可污染，需要检查 `CanPollute` 的判定（不在本片范围，未验证）。

### 确认无关的家族

- CompUseEffect_*/CompTargetEffect_*/CompTargetable_*（约 45 个）：道具使用效果框架（精神震荡、复活、植入、变血肉兽等），与海洋无任何类比点。
- Anomaly 家族（CompStudiable*、CompStudyUnlocks*、CompVoidNode、CompVoidStructure、CompSpawnsRevenant、CreepJoiner*、DelayedMetalhorrorEmerger 等）：虚空巨石结局与研究解锁，海洋 Mod 不打算接 Anomaly 体系则无关。
- Designator_* 大部分（区域涂画、允许区、居住区、除雪、油漆、计划等）：通用 UI，无关。
- 纯数据/枚举/DefOf 列表：见覆盖表 SKIP 项。
