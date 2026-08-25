# chunk-13 调研发现：JobDriver 家族（RimWorld 组，200 个文件）

本分片覆盖 `RimWorld/JobDriver_D*.cs` 至 `JobDriver_U*.cs`，共 200 个 JobDriver（ pawn 具体工作/行为 Job 的执行器）。绝大多数是"走过去→等待→做一件事"的固定套路，与海洋无关；但有若干家族对海底殖民地有直接/类比价值。

## TL;DR

1. **钓鱼系统完整可读**（`JobDriver_Fish.cs` / `JobDriver_FishAnimal.cs`）：Odyssey 的钓鱼 Job 给出全套挂点——`Zone_Fishing`、`FishingUtility.GetCatchesFor/GetNegativeFishingOutcomes`、`Map.waterBodyTracker`（鱼群资源追踪）、`StatDefOf.FishingSpeed`、负面渔获事件（伤害/hediff/信件）。海底殖民地的主食生产可以直接站在钓鱼系统上扩展。
2. **游泳娱乐 Job**（`JobDriver_GoSwimming.cs`）：`job.swimming` 姿势标记由 `Terrain.IsWater` 驱动，游泳可清除中暑（Heatstroke）。海底图"到处都是水"时该 Job 的语义需要重新审视（娱乐价值、要不要改成"潜水"）。
3. **灭火/点火 Job 全部不考虑水下**（`JobDriver_ExtinguishFiresNearby.cs` / `ExtinguishSelf.cs` / `Ignite.cs`）：非气密区充满海水的设定下，火应该无法存在；这些 Job 及 `pawn.natives.TryBeatFire/TryStartIgnite` 是水下禁用火的相关挂点（冲突类）。
4. **仰望天空类娱乐硬性要求露天**（`JobDriver_Skygaze.cs` / `Skydreaming.cs` / `GoForWalk.cs`）：`FailOn(Roofed)`、`weather.preventSkygaze`、`JoyUtility.EnjoyableOutsideNow`。海底"室外"致死且不见天日，这类娱乐必须屏蔽或替换（冲突类）。
5. **冥想类 Job 已挂 `MeditationUtility.SafeEnvironmentalConditions`**（`JobDriver_Meditate.cs` / `Reign.cs`）：这是原版用来屏蔽真空/恶劣环境冥想的检查点，海底非气密区可直接复用同一钩子（借用类）。
6. **MapPortal 进出 Job**（`JobDriver_EnterPortal.cs` / `HaulToPortal.cs` / `TakeAndEnterPortal.cs`）：Odyssey 的地图间传送门机制（进入→DeSpawn→在另一张图 Spawn、非口袋图自动卸货）。类比"海面基地↔海底殖民地"的潜水钟/竖井交通（借用类）。
7. **运输舱/舰载机登舰 Job**（`JobDriver_EnterTransporter.cs` / `HaulToTransporter.cs` / `PilotConsole.cs`）：`TransporterUtility.InitiateLoading`、`CompShuttle.IsAllowed`、GravshipLaunch 仪式。类比潜艇/深潜器载具（借用类）。
8. **觅食/狩猎/驯化等动物 Job 无环境感知**（`Forage`/`Hunt`/`PredatorHunt`/`Tame` 等）：`Forage` 只检查 `terrain.natural` 并产出 `Map.Biome.foragedFood`——自定义海底 Biome 需要配 foragedFood；捕食者被远程击中会逃跑（`StartFleeingBecauseOfPawnAction`），水生捕食者行为可直接继承。
9. **采矿/深钻 Job 无大气检查**（`JobDriver_Mine.cs` / `OperateDeepDrill.cs`）：海底岩层开采开箱即用，只需地形/矿脉 Def 支持。
10. **地图边缘离场语义**（`JobDriver_TakeAndExitMap.cs` / `Flee.cs` / `ReleaseAnimalToWild.cs`）：离场依赖 `exitMapGrid.IsExitCell` / `District.TouchesMapEdge`。海底图的"边缘"意味着什么（游走进深海？）需要定义。

## 主题详情

### 1. 钓鱼（直接相关，最重要）

