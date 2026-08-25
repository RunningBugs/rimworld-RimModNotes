# chunk-30 调研发现（RimWorld 组：WorkGiver 尾段 / World* / Xenotype / Zone 家族）

## TL;DR

- **`Zone_Fishing.cs` 是海洋 Mod 的核心入口之一**：渔业区直接挂在 `WaterBodyType` / `Map.waterBodyTracker` 鱼群密度系统上，含结冰（ThinIce）检测、目标捕捞量/重复次数模式、派系/意识形态禁令——做"海洋捕捞/水产养殖"整套照抄它即可。
- **`WorkGiver_PlantSeed.cs` 提供了"真空抗性植物"判定模板**：`plantDefToSpawn.plant.vacuumResistant || cell.GetVacuum(map) < 0.5f` —— 做"水生植物/海底种植"时这是直接的移植模板（加一个 `waterResistant` 式标志位 + 对应环境查询）。
- **`Zone_Growing.cs` 揭示生长季的温度语义**：陆地图按世界格温度算生长季，Pocket Map 直接看 `mapTemperature.OutdoorTemp` —— 海底恒温 4°C 下"室外种植全年不可行"会自然成立，无需额外屏蔽。
- **囚犯释放/奴隶解放受 `Map.CanEverExit` 门控**（`WorkGiver_Warden_ReleasePrisoner` / `WorkGiver_Warden_EmancipateSlave`），放生野生动物要求可达"室外格"（`WorkGiver_ReleaseAnimalsToWild`）——海底地图若不可步行出图，这些工作自动禁用，属预期行为但要在文档/玩法上交代。
- **异种（Xenotype）体系完整可读**：`XenotypeDef`（genes + inheritable + 生成权重）、`XenotypeSet`（派系权重，Baseliner 兜底）、`Xenogerm`（植入手术流程）——做"水生人种/鳃基因"走这条路，纯 Def 即可。
- **世界层对象定义**（`WorldObjectDef` / `WorldObjectDefOf` / `WorldInterface` / `WorldTargeter`）：`canBePlayerHome`、`canResizeToGravship`、`fullyExpandedInSpace`、`PlanetLayer` 跨层目标选择——海洋格定居点和"潜艇/潜水器"类世界对象的挂载点。

## 一、渔业系统（直接相关）

### Zone_Fishing（`Zone_Fishing.cs`，396 行，全读）

渔业区是本片最重要的文件，机制全部实读确认：

- **鱼群密度门控**：`OverTargetPopulation` 要求 `Map.TileInfo.MaxFishPopulation > 0` 且 `Map.waterBodyTracker.PopulationPercentAt(cell) > targetPopulationPct`（默认 0.5）。即每个水体（water body）有独立鱼群数量，过度捕捞会自然停捕。`GetInspectString` 里还读 `gameConditionManager.FishPopulationOffsetFactorPerDay` 显示"鱼群衰减"元凶（如污染/事件）。
- **水体类型匹配**：`FishType = Cells[0].GetWaterBodyType(map)`（`WaterBodyType` 枚举，含 None）；`IsFishable(cell)` 要求 `terrain.IsWater` 且水体类型与全区一致。→ 海洋语境：海水/淡水/毒水等不同 `WaterBodyType` 直接区分可捕鱼种的基础。
- **结冰检测**：`AllFishableCellsFrozenNow` 检查所有格地形是否为 `TerrainDefOf.ThinIce`（temporary 地形），60 tick 缓存。→ 深海恒温 4°C 不结冰，此分支天然不触发；若做"极地浅海"则直接可用。
- **捕捞模式**：`FishRepeatMode`（DoForever / TargetCount / RepeatCount），`OwnedFishCount` 统计 `ThingRequestGroup.Fish`（地上+殖民者携带）。暂停/恢复阈值（`pauseWhenSatisfied`/`unpauseAtCount`）是一套完整的"自动限量采集"UI 范式。
- **站位逻辑**：右键优先捕鱼时调用 `WorkGiver_Fish.BestStandSpotFor(pawn, cell)`（不在本片，未验证其实现），且首选"站位本身不是水"的格（`!spot.GetTerrain(map).IsWater`），找不到才允许站在水里。→ 海底语境下殖民者本身就"在水里"，钓鱼站位语义需要重新审视（潜水捕鱼？）。
- **意识形态**：`HistoryEventDefOf.SlaughteredFish` 可禁止捕鱼；`WorkTypeDefOf.Fishing`（`WorkTypeDefOf.cs`）是 `[MayRequireOdyssey]` 的独立工作类型。

