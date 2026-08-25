# chunk-15 调研笔记（RimWorld 组：JobGiver 尾部 / JoyGiver 全家 / LordJob 全家 / Layout·Lava·Landing 等）

本片覆盖 200 个文件，主要是 AI 行为家族（JobGiver/JoyGiver/LordJob/LearningGiver）、娱乐需求系统、Odyssey 的遗迹布局生成、熔岩蔓延事件、引力船降落结果框架。

## TL;DR

1. **「真空禁用户外行为」有一整套现成开关，海水环境可直接复用**：`JoyUtility.EnjoyableOutsideNow` 检查 `Biome.inVacuum + pawn.ConcernedByVacuum`；`JoyGiver.GetSearchSet` 及各娱乐子类统一用 `VacuumConcernTo` 过滤真空中的目标物。海洋 Mod 把"海水"类比为真空后，几乎所有户外娱乐/闲逛行为会自动失效，无需逐个改。（源：JoyUtility.cs、JoyGiver.cs 等 7 个 JoyGiver 文件）
2. **游泳娱乐（Odyssey）是海底最直接的冲突点**：室外温度 <10°C 或 `Biome.inVacuum` 时永不触发。海底恒温 ~4°C → 游泳永远不会发生，等于白送；若要"穿潜水服游泳"可在此基础上改造。（源：JoyGiver_GoSwimming.cs）
3. **LavaEmergence 是"液体蔓延事件"的完整模板**：洪泛扩张（openCells/lavaCells）、poolSize 总量上限、冷却延迟后逐格 `tempTerrain.QueueRemoveTerrain` 消退、`CanLavaSpreadInto` 只进 natural+Walkable 地形且避开地基。改造成"船体破裂海水涌入/淹没"事件几乎是改名级别的工作量。（源：LavaEmergence.cs、LavaEmergenceImmediate.cs、LavaRockIncoming.cs）
4. **LargeBuildingCellFinder 硬性拒绝水地形格**：大型威胁建筑/事件建筑选址时 `cell.GetTerrain(map).IsWater` 直接 0 分。海底地图若地表全是水地形，依赖它的袭击/事件（如 FleshmassHeart 类）会找不到落点而静默失败——必须处理。（源：LargeBuildingCellFinder.cs）
5. **LandingOutcomeDef + LandingOutcomeWorker 是"降落事故"框架**：weight 随机、workerClass 反射实例化，已有晕船、推进器故障、引擎过热、局部坠毁 4 种实现。若海洋 Mod 做潜水器/载人舱降落海底，这是现成模板。（源：LandingOutcome*.cs 共 6 文件）
6. **访客/商队 LordJob 内置环境撤离触发器**：`Trigger_PawnExperiencingDangerousTemperatures`、`Trigger_PawnExperiencingAnomalousWeather`、以及遍历所有 `GameConditionDef.causesTraderCaravanExit` 的 `Trigger_GameCondition`。海洋 Mod 新增"水下风暴/高压"类 GameCondition 时，挂 `causesTraderCaravanExit` 即可让访客自动撤离。（源：LordJob_TradeWithColony.cs、LordJob_AssistColony.cs）
7. **围攻袭击（LordJob_Siege）会按 blueprintPoints 现场建炮兵**，海底环境下"工业科技以上袭击"若保留围攻类型需要确认其合理性；袭击科技筛选不在本片（在 IncidentWorker_Raid 侧）。（源：LordJob_Siege.cs、LordJob_AssaultColony.cs）
8. **Odyssey 遗迹布局生成系统（LayoutDef/LayoutRoom/LayoutSketch/LayoutWorker_AncientRuins）是海底遗迹/沉船生成的直接模板**：随机房间布局→铺墙/门/地板→房间级 scatter/fillEdges/prefab 填充，纯 Def 驱动。（源：Layout*.cs）
9. 儿童学习的两类户外活动在海底会失效：仰望天空（找 unroofed 格）和亲近自然（找自然兴趣点）。（源：LearningGiver_Skydreaming.cs、LearningGiver_NatureRunning.cs）
10. LabyrinthMapComponent 展示了 pocket map（口袋地图）+ SkipUtility 传送的完整生命周期，可做"异次元海沟/沉船副本"参考。（源：LabyrinthMapComponent.cs）

## 主题详录

### 1. 真空↔海水类比：娱乐系统的现成开关（强相关，借用）

