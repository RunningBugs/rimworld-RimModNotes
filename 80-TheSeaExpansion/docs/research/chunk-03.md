# chunk-03 调研发现（RimWorld 组：Building_Turret* ~ CompCauseGameCondition*）

本片覆盖 200 个文件，主要是：炮塔/杂项建筑（Building_T*~W* 尾部）、ChoiceLetter 家族、Command 家族、CompAbilityEffect 全家族、CompA*~CompCause* 家族。

## TL;DR

1. **真空屏障 Building_VacBarrier 是"水压力场门"的直接模板**（`Building_VacBarrier.cs`）：通电时 `ExchangeVacuum=false`（阻断真空交换）+ `TempEqualizeRate=0`（隔温）+ `FreePassage=true`（自由通行）；断电则通透。海洋版只需把 VacuumComponent 换成"海水组件"。
2. **通风口 Building_Vent 是"排水阀/水密阀"的直接模板**（`Building_Vent.cs`）：flick 开关同时控制温度均衡（`GenTemperature.EqualizeTemperaturesThroughBuilding`）、气体均衡（`gasGrid.EqualizeGasThroughBuilding`）和真空交换，状态变化时 `VacuumComponent.Dirty()` 触发重算。
3. **装备武器水下禁用的现成钩子**（`CompApparelVerbOwner.cs:145`）：`CanBeUsed` 检查 `MapHeld.Biome.inVacuum && !verbProps.useableInVacuum` 则禁用该装备 verb（提示 "CannotFunctionInVacuum"）。海洋 Mod 若复用/仿造此机制，可精确控制哪些武器（鱼叉枪 vs 火焰喷射器）能在水下使用。
4. **全图温度/环境的实现路径是 GameCondition**（`Comp_AtmosphericHeater.cs`）：大气加热器注册**永久** `GameCondition_UnnaturalHeat` 并把目标温度写进 condition（`Condition.heaterTargetTemp`），由 condition 抬全图室外温度——海底"恒温 4°C"可以照此思路用一个永久 GameCondition 实现。
5. **远古喷口 = 海底热液喷口范本**（`CompAncientVent.cs` 及 3 个子类）：按 MTB 随机开/关，开启时注册全图 GameCondition、每 20 tick 推热（`GenTemperature.PushHeat`）、对周围 pawn 施加效果。
6. **钓鱼区在 1.6 核心已存在**（`Command_Hide_ZoneFishing.cs` 引用 `Zone_Fishing` 与 "FishingGroup" 文案）——海洋殖民地食物系统可直接站在钓鱼系统上。
7. **冲突清单**：水下必须处理火系（FireBurst/FireSpew/泡沫炮塔/点燃弹）、气体（烟雾/毒气弹、ToxVent）、天气限射程与"屋顶下禁射/禁放"（迫击炮、Flashstorm）、风力发电（CompAutoCutWindTurbine）。

## 一、强相关：真空→海水 的直接借用

### Building_VacBarrier（真空屏障 → 水压力场门）
`Building_VacBarrier.cs`（56 行，完整读过）。继承 `Building_SupportedDoor`：
- `FreePassage => true`、`AlwaysOpen => true`：永远通行，只是"一道场"。
- `ExchangeVacuum => !PowerTrader.PowerOn`：**通电=阻断真空交换，断电=真空自由穿透**。
- `TempEqualizeRate`：断电 14f（正常门漏温速度），通电 0f（完全隔温）。
- 电源信号（PowerTurnedOn/Off）时调用 `VacuumComponent.Dirty()` 让真空区域重算。
- 类比结论：这就是"通电力场门挡住海水但人可以游过/走过"的全套实现，耗电维持 + 断电进水，海洋 Mod 几乎只需改判定组件与美术。

### Building_Vent（通风口 → 排水阀）
`Building_Vent.cs`（65 行，完整读过）。继承 `Building_TempControl`：
- flick 开：`TickRare` 中双向均衡两侧温度（速率 14f）和气体（`gasGrid.EqualizeGasThroughBuilding`）。
- `ExchangeVacuum` 也被 flick 状态控制；flick 变化时 `Vacuum.Dirty()`。
- 类比结论："可开关的水密阀/平衡阀"模板——打开时两侧海水（及温度）连通，关闭时隔绝。

### CompApparelVerbOwner（装备 verb 的环境禁用钩子）
`CompApparelVerbOwner.cs`（152 行，完整读过）。`CanBeUsed` 两个环境检查：
- `IsPocketMap && !useableInPocketMaps` → 禁用；
- **`Biome.inVacuum && !vp.useableInVacuum` → 禁用（"CannotFunctionInVacuum"）**。
- 类比结论：VerbProperties 上有现成的 `useableInVacuum` 白名单字段。海洋图若直接把 biome 设成 inVacuum 风格标记（或 Harmony 仿造此检查加 useableUnderwater），即可让枪械/喷火器在水下失效、鱼叉/鱼雷类保留。注意这也提示：**如果直接把海洋 biome 标记为 inVacuum 来白嫖真空致死逻辑，所有没勾 useableInVacuum 的装备武器会全灭**，需要逐个配 XML。

