# chunk-19 发现文档（组：RimWorld，P 字母段：PlaceWorker* / Planet* / Plant* / Precept* / Psychic* / QuestPart* 等 200 文件）

## TL;DR

- **植物已有完整的"环境致死"框架可直接类比海水**：`Plant.cs` 里 `DyingBecauseExposedToVacuum`（每 tick 伤害 = 1 × 格子真空度，阈值 ≥0.5）+ `PlantProperties.vacuumResistant` 抗性 flag，和"缺光缓慢枯死"（`dieIfNoSunlight`）、`destroyedByFlooding`（被淹没地形直接摧毁，`Verse/TerrainGrid.cs:512` 消费）——海底作物/野生海藻的全部钩子都已存在。
- **`PlanetLayerDef` 是"只刷特定事件/袭击/抵达方式"的官方白名单机制**：`onlyAllowWhitelistedIncidents / Biomes / GameConditions / Quests / Arrivals / ArrivalModes` + `isSpace` + `raidPointsFactor`，海底殖民地的事件过滤可以直接借这套（源：`PlanetLayerDef.cs`）。
- **水力发电机放置校验是水中建筑的直接模板**：`PlaceWorker_WatermillGenerator` 要求地基格 `TerrainAffordanceDefOf.Heavy`、水格 `MovingFluid`，并防多机水域重叠（`PlaceWorker_WatermillGenerator.cs`）。
- **`Biome.inVacuum` 是官方环境总开关，且在各系统散落判定点**：真空覆盖层 UI 开关只在 `Biome.inVacuum` 时显示（`PlaySettings.cs:183`）；真空 biome 里禁止越狱（`PrisonBreakUtility.cs:171`）。海底若复用/仿造该标记，需同类盘点连带影响。
- **Prefab 盖章系统是海底沉船/遗迹生成的现成工具**：`PrefabUtility.SpawnPrefab` 支持地形+Thing+HP+品质+阵营+蓝图+递归子预制件（`PrefabUtility.cs` / `PrefabDef.cs`）。
- **冲突点：极端温度会阻止仪式类行为**——`PsychicRitualRoleDef` 的 `ExtremeTemperature` 条件用 `SafeTemperatureAtCell` 判定（海底室外 4°C 会挡住户外灵能仪式）；`Precept_Ritual.ignoreExtremeTemperatures` 是绕过字段。

---

## 一、植物 × 海洋环境（本片最大收获）

### Plant.cs —— 环境致死框架（强相关，借用）
- `DyingBecauseExposedToVacuum`：`!def.plant.vacuumResistant && Position.GetVacuum(Map) >= 0.5f` 时植物进入垂死，每 tick 伤害 `1f * GetVacuum()`（`VacuumDamagePerTick = 1f`），生长速度直接归 0（`GrowthPerTick` 里同条件 return 0）。死亡消息有专用 key `MessagePlantDiedOfRot_ExposedToVacuum`。
- 类比：把"真空度"换成"海水淹没度"，`vacuumResistant` 换成"水生"flag，就是整套海水杀陆生植物的机制，连 UI 提示文案的挂法都现成。
- 光照线：`dieIfNoSunlight`（默认 true）让植物在缺光 450000 tick 后进入垂死（每 tick 至少 0.005 伤害）；`DyingBecauseExposedToLight`/`diesToLight` 反向（见光死，洞穴蘑菇用）。深海低光环境下两组 flag 都有用：普通作物慢慢枯死、深海发光植物可用 diesToLight 做趣味机制。
- 生长公式入口：`GrowthRateFactor_Light`（读 `glowGrid.GroundGlowAt`）、`GrowthRateFactor_Temperature`、`GrowthRateFactor_Fertility`，海底恒温 4°C 下大部分作物温度系数会被压到很低。

### PlantProperties.cs —— 数据字段（强相关，直接可用字段清单）
实际读到的字段：`vacuumResistant`、`destroyedByFlooding = true`（默认 true！地形变为 IsFlood 类型时被摧毁，消费点在 `Verse/TerrainGrid.cs:512`）、`showInFrozenWater`、`growMinGlow = 0.51f` / `growOptimalGlow = 1f`、`diesToLight`、`dieIfNoSunlight`、`mustBePermanentDarknessToSow`（永久黑暗才能种——深海作物的现成字段）、`cavePlant` + `cavePlantWeight`（洞穴植物生成权重，可类比"海底洞穴"）、`wildPlantUseDistanceToShore`（野生植物按到海岸距离分布——海洋相关原生机制）、`wildTerrainTags` / `terrainBlacklist`（地形 tag 白/黑名单）、`minGrowthTemperature` / `maxGrowthTemperature`、`fertilityMin`、`topWindExposure`。