## 二、种植与温度（借用/冲突）

### WorkGiver_PlantSeed（`WorkGiver_PlantSeed.cs`）

DLC 新增的种子种植（CompPlantable，Ideology/Biotech/Anomaly/Odyssey 任一激活才启用）。关键判定出现两次（HasJobOnThing/JobOnThing 各一次）：

```csharp
compPlantable.Props.plantDefToSpawn.plant.vacuumResistant || !(plantCells[i].GetVacuum(t.Map) >= 0.5f)
```

即：**非真空抗性植物不允许种在真空度 ≥0.5 的格子上**。这是 Odyssey"真空环境门控行为"的标准写法，海洋 Mod 照此模式加"海水淹没门控"（如 `submersionResistant || cell 无海水`）即可，连阈值 0.5 的习惯都有先例。

### Zone_Growing（`Zone_Growing.cs`）

- `GrowingQuadrumsDescription(tile, plant)`：按世界格温度落在植物最适区间（默认 6–42°C）的十二分月数算生长季；海底世界格温度若按海洋处理，陆地作物生长季自然为零。
- **Pocket Map 分支**（`map.IsPocketMap`）：不看世界格，直接看 `map.mapTemperature.OutdoorTemp` 是否落在 6–42°C。→ 若海底地图做成 pocket map 式，室外恒温 4°C 会让室外种植显示"全年不可种植"——正确且免费的行为。
- 默认作物：全区被污染时默认改种 `Plant_Toxipotato`，否则土豆。

## 三、动物相关工作（部分借用）

- **`WorkGiver_HunterHunt.cs`**：狩猎硬性要求**非爆炸物的远程武器**（`HasHuntingWeapon`：IsRangedWeapon && HarmsHealth && !UsesExplosiveProjectiles），盾牌+枪组合不许狩猎；狩猎站位用 `CastPositionFinder.TryFindCastPosition`，射程取 `verb.EffectiveRange * 0.95`。→ 水下狩猎（鱼叉枪？）必须提供满足这三条件的"水下远程武器"，否则狩猎工作全体跳过（`ShouldSkip`）。意识形态崇敬动物/无辜动物杀戮事件也在此拦截。
- **`WorkGiver_TakeToPen.cs`**：畜栏体系全貌——`AnimalPenBalanceCalculator`（按图缓存，每 tick 标脏）、封闭栏（`RopeToPen`）/非封闭栏（`RopeRoamerToUnenclosedPen`）/系留柱（`GetHitchingPostAnimalShouldBeTakenTo`）三层目标。→ 海底养水生动物若沿用 pen 体系，围栏在海水里的通行/围挡语义是主要未知数（本片无答案）。
- **`WorkGiver_ReleaseAnimalsToWild.cs`**：放生要求 `JobDriver_ReleaseAnimalToWild.TryFindClosestOutsideCell` 找到可达"室外格"，否则报 `NoReachableOutsideCell`。全海底地图上"outside"的语义（气密建筑外=海水=致死）意味着陆生动物放不出去——合理，但要意识到该工作会永久不可用。
- `WorkGiver_Tame/Train/InteractAnimal/Slaughter/Milk/Shear`：通用驯养/训练/屠宰/采集，无环境门控，水生动物直接可用（驯服需要携带动物愿意吃的食物，`HasFoodToInteractAnimal` 按营养值数两份口粮）。

## 四、地图可退出性与囚犯/奴隶（冲突类，需交代）

- **`WorkGiver_Warden_ReleasePrisoner.cs`**：释放囚犯时若 `pawn.Faction != prisoner.Faction && !MapHeld.CanEverExit`，报 `CannotExitMap` 拒绝；且需 `RCellFinder.TryFindPrisonerReleaseCell` 找到释放格（通常是地图边缘）。
- **`WorkGiver_Warden_EmancipateSlave.cs`**：同样检查 `MapHeld.CanEverExit`。
- → 海底定居点若设定为不可步行离图（只能靠潜艇/运输舱），这两个工作自动禁用，是合理的玩法结果；但要确认地图边缘格在海底地图上的生成方式，避免 `TryFindPrisonerReleaseCell` 永远失败造成静默无作业。