**`JobDriver_Fish.cs`**（殖民者在钓鱼区钓鱼，Odyssey 限定）：
- 整个 Job 包在 `ModsConfig.OdysseyActive` 里；目标 A 是钓鱼点（水面格），B 是站立格（`ReservationLayerDefOf.Floor` 预留）。
- 持续条件：目标格必须处于允许钓鱼的 `Zone_Fishing` 内。
- 时长 `7500 / FishingSpeed`，期间学 Animals 技能、可被椅子提供舒适度；带 `EffecterDefOf.Fishing` 特效。
- 结算走 `FishingUtility.GetNegativeFishingOutcomes(pawn, cell)` + `FishingUtility.GetCatchesFor(pawn, cell, animalFishing: false, out rare)`：
  - 负面渔获（`NegativeFishingOutcomeDef`）：可对钓鱼者造成 `damageDef` 伤害、附加 hediff、发信件；并写 `map.waterBodyTracker.lastNegativeCatchTick`。
  - 正常渔获：`GenPlace.TryPlaceThing` 放到脚边；稀有渔获写 `waterBodyTracker.lastRareCatchTick` 并发正面信件；普通渔获调 `waterBodyTracker.Notify_Fished(cell, num)`（水体鱼群消耗），并记录 `HistoryEventDefOf.SlaughteredFish`。
- **海洋价值**：水下殖民地钓鱼=在"气密区内向海水开口的钓点"钓鱼，或者潜水员下水捕捞，整套产出/事件/资源消耗逻辑直接复用。`waterBodyTracker`（不在本片）是水体鱼群密度的核心数据结构，值得在对应分片重点读。

**`JobDriver_FishAnimal.cs`**（动物自己捕鱼，无 DLC 门）：逻辑是 Fish 的简化版（1500 tick、无负面事件），同样走 `FishingUtility.GetCatchesFor(animalFishing: true)` 和 `waterBodyTracker.Notify_Fished`。→ 水生驯养动物（如"渔兽"）的现成模板。

### 2. 游泳与水中姿势（直接相关）

**`JobDriver_GoSwimming.cs`**（Odyssey 游泳娱乐）：
- `CheckForSwimmingPose()`：脚底 `Terrain.IsWater` 则 `job.swimming = true`——这是 pawn 渲染成"游泳姿势"的开关，任何水中 Job 都可借用。
- 目的地在"心理上户外"（`Room.PsychologicallyOutdoors`）且 `JoyGiver_GoSwimming.HappyToSwimOutsideOnMap` 为假时 Job 失败（气温/天气不宜下水）。
- 游泳中 `lastSwamTick` 持续刷新；跳向下一目标时会**移除 Heatstroke hediff**（游泳降暑）。
- **海洋价值**：海底图全域是水——要么把"游泳"改成"舱外潜水"娱乐（需要潜水装备、不进入非气密区会死），要么在海底禁用该 JoyGiver。`HappyToSwimOutsideOnMap` 是现成的地图级开关挂点。

### 3. 火（冲突类）

- **`JobDriver_ExtinguishFiresNearby.cs`**：殖民者巡逻拍火，`pawn.natives.TryBeatFire(TargetFire)`，最长 600 秒。海水环境下 Fire 本体理应无法生成/持续；若 mod 直接让水下 Fire 无法存在，此 Job 自然空转，无需 patch；但**气密舱内失火**仍需要它——灭火 Job 不检查环境，舱内灭火开箱即用。
- **`JobDriver_ExtinguishSelf.cs`**：自燃自灭，150 tick 后销毁身上的 Fire。
- **`JobDriver_Ignite.cs`**：纵火（`pawn.natives.TryStartIgnite`）。敌对世界海盗纵火袭击在海底的表现依赖 Fire 是否能点着；如果气密建筑内可燃，纵火袭击仍成立。

### 4. 天空/户外依赖型娱乐（冲突类）

