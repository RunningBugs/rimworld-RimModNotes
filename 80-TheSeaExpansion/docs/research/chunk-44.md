# chunk-44 调研发现：BaseGen + SketchGen + RimWorld.IO + RimWorld.Utility

本片覆盖四大家族：
- `RimWorld.BaseGen`（125 个文件）：据点/遗迹/飞船坠毁点等**地图内建筑群的符号解析式生成管线**（SymbolResolver 家族）
- `RimWorld.SketchGen`（37 个文件）：**蓝图（Sketch）级别的建筑布局生成**（纪念碑、机械集群、Odyssey 重力舰、Cerebrex 核心等）
- `RimWorld.IO`（8 个文件）：虚拟文件系统抽象（tar/真实目录统一访问），与海洋无关
- `RimWorld.Utility`（4 个文件）：可装填装备接口 + Anomaly 结局工具

对海洋 Mod 的意义集中在：**水下 POI（沉船、海底遗迹、敌方前哨）的生成管线可以直接复用这两套框架**，且框架内已有多处"水域/危险地形/温度"的交互点需要处理。

## TL;DR

1. **BaseGen 是 XML 可扩展的符号解析管线**（`BaseGen.cs`）：RuleDef 把符号名映射到一组 `SymbolResolver`，mod 无需 patch 即可注册自己的 resolver 生成"海底遗迹/沉船"POI。→ 直接借用
2. **`BaseGenUtility.CheckSpawnBridgeUnder` + 据点生成的 Bridge 地板逻辑**：原版基地生成会在不支撑建筑的地形（含水面）下自动铺 `Bridge` 地形，并在不可通行地形上直接覆盖地板（`allowBridgeOnAnyImpassableTerrain`）。这是"海底建筑落在海床/水中"的官方做法模板。→ 直接相关（`BaseGenUtility.cs`、`SymbolResolver_Settlement.cs`、`SymbolResolver_FloorFill.cs`）
3. **`SymbolResolver_RemoveDangerousTerrain`**：生成前把 `TerrainDef.dangerous=true` 的格子替换为生物群系 Heavy 承载地形。**若海洋 Mod 把深水标为 dangerous，所有 BaseGen 据点会把海水抹掉**——要么接受（基地本就该排干），要么注意此冲突。→ 冲突/借用
4. **`BaseGenUtility.ScatterSentryDronesInMap` 用 `Room.ExposedToSpace` 判断室内**（Odyssey 真空暴露检查已进入 BaseGen）——"暴露于海水"的房间判定可以照此模式接入。→ 强相关
5. **路径/铺路的避水逻辑已存在**：`SymbolResolver_OutdoorsPath` 不在 `terrain.IsWater` 格铺路，且用 `TraverseMode.NoPassClosedDoorsOrWater`；`SymbolResolver_EnsureCanReachMapEdge` 会**拆墙开门**保证连通——在海底它会把气密墙打穿装门，需注意。→ 冲突点
6. **温度驱动行为**：`InteriorSymbolResolverUtility`/`Interior_Brewery`/`Interior_Storage`/`IndoorLighting` 全部按 `mapTemperature.OutdoorTemp` 决定是否放暖气/冷却器/火把。海底恒温 4°C：不触发暖气（阈值 3°C）、不触发被动冷却、低科技派系室内放火把（≤18°C）。→ 需核对是否符合设定
7. **农田生成在海底会自然失效**：`SymbolResolver_CultivatedPlants.DeterminePlantDef` 要求肥力>0 且 `minGrowthTemperature ≤ OutdoorTemp ≤ maxGrowthTemperature`，4°C 海床几乎选不到作物，`Farm` leaf 的 CanResolve 直接返回 false。→ 冲突（符合预期，海底无农田）
8. **沉船 POI 模板现成**：`SymbolResolver_Ship_Core/Ship_Pregen/Ship_Spine/Ship_Populate` 生成坠毁飞船（固定布局 + 随机损伤 HP + `GenLeaving.DropFilthDueToDamage` 撒损坏污迹 + 休眠舱冬眠）；`SketchResolver_Gravship`（Odyssey）用 `GravshipHull`+`Substructure` 地形拼重力舰；`SketchResolver_CerebrexCore` 在 Sketch 里直接使用 `TerrainDefOf.Space`（真空地形）+ `AncientBlastDoor`——"在生成物里包含致死环境地形"有官方先例，海水地形可照此进入 Sketch。→ 直接借用
9. **遗迹做旧管线**：`SketchResolver_DamageBuildings` 按方向梯度概率拆除建筑（最多拆 65%）、被拆地形替换为 `burnedDef`、并清理失去墙依附的门——"沉船/海底废墟做旧"直接可用。→ 借用
10. RimWorld.IO 全部 8 个文件是虚拟文件系统（tar/目录抽象），**与海洋无任何可类比点**，全部 SKIP。