`JoyUtility.EnjoyableOutsideNow(Map/Pawn)`（JoyUtility.cs:16-53）是户外娱乐总闸门：雨 ≥0.25 拒绝；`gameConditionManager.AllowEnjoyableOutsideNow` 拒绝；室外温度不在 pawn 舒适区拒绝；**`map.Biome.inVacuum && pawn.ConcernedByVacuum` 拒绝**。海洋语境：海底"室外"充满海水，只要把海水的致死性挂在与真空相同的 `Biome`/Concerns 体系上，所有调用此函数的 JoyGiver（散步 GoForWalk、堆雪人等）自动关闭。

`JoyGiver.GetSearchSet`（JoyGiver.cs:34-37）：基类统一把 `thing.Position.VacuumConcernTo(pawn)` 的候选娱乐建筑从搜索集中剔除。同样的 `VacuumConcernTo` 检查出现在：
- JoyGiver_InteractBuilding.cs:89（棋盘/电视等交互建筑娱乐的基类 CanInteractWith）
- JoyGiver_Ingest.cs:93（进食娱乐的目标物）
- JoyGiver_SocialRelax.cs:54,142,158,173（社交放松的聚集点、椅子、地面座位）
- JoyGiver_ViewArt.cs:53（艺术品）
- JoyGiver_VisitGrave.cs:25（坟墓）

结论：官方模式是"环境致死性集中在 `VacuumConcernTo`/`ConcernedByVacuum` 两个谓词上，各 AI 点查一次"。海水版只需提供同型谓词并在这 ~7 处补丁替换/扩展，覆盖即完成。

### 2. 游泳（直接相关，冲突+借用）

JoyGiver_GoSwimming.cs（Odyssey 限定，`ModLister.CheckOdyssey("Swimming")`）：
- `HappyToSwimOutsideOnMap`：室外温度 <10°C 拒绝、`map.Biome.inVacuum` 拒绝、`AllowEnjoyableOutsideNow` 拒绝。海底 ~4°C 恒温 → 游泳永不触发。
- 室内游泳要求房间温度 >10°C（EnjoyableInsideNow）。
- 目标格校验：`terrain.IsWater && toxicBuildupFactor == 0 && !Fogged && Standable`；`pawn.ConcernedByVacuum` 时要求 `room.Vacuum < 0.5f`。
- 寻路走专门的 `SwimPathFinder.TryFindSwimPath`（在 Verse 侧，非本片）。

海洋价值：冲突（海底不该触发普通游泳，恒温恰好自动屏蔽）；借用（若要"潜水娱乐"，`SwimPathFinder` + 本 giver 是模板；`toxicBuildupFactor == 0` 这类地形过滤写法可参考）。

### 3. 观天/户外向行为与深海低光（相关，冲突类）

- JoyGiver_Skygaze.cs：`weather.preventSkygaze` 标志直接禁用观天；Anomaly 的 UnnaturalDarkness 激活时也禁用。深海低光照→可让海洋生物群系的默认天气/条件置 `preventSkygaze`，或加一个常驻 GameCondition。
- JoyGiver_BuildSnowman.cs：依赖 `snowGrid.TotalDepth >= 200`。海底无雪 → 自动失效，无需处理（登记为 PASS，此处仅备注）。
- LearningGiver_Skydreaming.cs：儿童"白日梦"学习要求 `RCellFinder.TryFindAllowedUnroofedSpotOutsideColony`（无屋顶格）。海底基地全屋顶 → 该学习欲望永不满足。
- LearningGiver_NatureRunning.cs：儿童"亲近自然"要求 `NatureRunningUtility.TryFindNatureInterestTarget`（自然兴趣点，通常在野外）。海底室外=致死海水，需确认该 utility 的目标格校验是否会把孩子引向海水（未验证，NatureRunningUtility 不在本片）。

### 4. 熔岩蔓延 = 海水涌入事件模板（强相关，借用）

LavaEmergence.cs（Thing 子类）：
- SpawnSetup 时从 `TerrainDefOf.LavaDeep` 洪泛找出与"非岩浆且无建筑"格相邻的岩浆格，随机取 2~4 个涌出点；`FireLetter` 发 ThreatSmall 信件。
- 每 480 tick `SpreadLava`：从 openCells 加权随机（邻近岩浆越多权重越高 ×4）取一格，`terrainGrid.SetTempTerrain(result, TerrainDefOf.LavaShallow)`，把四邻合格格加入 openCells。
- 总量达到 poolSize（450~600 格）或无处可扩 → `BeginCooling`：每格按 coolDelay（300k~360k tick）+ 递推间隔 `Map.tempTerrain.QueueRemoveTerrain` 排队消退，然后 Destroy。
- `CanLavaSpreadInto`：只进 natural 地形、要求 `TerrainAffordanceDefOf.Walkable`、排除有地基（`terrainGrid.FoundationAt`）、排除不可清除建筑。
- LavaEmergenceImmediate.cs：一次性立即蔓延（poolSize 100~150），用于 LavaRockIncoming 砸落点。
- LavaRockIncoming.cs（Skyfaller）：落点生成一个 LavaEmergenceImmediate，`forcePoolSize = radius²×3`（radius 3~5），`forceCoolDelay` 48k~72k tick。

