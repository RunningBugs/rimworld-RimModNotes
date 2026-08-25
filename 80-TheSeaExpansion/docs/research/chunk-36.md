# chunk-36 调研发现（Verse 组：Pawn*–SpecialThingFilter* 共 250 文件）

## TL;DR

1. **`Room.Vacuum` 整套真空机制就在 Room 类里，是"房间海水度"的直接模板**（`Room.cs`）：每房间一个 0..1 浮点 `vacuum`、`ExposedToSpace` 判定（`Map.Biome.inVacuum` + 触图缘/暴露格）、变更时向房内所有 ThingWithComps 广播 `"Vacuum"` 信号。配套：`RegionAndRoomUpdater` 房间重建时同步温度+真空、`RegionDirtyer` 里有 `map.TemperatureVacuumCache` 和 `map.GetComponent<VacuumComponent>()`（真空是独立 MapComponent）、`Region.cs:434` 用 `room.Vacuum > 0.5f && p.ConcernedByVacuum` 做区域危险判定。
2. **温度模拟已内置"真空/深部"分支，海底恒温（约4°C）可直接挂在这套逻辑上**（`RoomTempTracker.cs`）：真空生物群系下墙体/薄顶均衡系数压到 5E-05（几乎不导热）；厚顶房间向 15°C 深部均衡（`DeepEqualizationTempChangePerInterval`，地下图 0.002、普通 5E-05）；`UsesOutdoorTemperature` 判定（无顶格≥25%或触图缘）。海底 mod 可仿照 inVacuum 加"inDeepSea"分支，把深部均衡目标温度改成 4°C。
3. **深海低光照有两条现成路线**（`SkyManager.cs`、`SkyTarget.cs`、`SectionLayer_Darkness.cs`）：a) `BiomeDef.disableSkyLighting` 直接关掉全图天光（SkyManager 里特判，连 FogOfWar 颜色都换掉）；b) GameCondition/天气/`CompAffectsSky` 通过 `SkyTarget.LerpDarken`（只向更暗插值）压暗——日蚀/非自然黑暗走的就是这条。Anomaly 的 `SectionLayer_Darkness` 是逐格黑暗遮罩渲染层（`gameConditionManager.DarknessVisible` + `MapBrightness`），深海"探照灯照亮周围"效果可仿写。
4. **水生单位支持在 Verse 层已内建**（`PawnWaterRippleMaker.cs`、`RaceProperties.cs`、`Reachability.cs`）：`pawn.Swimming` 状态、水面涟漪 fleck；`RaceProperties.waterSeeker` / `waterCellCost`（水生生物寻路代价字段）——"只刷水生生物"可直接用这些字段筛选；`TraverseMode.NoPassClosedDoorsOrWater` / `PassAllDestroyableThingsNotWater` 水感知可达性。
5. **1.6 已有完整水体渲染管线**（`SectionLayer_Watergen.cs`、`ShaderDatabase.cs`、`ShaderPropertyIDs.cs`）：`TerrainDef.waterDepthMaterial`、独立 `SubcameraDefOf.WaterDepth` 子相机层、`TerrainWater`/`MoteWater` shader、`_WaterCastVectSun/Moon`、`_WaterReflectionTex` 等全局参数（SkyManager 每帧写入日光/月光向量）。`PollutionGrid` 还引用了 `map.waterBodyTracker.Notify_PollutionChanged`——存在水体追踪器。
6. **Pocket Map（口袋地图）是海底殖民地的备选实现路径**（`PocketMapProperties.cs`、`PocketMapUtility.cs`）：口袋地图可带独立 `biome`、`temperature`、`tileMutators`，一行 `GeneratePocketMap` 即可生成。海底基地可以做成"从洋面世界格进入的 pocket map"，绕开主图改造。
7. **深度网格模板：SandGrid/SnowGrid**（Odyssey 沙层，0..1 深度 NativeArray、按地形 `holdSnowOrSand` 过滤、深度变化联动路径代价重算）——海底淤泥/沉积物覆盖层可整体仿写，渲染层 `SectionLayer_Sand/Snow` 配套。