## 二、相关：全图环境/气候控制（海底恒温与热液喷口）

### Comp_AtmosphericHeater（大气加热器）
`Comp_AtmosphericHeater.cs`（181 行，完整读过）。Anomaly DLC 建筑：
- 惰性属性 `Condition`：若图上没有 `GameCondition_UnnaturalHeat` 就**创建并注册一个 Permanent 的**；
- `TargetTemperature` 实际存取的是 `Condition.heaterTargetTemp`——即**温度目标存在 GameCondition 上**，由 condition 系统去影响全图；
- 本体只负责在有电有燃料且目标高于室外温时开 `CompHeatPusher`（推热 +10 偏移上限）。
- 类比结论：海底"室外恒温 4°C"不必改温度系统，注册一个自定义永久 GameCondition 覆写室外温即可，这正是官方在 Anomaly 里用过的路径。

### CompAncientVent 家族（远古热/烟/毒排气口 → 海底热液喷口）
`CompAncientVent.cs`（164 行，完整读过）+ `CompAncientHeatVent.cs`（96 行，完整读过）+ `CompAncientSmokeVent.cs`/`CompAncientToxVent.cs`（子类，仅扫过类声明与关键行）：
- 全图共享一个 MTB 判定（`sharedLastCheckMtbTickPerMap` 按 map.uniqueID 缓存，同图所有喷口同开同关）；开/关时长分别为 `onDurationMtbDays`/`offDurationMtbDays`。
- 开启时：若配了 `conditionToCause` 则注册一个 permanent、suppressEndMessage、conditionCauser=自己的 GameCondition；关闭时 End 掉。
- `PushHeatInterval=20`：每 20 tick `GenTemperature.PushHeat(parent, Props.heatToPush)`。
- `AppliesEffectsToPawns` 的子类（HeatVent）对建筑半径内所有 pawn 调 `ApplyPawnEffect`。
- 开关时发全图消息（`SendToggleAlert`），LookTargets 指向同 def 所有建筑。
- 类比结论：周期喷发的海底热液喷口/冷泉可以直接抄：MTB 节律 + 全图 condition + 局部推温 + 范围 pawn 效果（水下可换成" heated 水域取暖点"或"有毒喷口"）。

### CompCauseGameCondition（装置 → 世界范围 condition 源）
`CompCauseGameCondition.cs`（212 行，完整读过）+ `CompCauseGameCondition_AncientReactor.cs`（10 行子类，扫过）：
- `worldRange>0` 时对**世界地图上该距离内所有已加载地图**强挂 condition（`InAoE` 用 `WorldGrid.ApproxDistanceInTiles`，同 Layer）。
- `preventConditionStacking` 可复用图上已有 condition；源被毁时逐图清理（causedConditions 字典 + Expired 检查）。
- 类比结论："海底火山口/污染源影响一片海域"的世界级装置模板；也提醒：海洋格上敌方放的 condition 源会隔着世界地图影响到玩家海底殖民地。

## 三、食物/水产

### Command_Hide_ZoneFishing（钓鱼区存在的证据）
`Command_Hide_ZoneFishing.cs`（43 行，完整读过）：本身只是区域显示/隐藏 UI，但证明 1.6 核心有 `Zone_Fishing` 区域类型和 `UI/Designators/ZoneCreate_Fishing` 图标（"FishingGroup" 分组）。海洋 Mod 的捕鱼业应直接研究/复用钓鱼区系统（Zone_Fishing 本体不在本片）。

## 四、水主题技能与水的程序化处理

### CompAbilityEffect_Waterskip（水跃）
`CompAbilityEffect_Waterskip.cs`（76 行，完整读过）：对半径内每格——摧毁 Fire、对 pawn 破隐身（`GetInvisibilityComp()?.DisruptInvisibility()`）、空格铺 `Filth_Water` 并放 `WaterskipSplashParticles`。要求目标格非 Filled、视线可达边缘。水主题技能的完整范本（灭火+造水渍+特效+skip 信号）。

### Bullet（子弹落水水花）
`Bullet.cs`（88 行，完整读过）：未命中落地时，若落点地形 `takeSplashes` 则 `FleckMaker.WaterSplash`（规模 ∝ √伤害），否则尘土；`DamageDef.igniteCellChance` 命中概率尝试 `FireUtility.TryStartFireIn`。
- 类比结论：海床地形若想表现"子弹打进水里/泥沙"，把地形 takeSplashes 勾上即可白嫖水花；**燃烧弹水下点火问题在 FireUtility 侧处理（不在本片），但点燃入口在这里**。

### CompAbilityEffect_Explosion（爆炸的水中分支）
`CompAbilityEffect_Explosion.cs`（51 行，关键行读过）：透传 `Props.postExplosionSpawnThingDefWater` 给 `GenExplosion.DoExplosion`——爆炸系统原生支持"水中格生成不同的东西"。做水下爆炸物（深水炸弹）时可利用该参数区分水格/干格残骸。

