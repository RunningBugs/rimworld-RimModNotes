# chunk-04 调研发现（组：RimWorld，ThingComp C 段 + Complex 家族）

本片 200 个文件，几乎全是 `ThingComp`（Comp* 开头）加上 Anomaly 的 Complex（遗迹/迷宫）威胁生成家族。Comp 是 RimWorld 机制的最小复用单元——大量条目本身就是"可直接抄作业的模板"。

## TL;DR

1. **真空泵直接模板：`CompOxygenPusher`**（RimWorld/CompOxygenPusher.cs）——Odyssey 的氧气泵，24 行，每 TickRare 执行 `room.Vacuum = room.UnsanitizedVacuum - num`（num = 100/房间格数 × Props.airPerSecondPerHundredCells × 4.167）。"排水阀"照抄：把 Vacuum 语义换成"海水占比"即可，连功率检查（`!Props.requiresPower || !PowerTrader.Off`）都一样。
2. **Odyssey 真空 API 全貌在 `CompLowPowerInSpace` 里暴露**（CompLowPowerInSpace.cs）：`Room.ExposedToSpace`、`Room.Vacuum`（float，0≈无真空）、`Room.UnsanitizedVacuum`、`Map.Biome.inVacuum`。它还演示了"按环境切功耗档"（lowPowerConsumptionFactor）——海底设备可做"水下低压运行"同款。
3. **风电已在真空下禁用，水下要同样处理**：`CompPowerPlantWind.RecalculateBlockages()` 直接 `blockedDueToInOrbit = parent.Map.Biome.inVacuum`（CompPowerPlantWind.cs:217）。海洋 biome 若复用类似 flag，风电自动获得"水下不可用"提示（"CannotFunctionOnLayer"/被堵文案）。
4. **深海水下太阳能归零是"免费"的**：`CompPowerPlantSolar.DesiredPowerOutput` 正比于 `Map.skyManager.CurSkyGlow`（CompPowerPlantSolar.cs:17）——只要海底地图的 SkyGlow 极低，太阳能自然失效，无需改 comp。
5. **水车（CompPowerPlantWater）是"洋流发电机"的现成模板**：用 `cell.GetAffordances(map).Contains(TerrainAffordanceDefOf.MovingFluid)` 判定工作格，用 `map.waterInfo.GetWaterMovement(pos)` 取水流方向决定叶片转向（CompPowerPlantWater.cs:72-117）。水下涡轮只需把 affordance 换成海底专属地形。
6. **建筑→全图环境条件的整套模式**（CompCauseGameCondition_* 家族）：ForceWeather（强制天气）、TemperatureOffset（气温偏移，即"气候调节器"）、ToxicSpewer（毒气+对 worldRange 内商队掉血）。海底恒温 4°C 意味着必须屏蔽/改写 TemperatureOffset 类条件（热浪/寒流同理，属 GameCondition 层，不在本片）。
7. **光环 hediff 两个模板**：`CompCauseHediff_AoE`（半径+视线无关+可设 onlyTargetMechs/ignoreMechs，配 HediffComp_Disappears 自动消退）和 `CompGiveHediffSeverity`（**同房间判定** `pawn.GetRoom() != parent.GetRoom()`，87 tick 间隔加 severity）——"深海高压""氧中毒""减压病"等可直接用。
8. **潜水服模板：`CompCauseHediff_Apparel`**——穿上装备即获得绑定该装备的 hediff（HediffComp_RemoveIfApparelDropped 自动移除）。
9. **泵类循环模板：`CompPollutionPump`**——60 tick 检查、半径内螺旋扫描第一个合法格、产出副产物（垃圾包）、被"人造建筑列表"禁用。排水/采集资源的节奏控制可直接借用。
10. **世界格倾倒废料对水格有减免**：`CompDissolutionEffect_Pollution` 里 `tile.WaterCovered` 时污染量 × `Props.waterTilePollutionFactor`（CompDissolutionEffect_Pollution.cs:39-41）——原版已有"水格污染折减"先例，海洋殖民地排废可以挂这个系数。
11. **Complex 威胁家族 = 海底沉船/遗迹探索的生成框架**：`ComplexThreatWorker` 基类的信号触发体系（房间进入触发 RectTrigger、半径触发 RadialTrigger、黑客入侵触发、延迟+威胁点数曲线），`ComplexThreatWorker_SleepingThreat` 按 `RaceProps` 过滤刷怪（昆虫/机械族）——换成"水生生物"谓词就是"沉睡的深海怪物"。
12. **深钻在深水格不下矿**：`CompDeepScanner.CanScatterAt` 排除 `terrainDef.IsWater && passability == Impassable` 的格子（CompDeepScanner.cs:71-76）。海底地图若整图是 Impassable 水，深层扫描会完全找不到落点——需要自定义海底基底地形（非 Impassable 的"海床"）。