## 主题分类详录

### 一、真空 → 海水（最核心发现）

- **`Verse/Room.cs`**：
  - `private float vacuum`，`public float Vacuum` 属性：非 `inVacuum` 生物群系恒返回 0；`ExposedToSpace`（触图缘或暴露格>0）时恒 1；门洞房间取相邻房间真空均值；≤0.01 视为 0。setter 里 `Mathf.Clamp(value, -0.1f, 1f)`（`MinVaccum = -0.1f`，负值可能是"被抽气"状态）并向房内每个 ThingWithComps 广播 `VacuumChangedSignal = "Vacuum"`。
  - `vacuumOverlayDrawn` / `vacuumOverlayRects` 字段说明真空有专门的覆盖渲染（绘制代码在 RimWorld 侧，本片未见）。
  - `districts[i].ExposedVacuumCount(threshold)`（Room.cs:584）：按区(District)统计暴露格数。
  - **类比结论**：海水度 = 把 `inVacuum` 换成 `inDeepSea`、把"真空泵"换成"排水泵"即可整套搬。广播信号机制可让建筑（如耐压门、泵）即时响应海水侵入。
- **`Verse/Region.cs`**：`Allows()` 里按 TraverseMode 区分水格通行（NoPassClosedDoorsOrWater / PassAllDestroyableThingsNotWater）；`:434` 区域对 pawn 的危险判定含 `room.Vacuum > 0.5f && p.ConcernedByVacuum` → 海水房间的危险警告/AI 回避可走同一入口。
- **`Verse/RegionDirtyer.cs`**：格子变脏时调用 `map.TemperatureVacuumCache.TryCacheRegionTempInfo` / `ResetCachedCellInfo`，并 `map.GetComponent<VacuumComponent>().SetDrawerDirty()` —— 证明真空系统有独立 MapComponent（绘制器）+ 温度/真空联合缓存（均在 RimWorld 侧，未在本片）。
- **`Verse/RegionAndRoomUpdater.cs`**：房间重建流程 `NotifyAffectedDistrictsAndRoomsAndUpdateTemperatureVacuum()`，`map.TemperatureVacuumCache.ResetTemperatureCache()` —— 气密/海水状态是随房间系统全自动重算的，mod 不需要自己维护连通性。
- **`Verse/RegionType.cs` / `RegionTypeUtility.cs`**：区域类型 `Portal`（门，单间区）、`ImpassableFreeAirExchange`（不可通行但通气——如栅栏/隔栏）、`Fence`。`GetExpectedRegionType` 按门/栅栏/可行走/Fillage 判定。**气密隔绝的根基**：是否通气由区域类型决定，海水版可复用（防水门=Portal 变体，格栅=自由水交换）。

### 二、温度 → 海底恒温 4°C

- **`Verse/RoomTempTracker.cs`**（全文精读）：
  - 均衡四路：墙体（`WallEqualizeFactor=0.00017`，每 tick 抽 20% 均衡格循环采样）、薄顶（5E-05）、无顶（0.0007）、厚顶深部（`DeepEqualization`，目标温度硬编码 **15°C**，`Map.generatorDef.isUnderground` 时系数 0.002 否则 5E-05）。
  - 真空分支：`Map.Biome.inVacuum` 时墙体均衡差值乘 5E-05（真空不导热，只有接触传热）；`VacuumEqualizeFractionPerTick = 5E-05`。
  - `UsesOutdoorTemperature`（Room.cs）：触图缘或无顶≥25% → 直接用室外温度。
  - **类比结论**：海底室外恒温 4°C 可由 mapTemperature/biome 层提供（本片外的 MapTemperature 负责室外温度）；室内保温现成。注意"厚顶→15°C"是硬编码，海底岩层下房间会向 15°C 回归——要么 patch 这个 15，要么海底地图干脆不用厚顶。
- 冲突点：`TempDiffFromOutdoorsAdjusted` 对 |ΔT|>100 的温差做 5 倍放大——海底一般用不到，无碍。

