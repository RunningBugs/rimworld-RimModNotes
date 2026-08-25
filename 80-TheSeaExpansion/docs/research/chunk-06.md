# chunk-06 调研发现（RimWorld 组：CompProperties M–W + Comp P–Ship 段）

本片 200 个文件全部是 `CompProperties_*`（纯数据属性类）和 `Comp*`（ThingComp 实现）的 M–Sh 字母段。大量文件是仪式视觉特效、Anomaly/Royalty 专属绑定，与海洋无关；但夹藏着几块对「海底殖民地」极关键的机制拼图：Odyssey 的氧气/真空体系触点、抽水泵/地形改造器、气密封堵、着陆区、扫描仪、腐烂/温控。

## TL;DR

- **Odyssey 氧气推送器属性在本片**：`CompProperties_OxygenPusher.cs` —— `requiresPower` + `airPerSecondPerHundredCells = 0.1`，绑定 `CompOxygenPusher`（实现类不在本片）。海底基地的「供氧」可以直接照抄这套参数模型。
- **「真空中不工作」开关的完整范式在本片**：`CompRefuelable.cs` 继承 `ThingComp_VacuumAware`（在 Verse 命名空间，`InVacuum => parent.PositionHeld.GetVacuum(map) > 0f`），`CompProperties_Refuelable.cs` 有 `functionsInVacuum = true` 字段。类比：燃烧类设备（篝火、发电机、火把）在海水中应直接失效——这套「环境感知 comp + 属性开关」就是要借用的模式。
- **抽水泵全家桶在本片**：`CompProperties_TerrainPump.cs`（基类：`radius=5.9`、`daysToRadius=5`）→ `CompProperties_TerrainPumpDry.cs`（潮湿泵，绑定 `CompTerrainPumpDry`）+ `CompProperties_Terraformer.cs`（每 20 秒把一格地形转成 `convertTerrainDef`，半径 6.9，实现 `IPlantEffectRadius`）。「排水阀/海底造陆」的参数模板直接在这。
- **气密封堵的官方实现在本片**：`CompSealable.cs` + `CompProperties_Sealable.cs` —— 把 `MapPortal`（口袋地图入口）永久封死，封前弹确认框列出「将被留下的人员/动物」，可选 `destroyPortal`。海底舱段弃舱封门可整套借用。
- **扫描仪有屋顶禁用冲突**：`CompScanner.cs:32` 的 `CanUseNow` 里 `RoofUtility.IsAnyCellUnderRoof(parent)` 直接拒绝工作（"CannotUseScannerRoofed"）。海底地图如果把「头顶海水」实现为屋顶，地质扫描仪/深扫描会全部瘫痪——必须决策。
- **着陆信标同样查屋顶**：`CompShipLandingBeacon.cs` 的 inspect 显示 `BlockedByRoof` / `BlockedByTerrain`（地形适配 `landingAreaTerrainSupport`）。潜水器/潜艇停泊区可借用信标圈地模型，但屋顶检查是冲突点。

---

## 一、氧气 / 真空体系触点（强相关，借用）

### CompProperties_OxygenPusher.cs
- 类：`CompProperties_OxygenPusher : CompProperties`，绑定 `CompOxygenPusher`（不在本片）。
- 字段：`requiresPower = true`；`airPerSecondPerHundredCells = 0.1f`——即「每 100 格每秒产 0.1 单位空气」的产氧速率模型。
- 类比：海底气密舱的供氧建筑照抄此参数结构；氧气系统在海底就是「气密空间内可呼吸」的核心资源。

### CompRefuelable.cs / CompProperties_Refuelable.cs（真空感知范式）
- `CompRefuelable : ThingComp_VacuumAware`（`Verse/ThingComp_VacuumAware.cs`：`InVacuum => parent.PositionHeld.GetVacuum(parent.MapHeld) > 0f`，抽象属性 `FunctionsInVacuum`，不满足时 inspect 显示 "CannotFunctionInVacuum" 警告色文本）。
- `CompRefuelable.cs:71-74`：`HasFuel` 里若 `!FunctionsInVacuum` 且 `InVacuum` 则视为无燃料——篝火/燃料发电机在真空自动熄火。
- 属性侧：`functionsInVacuum = true`（XML 可关）；另有 `fuelConsumptionPerTickInRain`（`CompRefuelable.cs:231`：降雨强度 >0.4 且无屋顶时额外耗燃料）。
- 类比：海洋版应做 `ThingComp_SeaWaterAware`（或复用 VacuumAware 的格子查询改成查「海水」），燃烧设备、普通电器在海水格里失效。这是本片最具复制价值的设计模式。

