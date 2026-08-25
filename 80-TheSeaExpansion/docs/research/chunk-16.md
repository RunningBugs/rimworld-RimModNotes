# chunk-16 调研发现（RimWorld 组：LordJob/LordToil、Main* UI、Map*、Me* 家族）

本片 200 个文件，主体是三大家族：
1. **Lord AI 家族**（LordJob_*/LordToil_*/LordToilData_*，约 100 个文件）：袭击者/访客/仪式/虫群等群体 AI 的状态机节点，绝大多数只是"给每个 pawn 发一个 DutyDef"的薄壳，业务逻辑在 DutyDef 对应的 JobGiver 里（不在本片）。
2. **主界面 UI 家族**（MainButton*/MainTabWindow*/MainMenuDrawer）：底部按钮栏与各标签页窗口，纯 UI。
3. **Map/Me 工具家族**（MapGen*、MapPortal、Meditation*、Mech*、Mineable 等）：本片海洋价值几乎全部集中在这里。

## TL;DR（最重要发现）

- **海底恒温有现成字段**：`MapGenUtility.SetMapRoomTemperature` 里直接读 `map.Biome.constantOutdoorTemperature`（BiomeDef 字段），室外恒温 4°C 不需要新机制，定义一个海洋 BiomeDef 即可。（源：MapGenUtility.cs:985-990）
- **"排水"可完全类比"抽真空"**：同一方法里 `if (map.Biome.inVacuum && !room.ExposedToSpace) room.Vacuum = 0f;`——结构生成时给密闭室内清零真空度。BiomeDef.inVacuum + Room.Vacuum + Room.ExposedToSpace 就是 Odyssey 真空整套接口，海洋 Mod 的"海水/气密"应照搬这套 shape。（源：MapGenUtility.cs:1011-1014）
- **NPC 生成已内置真空避让**：`MapGenUtility.GeneratePawns` 的落点校验 `pawn.ConcernedByVacuum` 时要求所在 room 非 `ExposedToSpace`——海洋里等价于"怕水 pawn 不刷在海水格"，有现成模式可抄。（源：MapGenUtility.cs:871-878）
- **口袋地图传送门是"下潜点"的直接模板**：`MapPortal` + `ThingDef.portal`（MapPortalProperties：pocketMapGenerator/exitDef/进出信件/音效）实现"进入洞穴/裂隙→生成并进入子地图"。海底殖民地可用它做"下潜舱/海洞入口"。（源：MapPortal.cs、MapPortalProperties.cs）
- **海洋生物群系水体地形全是 BiomeDef 可配字段**：`oceanDeepTerrain`/`oceanShallowTerrain`/`waterDeepTerrain` 等，`TileMutatorDef` 还能 override 海滩/湖岸/泥地/河岸地形。（源：MapGenUtility.cs:180-256）
- **冥想/户外活动的"环境安全"检查是注入点**：`MeditationUtility.SafeEnvironmentalConditions` 依次查 毒尘+无屋顶、NoxiousHaze、危险、`cell.VacuumConcernTo(pawn)`——海水暴露应加进同一条链（或 patch 此方法）。（源：MeditationUtility.cs:223-242）
- **机械集群落点主动避开厚岩顶/屋顶/室内**：全密闭海底基地会让 MechCluster 评分趋零落不下来；"只刷工业以上袭击"若依赖机械集群空投需要另寻路径。`ThingDef.preventSkyfallersLandingOn` 可用来封死空投点。（源：MechClusterUtility.cs:79-160）
- **访客会因"危险温度"撤离**：`LordJob_VisitColony` 挂了 `Trigger_PawnExperiencingDangerousTemperatures`，海底室外恒温 4°C 对无保暖访客会触发撤离+提示消息——要么给访客配发装备，要么接受"海底无访客"。（源：LordJob_VisitColony.cs）
- **深海视觉层模板**：`MapDrawLayer_OrbitalDebris` 用 `Map.OrbitalDebris`（OrbitalDebrisDef）在地形之下画带视差的碎片群，相机移动产生景深——"海洋雪/浮游生物/远处鱼影"背景层可直接仿写。（源：MapDrawLayer_OrbitalDebris.cs）
- **洞穴隧洞算法可复用为海沟/海洞**：`MapGenCavesUtility.GenerateCaves(map, visited, group, directionNoise, parms, isRock)` 的 `isRock` 是谓词参数，换成"海底岩层"判定即可在海床山里挖隧洞。（源：MapGenCavesUtility.cs:74-95）