- **`JobDriver_Skygaze.cs`**：`FailOn(Roofed)`、`FailOn(weatherManager.curWeather.preventSkygaze)`、`FailOn(!JoyUtility.EnjoyableOutsideNow(pawn))`；愉悦增益系数 `gameConditionManager.AggregateSkyGazeJoyGainFactor`（日蚀/极光会改变它）；报告文案按 `Map.TileInfo.Layer.Def.isSpace`（太空层→"观星"）、日蚀、极光、`GenCelestial.CurCelestialSunGlow` 切换。**海底没有天空**：若海底图做成"全天黑+无日光"，Skygaze 会自动退化但仍可进行，需要决定是禁掉还是替换为"观海"。
- **`JobDriver_Skydreaming.cs`**（儿童"做白日梦"学习）：同样 `FailOn(Roofed)`。
- **`JobDriver_GoForWalk.cs`**（散步娱乐）：`FailOn(!JoyUtility.EnjoyableOutsideNow(pawn))`。`EnjoyableOutsideNow` 是温度/天气综合判断，海底"室外致死"不等于"不宜散步"——该工具函数只查温度天气，**不查真空/海水**，需要额外屏蔽手段（这正是 Odyssey 真空场景同样面对的问题，可参考 `MeditationUtility.SafeEnvironmentalConditions` 的做法）。

### 5. 冥想/王座（借用类）

- **`JobDriver_Meditate.cs`**（200 行，读了核心 80 行）：标准冥想流程，`MeditationUtility.PsyfocusGainPerTick(pawn, focusThing)` 按专注物加成。
- **`JobDriver_Reign.cs`**（王座"统治"冥想，继承 Meditate）：`FailOn(() => !MeditationUtility.CanMeditateNow(pawn) || !MeditationUtility.SafeEnvironmentalConditions(pawn, base.TargetLocA, base.Map))`——**`SafeEnvironmentalConditions` 就是原版（Royalty 起）用来阻止 pawn 去会死的地方做长时间静态活动的检查**（真空/极端温度）。海底非气密区只要被该函数识别为不安全，冥想/王座类 Job 就自动不会选外面；这是"海水致死区"应当对接的现成钩子（强烈建议后续查 `MeditationUtility.SafeEnvironmentalConditions` 本体，它多半在别的分片）。

### 6. 地图间交通：传送门与载具（借用类）

- **`JobDriver_EnterPortal.cs`**：进入 `MapPortal`——90 tick 等待后 `DeSpawnOrDeselect` → 在 `portal.GetOtherMap()` 的 `GetDestinationLocation()` 重生（不可站立则 `CellFinder.StandableCellNear` 5 格内找）；**进非口袋图（`!otherMap.IsPocketMap`）自动 `inventory.UnloadEverything = true`**；玩家控制的角色保持征召状态/可配置 `AutoDraftOnEnter`；手上的东西非征召时掉落。
- **`JobDriver_HaulToPortal.cs`**：向 Portal 装货（`EnterPortalUtility.FindThingToLoad`），90 tick 存放。
- **`JobDriver_TakeAndEnterPortal.cs`**：扛着倒地 pawn/物品进传送门（继承 EnterPortal）。
- **海洋价值**：海面↔海底的"潜水钟/升降竖井"最省事的实现就是 MapPortal 的 reskin——本片证明其 Job 层完全自包含。
- **`JobDriver_EnterTransporter.cs`** / **`HaulToTransporter.cs`**：运输舱/ shuttle 装载，`TransporterUtility.InitiateLoading`、`LoadTransportersJobUtility.FindThingToLoad`、`CompShuttle.IsAllowed(pawn)` 权限检查。类比"潜艇接送"。
- **`JobDriver_PilotConsole.cs`**：驾驶台触发 `PreceptDefOf.GravshipLaunch` 仪式窗口（Ideology 的 gravship 发射仪式）——载具出发的仪式化入口模板。
- （`JobDriver_InspectGravEngine.cs` 只是调 `Building_GravEngine.Inspect()` 的 31 行封装，无海洋价值。）

### 7. 动物相关（部分相关）