### PlantUtility.cs —— 种植校验与生长系数（相关，公式模板）
- `CanEverPlantAt`：依次查肥力（`fertilityGrid.FertilityAt` < `fertilityMin` 拒）、地图温度极值（`TileInfo.Min/MaxTemperature`）、污染（`Pollution.CleanOnly/PollutedOnly`）、terraformable、地形 tag（`WildTerrainTags` 与 `terrain.tags` 求交）与黑名单、阻挡物、不可通行植物旁不许有门。
- `GrowthRateFactorFor_Temperature`：`[minGrowth, minOptimal]` 和 `[maxOptimal, maxGrowth]` 区间线性插值；海底 4°C 处于大多数作物 minGrowth(≈0°C) 边缘，可获得"能活但极慢"的自然效果。
- `GrowthRateFactorFor_Light`：`InverseLerp(growMinGlow, growOptimalGlow, glow)`。
- 结论：海底农场（海藻/发光菌）只需要新 ThingDef + 调整这些字段，不需要新代码。

## 二、行星层与抵达（Odyssey 白名单机制）

### PlanetLayerDef.cs（强相关，借用）
Odyssey 行星层定义（Surface/Orbit 两个实例见 `PlanetLayerDefOf.cs`）。关键字段：`onlyAllowWhitelistedIncidents / onlyAllowWhitelistedBiomes / onlyAllowWhitelistedGameConditions / onlyAllowWhitelistedQuests / onlyAllowWhitelistedArrivals / onlyAllowWhitelistedArrivalModes`（一整套白名单开关）、`isSpace`、`canFormCaravans`、`raidPointsFactor`（袭击点数系数）、`backgroundBiome`、`ignoreNoBuildArea`、独立 `worldGenSteps` 列表、`defaultBiome / defaultMapWorldObject / settlementWorldObjectDef`。
- 类比：海底 Mod 若把海底做成独立行星层（或模仿其结构），"只有工业以上袭击、只刷水生事件"可直接用白名单字段实现；`raidPointsFactor` 可调难度。即便不做独立层，这套字段清单也是"需要拦截哪些系统"的官方答案。
- 注意：`PlanetLayerSettings.cs` 只是渲染参数（半径/视角/相机偏移），无玩法。

### PlayerPawnsArriveMethod.cs（弱相关）
抵达方式枚举只有 `Standing / DropPods / Gravship` 三种（`PlayerPawnsArriveMethodExtension.cs` 给文案）。海底降落（潜水舱/潜航器）若做新抵达方式需扩展此枚举，并与上面的 `onlyAllowWhitelistedArrivalModes` 联动。

### PocketMapExit.cs（相关，借用）
`MapPortal` 子类，口袋地图（洞穴/坑洞）的出口：生成时经 `PocketMapUtility.currentlyGeneratingPortal` 与入口双向绑定，`GetOtherMap()/GetDestinationLocation()` 指回入口图，有"查看入口"Gizmo（`CameraJumper.TryJumpAndSelect`）。图标资源名是 `UI/Commands/ExitCave`。
- 类比：海底基地 ↔ 海面/潜艇的"舱门"可以直接复用 MapPortal/PocketMap 体系（Anomaly 坑洞、Odyssey 都在用），不必自研跨图传送。

## 三、水中/环境建筑放置校验（PlaceWorker 家族）

家族共 29 个，绝大多数是画放置预览圈（PASS）。有海洋价值的三个：

- **PlaceWorker_WatermillGenerator.cs（直接相关）**：水轮发电机放置校验——地基格每格需 `TerrainAffordanceDefOf.Heavy`，水格（`CompPowerPlantWater.WaterCells`）每格需 `MovingFluid` affordance，否则报 `MustBeOnMovingWater`；ghost 绘制时列出现有/蓝图中/建造中的水轮并检测 `WaterUseRect` 重叠（橙色闪烁提示）。这是"必须坐落在特定流体地形上"建筑的完整模板（校验+预览+互斥区），潮汐发电机可照抄结构。
- **PlaceWorker_Vent.cs（相关，类比借用）**：通风口要求两侧格（`center ± facing`）都非 Impassable；预览高亮两侧房间（`Room.UsesOutdoorTemperature` 的不高亮）。类比：海底"排水阀/均衡阀"若要连通两侧舱室做水体/气体交换，放置校验（两侧必须是可通行空间）就是这个模式。
- **PlaceWorker_WindTurbine.cs（冲突类）**：仅 13 行，画 `WindTurbineUtility.CalculateWindCells` 遮挡区。海底无风，风力发电应被禁用或改造为"洋流涡轮"（结构类比水轮）。

## 四、环境标记的全局开关与连带影响

- **PlaySettings.cs（相关）**：`showVacuumOverlay` 开关只在 `ModsConfig.OdysseyActive && Find.CurrentMap != null && Find.CurrentMap.Biome.inVacuum` 时出现在右下角开关栏，否则强制 false（183-190 行）。同栏还有 `showPollutionOverlay`（Biotech 门控）、`showTemperatureOverlay` 等。海底 Mod 做"海水覆盖层"UI 时挂这里；同时说明 `Biome.inVacuum` 是官方环境布尔标记。
- **PrisonBreakUtility.cs（冲突/参考）**：约 171 行，`pawn.Spawned && pawn.Map.Biome.inVacuum` 直接不允许越狱（真空中挖不出去）。启示：biome 级环境标记会被各系统分别查询并产生连带玩法效果；海底若引入 `inDeepSea` 类标记（或复用 inVacuum），需要像本调研一样全库盘点判定点。
- **PsychicRitualDef.cs:356（参考）**：`TraverseMode.NoPassClosedDoorsOrWater` —— 寻路 TraverseMode 原生就区分"不过水"，做水下寻路/两栖生物时注意这个枚举。

