# chunk-22 调研发现（RimWorld 组：Ritual* 全家 + Road/River + RoomContents/RoomLayout 结构生成）

## TL;DR

1. **结构生成器已内建"真空暴露地形"感知**（`RoomContentsWorker.cs:452`）：遗迹内容放置时，若格子地形 `exposesToVacuum` 则只放有屋顶的格子。→ 海洋 Mod 若定义"暴露于海水"的地形属性，沉船/海底遗迹生成可直接照搬此判定。
2. **BiomeDef.inVacuum 分支模式**（`RoomContents_InnerCourtyard.cs`、`RoomContents_NarrowHalls.cs`）：`map.Biome.inVacuum ? TerrainDefOf.Space : TerrainDefOf.PackedDirt`。→ 海底 Biome 可加同类 flag，供一切生成代码分支（露天区放"海床沙"而非土）。
3. **道路生成显式处理海洋地形**（`RoadDefGenStep_Place.cs:54-61`）：桥面道路会把 `WaterOceanDeep` 降为 `WaterOceanShallow` 再铺桥；`TerrainDef.bridge`/`isFoundation` 在此被消费。→ 海洋格若允许道路穿过会产生桥，Mod 需在世界生成层决定海洋格是否通路。
4. **仪式参加有极端温度硬检查**（`RitualRoleAssignments.cs:622`）：目标格温度不在 pawn `SafeTemperatureRange` 内则拒绝参加（除非 RitualPatternDef `ignoreExtremeTemperatures`）。海底恒温 4°C 时户外仪式可能集体被拒 → 冲突类，需在 Mod 的仪式 pattern 上豁免或保证仪式点在气密室内。
5. **通用房间布局/内容生成工具链全部 public static 可复用**（`RoomLayoutGenerator.cs`、`RoomGenUtility.cs`、`RoomContentsWorker.cs`）：随机房间切分、走廊形状（直/十字/T/H）、门位生成、贴边摆货架、密封箱、休眠威胁集群。→ 海底沉船/遗迹 POI 生成的现成骨架。
6. **Odyssey 遗迹的真空主题战利品/生命维持范例**（`RoomContents_TransportRoom.cs` 生成 Vacsuit 战利品；`RoomContents_Orbital_Corridor.cs` 生成 LifeSupportUnit 墙挂）→ 海底版直接类比：潜水服战利品 + 氧气/生命维持装置。

## 主题一：仪式系统（Ritual*，约 110 个文件，整体 PASS）

本片大头是 Ideology 仪式系统：OutcomeComp（质量因子）、EffectWorker（结算）、Position（站位）、Role（角色筛选）、Stage（阶段动作）、TargetFilter（目标过滤）。逐类读完，绝大多数是纯社交/文化机制，与海洋语境无任何交集。有类比价值的只有三处：

### 1.1 极端温度拒绝参加（冲突类）——RitualRoleAssignments.cs:622

`PawnNotAssignableReason` 末尾硬检查：

```csharp
else if (ritualTarget.IsValid && (ritual == null || !ritual.ignoreExtremeTemperatures) && !p.SafeTemperatureRange().IncludesEpsilon(ritualTarget.Cell.GetTemperature(ritualTarget.Map)))
    return "MessageRitualWontAttendExtremeTemperature";
```

结论（读出）：除非仪式 def 设了 `ignoreExtremeTemperatures`，否则 pawn 不会去温度超出自身安全范围的仪式点。海底"室外"≈4°C 恒温，落在人类安全温度范围内的具体边界**未验证**（SafeTemperatureRange 由 StatDef/种族决定，本片无此代码）；若 4°C 出界，所有户外仪式点对未穿暖的殖民者不可用。Mod 对策：仪式点强制放气密室内，或给自定义 RitualPatternDef 加 `ignoreExtremeTemperatures`。

### 1.2 室内/房间状态加成（借用类）——RitualOutcomeComp_Indoors.cs、RitualOutcomeComp_RoomStat.cs

- `RitualOutcomeComp_Indoors.Count` 用 `room.PsychologicallyOutdoors` 判断仪式点是否"室内"给质量加成。
- `RitualOutcomeComp_RoomStat` 按房间统计值（观感/空间等）走曲线给质量。

结论：这两类只依赖 Room 系统，海底殖民地房间系统不变则自动正常工作，无需改动；反而说明"气密室=唯一舒适区"的玩法会自然传导到仪式质量上。

### 1.3 仪式 pattern 的层/科技过滤（借用类）——RitualPatternDef.cs

字段：`layerWhitelist`/`layerBlacklist`（List\<PlanetLayerDef\>）、`minTechLevel`/`maxTechLevel`、`ignoreExtremeTemperatures`、`CanFactionUse(faction)` 按科技水平过滤。

结论：若海底参考 Odyssey 做成独立 PlanetLayer，仪式可按层白/黑名单整体屏蔽或专属；科技门槛模式（`CanUseWithTechLevel`）与"只刷工业以上袭击"的需求同构，可参考其判定写法。

### 1.4 Odyssey 起飞仪式（弱借用）——RitualOutcomeEffectWorker_GravshipLaunch.cs、RitualOutcomeComp_GravshipFacilities.cs

- GravshipLaunch：仪式 progress<1 被打断则发负面消息取消；成功时把 `quality` 写入 `LaunchInfo`，并用 `GravshipUtility.NegativeLandingOutcomeFromQuality(quality)` 随机决定降落事故。→ "仪式质量→交通风险"模式可类比"潜水钟/潜艇下潜仪式"。
- GravshipFacilities：遍历引擎 `GravshipComponents`，只统计通电且可用的设施给质量加成。→ 若做"下潜准备仪式"，可同样遍历潜水器组件。