---

## 1. 真空/海水环境（Odyssey API 实证）

### CompOxygenPusher.cs ★ 本片最重要
```csharp
public override void CompTickRare()
{
    if (!Props.requiresPower || !PowerTrader.Off)
    {
        Room room = parent.GetRoom();
        float num = 100f / (float)room.CellCount * Props.airPerSecondPerHundredCells * 4.1666665f;
        room.Vacuum = room.UnsanitizedVacuum - num;
    }
}
```
- `Room.Vacuum` 是可写 float，`UnsanitizedVacuum` 是"自然值"，泵按房间大小把 Vacuum 往下压。房间越小抽得越快。
- 类比：**"排水阀/排水泵"= 把 Vacuum 语义替换为海水占比**。房间格数归一化（per hundred cells）的设计直接照抄。

### CompLowPowerInSpace.cs
- `NeedsPower()`：`Props.checkRoomVacuum` 时检查 `room.ExposedToSpace` 与 `Mathf.Approximately(room.Vacuum, 0f)`；再检查 `parent.Map.Biome.inVacuum`。
- 低功耗模式下 `PowerTrader.PowerOutput = -PowerConsumption * lowPowerConsumptionFactor`。
- 类比：海底设备"暴露在海水中时降功耗/升功耗"的同款开关；同时确认了 **Room 级真空度的三个公开成员**（ExposedToSpace / Vacuum / UnsanitizedVacuum）和 **Biome.inVacuum** 是环境判定的官方入口。

## 2. 发电与环境的耦合（海洋=无风、无光、有水流）

- **CompPowerPlantWind.cs:217**：`blockedDueToInOrbit = parent.Map.Biome.inVacuum` → 真空中风电输出强制 0，并在 InspectString 显示 "CannotFunctionOnLayer"。海洋 biome 需要等价 flag 让风电失效。
- **CompPowerPlantSolar.cs:17**：输出 = `Lerp(0, 满功率, Map.skyManager.CurSkyGlow) × 屋顶遮挡因子`。海底 SkyGlow≈0 → 太阳能自然报废，无需禁建造。
- **CompPowerPlantWater.cs**：水车判定格必须含 `TerrainAffordanceDefOf.MovingFluid`；`WaterUseRect`（7×13）内若有另一台水车则功率 ×0.3；叶片转速方向由 `map.waterInfo.GetWaterMovement()`（格级水流向量）决定。→ **海底洋流发电机的完整参照**，水流数据在 `Map.waterInfo` 已存在。
- **CompPowerPlantGravcore.cs**：每 60 tick 检查脚下 `terrainGrid.FoundationAt(pos)?.IsSubstructure`，不在地基上不发电——"必须建在特定地形/地基上"的检查模板（海底版："必须建在海床/耐压壳上"）。
- CompPower / CompPowerTrader / CompPowerBattery / CompPowerPlant 等电网基础件与海洋无特殊交互（PASS）。

## 3. GameCondition 引发器家族（环境事件的"源"）

父类 CompCauseGameCondition 不在本片，但五个子类展示了完整用法：

- **CompCauseGameCondition_ForceWeather**：建筑强制全图天气（weatherDef 来自 conditionDef.weatherDef）。→ 海洋可用来锁死"无雨/常态阴"或做"风暴"事件源。
- **CompCauseGameCondition_TemperatureOffset**：气候调节器。`GameCondition_TemperatureOffset.tempOffset` 直接加在地图温度上；`RandomizeSettings` 会参考世界格最低/最高温（GenTemperature.MinTemperatureAtTile/MaxTemperatureAtTile）决定给玩家加热还是降温。→ **海底恒温 4°C 的殖民地，热浪/寒流/气候调节器都应在 GameCondition 层屏蔽**（本片确认偏移量机制只是 tempOffset 加法，拦截容易）。
- **CompCauseGameCondition_ToxicSpewer**：除地图条件外，每 3451 tick 对 worldRange 内的**商队** pawn 直接 DoPawnToxicDamage——"环境伤害波及世界地图旅行者"的写法。
- PsychicEmanation / PsychicSuppression：心灵类，与海洋无关（PASS）。