### 三、光照 → 深海黑暗

- **`Verse/SkyManager.cs`**：`map.Biome.disableSkyLighting` 为真时直接把 LightOverlay 设为透明黑并换 FogOfWar 颜色——**最省事的深海全局黑暗开关**。否则正常走天气+GameCondition+`CompAffectsSky`（ThingRequestGroup.AffectsSky 的 Thing  comp）合成 `SkyTarget`。每帧向 shader 写 `_WaterCastVectSun/_WaterCastVectMoon`（水面焦散方向）。
- **`Verse/SkyTarget.cs` / `SkyColorSet.cs`**：`SkyTarget.LerpDarken` 各项取 `Min(A,B)`——只会变暗不会变亮，正是"永久阴暗环境叠加临时事件"的语义。
- **`Verse/SectionLayer_Darkness.cs`**（Anomaly）：`Visible` 要求 `gameConditionManager.DarknessVisible`；`Regenerate` 用 `glowGrid.GroundGlowAt(c, ignoreSky: true)` 逐格算光，9 顶点平滑插值出黑暗遮罩；挡光建筑（`def.blockLight` 的 edifice）会从邻格借光。深海"室外全黑、灯光晕开"可直接仿这层。
- **`Verse/SectionLayer_Gas.cs`**：`map.gasGrid` 的逐格密度云渲染（`AnyGasAt`/`DensitiesAt`，顶点位移噪声）。**类比**：若把"海水"做成类气体的逐格密度（而非房间制），渲染层模板在此；逻辑网格 gasGrid 在本片外。
- **`Verse/SectionLayer_LightingOverlay.cs`**：主光照叠加层（未深读，PASS 级）。

### 四、水与水生单位

- **`Verse/PawnWaterRippleMaker.cs`**：`pawn.Swimming`（游泳中，20 tick 一次涟漪）或"移动中+脚下 `terrain.IsWater`"（25 tick）→ `FleckMaker.WaterRipple`。证明 Verse 层已有游泳状态和 IsWater 地形判定。
- **`Verse/RaceProperties.cs`**：`waterSeeker`（寻水？）、`int? waterCellCost`（该种族在水格的寻路代价）——**筛选"水生袭击者/动物"的现成字段**。
- **`Verse/Reachability.cs`**：多处特判 `TraverseMode.NoPassClosedDoorsOrWater` / `PassAllDestroyableThingsNotWater`（`:318` 处 `c.GetTerrain(map).IsWater` 直接挡洪水填充）。
- **`Verse/ProjectileProperties.cs` / `Projectile_Explosive.cs`**：`postExplosionSpawnThingDefWater`（爆炸落在水格时改生成的 ThingDef）、`spawnTerrain`/`terrainChance`/`terrainReplacesFloors`（爆炸改地形）——水下武器/深水炸弹的差异化效果钩子已存在。
- **`Verse/SectionLayer_Watergen.cs`**：继承 `SectionLayer_Terrain`，用 `terrain.def.waterDepthMaterial` 画到 `SubcameraDefOf.WaterDepth` 层——1.6 的水深渲染是独立子相机+独立材质通道。
- **`Verse/ShaderDatabase.cs` / `ShaderPropertyIDs.cs`**：`TerrainWater`、`MoteWater` shader；`_WaterOutputTex/_WaterOffsetTex/_WaterReflectionTex` 等——水体渲染是屏幕空间反射+扰流方案，深海"头顶水面"效果可复用这套 shader 参数。
- **`Verse/PollutionGrid.cs`**：`CanPollute` 走 `terrain.canBePolluted`；污染变化通知 `map.waterBodyTracker?.Notify_PollutionChanged`——存在 WaterBodyTracker（本片外，RimWorld 侧），水体是独立追踪对象。
- **`Verse/PollutionUtility.cs`**：`GrowPollutionAt` = Perlin 噪声边缘 + 优先队列的斑块扩散算法，`PolluteMapToPercent` 按百分比整图铺设。**类比**：海底"淤泥扩散"、"泄漏污染"、甚至"海水灌入破洞的逐格蔓延"都可抄这个生长算法。
- **`Verse/PersistentDangerSource.cs`**：把 `terrain.dangerous` 的格子打进常驻危险位图 → 海水地形标 `dangerous` 即被危险系统识别。
- **`Verse/PerceptualSource.cs`**：`terrainDef.extraNonDraftedPerceivedPathCost` / `extraDraftedPerceivedPathCost` 进感知路径代价 → 让 AI 袭击者天然避开水格/深水格，只需地形 def 字段。