### CompReleaseGas.cs / CompProperties_ReleaseGas.cs
- 机制：`GasUtility.AddGas` 按 `durationSeconds` 把 `cellsToFill * 255` 总量的气体（`GasType` 枚举）均匀释放；可挂在服装/携带物上（EffecterSourceThing 处理了装备/携带持有者）。
- 类比：气体网格系统已存在且可任意扩展 GasType——「空气泡」「氧气释放」「毒气在水中扩散」都有落点；也可反向用于表现气密舱漏气。

## 二、排水 / 造地（直接相关，模板）

### CompProperties_TerrainPump.cs → CompProperties_TerrainPumpDry.cs
- 基类仅两字段：`radius = 5.9f`、`daysToRadius = 5f`——潮湿泵「每天扩张 1/5 半径直至抽干」的节奏参数。子类加 `soundWorking`，绑定 `CompTerrainPumpDry`（不在本片）。
- 类比：海底排水泵/围堰抽水的直接模板。

### CompProperties_Terraformer.cs
- 字段：`radius=6.9`、`secondsPerConvert=20`、`convertTerrainDef`；实现 `IPlantEffectRadius`（选中时画影响半径圈）。
- 类比：把「海床」地形逐格改造成「干燥海底地基/陆地」的造陆设备模板，20 秒一格的节奏可参考。

## 三、气密与封堵（强相关，借用）

### CompSealable.cs / CompProperties_Sealable.cs
- 机制（已逐行读）：挂在 `MapPortal` 上（ConfigErrors 强制 thingClass 必须是 MapPortal）。`Seal()`：标记 isSealed → `PocketMapUtility.DestroyPocketMap` 销毁口袋地图 → 广播 "Sealed" 信号 → 可选 `destroyPortal` 销毁入口。
- 封门前交互：选择殖民者执行 `JobDefOf.Seal`；确认框自动列出仍在口袋地图内的玩家人类与动物（"PeopleWillBeLeftBehind" / "AnimalsWillBeLeftBehind" 红字警告）。校验：必须能 Touch 到达、非倒地。
- 类比：海底基地「弃舱/封舱门」玩法的完整官方范式——警告文案、确认流程、信号广播都可照搬；区别只是海洋里封的是房间/舱段而非口袋地图。

## 四、扫描与着陆（相关，有冲突点）

### CompScanner.cs / CompProperties_Scanner.cs / CompProperties_ScannerMineralsDeep.cs
- `CompScanner.CanUseNow`（已读）：未生成/断电/**任一格子在屋顶下（"CannotUseScannerRoofed"）**/被禁/非玩家派系 → 拒绝。
- 发现节奏：`scanFindMtbDays / scanSpeed` 的 MTB + `scanFindGuaranteedDays` 保底（`TickDoesFind`，59 tick 哈希间隔）；`daysWorkingSinceLastFinding` 按 `scanSpeedStat` 累积。
- `CompProperties_ScannerMineralsDeep` 空子类，仅绑定 `CompDeepScanner`。
- 类比（冲突）：海底矿脉扫描（声呐/深海钻探定位）玩法现成，但「屋顶下不能用」在海底是硬伤——若海水=屋顶则需 patch；若海水=自定义格子数据则无碍。**未验证**：海水在目标 Mod 里如何实现，需后续对照 Odyssey 真空格子的做法。

### CompShipLandingBeacon.cs / CompProperties_ShipLandingBeacon.cs
- 机制（已读）：4 个信标（横竖对齐）围出 `ShipLandingArea`（矩形内缩 1 格）；`RecalculateBlockingThing` 每 rare tick 检查；inspect 报告三类阻塞：`BlockedByRoof`、`BlockedByTerrain`（对照属性里的 `landingAreaTerrainSupport : TerrainAffordanceDef`）、`BlockedBy(具体物体)`；信标间距小于 `edgeLengthRange.min-1` 报 "TooCloseToOtherBeacon"。区域颜色取自 CompGlower 发光色。
- 类比：潜水艇/潜水器停泊坪 = 信标圈地模型的直接复用；`landingAreaTerrainSupport`（地形适配需求）正好用于「必须停在平坦海床上」。冲突：屋顶阻塞检查同扫描仪。

## 五、温度与腐烂（相关，海底恒温语境）

### CompRottable.cs / CompProperties_Rottable.cs
- 已逐行读：腐烂速率 = `GenTemperature.RotRateAtTemperature(parent.AmbientTemperature)`；<0.001 显示「已冷冻」、<0.999 显示「已冷藏」（inspect 文案直接由速率区间决定）；`rotDestroys` 时烂完销毁，生肉烂掉时放 `GasType.RotStink` 气体；建筑 `preventDeteriorationInside` 可免腐烂伤害；`disableIfHatcher`（蛋未受温度损伤时不烂）。
- 类比：海底室外恒温 ~4°C 意味着户外食物天然「冷藏」——与设计一致，但注意这是 `AmbientTemperature` 驱动的，只要把海水格温度钉在 4°C 就自动生效，无需改本 comp。