## 一、BaseGen 框架本身（扩展点）

- `BaseGen.cs`：`Reset()` 时把所有 `RuleDef` 按 `symbol` 字符串建索引；`Generate()` 弹符号栈，对每个符号在候选 resolver 中按 `selectionWeight` 随机选一个执行，上限 10 万次迭代防死循环。**RuleDef 是纯 XML Def，其 `resolvers` 列表指向 SymbolResolver 类——mod 可以通过自己的 RuleDef + 自定义 SymbolResolver 子类挂入管线**，无需 Harmony patch（resolver 类本身是普通 public 类，Def 引用走 Class 类型解析）。
- `SymbolResolver.cs`：基类只有 `minRectSize`/`selectionWeight`/`CanResolve`/`Resolve`，极易继承。
- `ResolveParams.cs`：约 100 个可选参数的巨型 struct。与海洋相关的参数：`floorDef`/`floorOnlyIfTerrainSupports`/`allowBridgeOnAnyImpassableTerrain`（水上铺地板三件套）、`spawnBridgeIfTerrainCantSupportThing`（单个建筑下补桥）、`noRoof`/`roofDef`、`clearRoof`/`clearEdificeOnly`/`clearFillageOnly`、`edgeUnpolluteChance`、以及 `SetCustom<T>(name, obj, inherit)` 自定义参数通道（可传海洋专属参数给自定义 resolver）。
- `GlobalSettings.cs`：生成全程共享的状态（map、minBuildings/minLandingPads 等配额计数）。`landingPadsGenerated` 供生成后读取。
- `SymbolStack.cs`：符号栈，Push(symbol, ResolveParams)，支持自定义路径名。

## 二、与水/危险地形的交互点（最重要）