海洋价值：`SetTempTerrain`/`QueueRemoveTerrain` 的"临时地形+定时消退"机制 + 洪泛扩张 + 建筑/地基阻挡判定，就是"气密舱破裂→海水灌入→修复后排水消退"的全部骨架。涌水事件可照抄此结构，把 LavaShallow 换成浅水地形、把"自然地形"限制改为"非气密区域"。

### 5. 大型建筑选址拒绝水地形（冲突，必须处理）

LargeBuildingCellFinder.cs `GetCellScore`（:205-208）：`cell.GetTerrain(map).IsWater` → 返回 0 分，整片区域直接失去候选资格（GetAreaScore 遇 0 分格即整片作废）。该 finder 被 FleshmassHeart 等事件建筑、威胁建筑群使用（DebugDraw 里直接用 `IncidentWorker_FleshmassHeart.HeartSpawnParms`）。海底地图若海底地表地形 `IsWater == true`，这类事件永远找不到落点。两个方向的修法：海底地表不用 IsWater 地形（用专门的"海床"地形），或补丁该 finder。配套 LargeBuildingSpawnParms 是纯参数结构（canSpawnOnImpassable、ignoreTerrainAffordance 等），无逻辑。

### 6. 降落事故框架（相关，借用）

LandingOutcomeDef.cs：`weight` + `letterLabel/letterText/letterDef` + `workerClass` 反射出 LandingOutcomeWorker。LandingOutcomeWorker.cs 基类负责格式化信件（GRAVSHIP/PILOT/COPILOT 命名参数 + 附录"负面结果概率"文本）。四个实现：
- GravNausea：给所有血肉 pawn 加 `HediffDefOf.GravNausea`。
- MinorGravshipCrash：随机一个带推进器的组件，半径 5.9~11.9 内所有建筑吃 25~30 点 Crush 伤害。
- OverheatedGravEngine：引擎冷却 +120k~240k tick。
- ThrusterBreakdown：随机 2~4 个推进器 `CompBreakdownable.DoBreakdown()`。

海洋价值：若做"潜水舱/潜水器降落海底"（类比引力船），负面结果（舱体渗漏、压载故障、螺旋桨损坏、减压病 hediff）可直接注册新的 LandingOutcomeDef + worker。

### 7. 袭击与访客 LordJob（部分相关）

- LordJob_AssaultColony.cs：主力强攻状态机。注意点：`Trigger_FractionColonyDamageTaken(0.25~0.35)` 满足后袭击者"抢够了就走"；`canKidnap`/`canSteal` 由子图 LordJob_Kidnap/LordJob_Steal 承担。绑架判定 KidnapAIUtility.TryFindGoodKidnapVictim 要求 `CanReachMapEdge`——海底地图的"地图边缘"语义（水面？）需要可达性可用，否则绑架逻辑不触发（未验证，属 RCellFinder/可达性侧）。
- LordJob_Siege.cs：Travel→LordToil_Siege（按 blueprintPoints 建炮兵蓝图）→强攻。海底保留围攻则=敌人在海床上现场造迫击炮，设定上可接受与否需玩家决策；筛选袭击类型在 IncidentWorker 侧，不在本片。
- LordJob_TradeWithColony.cs：商队。内置三类环境撤离：`Trigger_PawnExperiencingDangerousTemperatures`（MessageVisitorsDangerousTemperature）、`Trigger_PawnExperiencingAnomalousWeather`、遍历 `GameConditionDef.causesTraderCaravanExit` 的 Trigger_GameCondition。海洋新增恶劣条件时挂 `causesTraderCaravanExit` 即可。注意：4°C 是否触发危险温度撤离未验证（取决于 pawn 舒适温度范围判定，在 Trigger 侧）。另外海底 4°C 对正常人类不算危险温度（人类下限约 0°C 以下才有低体温症风险），未验证，不臆断。
- 休眠-唤醒袭击模板：LordJob_SleepThenAssaultColony / LordJob_SleepThenMechanoidsDefend / LordJob_StructureThreatCluster（DormancyWakeup/Clamor 信号唤醒 + 唤醒音效）。"沉睡的深海巨兽被吵醒"类事件可照用（本片仅登记，PASS）。
- KidnappedPawnsTracker.cs：被绑 pawn 进 WorldPawns，每 15051 tick 按 MTB 30 天转正为绑架派系成员。与海洋无交集（PASS）。