### CompProperties_TemperatureRuinable.cs / CompProperties_TempControl.cs
- TemperatureRuinable：`minSafeTemperature` / `maxSafeTemperature=100` / `progressPerDegreePerTick=1E-05`——超温按度数累积毁坏。
- TempControl：`energyPerSecond=12`、`defaultTargetTemperature=21`、目标范围 -50~50、`lowPowerConsumptionFactor=0.1`（达温后低保功耗）。
- 类比：4°C 海水是天然冷源，温控设备在海底的能耗平衡（制冷几乎免费、制热昂贵）就是调这几个数。

## 六、电力与水的交互（相关）

### CompProperties_Power.cs
- `shortCircuitInRain` 字段：电力设备雨中短路开关（另有 `idlePowerDraw`、`powerUpgrades` 研究加成）。
- 类比：海底「非防水电器泡水短路」最直接的现成开关——但注意它检查的是降雨而非格子环境，海洋版大概率要加自己的「浸水」检查（未见本字段与真空/水体联动的代码，本片内仅此开关）。

## 七、袭击/防御与生成（弱-中相关）

- **CompProperties_ProjectileInterceptor.cs**：定点拦截器（半径、EMP 瘫痪 `disarmedByEmpForTicks`、可拦截地面/空中/非敌对/向外弹道、充能或血条两种模式互斥）。类比：鱼雷/水下弹道的点防御。
- **CompShield.cs / CompProperties_Shield.cs**：能量护盾（`energyLossPerDamage=0.033`、破盾后 `startingTicksToReset=3200` 重置、`blocksRangedWeapons`）。弱相关，水下护盾视觉效果可借。
- **CompProperties_SpawnerPawn.cs**：巢穴生成器全参数（points 预算、`pawnSpawnIntervalDays`、`maxSpawnedPawnsPoints`、lordJob 加入、`defendRadius=21`）。类比：海底生物巢穴（只刷水生生物的生成源）模板。
- **CompSendSignalOnMotion.cs / CompProperties_SendSignalOnMotion.cs + CompRadialPulse.cs / CompProperties_RadialPulse.cs**：运动感应触发（半径/仅人类/房间内触发/延迟启用，触发时播 MechanoidsWakeUp 音效+发信号）；`CompRadialPulse` 在未触发时画一个周期性扩张的圆环（ForceField 材质，alpha 随相位衰减），且**直接联动** `CompSendSignalOnMotion.Sent` 状态。类比：这就是一套现成的「声呐脉冲+运动告警」视觉/逻辑组合，海底版改个贴图音效即可。

## 八、污染主题（冲突/决策点，弱相关）

- `CompProperties_Toxifier.cs`（半径 5.9、每 60000 tick 污染 6 格）、`CompProperties_PolluteOverTime.cs`（每天污染 N 格）、`CompProperties_PollutionPump.cs`（半径 9.9、每 60000 tick 清污、3 次出 1 垃圾袋、`disabledByArtificialBuildings`）。
- 类比：海水里「污染格」概念是否成立需要设计决策（稀释？海底沉积污染？）；若保留，污染泵是清污模板。

## 九、行星层门控（弱相关）

- `CompProperties_Usable.cs`：`layerWhitelist` / `layerBlacklist`（`PlanetLayerDef` 列表）——物品按行星层禁用/启用。类比：若海底实现为独立星球图层（Odyssey 式 PlanetLayer），可用来禁掉「水下不可用」的物品（如信号弹）。

## 十、其余微类比（一句话级）

- `CompProperties_SnowExpand.cs`：积雪每 500 tick 涨 0.12、最大半径 55——可类比「海底淤泥缓慢淤积覆盖建筑」。
- `CompProperties_MoteEmitter.cs` 家族（含 ProximityScan、RandomColor）：通用粒子发射器——气泡、浮游生物、深海微粒的视觉全靠它，`MoteEmitterProximityScan` 本身就是「靠近时扫描脉冲」的声呐感视觉。
- `CompProperties_Refuelable.cs` 的 `fuelConsumptionPerTickInRain`：雨天额外耗燃料——可类比「设备在高压/低温海水中额外耗能」。

## 覆盖说明

本片 200 文件中：36 个 NOTE（上文有条目）、145 个 PASS（读过，仪式视觉特效/Anomaly/Royalty/通用绑定与海洋无类比价值）、19 个 SKIP（空绑定类/空子类，如 `CompProperties_ShipPart`、`CompProperties_Resurrect` 这类仅一行 `compClass` 赋值的文件）。未验证项已在上文标注（海水格子的实现方式决定扫描仪/信标冲突是否成立）。
