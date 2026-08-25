# chunk-05 调研发现：RimWorld / CompProperties_A*–M*（200 个文件）

本片覆盖 `RimWorld` 命名空间下 `CompProperties_Ability*`（54 个）到 `CompProperties_MetalhorrorInfectible` 共 200 个 **ThingComp 参数数据类**。它们本身几乎不含逻辑（逻辑在同名 `Comp*` 类里），但字段定义揭示了原版机制的挂接点。以下只列对「海洋扩展」有类比价值的条目。

## TL;DR

1. **深海热泉的直接模板**：`CompProperties_AncientVentEmitter` / `CompProperties_AncientHeatVent` —— 地图建筑按 MTB 周期触发 GameCondition + 推热量 + 呼吸辉光，改皮即成「海底热液喷口」。
2. **原版已有"按水体分叉"的爆炸产物字段**：`CompProperties_AbilityExplosion.postExplosionSpawnThingDefWater` —— 爆炸后生成物可以针对水格指定不同 ThingDef，证明 vanilla 已有水格判定钩子可效仿。
3. **真空功耗联动**：`CompProperties_LowPowerUnlessVacuum`（Odyssey）—— 设备功耗随所在房间是否真空切换（`checkRoomVacuum`），可反转为「海水/气密」环境联动功耗的模板。
4. **深海照明三件套**：`CompProperties_Glower`（含 `darklightToggle`、`overlightRadius`、半径上限 40）+ `CompProperties_DarklightOverlay` —— Anomaly 暗光（darklight）体系正好回答「光照极低的海底怎么做照明」。
5. **温度/地图条件控制**：`CompProperties_CausesGameCondition` 家族（含 `ClimateAdjuster` 的 `temperatureOffsetRange`）—— 可用来强制海底恒温、也可用于屏蔽热浪/寒流。
6. **虫害冲突需处理**：`CompProperties_CreatesInfestations` / `CompProperties_Infestation` —— 厚顶岩层虫害事件在海底语境与「只刷水生生物」冲突，需拦截或替换。
7. **图层白名单**：`CompProperties_Mannable.planetLayerWhitelist`（`List<PlanetLayerDef>`）—— Odyssey 按星球图层禁用设备的先例，海洋若做成独立地图/图层可照此限制设备。
8. **海底资源**：`CompProperties_DeepDrill`（本类无字段，逻辑在 `CompDeepDrill`）—— 深钻井是海底矿藏开采的天然模板，需确认其地下资源生成是否依赖地表 biome。

## 分主题详述

### 1. 热液喷口 / 环境热源（强相关）
- `CompProperties_AncientVentEmitter.cs`：字段 `conditionToCause`（GameConditionDef）、`onDurationMtbDays` / `offDurationMtbDays`（开关周期）、`heatToPush`（推热量）、启动/关闭消息 key、激活音效。即「一个遗迹建筑周期性向地图推热并触发 GameCondition」。
- `CompProperties_AncientHeatVent.cs`：继承上者，加 `minGlowBrightness`/`maxGlowBrightness`/`pulseSpeed`/`rampUpTime`/`rampDownTime` —— 呼吸灯式辉光脉动。
- 类比：海底热液喷口（黑烟囱）—— 在 4°C 恒温海底提供局部热源 + 生态/事件触发器，机制全套可借，美术与 Def 重做即可。
- 另见 `CompProperties_HeatPusherEffecter.cs`（仅挂 effecterDef，PASS 级）。

### 2. 真空 ↔ 海水环境联动（强相关）
- `CompProperties_LowPowerUnlessVacuum.cs`：`lowPowerConsumptionFactor = 0.1f`、`checkRoomVacuum = true`，compClass 为 `CompLowPowerInSpace`。即 Odyssey 里「在真空中才全功率、有大气时低功率」（或反向）的设备功耗模式。
- 类比：海洋版可直接仿写 `CompLowPowerInSpace` 判定逻辑，把「房间真空度」换成「房间是否进水/气密」——例如气密舱内设备正常、进水区域电器降功率或短路。