## 4. hediff 光环与穿戴防护（高压/潜水病/潜水服）

- **CompCauseHediff_AoE**：半径 range 内所有 pawn（可选只打机械/忽略机械）挂指定 hediff，靠 `HediffComp_Disappears`（ticksToDisappear = checkInterval+5）离开范围自动消退；可挂 CompPowerTrader 供电门控。→ "高压区""热泉温暖区"。
- **CompGiveHediffSeverity**：`AppliesTo` 用 **同一房间** 判定（`pawn.GetRoom() != parent.GetRoom()`），每 87 tick 加 severityPerSecond×1.45，支持燃料/供电门控。→ 房间级环境 buff/debuff（如"加压舱内逐渐适应"）。
- **CompCauseHediff_Apparel**：`Notify_Equipped` 时 AddHediff，并用 `HediffComp_RemoveIfApparelDropped.wornApparel = 本装备` 绑定，脱掉自动移除。→ **潜水服/抗压甲的标准做法**。

## 5. 泵与周期处理模板

- **CompPollutionPump**：Active 条件=生成中+未被附近人造建筑禁用（`map.listerArtificialBuildingsForMeditation.GetForCell`）+通电+还有活干；每 intervalTicks 螺旋扫描半径取第一个 `CanUnpollute` 格处理，N 次产出一个副产物。→ 排水/滤水/采砂的节奏框架。
- **CompPolluteOverTime**：反向，每天 N 格污染扩散（`intVec.CanPollute(map)` / `Pollute(map)`）。
- **CompDissolution**：垃圾溶解速率受 `UsesOutdoorTemperature`（室外）与**降雨**（`curWeather.rainRate > 0.1f` 加速 dissolutionFactorRain 倍）影响，冻结（AmbientTemperature≤0）停止。→ 海底"室外"=泡在水里，物品腐蚀速率可挂同一组判断。
- **CompDissolutionEffect_Pollution**：世界格倾倒时 `tile.WaterCovered` → 污染 ×waterTilePollutionFactor（原版承认水体的稀释作用）。

## 6. 海底采矿与"惊扰"

- **CompDeepDrill**：portionProgress/portionYieldPct 模型，资源取自 `map.deepResourceGrid`；枯竭后回落到 `DeepDrillUtility.GetBaseResource`（基底资源），并自动禁用周围 21 格内无矿的钻头。
- **CompDeepScanner.CanScatterAt**：`(terrainDef.IsWater && terrainDef.passability == Impassable) || !pos.GetAffordances(map).Contains(DeepDrill.terrainAffordanceNeeded)` → 不生成矿点。**注意：深海 Impassable 水体会让扫描完全失效**，海底需要可通行的"海床"基底地形。
- **CompCreatesInfestations**：钻井工作时可引发 infestation，两个节流器：本机 7 天（420000 tick）冷却 + 10 格内其他同类装置近期触发过则抑制。→ "钻探惊扰海底生物"的现成节流模式。

## 7. Complex 威胁生成框架（沉船/海底遗迹）

- **ComplexThreatWorker（基类）**：触发信号三级回退——复用房间内已有触发物（CompHackable 的 hackingStarted/CompletedSignal、Building_Casket.openedSignal）→ `ComplexUtility.SpawnRoomEnteredTrigger`（RectTrigger）→ 纯被动。支持 delayChance + `threatFactorOverDelayTicksCurve`（延迟触发换威胁加成）。
- **ComplexThreatWorker_SleepingThreat**：按 `GetCombatPawnKindsForPoints` 谓词刷"沉睡怪"（子类分别过滤 `RaceProps.Insect` / `IsMechanoid && isFighter && allowInMechClusters`），LordJob_SleepThenAssaultColony，20% 概率加 RadialTrigger 近距离惊醒。→ **把谓词换成"水生"即是海底版**；`CanSpawnAt` 要求 Standable+无门+无人。
- **ComplexUtility**：`SpawnRoomEnteredTrigger` / `SpawnRadialDistanceTrigger`（视线+半径）两个静态 helper 可直接复用。
- 其余 Worker（Ambush/CryptosleepPods/FuelNode/Infestations/RaidTerminal/SecurityCrate）均为同一信号体系的实例（PASS，无新机制）。ComplexLayoutDef/ComplexThreatDef 等是纯数据 Def（SKIP）。

## 8. 建筑启用条件的检查模式