## 主题二：世界地图 河/路（River*/Road*）

- `RiverDef`/`RiverNode`：纯数据（分支/流量阈值/宽度），无行为代码，SKIP。
- `RoadDef`：数据 Def（`movementCostMultiplier`、`pathingMode`、渲染层宽缓存），PASS。
- `RoadDefGenStep_DryWithFallback`：沿路把带 `driesTo` 的地形逐级"抽干"，抽不干且不可通行/是河流则铺 fallback。与海洋弱相关（`driesTo` 链是官方"排水"数据模型，潮湿泵同族），PASS。
- `RoadDefGenStep_Place`（NOTE）：桥梁地形的特判——`terrainDef.bridge` 为真时把 `WaterDeep→WaterShallow`、`WaterOceanDeep→WaterOceanShallow` 再铺桥；`isFoundation` 走 `SetFoundation`。结论：官方道路生成会在深海格上架桥修路。海洋 Mod 若不希望道路横穿海洋格，需在世界级道路生成（GenStep_Roads/WorldGenStep，本片外）拦截；本地图级步骤确认了桥对 Ocean 地形的降级映射关系。

## 主题三：结构/房间内容生成（RoomContents*/RoomLayout*/RoomGen*）——本片最有价值的家族

这是 Odyssey/Anomaly 遗迹（轨道站、迷宫、地下储藏室）的生成框架，全部围绕 `LayoutRoom` + `RoomContentsWorker.FillRoom` 展开，是海底沉船/海底基地遗迹 POI 的直接模板。

### 3.1 真空感知的内容放置（强借用）——RoomContentsWorker.cs:447-459

```csharp
if (cell.GetTerrain(map).exposesToVacuum)
    return cell.GetRoof(map) != null;
```

`IsValidCellBase`：地形暴露真空时，只有带屋顶的格子才允许摆放内容物。结论：海洋 Mod 定义"暴露于海水"的地形属性后，遗迹生成只需在同等位置加"暴露海水→只放气密格"判定，范式完全现成。

### 3.2 Biome.inVacuum 地形分支（直接模板）——RoomContents_InnerCourtyard.cs、RoomContents_NarrowHalls.cs

两处都用 `map.Biome.inVacuum ? TerrainDefOf.Space : TerrainDefOf.PackedDirt` 决定露天内院地形，并 `SetRoof(cell, null)` 开天窗。结论：BiomeDef 级布尔 flag 是官方认可的"环境类型分支"挂载点；海底 Biome 的同类 flag 可让同一套布局 def 在海底生成"海床"庭院。

### 3.3 房间内造水体（直接模板）——RoomContents_ToxicWaterRoom.cs

`SpawnPool`：取最大 rect，用 `GridShapeMaker.IrregularLump` 生成不规则水面（`ToxicWaterShallow`），并把邻水非水格刷成 `Gravel` 滩涂。结论：在结构房间内部生成"水池+滩涂"的完整范例，海底遗迹的"进水舱室/压载舱"可照抄（换 TerrainDef 即可）。

### 3.4 真空主题遗迹内容（类比范本）

- `RoomContents_TransportRoom.cs`：AncientFortifiedWall 围子房间 + AncientBlastDoor + 无顶+MetalTile 地板，货架上概率放 `Apparel_VacsuitHelmet`/`Apparel_Vacsuit` 并设为 forbidden。→ 海底版：气密舱 + 潜水服战利品。
- `RoomContents_Orbital_Corridor.cs`：沿走廊墙挂 `LifeSupportUnit`（生命维持装置，轨道站主题）。→ 海底栖息地的"氧气泵/生命维持"建筑装饰位同款。
- `RoomContents_Stockpile.cs` / `RoomContents_StockpileEntrance.cs`：口袋地图（PocketMap）遗迹——`AncientHatchExit` + `PocketMapExit.entrance` 拿生成参数（stockpileType 决定战利品种类：药品/化合燃料/部件/武器/Gravcore/毒品），并把入口格设为 `MapGenerator.PlayerStartSpot`。→ "海面 hatch 下到沉船口袋地图"的完整范式。

### 3.5 可复用的生成算法（骨架级借用）

- `RoomLayoutGenerator.GenerateRandomLayout`（public static）：容器矩形随机切分房间、四种走廊形状（Straight/Cross/T/H/AsymmetricCross）、房间合并/剪枝、`CreateDoors` 自动在墙格找合法门位（区分内外门配额）。
- `RoomGenUtility`（public static 工具箱）：`FillWithPadding`（内部散布）、`FillAroundEdges`（贴墙摆组）、`SpawnHermeticCrate`（密封战利品箱+开箱信号）、`SpawnCryptoCasket`（冬眠舱+内容物）、`SpawnDormantThreatCluster`（休眠虫群/机械群+唤醒信号 LordJob）、`SubdividedIntoRooms_NewTemp`（大房间二次隔断）、`SpawnDoorBarricades`（门口路障）。
- `RoomContentsWorker` 基类的 def 驱动参数面：`LayoutRoomDef.scatter/scatterTerrain/fillEdges/fillInterior/prefabs/parts/wallAttachments`、`threatPointsScaleCurve`、`canRemoveBorderWalls`、`noRoof`——纯 XML 即可配置大部分房间内容，Mod 只需写少量自定义 Worker。

## 主题四：杂项

- `RoleEffect*`/`RoleRequirement*`（14 个文件）：文化职位的属性加成/装备限制/任职条件，纯社交机制，PASS。
- `RitualVisualEffect*`：仪式 mote/fleck 特效，`ScaleForRoom` 按房间尺寸缩放特效——PASS。
- `RoofDefOf`、`RoadDefOf` 等 DefOf：SKIP。