### 五、Pocket Map / 地图结构

- **`Verse/PocketMapProperties.cs`**：`biome`、`tileMutators`、`temperature`、`destroyOnParentMapAbandoned`、`canLaunchGravship`、`canBeCleaned`——口袋地图可以有独立生物群系和固定温度，海底基地"独立 4°C 小图"方案的技术基础。
- **`Verse/PocketMapUtility.cs`**：`GeneratePocketMap(size, generatorDef, extraGenSteps, sourceMap)` 创建 PocketMapParent 世界对象并生成图；`DestroyPocketMap` 销毁。API 极简。
- **`Verse/Root_Play.cs` / `Root.cs` / `Root_Entry.cs`**：游戏根节点（PASS，无海洋价值）。

### 六、地表覆盖/装饰（淤泥类比）

- **`Verse/SandGrid.cs`**：Odyssey 沙层——`NativeArray<float>` 0..1 深度、`CanHaveSand`（地形需 `holdSnowOrSand`、非全填充建筑）、深度跨档时 `RecalculatePerceivedPathCostAt`（走 `WeatherBuildupUtility.GetBuildupCategory` 分档）。存档用 ushort 压缩。**海底淤泥层整体仿写点**。
- **`Verse/SnowGrid.cs`**：同款雪层（本片仅扫读，结构同上）。
- **`Verse/SectionLayer_Sand.cs` / `SectionLayer_Snow.cs`**：对应渲染层（基于 `SectionLayerGeometryMaker_Solid` 基础网格），PASS。
- **`Verse/ScatterableDef.cs` / `SectionLayer_TerrainScatter.cs`**：地形表面随机散布装饰纹理（入 `TextureAtlasGroup.Terrain` 图集）——海底珊瑚碎屑、贝壳、结核等地表装饰走这套。
- **`Verse/ScattererValidator_TerrainDef.cs`**：撒布校验"只在指定地形上"（`terrainDef` 字段）；`ScattererValidator_Buildable.cs`：要求地形 `affordance`。**海底地图生成时限定撒布到海底地形的现成校验器**。其余 ScattererValidator_*（避开特殊 Thing/屋顶/已用矩形等）为通用校验，PASS。

### 七、其余读过但无海洋价值的家族（PASS/SKIP 摘要）

- 屋顶系统（RoofGrid/RoofDef/RoofUtility/RoofCollapse*）：屋顶坍塌/支撑判定，海洋语境顶多类比"耐压壳"，机制本身无复用价值。
- 区域/可达性基础设施（RegionGrid、RegionLink*、RegionMaker、RegionTraverser、RegionProcessor*、Reachability* 其余、RegionAndRoomQuery）：通用泛型设施，间接支撑气密系统但无直接发现。
- 射击/投射物其余（Projectile 基类、ShootLine、ShotReport、ShootTuning、Skyfaller*）：无水分支（Projectile.cs 全文 grep 无 water/vacuum 命中）。
- Scribe_*（13 个序列化文件）、存档/读档、Prefs、Profiler、随机数、SimpleCurve/UI、SectionLayer 其余渲染层、Room 角色/属性（RoomRole*/RoomStat*）、配方/研究 Def、ReorderableWidget 等：均与海洋无关。

*未验证项：`waterBodyTracker`（WaterBodyTracker）、`VacuumComponent`、`TemperatureVacuumCache` 的具体实现均在 RimWorld 程序集，本片只有调用点，需在其他分片确认。*