- **CompGravshipThruster**："推进器必须朝室外"——`IsOutdoors` 检查尾焰侧相邻格的 `room.UsesOutdoorTemperature`；`IsBlocked` 检查排气区是否有 `blockWind` 物体或 `terrainDef.IsSubstructure`。→ "排水阀/推进器必须接触海水"的判定写法（换谓词即可）。
- **CompMannable**：`Props.planetLayerWhitelist`（PlanetLayerDef 列表）——不在白名单层则 InspectString/菜单直接报 "CannotFunctionOnLayer"。→ 若海洋实现为独立 PlanetLayer，这是**最省事的建筑层禁用手段**。
- **CompPilotConsole**：gravship 选目的地时调用 `TileFinder.IsValidTileForNewSettlement(tile, cannotPlaceTileReason, forGravship: true)`——**"海洋格能否降落"的合法性钩子入口在 TileFinder**（不在本片，需后续分片确认海洋格是被拒还是可落）。
- **CompInteractable**（基类，479 行，已读核心 240 行）：玩家手动激活框架——activeTicks/cooldownTicks 状态机、进度条 mote、可选携带物品（interactableIndex）、`CanInteract` 全套 AcceptanceReport（燃料/电/禁区/倒地/操作能力/可达性）。→ 任何"派人去拧阀门"的建筑（排水阀、舱门泄压）直接继承。

## 9. 恒温 4°C 下的行为推论（读出来的事实）

- **CompMelter**：`AmbientTemperature < 0` 才不化；4°C 时冰块每 TickRare 化 `RoundRandom(0.15×4/10)`≈每 rare tick 0~1 点 Rotting 伤害——海底殖民地冰无法天然保存，但也化得慢。
- **CompMaintainable**：维护三阶段（Healthy/NeedsMaintenance/Damaging），Damaging 阶段每 rare tick 吃 `DamageDefOf.Deterioration`——"海水腐蚀需要定期维护"可直接用此 comp。
- **CompEggLayer / CompHatcher**：蛋的孵化由 CompTemperatureRuinable 冻结判定参与；4°C 不会"冻坏"（Ruined 阈值在 CompTemperatureRuinable，本片外）。
- **CompHasGatherableBodyResource**（抽象基类，CompMilkable 等的父类）：fullness 按天累积、`PawnUtility.BodyResourceGrowthSpeed` 修正、`StatDefOf.AnimalGatherYield` 决定采集成败。→ 水生生物的"采珍珠/采毒液/挤墨汁"直接继承。

## 10. 冲突/需屏蔽清单

- **水下气体**：`CompGasOnDamage` / `CompGasOnDestroyed` 走 `GasUtility.AddGas`（毒气/死灵尘等）。水下气体应当无法存留（或转为上浮气泡视觉）——所有产气 comp 依赖 GasUtility，堵在 GasUtility 层即可（GasUtility 不在本片）。
- **火**：CompFireOverlay/CompFireBurst 等燃烧表现未做任何环境检查（comp 层不管，灭火逻辑在 Fire 主类，本片外）。水下需在上层禁火。
- **风电/太阳能**：见 §2，靠 Biome flag 与 SkyGlow 自然失效，但要确认玩家看到的是"不可用的提示"而非"建了个废物"。
- **CompObelisk_Mutator.cs:157**：刷怪格检查 `!c.GetTerrain(map).IsWater`——原版已有"不在水格刷怪"先例，水生怪反过来用即可。

## 覆盖说明

- 本片全部 200 个文件均已处置：30 NOTE / 139 PASS / 31 SKIP，明细见 `coverage/results/chunk-04.tsv`。
- 大文件未逐行读完的：`CompHackable`（613 行，读成员大纲）、`CompHoldingPlatformTarget`（446 行，大纲）、`CompLaunchable`（637 行，大纲+关键方法）、`CompNociosphere`（493 行，大纲）、`CompMetalhorror`（196 行，大纲）、`CompOverseerSubject`（读前 100 行）、`CompPilotConsole`（读前 120 行）、`CompPlantable`（读前 120 行）、`CompInteractable`（读前 240 行）、`CompProjectileInterceptor`（读前 200 行）、`CompIngredients`（读前 80 行）、`CompCerebrexCore`/`CompDeathrestBindable`（成员大纲）。这些文件的海洋相关面均已覆盖，未读部分均为 DLC 专属细节（机巢/死眠/异常实体），如有疑义标注「未验证」。