### 3. 深海照明（直接相关）
- `CompProperties_Glower.cs`：`glowRadius`（默认 14，ConfigErrors 强制 ≤40）、`overlightRadius`、`glowColor`、`colorPickerEnabled`、`darklightToggle`（暗光开关）、`overrideIsCavePlant`。
- `CompProperties_DarklightOverlay.cs`：继承 `CompProperties_FireOverlay`，给暗光源叠专用覆盖图（`CompDarklightOverlay.DarklightGraphic`）。
- 类比：Anomaly 的 darklight = 「只有特定感知能利用的光」。深海可借此做：生物荧光（低亮度装饰光）、暗光植物（对应洞穴植物）、普通灯在海底衰减。`overrideIsCavePlant` 字段还提示发光植物与洞穴种植体系的挂点。

### 4. 温度与地图条件（相关/冲突双面）
- `CompProperties_CausesGameCondition.cs`：建筑使地图/世界获得 GameCondition，`worldRange`、`hideSource`、`preventConditionStacking`。
- `CompProperties_CausesGameCondition_ClimateAdjuster.cs`：`temperatureOffsetRange`（默认 -10~10°C），compClass 为 `CompCauseGameCondition_TemperatureOffset` —— 气候调节器。
- `CompProperties_CausesGameCondition_PsychicEmanation.cs`：心灵 drones 等级。
- 类比：① 海底恒温 4°C 可用一个隐形 GameCondition/气候调节机制实现；② 反过来，玩家或事件方的 ClimateAdjuster 在海底应如何处理（热源在热泉附近叠加？）需要规则。

### 5. 水体分叉钩子（直接证据）
- `CompProperties_AbilityExplosion.cs`：`postExplosionSpawnThingDefWater` 与 `postExplosionSpawnThingDef` 并列 —— 爆炸后生成物对「水格」有单独 Def。说明 vanilla 在爆炸结算时已按格子是否水体选择生成物，mod 可复用同一判定路径（实际判定在 `CompAbilityEffect_Explosion`，本片不含）。
- `CompProperties_DissolutionEffectPollution.cs`：`waterTilePollutionFactor = 8f` —— 污染消解对水格的地块污染权重是 8 倍。又一个「水格特判」先例：污染进海扩散更快，海底殖民地排污/垃圾入海可借。

### 6. 虫害事件冲突（冲突类）
- `CompProperties_CreatesInfestations.cs`：无字段，compClass `CompCreatesInfestations`（深钻井等触发虫害的挂点）。
- `CompProperties_Infestation.cs`：仅 `points`，能力版虫害召唤。
- 类比：虫害（insectoid）在「只刷水生生物」的海底格格不入。若保留深钻井，需把 `CompCreatesInfestations` 的产物换成水生威胁（如巨型蟹/蠕虫），或整体禁用。

### 7. 图层/地图类型限制
- `CompProperties_Mannable.cs`：`planetLayerWhitelist : List<PlanetLayerDef>` —— 可遣操作设备只在白名单星球图层可用（Odyssey 地表/轨道分层先例）。
- 类比：海洋若实现为独立 PlanetLayer 或特殊地图，某些地表设备（如迫击炮？）可按图层禁用，接口现成。

### 8. 海底资源开采
- `CompProperties_DeepDrill.cs`：空 props，compClass `CompDeepDrill`。深钻井本身是「从脚下无限采稀薄矿脉」的范本，海底钻探直接改名换皮可用；但资源分布逻辑（DeepDrill 的地下资源网格是否按 biome 生成）在本片之外，**未验证**。
- `CompProperties_LongRangeMineralScanner.cs`：空 props 继承 `CompProperties_Scanner`，远程找矿同样可借用为「声呐探矿」（PASS 级，仅提一句）。

### 9. 环境危害光环（借用类，中低优先）
- `CompProperties_GiveHediffSeverity.cs`：范围内按 `severityPerSecond` 持续给 pawn 叠 hediff（带 `allowMechs`、`drugExposure`）。
- `CompProperties_CauseHediff_AoE.cs`：AoE hediff 光环，`ignoreMechs`/`onlyTargetMechs` 开关。
- 类比：海水暴露伤害（溺水/失温/减压病）若不做成真空式即死，而做成「暴露即叠 severity」，这两个 comp 就是现成模板；`ignoreMechs` 正好表达「机械族不怕水淹（或怕短路，反向配置）」。

## 其余 184 个文件
均为纯数据 props（能力参数、美术偏移、音效引用、信件文案等），逐一读完，与海洋语境无任何类比挂点，登记为 PASS/SKIP，见覆盖表 `coverage/results/chunk-05.tsv`。