- **`JobDriver_Forage.cs`**：动物在自然地形（`terrain.natural`）上觅食 240 tick，产出 `Map.Biome.foragedFood` × 10~25。**自定义海底 Biome 必须配置 foragedFood**，否则动物觅食报错/无产出；`FailOn(!terrain.natural)` 意味着铺了地板的海底舱内不能觅食，合理。
- **`JobDriver_Hunt.cs`** / **`PredatorHunt.cs`**：狩猎/捕食流程（5000 tick 上限、0.95 倍射程接近、倒地后猎杀 `ExecutionUtility.DoHuntingExecution`）。`PredatorHunt.Notify_DamageTaken`：捕食者被**远程**攻击且非目标所为时 `StartFleeingBecauseOfPawnAction` 逃跑。对水生捕食者（鲨鱼）完全通用，无地形假设。
- **`JobDriver_Tame.cs` / `InteractAnimal.cs` / `Train.cs` / `Milk.cs` / `Shear.cs` / `GatherAnimalBodyResources.cs`**：驯化/训练/挤奶/剪毛，纯人际互动+喂食逻辑，无任何地形检查——水生动物驯养开箱即用。
- **`JobDriver_ReleaseAnimalToWild.cs`**：放生要走到 `District.TouchesMapEdge` 的可站立格。海底图边缘放行=游进深海，语义 OK，但"可站立格"（`Standable`）对纯水生动物的含义取决于水生移动实现。
- **`JobDriver_Mate.cs` / `LayEgg.cs`**：繁殖/产蛋，无环境检查。PASS 级。

### 8. 采集/建造（弱相关，基本兼容）

- **`JobDriver_Mine.cs`**：挖矿对 mineable 造成 Mining 伤害（天然岩 80/击、其他 40/击），`MineStrikeManager.CheckStruckOre`；无大气/光照检查——海底开矿无需 patch。
- **`JobDriver_OperateDeepDrill.cs`**：`CompDeepDrill.CanDrillNow()` + `DrillWorkDone`——深海钻探直接可用。
- **`JobDriver_PlantSow.cs` / `PlantSeed.cs`**：种植检查 `PlantUtility.AdjacentSowBlocker` 和 `plantDefToSow.CanNowPlantAt/CanEverPlantAt`（温度、地形肥力等在 Def 侧）；`PlantSeed` 走 `CompPlantable.PlantCells`（限定可种格）。海底农业=新植物 Def + 地形配置即可，Job 层无需改动。
- **`JobDriver_RemoveFloor.cs` / `RemoveFoundation.cs` / `SmoothFloor.cs` / `SmoothWall.cs` / `PaintFloor.cs`**：地形改造 Job，操作 `terrainGrid`；与海床地形编辑兼容。PASS。

### 9. 离场与边缘（弱相关）

- **`JobDriver_TakeAndExitMap.cs`**：带物离场，`exitMapGrid.IsExitCell` 或 `Position.OnEdge` 时 `ExitMap(allowedToJoinOrCreateCaravan: true, ...)`。
- **`JobDriver_Flee.cs`**：逃跑至边缘离场（`exitMapOnArrival`）。
- **海洋价值**：海底图的地图边缘是否允许"走出去组成商队"？大概率为否（需要载具/传送门），届时这些 Job 的离场路径要封掉或改接到潜艇。

### 10. 其他（确认无关）

其余约 170 个 JobDriver 为：人际互动（Insult/Romance/SingleInteraction…）、仪式（GiveSpeech/Sacrifice/Scarify…）、医疗（TendPatient/FeedBaby…）、搬运/库存（Unload*/TakeCount…）、娱乐建筑（PlayBilliards/Poker…）、Anomaly 实体管理（ExtractBioferrite/Revenant*/UnnaturalCorpse*…）、机械族（MechCharge/RepairMech…）、奴隶/囚犯（SuppressSlave/Execute…）等，均不触碰地形/大气/水体/温度语义，与海洋扩展无交集。其中 **`JobDriver_Seal.cs`**（31 行，调 `CompSealable.Seal()`，Anomaly 用于封矿井口）名字虽像"密封舱门"，实际只是通用可密封物交互，若做气闸可借鉴 `CompSealable` 模式但本片仅此一层封装。