## 主题一：海洋生物群系与地图生成（直接相关）

### MapGenUtility.cs（1449 行，重点读水体/真空/温度相关段）
- 水体地形全部走 BiomeDef 字段，缺省回落到 TerrainDefOf：
  - `DeepFreshWaterTerrainAt` → `biome.waterDeepTerrain ?? WaterDeep`（:180）
  - `ShallowFreshWaterTerrainAt` → `waterShallowTerrain ?? WaterShallow`（:185）
  - `DeepMovingWaterTerrainAt` → `waterMovingChestDeepTerrain ?? WaterMovingChestDeep`（:190）
  - `ShallowMovingWaterTerrainAt` → `waterMovingShallowTerrain ?? WaterMovingShallow`（:195）
  - `DeepOceanWaterTerrainAt` → `oceanDeepTerrain ?? WaterOceanDeep`（:200）——**已有"海洋深水/浅水"地形槽位**，海洋生物群系只需填这两个字段。
  - `ShallowOceanWaterTerrainAt` → `oceanShallowTerrain ?? WaterOceanShallow`（:205）
  - 海滩/湖岸/泥地/河岸（:210-256）还会先查 `TileMutatorDef.overrideCoastalBeachTerrain` 等 override——Tile 级别的变异器可以整体替换海岸地形。
- `MaxWaterOverlap = 0.25f`（:158）：`TryGetStructureRect` 要求矩形内水体格占比 ≤25% 才放结构（:808）。
- `IsCellValid`（:755）在 `checkWaterRoads` 时排除 `terrainDef.IsWater || IsRoad` 的格子——空地查找默认避水。
- `SetMapRoomTemperature`（:985-1020）：**BiomeDef.constantOutdoorTemperature**（室外恒温）与 **BiomeDef.inVacuum**（该生物群系是否真空）都在此方法中被消费；inVacuum 时给密闭房间 `room.Vacuum = 0f`。这是"结构生成后初始化室内环境"的统一入口，海洋版应在此等价物里做"室内排水"。
- `GeneratePawns`（:828-890）：落点校验包含 `pawn.ConcernedByVacuum` → 要求 room 存在且非 `ExposedToSpace`。NPC 刷点避让致死环境是现成模式。

### MapGeneratorDefOf.cs
- 登记了全部地图生成器：`Base_Player`、`Base_Faction`、`Undercave`/`MetalHell`/`Labyrinth`（Anomaly）、`SpacePocket`/`Space`/`OrbitalRelay`/`Mechhive`/`InsectLair`（Odyssey）。海洋 Mod 需要自己的 `MapGeneratorDef`（如 "Seafloor"），与 `Space` 平级。

### MapGenCavesUtility.cs
- 完整的洞穴隧洞生成器：flood-fill 找岩组（`isRock` 谓词）、开洞隧道+封闭隧道、Perlin 方向噪声+宽度噪声、分支概率。`CaveGenParms.Default` 给出全部调参（openTunnelsPer10k=5.8、minTunnelWidth=1.4 等）。海床山脉/海沟可直接复用。

### MapGenTuning.cs
- 只有 6 个常量：海拔→岩层阈值 0.7、各地貌的 ElevationFactor（0.8~1.2）。海底地貌生成若沿用海拔噪声会用到这些系数。价值低，登记备查。

### MapNoiseUtility.cs
- 通用噪声工具（FalloffAtEdge/FalloffAtAngle/CreateFalloffRadius），与海洋无直接类比，但做"靠近海沟边缘渐深"之类效果时是基础件。PASS 级。