## 五、异种体系（水生人种路线）

- **`XenotypeDef.cs`**：纯 Def——`genes`、`inheritable`、`combatPowerFactor`、`canGenerateAsCombatant`、`factionlessGenerationWeight`、`forbiddenWeaponClasses`、`doubleXenotypeChances`（校验：合计 ≤1 且目标必须 inheritable）。→ "水生人种"加一个 XenotypeDef + 若干 GeneDef（如水下呼吸、耐压、低温适应）即可进生成池，无需 C#。
- **`XenotypeSet.cs`**：派系的异种权重集合；`BaselinerChance = 1 - Σ(非 Baseliner 权重)`，未激活 Biotech 时恒为 1。给海底袭击派系配权重用。
- **`Xenogerm.cs`**：异种胚芽植入的完整流程——确认对话框（覆盖现有异基因警告、代谢率变化预览）、`RecipeDefOf.ImplantXenogerm` 手术账单、医疗床/医药不足提示信、`GeneTuning.BiostatRange` 下限校验、意识形态拦截（Bloodfeeder 基因、非偏好异种）。→ 若做"海洋适应基因"植入，玩家走的就是这条路，无冲突。

## 六、世界层（海洋格定居的挂载点）

- **`WorldInterface.cs`**：世界界面中枢，聚合 `WorldSelector`/`WorldTargeter`/`TilePicker`/`WorldRoutePlanner`/`WorldGlobalControls`；`Reset()` 里处理起始格越界修正并跳转相机。选海洋格降落的 UI 流程经 `TilePicker`（本片无其实现）。
- **`WorldTargeter.cs`**：世界目标选择器。值得注意的是 `originForClosest.Layer != PlanetLayer.Selected` 时用 `PlanetLayer.Selected.GetClosestTile_NewTemp(origin)` 算跨层最近格——Odyssey 的多层世界（地表/轨道）交互模式。→ 若"海面/海底"做成两个 PlanetLayer，这套跨层目标选择直接可借用。
- **`WorldObjectDef.cs`**：世界对象 Def 的关键布尔位：`canBePlayerHome`（玩家定居点）、`canResizeToGravship`（可被重力船"吸走"改尺寸）、`requiresSignalJammerToReach`、`fullyExpandedInSpace`（太空中始终展开图标）、`blockExitGridUntilBattleIsWon`、`mapGenerator`（MapGeneratorDef 挂点）、`overrideMapSize`。→ 海底定居点 WorldObject 的定义模板全在这里。
- **`WorldObjectDefOf.cs`**：内置种类清单——`PocketMap`、`Site`、`Camp`、`ClaimableSite`、`ClaimableSpaceSite`（Odyssey）、`Gravship`、`AbandonedLandmark`（Odyssey）等。"海底遗迹/沉船点"应对标 `Site`/`ClaimableSite` 家族。
- `WorldObjectCompProperties_*` 家族全部是 11–24 行的 `compClass` 绑定壳（TimedDetectionRaids、FormCaravan、Abandon、EscapeShip 等），实际逻辑在对应 Comp 类（不在本片）。

## 七、确认无关的家族（PASS 概览）

- **Tend/PatientGoToBed/VisitSickPawn** 医疗家族、`Warden` 大部分（招募/转化/喂食/处决/镇压/审讯）、`Strip`、`RescueDowned`、`TakeToBed*`：纯人际/医疗逻辑，无环境判定。
- **Paint/RemovePaint** 四件：染色系统（`building.paintable`、`terrain.isPaintable`、染料 ThingDefOf.Dye）。
- **Scanner/Researcher/Study/Suppress/TakeBioferrite/TransferEntity/TendEntity**：Anomaly 实体与研究体系。
- **Merge/UnloadCarriers/LoadTransporters/HelpGatheringItemsForCaravan**：搬运物流。
- **RemoveRoof/Repair/RemoveBuilding/Uninstall/Open/Miner/PlantsCut/Replant**：建造/拆除/收割常规工作（RemovePaintFloor 里有一处 `terrainAffordanceNeeded` 对底层地形 affordance 的检查，是"地板依赖地基"的零散先例，但不足以构成发现）。
- **WornGraphic* 三件、ZonePresetNames、Zone_Stockpile、WorkTypeDefOf 其余字段、各 DefOf/纯数据类**：渲染数据与翻译工具。