- `SymbolResolver_RemoveDangerousTerrain.cs`（19 行）：对 rect 内每格，若 `terrainGrid.TerrainAt(c).dangerous` 为真，替换为 `map.BiomeAt(c).TerrainForAffordance(TerrainAffordanceDefOf.Heavy)`。**关键问题：海洋 Mod 的"海水"地形若设 `dangerous=true`（很可能，类比深水），则据点/遗迹生成会自动把海水替换掉**——这其实是想要的行为（建筑落在干地上），但要意识到它会发生；`SymbolResolver_Settlement` 最后一步就推了 `removeDangerousTerrain`。
- `SymbolResolver_Settlement.cs`：据点生成完整流水线的最佳阅读样本：lootScatter → pawnGroup → outdoorLighting → firefoamPopper（工业+）→ edgeDefense → ensureCanReachMapEdge → basePart_outdoors → **floor（floorDef=Bridge、allowBridgeOnAnyImpassableTerrain=true、floorOnlyIfTerrainSupports=true）**→ removeDangerousTerrain → unpollute（Biotech）。即"先建，再把不支撑的地方铺桥，再清危险地形"。
- `SymbolResolver_FloorFill.cs`：`floorOnlyIfTerrainSupports` 时只在 `GenConstruct.CanBuildOnTerrain` 的格子铺地板；`allowBridgeOnAnyImpassableTerrain` 时**不可通行地形（含深水）也直接覆盖地板**。
- `BaseGenUtility.CheckSpawnBridgeUnder`：建筑占地内不支撑其 `terrainAffordanceNeeded` 且能造 `TerrainDefOf.Bridge` 的格子全部 SetTerrain(Bridge)；多格建筑还会向外扩 1 圈补桥。`SymbolResolver_SingleThing`/`SymbolResolver_OutdoorLighting` 默认开启（`spawnBridgeIfTerrainCantSupportThing` 默认 true）。
- `SymbolResolver_OutdoorsPath.cs`：`CanPlacePath` 明确排除 `c.GetTerrain(map).IsWater`；门到地图边的通路检查用 `TraverseMode.NoPassClosedDoorsOrWater`——原版寻路体系本就有"不涉水"模式。
- `SymbolResolver_EnsureCanReachMapEdge.cs`：对不可达地图边的区域，FloodFill 找到可达格后**沿路拆掉所有墙/自然岩，并在首末断口处造门**（材质沿用被拆墙或 `RandomCheapWallStuff`）。海底语境：这会在气密舱壁上开门——若"门不隔海水"，则该机制会把海水放进基地；生成海底设施时要么不用它，要么让"门"具备气密性。
- `BaseGenUtility.ScatterSentryDronesInMap`：筛选房间用 `!r.ExposedToSpace && !r.OutdoorsForWork && r.CellCount > 10`——**`Room.ExposedToSpace`（Odyssey 真空暴露）已在 BaseGen 工具里被当作"室内/室外"判据使用**。海洋 Mod 若有 `ExposedToWater`/气密概念，这里是最接近的接入范式。

## 三、屋顶与支撑

- `SymbolResolver_Roof.cs`：rect 内未盖顶格子全部设 `rp.roofDef ?? RoofConstructed`；`noRoof` 可关。
- `SymbolResolver_EnsureCanHoldRoof.cs`：找出不连任何支撑（`RoofCollapseCellsFinder.ConnectsToRoofHolder`）或超出支撑范围的屋顶区，循环在评分最高的空格造墙柱（优先 8 邻全可走的格子）。**海底穹顶/耐压壳内部支撑柱生成的直接参考**；也提示：海底大空间若无柱会塌顶。
- `SymbolResolver_Clear.cs`：清场三模式（只清建筑/只清填充物/全清，均要求 `def.destroyable`）+ `clearRoof` 掀顶。

## 四、温度/光照驱动的生成行为（海底 4°C 的影响）

- `InteriorSymbolResolverUtility.cs`：`OutdoorTemp > 22°C` 放被动冷却器；`< 3°C` 放暖气（工业）或篝火/火把（`< -20°C` 才篝火）；`< -45°C` 放两个暖气。**海底 4°C：不触发任何暖气**——4°C 对殖民者仍偏冷但原版据点生成不管。
- `SymbolResolver_Interior_Brewery.cs`：`> 发酵桶 maxSafeTemperature` 放被动冷却；`< 7°C` 放暖气/篝火。**4°C 会触发** brewery 加热。
- `SymbolResolver_Interior_Storage.cs`：`> 15°C` 放被动冷却。
- `SymbolResolver_IndoorLighting.cs`：工业+ 用落地灯；低科技且 `OutdoorTemp ≤ 18°C` 用火把，否则不放（火把会供暖，热天不放）。海底 4°C → 低科技派系室内全是火把。
- `SymbolResolver_OutdoorLighting.cs`：只在 `room.PsychologicallyOutdoors && room.UsesOutdoorTemperature` 的格放灯，并会 `CheckSpawnBridgeUnder`——**水下"室外"灯会被铺桥垫起来**。

## 五、农业/生物生成在海床的行为