### MapMeshFlagDefOf.cs / MapMeshFlagDef.cs
- 地图网格脏标记位：Things/FogOfWar/Buildings/GroundGlow/Terrain/Roofs/Snow/Zone/Plan/PowerGrid/BuildingsDamage/Gas/Pollution(Biotech)/**Sand(Odyssey)**。Odyssey 为沙尘加了独立 mesh flag——若海水要做动态水面渲染层，这里是要扩展的位掩码（64 位 mask，目前用了约 15 位）。MapMeshFlagDef 本身只是 index→mask 的 Def。

### MapInterface.cs
- Map 上有 `waterInfo`（DebugDrawRiver，:185）和 `waterBodyTracker`（:187）成员；`mapTemperature.TemperatureUpdate()` 每帧驱动（:151）。另有 Odyssey 的 `GravshipUtility.ShowConnectedSubstructure` UI 分支（:63,140）。说明 Map 级水体追踪组件已存在（实现不在本片），海洋 Mod 应调查 WaterInfo/WaterBodyTracker 能否承载"被淹没区域"。

## 主题二：口袋地图与传送（直接相关）

### MapPortal.cs（340 行，全文读）
- `MapPortal : Building, IThingHolder`：持有一个 `pocketMap`（惰性生成，父 MapParent 没图时自动置空）和出口 `PocketMapExit exit`。
- 进入流程：Gizmo → `Dialog_EnterPortal` → 装载清单 `leftToLoad`（TransferableOneWay）→ `LordJob_LoadAndEnterPortal`；`JobDefOf.HaulToPortal`/`EnterPortal` 两个 Job 搬货进人。
- `GeneratePocketMapInt`（:331）调 `PocketMapUtility.GeneratePocketMap(size, def.portal.pocketMapGenerator, GetExtraGenSteps(), base.Map)`——生成器与尺寸都来自 ThingDef.portal（MapPortalProperties）。
- 首次进入发信件（enteredLetterLabel/Text/Def），穿越播放 `traverseSound`。
- `IsEnterable` 遍历所有 comp 的 `CanEnterPortal()`——**comp 级准入钩子**，可做"需要潜水服才能进下潜舱"。
- **类比结论**：下潜舱/潜艇/海洞入口 = MapPortal 子类 + 自定义 MapGeneratorDef + 准入 comp。整套装载 UI 和信件都白送。

### MapPortalProperties.cs
- 纯数据：`pocketMapGenerator`、`exitDef`、`pocketMapSize=100`、进出信件三件套、`traverseSound`。

## 主题三：环境暴露与致死环境（直接相关）

### MeditationUtility.cs
- `SafeEnvironmentalConditions(pawn, cell, map)`（:223-242）是"该格能否安心做户外长期活动"的统一判定：毒尘+无屋顶 → NoxiousHaze（Biotech）→ 战斗危险 → **`cell.VacuumConcernTo(pawn)`**（Odyssey 真空）。这是真空机制渗入通用 AI 的精确位置之一，海水暴露检查应挂在同一条链上（patch 或复刻）。

### MapDrawLayer_OrbitalDebris.cs（187 行，全文读）
- Odyssey 的轨道碎片背景层：`Visible` 要求 `ModsConfig.OdysseyActive && Map.OrbitalDebris != null`；按 Map.uniqueID 播种随机，在 `AltitudeLayer.BelowTerrain` 高度画 5-20 片一组的碎片 quad，每个图形有 `parallaxPer10Cells` 视差系数，相机移动时整体偏移 `vector/10*parallax`。
- **类比结论**：深海"海洋雪"/浮游颗粒/远景鱼影层照抄此结构，把 `Map.OrbitalDebris` 换成自定义 MapComponent/Map 属性即可。

## 主题四：袭击与敌对群体 AI（冲突/借用各半）

### LordToil_Siege.cs（364 行，全文读）
- 围城流程：选 siegeCenter → `SiegeBlueprintPlacer.PlaceBlueprints` 放沙袋+迫击炮蓝图 → **空投建材+每炮 5 发炮弹+生存口粮**（`DropPodUtility.DropThingGroupsNear`，:138）→ 分建筑工/防御者 → 每 500 tick 检查炮弹/口粮/燃料，不足则空投补给。
- **冲突点**：海底地图"空投补给落在海水里""迫击炮架在水下"都不合理；围城袭击要么禁用，要么换成"潜水舱投放"。`Find.Storyteller.difficulty.classicMortars` 分支说明迫击炮燃料（强化桶）逻辑也在这。
- 战败转总攻阈值 `FractionLossesToAssault = 0.4f`（memo "NoArtillery"/"NoBuilders" 由 LordJob_Siege 消费，不在本片）。

### LordToil_AssaultColonyBreaching.cs（259 行，全文读）+ LordToilData_AssaultColonyBreaching.cs + LordToil_AssaultColonySappers.cs
- 破墙袭击：`BreachingGrid.CreateBreachPath(breachStart, breachDest, radius, walkMargin, useAvoidGrid)` 算出穿墙路径；按 `BreachingUtility.FindVerbToUseForBreaching` 把袭击者分为远程破墙/近战破墙/单人突破（20% 概率）/护送；破墙目标由 `breachingGrid.FindBuildingToBreach()` 逐个给出。
- Sapper 版本用 `SappersUtility.IsGoodSapper`（挖矿技能）选人挖墙，其余人按武器类型在 15-19（远程）/23-26（近战）半径护送。
- **类比结论（设计机会）**：海底基地的"墙"是耐压壳，破墙=进水。AI 侧不用改（破墙逻辑通用），但"墙破后发生什么"是海洋 Mod 的核心差异化系统（本片无进水逻辑，需另找 Vacuum/Room 扩散机制所在分片）。

### LordToil_AssaultColony.cs 等其余 Assault 系列
- 全部只是发 `DutyDefOf.AssaultColony`/`NestAssault`/`FleshbeastAssault` 等 duty 的薄壳；`attackDownedIfStarving`、`canPickUpOpportunisticWeapons` 参数化。无海洋价值，PASS。

### ManhunterPackGenStepUtility.cs
- 猎人生成群包刷怪：`AggressiveAnimalIncidentUtility.TryFindAggressiveAnimalKind(points, tile, out kind)`，失败再用 `PlanetTile.Invalid` 兜底。**海洋生物群系的 tile 会参与这个查找**——"只刷水生生物"的关键是让该工具方法在海洋 tile 上只返回水生 PawnKindDef（实现不在本片，在 AggressiveAnimalIncidentUtility）。NOTE 级登记。

### LordJob_VisitColony.cs（130 行，全文读）
- 访客状态机：Travel→DefendPoint 停留 8000~22000 tick→送礼→TravelAndExit。
- 关键触发器：`Trigger_PawnExperiencingDangerousTemperatures`（发"访客遭遇危险温度"消息并撤离）、`Trigger_PawnExperiencingAnomalousWeather`、`Trigger_PawnCannotReachMapEdge`（被困换 ExitMap 挖掘离开，canDig=true）。
- **冲突点**：海底室外 4°C 恒温会持续触发危险温度撤离；且访客假定能从地图边缘离开——若海底地图边缘是海水（非气密 pawn 走入即死），需要保证边缘有安全通道或改用穿梭机/传送门离场。

### LordToil_EntitySwarm.cs + Sightstealer/Shambler 系列
- EntitySwarm 是"虫群沿路径整体移动"基类（SwarmRadius=7、600 tick 挪一个路径节点）；Sightstealer 限制 `p.kindDef == PawnKindDefOf.Sightstealer`。Sightstealer 是 Anomaly 的黑暗生物，但其避光逻辑不在本片（在 JobGiver/Hediff 侧）。深海黑暗生物群可借用 swarm 基类，NOTE 级偏低，本片只作登记（PASS，借用处已写明）。

### 其余 LordToil/LordJob（约 80 个）
- 仪式（婚礼/演讲/授勋/派对/血族会议/心灵仪式）、商队集结（PrepareCaravan 全家）、越狱、绑架、偷窃、Boss 护送、巢穴防御等——全部是 duty 分发薄壳，与海洋无任何类比点，统一 PASS。
- `LordToilData_*`（约 20 个）是纯 Scribe 数据类，SKIP/PASS。

## 主题五：采矿与资源（弱相关）

### Mineable.cs
- 矿物建筑基类：`def.building.mineableThing`（产物）、`mineableDropChance`、`EffectiveMineableYield`、`mineableYieldWasteable`（浪费型产出按 `yieldPct` 累积结算，`MiningYield` stat 加成）。海底矿脉/锰结核直接用这个基类+def 配置即可，无需新代码。NOTE（弱）。

### MineStrikeManager.cs / MineAIUtility.cs
- 挖到矿的"发现矿脉"消息（相邻格有值钱矿且未可见时提示，12 格内同类不重复提示）；采矿 Job 的可达性校验（8 邻格可站）。纯通用逻辑，PASS。

## 主题六：机械族（弱相关/冲突）

### MechClusterUtility.cs（部分读）
- `FindClusterPosition`/`GetClusterPositionScore`：落点评分 = 阻挡率×雾中率×**屋顶率**×**室内率**；`CheckCell` 对 `RoofRockThick` 直接判非法（:140-142），对含 `preventSkyfallersLandingOn` 的格的 Thing 判非法（:154）。
- **冲突点**：海底殖民地若全图铺厚顶/建筑，机械集群评分趋零——"只有工业科技以上袭击"的主力（机械族）可能永远落不下来，需要给海洋地图设计替代入场方式（如边缘潜水舱生成）。

### MechClusterGenerator.cs（skim）
- 集群 Sketch 生成：points→pawn/建筑配比曲线、tag 系统（MechClusterMember 等）。纯生成参数，PASS。

### Mechanitor*/Mech*Gizmo/MechRepairUtility/Mechlink/MechhiveTransition
- 机械师带宽/控制组 UI、机械维修、机械链接物品、机械巢轨道转移（要求某图有 CerebrexCore）。与海洋无类比点，PASS。