### 8. Odyssey 遗迹布局系统 = 海底遗迹/沉船生成模板（借用）

- LayoutDef.cs：布局总定义。房间权重表（LayoutRoomWeight）、workerClass、`shouldDamage`（做旧）、`noRoof`、走廊合并/删门概率、以及 junkScaterrers/scatterTerrain/fillEdges/fillInterior/wallAttachments/prefabs/parts 等纯 Def 驱动的填充参数表。
- LayoutRoom.cs：布局房间=若干 CellRect + entryCells + 连接关系；`TryGetRandomCellInRoom(ThingDef...)` 提供房内合法落点搜索（含交互格、门旁规避）。
- LayoutRoomDef.cs：单个房间的内容定义：floorTypes/edgeTerrain 铺地、sketchResolverDef、RoomContentsWorker（前/中/后三段填充）、threatPointsScaleCurve、roofDef/noRoof。
- LayoutSketch.cs：`FlushLayoutToSketch` 把 StructureLayout 翻成实体草图——墙格放 WallThing（可 per-room stuff）、地板铺 FloorTerrain（默认 PavedTile）、门格放 DoorThing。
- LayoutWorker_AncientRuins.cs：`RoomLayoutGenerator.GenerateRandomLayout` 生成随机房间+走廊布局；木材墙体强制替换为钢铁（海底遗迹同理可强制石质/钢铁）；_Reactor/_Terraformer/Mechhive/SingleRoom 只是参数变体。
- LayoutDefOf（SKIP 登记）列出 Odyssey 已有布局：SpaceRuins、OrbitalMechanoidPlatform、AncientRuinsGlacier、Mechhive 等——海底遗迹新增 LayoutDef 即可挂进同一体系。
- 其余 Layout*Parms/LayoutThing/LayoutStructureSketch 均为纯数据/序列化容器（SKIP/PASS）。

### 9. 口袋地图与多层连接（弱相关，参考）

- LabyrinthMapComponent.cs（Anomaly）：CustomMapComponent 管理迷宫口袋地图——无殖民者且方尖碑毁后 `PocketMapUtility.DestroyPocketMap`；关闭流程用 `SkipUtility.SkipTo` 把 pawn/物品逐个传送回源图。海洋若做"进入沉船内部/海沟副本"可参考此生命周期管理。
- LayerConnection.cs：Odyssey 多层地图连接的数据载体（tag + fuelCost + ZoomMode In/Out），纯数据类。海面层↔海底层的双层地图若走 PlanetLayer 体系会用到同型数据（本片只有数据类，逻辑不在此）。

### 10. 明确无关（PASS 摘要）

- 药物/库存 JobGiver（TakeCombatEnhancingDrug、TakeDrugsForDrugPolicy、TakeCountToInventory、TakeForInventoryStock、TakeWoundedGuest、Unload*）：通用后勤 AI，无环境耦合。
- JobGiver_Work（工作扫描主循环）、JobInBedUtility、JobGiver_Tantrum、Wander 系列（参数都在 Verse 侧基类）、UnnaturalCorpse 系列（Anomaly 尸体 AI，基于 CanReach/Standable，无环境特判）。
- Joy 数值体系（JoyToleranceSet 厌倦度、JoyTunings 常量、JoyKindDef 头衔限制）。
- 学习系统其余（课桌/黑板 LearningUtility、观看工作、上课、无线电、读书、地板画）、教程 UI（Lesson/LessonAutoActivator/LearningReadout/Lesson_Instruction/Lesson_Note）。
- LifeStageWorker（成人/儿童生命阶段：换衣服、补背景故事、发信）。
- Lister 家族（Haulables/Mergeables/FilthInHomeArea/BuildingsRepairable/冥想建筑缓存）与 Listing_ResourceReadout：缓存/UI。
- JumpUtility（PawnFlyer 跳跃）、LiquidFuel（1500 tick 自干的燃料污渍）、LightningBoltMesh*/LineMeshGenerator（渲染）、LastPlayedVersion/LatestVersionGetter（版本号）、LatitudeSectionUtility（纬度带纯计算，海底恒温由别处覆盖）、LeaveQuestPartUtility（任务结束 pawn 离场）、LoadTransportersJobUtility（装运输舱）、Kidnap 三件套、以及全部仪式/聚会/典礼类 LordJob（Ritual、Duel、Marriage、Party、Concert、Speech、BestowingCeremony——注意 BestowingCeremony/SanguophageMeeting 有"中暑/低体温 ≥0.35 严重度就撤离"的触发器，同类环境撤离模式，但本体是任务典礼，PASS）。
- 所有 DefOf/枚举/参数结构：见 TSV 的 SKIP 行。