- `SymbolResolver_CultivatedPlants.DeterminePlantDef`：遍历 rect 取最低肥力，从所有可播种、非树、非洞穴植物、`!diesToLight`、可收获、`minGrowthTemperature ≤ OutdoorTemp ≤ maxGrowthTemperature` 的植物里随机。海底 4°C + 肥力 0（若海床无肥力）→ 返回 null → `SymbolResolver_Farm` 与 `BasePart_Outdoors_Leaf_Farm` 的 `CanResolve` 均为 false，**敌方据点不会在海床生成农田**。若 Mod 想要"海底养殖场"，需自定义作物（`cultivatedPlantDef` 参数可强制指定）或自定义 resolver。
- `SymbolResolver_WorkSite_Farming` 同理依赖 `CanEverPlantAt`。
- `SymbolResolver_Hives`/`SymbolResolver_Infestation`：虫巢生成（Infestation 是 SignalAction，可 `ignoreRoofedRequirement`）。"海底虫巢"无对应物；`SymbolResolver_Interior_SleepingInsects`/`SleepingMechanoids`/`RandomFleshbeastGroup` 是"沉睡守卫"模板，可类比为"沉睡的深海巨兽"守卫遗迹。

## 六、沉船/废墟 POI 模板（直接可借用）

- `SymbolResolver_Ship_Core.cs`：坠毁飞船入口——收缩 rect、按随机值给 `hpPercentRange`（0.2~1.5 插值）、推 `ship_pregen`/`ensureCanReachMapEdge`/`clear`。
- `SymbolResolver_Ship_Pregen.cs`：30 个部件的固定偏移布局（反应堆/梁/引擎/休眠舱），按 `hpPercentRange` 随机扣血并 `GenLeaving.DropFilthDueToDamage` 撒损坏污迹，`CompHibernatable` 设为休眠。**"海底沉船残骸"可以直接换 def 复刻此结构**。
- `SymbolResolver_Ship_Spine.cs`/`Ship_Populate.cs`：递归生成船脊梁 + 把 `ShipUtility.RequiredParts()` 贴梁摆放。
- `SymbolResolver_LandingPad.cs`：停机坪 = 地板 + 四角信标 + 一艘限时飞走的 Shuttle（TransportShip Job）。水下可类比"潜水器泊点"。
- `SketchResolver_Gravship.cs`（Odyssey）：硬编码 9×10 结构矩阵，`Substructure` 地形 + `GravshipHull`（钢）+ 引擎/燃料罐/驾驶台/货架，是"载具残骸 Sketch"的最新范式。
- `SketchResolver_CerebrexCore.cs`（Odyssey）：61×61 大场面，**直接在 Sketch 里 `AddTerrain(TerrainDefOf.Space, ...)`**——证明"致死环境地形"可以合法地作为生成物的一部分出现；海水地形进 Sketch 没有框架障碍。还有 `AncientBlastDoor` 作为通道密封门。
- `SketchResolver_DamageBuildings.cs`：选一个随机方向，建筑按沿该方向的位置做 pow 衰减概率被移除（最多拆 65%），幸存物随机扣 HP；被拆地形若有 `burnedDef` 则替换（烧痕化）；并清理失去相邻墙的门。`DamageBuildingsLight` 参数更温和（15%/2.0）。`MonumentRuin` = `Monument` + `DamageBuildings` 的组合示例；`AncientLandingPad`/`AncientUtilityBuilding` 生成后同样过一道 `DamageBuildings`。**海底废墟做旧标配**。
- `SymbolResolver_AncientComplex_Base.cs`：古代设施用 `LayoutDef.Worker.GenerateStructureSketch` 布房间，铺地时 `floorDef=AncientConcrete, allowBridgeOnAnyImpassableTerrain=true, floorOnlyIfTerrainSupports=false`——**强制把地板铺到任何地形上**（包括水面），古代设施压水的官方写法。
- `SymbolResolver_AncientRuins.cs`：极简废墟 = emptyRoom + `chanceToSkipWallBlock=0.1` + `noRoof=true` + `clearEdificeOnly`。
- `SymbolResolver_AncientTemple.cs`：用 SketchGen 的 Monument 生成神庙后 `sketch.Spawn(..., wipeIfCollides: true, clearEdificeWhereFloor: true, buildRoofsInstantly: true)`，再配 RectTrigger+警报信的预警机制。
- `SymbolResolver_Unpollute.cs`：`item.CanUnpollute/Unpollute` 清污染（边缘格按 `edgeUnpolluteChance` 概率跳过）——若海洋有"淤积/污染"概念可类比清理。