## 主题七：UI 与其他（无价值，家族登记）

- MainButton*/MainTabWindow*（30 个）：底部按钮栏、研究/任务/历史/检查/动物/野生生物/工作/日程/派系/意识形态/机械标签页。纯 UI 绘制。`MainTabWindow_Wildlife` 列出无派系动物——海洋地图水生生物会自动出现在此标签页，无需改动。全部 PASS。
- Meditation*（其余）：冥想焦点 Def、可用性缓存（按背景故事/头衔缓存 PawnCanUse）。PASS。
- Meme*/MentalState*/Message*/MayRequire*/Medical*/Marriage*/Mass*/Memory*/Metal*/Mesh*/Minified*/Minify*：模因 Def、精神状态 DefOf、医疗照护、结婚点校验（要求 Standable+有顶或晴天）、负重、记忆思想、金属恐怖（Anomaly 感染）、网格工具、打包物品。全部 PASS/SKIP。
- `MayRequireOdysseyAttribute`（modId `ludeon.rimworld.odyssey`）——若海洋 Mod 依赖 Odyssey 的真空类，可仿照写 `[MayRequire("modId")]`。登记备查（SKIP 级）。

## 未验证事项
- `AggressiveAnimalIncidentUtility.TryFindAggressiveAnimalKind` 的 tile→动物种类过滤实现不在本片，"只刷水生生物"的具体钩子需在其所在分片确认。
- `Map.waterInfo`/`waterBodyTracker` 的实现（Verse 侧？）不在本片，能否承载淹没区未验证。
- 真空扩散/抽真空泵的核心循环（Room.Vacuum 的 tick 更新）不在本片；本片只确认了其消费点（生成时清零、刷点避让、冥想检查）。