### Building_VoidMonolith（运行时改地形的实例）
`Building_VoidMonolith.cs`（739 行，读关键部分）：`UpdateAttachments` 中用 `base.Map.Biome.TerrainForAffordance(...)` 取地形，然后 `terrainGrid.RemoveTopLayer` + `terrainGrid.SetTerrain` 把附件占格改成该地形（`Building_VoidMonolith.cs:453-457`）。证明运行时可安全地程序化改地形——"排水阀排干一片海水后把海床泥换成干地/可建造地形"有现成 API 路径。其余为 Anomaly 巨碑业务逻辑，与海洋无关。

## 五、冲突类：水下必须屏蔽或改造

- **火系技能全灭**：`CompAbilityEffect_FireBurst.cs`（以自身为中心 Flame 爆炸+撒 Filth_Fuel 燃料）、`CompAbilityEffect_FireSpew.cs`（ Flame 爆炸 + `flammabilityAttachFireChanceCurve` 附着点燃）——水下点燃语义不成立，需禁用或替换（关键行已读）。`Building_TurretFoam.cs`（31 行，完整读过）泡沫炮塔只瞄准 Fire/着火附着物——水下无火即废铁。
- **气体技能**：`CompAbilityEffect_Smokepop.cs`（BlindSmoke 爆炸）、`CompAbilityEffect_Firefoampop.cs`（Extinguish 爆炸 + Filth_FireFoam）、`CompAbilityEffect_ReleaseGas.cs`（25 行，完整读过，`GasUtility.AddGas` 按格铺 255×cells 的气体）——水下"气体"应整体评估：要么禁用，要么换成气泡/浊流表现。
- **天气与屋顶语义**：`Building_TurretGun.cs`（700 行，读关键部分）：弹道 overhead 的炮塔（迫击炮类）在**屋顶下不能开炮**（`NeedNotUnderThickRoof`/`Position.Roofed` 检查，`Building_TurretGun.cs:357,447-448,497,608`）；且 `weatherManager.CurWeatherMaxRangeCap` 会在某些天气压低射程上限并禁用开火（:614-617）。海洋语境两点：① 若把"头顶水体"实现为屋顶，迫击炮全废（也天然解释了"水下无轨道打击/迫击炮"）；② 可借 CurWeatherMaxRangeCap 做"浑浊洋流降低射程/能见度"。
- **Flashstorm 需露天**：`CompAbilityEffect_Flashstorm.cs`（58 行，完整读过）`Valid` 要求目标格 `!Roofed`——水下/室内不可放；同时它会生成 GameCondition_Flashstorm，雷暴类天气在海底本来就不该出现。
- **风力发电**：`CompAutoCutWindTurbine.cs`（58 行，完整读过）风力涡轮按 `WindTurbineUtility.CalculateWindCells` 自动砍挡风植物（blockWind）——海底无风，风机应禁用或换成"洋流涡轮"新建筑。
- **虫巢/巢穴生成约束**：`CocoonInfestationUtility.cs`（105 行，完整读过）+ `CocoonSpawner.cs`（32 行，完整读过）：茧必须落在 `Walkable` 格、周围 2 格内无 impassable 建筑/其他茧，按 combatPower 花点数从 `building.isInsectCocoon` 的 def 里抽。做"海底巢穴事件"（只刷水生生物）时这套点数花费+间距约束的生成器可直接仿写，但注意 Walkable 前提与海床/水格的定义要协调。

## 六、读过但无海洋价值的主体（PASS 概览）

- **CompAbilityEffect 其余 ~60 个**：灵能/技能效果的具体业务（复活、交易心灵、给 Hediff、召唤机械族等），机制上与环境系统无交集。
- **ChoiceLetter_*（12 个）**：信件 UI 流程，与环境无关。
- **Command_*（其余）**：gizmo 命令类。
- **ColonistBar/CharacterCardUtility 等 UI**：殖民地头像栏与角色卡绘制。
- **CompBiosculpterPod/CompAssignableToPawn/CompAnalyzable 等**：设备业务 comp。
- **Chunks.cs**（217 行，完整读过）：袖珍地图（pocket map）区域分块工具类，纯数据结构。
- **CaveExit.cs**（29 行，完整读过）：袖珍地图出口的绳索贴图，纯表现。
- **CompAtomizer/Building_WastepackAtomizer**：垃圾原子化业务；**CompBreakdownable**：MTB 故障；**CompActivity**：Anomaly 实体活跃度——均无环境交集。

## 未验证声明

- CompAncientSmokeVent/CompAncientToxVent 仅读了类声明和基类，具体 pawn 效果内容（烟/毒如何实现）未逐行读。
- 火系冲突项中 FireBurst/FireSpew 只读了爆炸调用关键行，预热/冷却流程未细读。
- CompAbilityEffect_Farskip（244 行）、CompBiosculpterPod（1337 行）、CharacterCardUtility（1501 行）等大文件只扫了类声明与关键词，未发现水/真空/温度相关引用（已用关键词扫描兜底），细节未逐行读。