## 七、SketchGen 框架

- `SketchGen.cs`/`SketchResolver.cs`：比 BaseGen 更简单——`SketchResolverDef.Resolve(SketchResolveParams)` 直接链式调用，产出 `Sketch`（不直接落地图，由调用方 `sketch.Spawn`）。
- `SketchResolveParams.cs`：参数含 `useOnlyStonesAvailableOnMap`（只用本地石材）、`allowWood`/`allowFlammableWalls`/`onlyStoneFloors`、`allowedMonumentThings` 过滤等。
- `SketchGenUtility.cs`：`IsStuffAllowed`/`IsFloorAllowed`（石材限定、可燃墙过滤）、`FindBiggestRect`（最大空矩形，房间划分用）。
- 各 `SketchResolver_Ancient*`（兵营/实验室/军械库/储能/动力/充电/机械培育…）都是"小房间摆设配方"，大量用 `ModLister.CheckXxx` 做 DLC 门控——Mod 的房间配方可照写。
- `SketchResolver_MechCluster`/`MechClusterWalls`/`Mechhive*`（Odyssey 机械蜂群前哨/发射台/处理器阵列）：敌对机械据点 Sketch 模板，可类比"海底机械前哨"。
- `SketchResolver_Monument.cs`：对称随机纪念碑生成（AbstractShapeGenerator 出形状→墙→AssignRandomStuff→FloorFill→柱→家具→对称→补门），`TryAddDoorsToClosedRooms` 用 FloodFill 给封闭房间补门——**任何"海底封闭建筑"生成都要解决同样的补门问题**。
- `SketchResolver_Symmetry.cs`/`AddColumns`/`AddCornerThings`/`AddWallEdgeThings`/`AddThingsCentral`/`AddInnerMonuments`/`FloorFill`/`AssignRandomStuff`：通用布局零件，与海洋无直接关系但都是自定义 resolver 的积木。

## 八、杂项

- `RimWorld.Utility/ReloadableUtility.cs` + `ICompWithCharges`/`IReloadableComp`：可装填装备框架。`FindSomeReloadableComponent` 会检查 pawn 主手装备与**服装上的 `CompApparelReloadable`**——"潜水服/氧气瓶充氧"可以直接用 `CompApparelReloadable`（服装弹药=氧气）实现，无需自造轮子。这是本片 Utility 家族唯一的海洋类比点。
- `VoidAwakeningUtility.cs`：Anomaly DLC 结局逻辑（拥抱虚空/断开链接、metalHell pocket map 收尾、虚空金属地形铺撒 `SpawnVoidMetalAround` 含 `IrregularLump` 地形团块生成）。结局专用，与海洋无关；唯一可参考的是 `SpawnVoidMetalAround` 的"不规则地形团块+杂散建筑"撒布写法。
- `RimWorld.IO/*`（8 个）：`AbstractFilesystem`/`VirtualDirectory`/`VirtualFile` 抽象 + `FilesystemDirectory/File`（真实磁盘）+ `TarDirectory/TarFile`（SharpZipLib tar 包）+ `LoadAsXDocument`（XML 加载）。纯内容加载基建，全部 SKIP。

*备注：本片所有结论均基于逐文件阅读；未读 XML Def（RuleDef 的 symbol 名与 resolver 对应关系在 XML 侧），RuleDef 具体字符串映射"未验证"（不影响上述结论）。*