## 五、Prefab 盖章系统（海底遗迹/沉船生成）

- **PrefabDef.cs / PrefabThingData.cs / PrefabTerrainData.cs / PrefabParms.cs（相关）**：预制件 = Thing 列表（def/stuff/颜色/品质/HP/stackCount/chance/位置或矩形）+ 地形矩形（含颜色）+ 递归子预制件；`PrefabDefOf` 里 Odyssey 实例为 AncientUplink、SurveyScanner、CerebrexCore、CorridorBarricade。
- **PrefabUtility.cs（相关，直接可用）**：`SpawnPrefab` 支持阵营归属、按概率放置、蓝图模式（`PlaceBlueprintForBuild`）、`overrideSpawnData` 回调替换 def/stuff、`onSpawned` 回调；`CanSpawnPrefab` 逐格校验（递归深度上限 15）；`CreatePrefab` 可从地图上框选区域反向生成 PrefabDef（含地形）。海底沉船、废弃前哨站、热泉矿区都可用此系统盖章生成，且可用 CreatePrefab 在开发者模式下快速制作预制件。

## 六、仪式 × 温度（冲突类）

- **PsychicRitualRoleDef.cs**：`Condition.ExtremeTemperature = 0x80`，`PawnCanDo` 里若不允许该条件且 `!pawn.SafeTemperatureAtCell(target.Cell, map)`（仅对玩家派系 pawn 检查）则拒绝参与，文案 `PsychicRitualLeaveReason_ExtremeTemperature`。海底室外 4°C 对大部分种族不在安全温度内 → 户外灵能仪式会被系统性挡住，需要规划（室内仪式圈或给角色配温度耐受）。
- **Precept_Ritual.cs**：`ignoreExtremeTemperatures` 字段（默认 false，可序列化）——仪式定义层就可以声明无视极端温度，海底自定义仪式记得设置。

## 七、确认无海洋价值的主要家族（快速结论）

- **Precept / PreceptComp / PreceptWorker 全家（约 45 文件）**：Ideology 信条系统——服装要求、思想（Thought）、不愿做的事件、角色、圣物、仪式座位等，全部围绕文化/心情/服装，无环境交互。`PreceptDef.prefersDarkness` 是唯一沾边字段（黑暗模因偏好，深海叙事可呼应，但不构成机制借用）。
- **PsychicRitualDef_* 全家（约 18 文件）**：Anomaly 灵能仪式（血雨、洗脑、 Chronophagy、召唤血肉兽/蹒跚怪/坑门等），均为 Toil 装配代码，无环境钩子。`PsychicRitualDef_SummonAnimals` 召唤的动物种类不在此文件决定（在 Toil 里，本片外）。
- **Quest / QuestManager / QuestPart_*（约 28 文件）**：任务信号系统（inSignal/outSignal 状态机、袭击 lord 生成、摄像机跳转、警报等），纯任务编排，无海洋交互。
- **PowerNet / PowerNetGrid / PowerNetManager / PowerConnectionMaker / PowerNetGraphics / PowerNetMaker / PowerOverlayMats**：电力网络（电网泛洪建网、延迟注册、走线绘制），与水无任何交互（没有"电缆泡水短路"之类机制——短路事件在别处）。
- **Quality / Price / Pregnancy / Prisoner / Psycast / Quadrum / Portraits / Policy 等**：品质、价格、怀孕、囚犯交互、灵能施法 entropy、季节、头像缓存、策略基类——均与海洋无关。
- **Projectile_DoomsdayRocket / PowerBeam**：爆炸/轨道打击特效，无关。
- **Projectile_Liquid.cs（边缘相关，登记为 NOTE 见下）**：液体弹药命中后按 `filthChance` 铺污物、按 `terrainChance` 把地形替换为 `def.projectile.spawnTerrain`（可配是否覆盖地板），并对格内所有 Thing 造成伤害。类比：喷水/排污武器、或"地形改造弹"（把海床格变成别的地形）的实现参考。

## 八、其余 NOTE 级文件索引

| 文件 | 一句话 |
|---|---|
| `Projectile_Liquid.cs` | 液体弹药→铺 filth/改地形的实现参考（喷墨、排污、地形改造弹） |
| `PlayerPawnsArriveMethod.cs` | 抵达方式枚举仅 Standing/DropPods/Gravship，海底新抵达方式需扩展 |
| `PlanetLayerDefOf.cs` / `PlanetLayerSettingsDef(Of).cs` | 层实例仅 Surface/Orbit，自定义层的注册样式参考 |

*未验证项：Plant.cs 共 1240 行，本文只精读了环境致死/生长系数相关区段（经全文关键词定位），其余区段（落叶、雪覆盖渲染、抽穗等图形逻辑）仅扫过方法名。*
