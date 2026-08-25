# chunk-09 调研发现（RimWorld 命名空间 F~G 字头，200 文件）

## TL;DR

1. **Odyssey 有现成的"液体蔓延"抽象基类 `Flood`**（Flood.cs）：按权重随机向四邻蔓延、把地形替换为 `terrainDef.floodTerrain`、被地基/建筑挡住、蔓延完毕自毁。海底 Mod 的"舱室破损进水"可以直接继承或仿写它。
2. **GameCondition 基类自带地图层级/地下过滤与"需要有鱼"开关**（GameCondition.cs）：`CanApplyOnMap` 检查 `map.generatorDef.isUnderground && !def.allowUnderground`、`layerWhitelist/layerBlacklist`、以及 Odyssey 的 `def.requireFish`（要求 `map.waterBodyTracker.AnyBodyContainsFish`）。海底图要屏蔽/放行全图事件，走这套现成过滤即可。
3. **温度类全图事件（热浪/寒流/气候循环/火山冬天/温度偏移）全部通过 `GameCondition.TemperatureOffset()` 叠加**（GameConditionManager.AggregateTemperatureOffset 汇总）。海底恒温 4°C 的设定必须处理这些 offset：要么用 layer 过滤屏蔽，要么接受叠加。
4. **深海低光照有两套现成机制可借用**：`GameCondition_NoSunlight`（SkyTarget glow=0，Permanent 时使 `GameConditionManager.IsAlwaysDarkOutside=true`）和 Anomaly 的 `MapBrightnessTracker`（`gameConditionManager.MapBrightness`，`SetTargetBrightness` 可渐变，UnnaturalDarkness 用它做 0 亮度+黑暗暴露 hediff）。
5. **钓鱼系统已参数化且全部挂在 WaterBody/biome 上**（FishingUtility.cs）：鱼群量按水体大小曲线再生（0.025/天）、污染决定毒鱼概率、`MinFishPopulationForFishing=10`；`GameCondition_GillRot`（鳃腐病）通过 `fishPopulationOffsetFactorPerDay` 削减鱼群再生。海底图直接就有钓鱼经济，但鱼种来自 `Map.Biome.fishTypes`，需要自定义海洋 biome。
6. **火焰与水的互斥已有多个钩子**（Fire.cs / FireUtility.cs / FireArrow.cs）：地形 flag `extinguishesFire`、水体地形易燃性为 0 使 `ChanceToStartFireIn` 返回 0、`FireArrow.Impact` 显式检查 `terrain.IsWater` 不撒燃料、真空通过 `GetEffectiveVacuumForFire` 让火焰持续受 Extinguish 伤害。海底非气密区灭火可照抄真空路径。
7. **"露天暴露伤害"模式**（GameCondition_ToxicFallout.cs / GameCondition_VolcanicAsh.cs）：周期性对 `!Roofed` 的 pawn 加 hediff——类比到海底即"非气密区暴露于海水"的伤害/溺水 tick 模板。

---

## 一、进水/液体蔓延（强相关）

### Flood（Flood.cs，Odyssey 专属）
- 抽象 Thing 子类，需 `ModLister.CheckOdyssey("Flood")`。子类实现 `GetInitialCells(map)`（返回 (cell, 权重)）和 `SpreadFlood(cell, sourceTerrain)`。
- 蔓延逻辑：按 `MaxFloodDurationTicks / estimatedFloodedTiles` 的间隔 tick，从高权重桶里随机取开放格，向四邻（`GenAdj.CardinalDirections`，洗牌后）扩展；目标格地形被换成 `terrainDef.floodTerrain`（TempTerrain 优先，否则 BaseTerrain）。
- 阻挡规则（`CanFloodSpreadInto`）：越界不行、已是水地形（`terrain.IsWater`）不行、`terrainGrid.FoundationAt(cell) != null`（有地基，即 gravship 地板类）不行、有 edifice（墙等）不行。
- 监听 `map.events.TerrainChanged / BuildingSpawned / BuildingDespawned`，被堵死后有新开口会恢复蔓延；无格可蔓延时 `noPossibleCell` 短路。
- **海洋类比**：这就是"海水灌入破损舱室"的完整模板——把 floodTerrain 换成"淹没地形"，初始格取破口处即可。权重桶（10~12 宽度）还能模拟"近破口处先淹"。

### GameCondition_LavaFlow（GameCondition_LavaFlow.cs，Odyssey）
- Flood 的"事件版"近亲：全图事件，从地图边缘火山岩地形起 3~6 个点，每 5 tick 蔓延一格 `SetTempTerrain(LavaShallow)`，并 `tempTerrain.QueueRemoveTerrain` 定时冷却移除（FlowDuration + 60000 tick）。被 Foundation 和非火山岩地形阻挡。
- **海洋类比**：`SetTempTerrain + QueueRemoveTerrain` 的"临时地形+自动复原"套路可用于"临时淹水/退潮"。

## 二、GameCondition 框架（海底全图事件的总开关）

### GameCondition 基类（GameCondition.cs）
- 生命周期：`Permanent` 或 `Duration`；`GameConditionTick/GameConditionDraw/Init/End` 虚方法。
- **过滤（关键）**：`CanApplyOnMap` — ① `map.generatorDef.isUnderground && !def.allowUnderground` 直接排除；② `MapExcludedByFilter` 按 `def.layerWhitelist / layerBlacklist / canAffectAllPlanetLayers` 与 `map.Tile.LayerDef.onlyAllowWhitelistedGameConditions` 过滤；③ Odyssey 下 `def.requireFish` 要求 `map.waterBodyTracker.AnyBodyContainsFish`。
- 效果钩子（全是虚方法，子类覆盖）：`TemperatureOffset()`、`SkyTarget()/SkyTargetLerpFactor()`（天色/亮度）、`AnimalDensityFactor/PlantDensityFactor`（刷动物/植物密度乘数）、`AllowEnjoyableOutsideNow`、`ForcedWeather()`、`MinWindSpeed()`、`DoCellSteadyEffects(cell)`（每格稳态效果）、`SkyGazeChanceFactor` 等。
- **海洋类比**：海底若是独立 planet layer 或自定义 generator，把 `onlyAllowWhitelistedGameConditions` 一开，就只有白名单事件能进海底图——这是屏蔽热浪/寒流/火山冬天/暴风的正道，比逐个 patch 事件干净得多。

### GameConditionManager（GameConditionManager.cs）
- `AggregateTemperatureOffset()` 把所有生效条件的 `TemperatureOffset()` 加总（含世界层 parent 条件）。**海底恒温 4°C 与热浪(+17)/寒流(-20)/气候循环(±20 正弦)/火山冬天(-7)/HeatVentHeat(+15)/TemperatureOffset(def 驱动) 全部冲突，需在 layer 过滤或 biome 层面解决。**
- `MapBrightnessTracker`（Anomaly）：`MapBrightness` 0~1，`SetTargetBrightness(target, lerpSeconds)` 平滑过渡并 dirty GroundGlow。`IsAlwaysDarkOutside` = 存在 Permanent 的 `GameCondition_NoSunlight`。
- `FishPopulationOffsetFactorPerDay(map, out culprit)`：若有 GillRot 条件则返回其 factor，否则默认 0.025（每日鱼群再生率）。

### 光照/黑暗类条件（深海低光照直接借用）
- `GameCondition_NoSunlight`（GameCondition_NoSunlight.cs）：SkyTarget glow=0 + EclipseSkyColors，TransitionTicks=200 渐变。Permanent 版本即"永夜"。`_Instant` 子类过渡时间为 0。**海底"无阳光"直接注册一个 Permanent NoSunlight 即可。**
- `GameCondition_UnnaturalDarkness`（GameCondition_UnnaturalDarkness.cs，Anomaly）：配合 MapBrightness≈0，给露天（`GroundGlowAt<=0`）的殖民者加 `DarknessExposure` hediff；门格按周边格平均亮度判定。`UnnaturalDarknessAt(cell, map)` 是静态查询入口。**想做"深海黑暗压力/需要照明"可抄。**
- `GameCondition_Aurora` / `GameCondition_BioluminescentSpores`：彩色天空+SkyGaze 奖励；后者带 GlowSporeOverlay 全屏覆盖层。**"生物荧光"这个名字和深海氛围天然契合，可做海底正面事件（参考其 overlay + SkyTarget 写法）。注意 Aurora 在 `IsAlwaysDarkOutside` 的图上直接不生效（SkyGaze 因子归 0）。**
- `GenCelestial`（GenCelestial.cs）：太阳高度角/日光强度纯数学（纬度、一年第几天、一天百分比 → `CelestialSunGlow` 0~1，`IsDaytime` 阈值 0.6）。海底不需要改这里——日光是被 SkyTarget/glowGrid 消费的，用条件压掉即可。

### 暴露伤害模式（非气密区致死模板）
- `GameCondition_ToxicFallout`：每 3451 tick 对所有 `!kindDef.immuneToGameConditionEffects` 的 spawn pawn 调 `ToxicUtility.DoAirbornePawnToxicDamage`（内部按屋顶判定）；`DoCellSteadyEffects` 对未屋顶格杀植物（`plant.dieFromToxicFallout`）并加速物品腐烂。还把 AnimalDensity/PlantDensity 归 0。
- `GameCondition_VolcanicAsh`（Odyssey）：每 3251 tick 对 `!Roofed && GasUtility.IsAffectedByExposure` 的 pawn 加/刷新 `VolcanicAsh` hediff。
- **海洋类比**：把 `!Roofed` 换成"非气密"，hediff 换成"溺水/水压"，就是海水暴露伤害。

### 其他条件速览
- `GameCondition_GillRot`（Odyssey）：仅持有一个 `fishPopulationOffsetFactorPerDay`（来自 `def.fishPopulationOffsetPerDay`），被 GameConditionManager 查询——鱼群病害事件的全部逻辑就这么点，真正的扣减在 WaterBodyTracker 侧（本片外）。
- `GameCondition_Drought`：植物生长率 ×0.3（常量 PlantGrowthRateFactor），每 60 tick 检查 `curWeather.rainRate > 0` 则结束。`DroughtInitial` 4 天后升级为正式干旱 incident。**海底无雨，Drought 一旦触发永不自动结束 → 必须用 layer 过滤屏蔽。**
- `GameCondition_TemperatureOffset`：def 驱动的恒定温度偏移——反过来也可被 Mod 利用做"海底恒温修正"。
- `GameCondition_HeatVentHeat`/`UnnaturalHeat`：逐 tick 向目标 offset 爬升（0.001/tick）的"建筑驱动温度事件"模式（AtmosphericHeater 决定目标值）。
- `GameCondition_DisableElectricity`：仅一个 `ElectricityDisabled => true`（EMI 场）。**"水下电器短路"事件可一行借用。**
- `GameCondition_Flashstorm`：雷暴，落雷点要求 `!Roofed && Standable`；结束后 `DisableRainFor(30000)`。海底应屏蔽。
- `GameCondition_VolcanicWinter`/`SmokeSpewer`：温度 -7、动物密度 ×0.5、天空变暗。
- `GameCondition_NoxiousHaze`（Biotech）：与世界污染联动，动植物密度归 0。
- `GameCondition_ForceWeather`：强制天气基类（BloodRain/GrayPall/DeathPall/UnnaturalDarkness 继承）。海底天气系统若整体替换，这些全被 layer 过滤挡掉。
- `GameConditionMaker.MakeCondition/MakeConditionPermanent`：注册入口（Permanent 版本 startTick 回拨 3 天让 lerp 直接满）。

## 三、钓鱼与水体生态（直接相关，Odyssey）

### FishingUtility（FishingUtility.cs）
- 常量：基础钓鱼时长 7500 tick（动物 1500）、稀有捕获 1%（冷却 300000 tick）、负面捕获 2%（冷却 120000）、最低可钓鱼群 10、每水体最低鱼群 20。
- 曲线：`FishPopulationFactorPerBodySizeCurve`（75 格以下水体无鱼，2 万格满系数）、常见/稀有鱼种概率随水体大小、`PopulationToFishYieldCurve`（鱼群量→每次捕获条数）、`PollutionToxfishChanceCurve`（污染 5%~80% → 毒鱼概率 0~0.6）。
- `GetWaterBody(cell, map)` → `map.waterBodyTracker.WaterBodyAt`；捕获从 `waterBody.CommonFishIncludingExtras / UncommonFish` 随机；稀有捕获用 `Map.Biome.fishTypes.rareCatchesSetMaker`。
- `FishChance`（FishChance.cs）：XML 数据类 (fishDef, chance)，biome 鱼种表条目。`FishRepeatMode`：捕鱼工单重复模式枚举。
- **海洋类比**：海底图整图是水，WaterBody 系数直接拉满（2 万格水体=1.0）；只要自定义海洋 BiomeDef 的 fishTypes 即可拥有全套钓鱼经济。GillRot 事件天然适配海洋主题。

### Fleck 特效（FleckDefOf.cs / FleckMaker.cs）
- 已有 `FishShadow/FishShadowReverse`（Odyssey）、`WaterSplash`、`WaterRipple`  fleck；FleckMaker 有现成 `WaterSplash(loc,map,size,velocity)` 和 `WaterRipple` 抛射方法。海底氛围特效可直接复用。

## 四、火与水互斥（冲突/借用）

- `Fire.DoComplexCalcs`（Fire.cs）：地形 `extinguishesFire` 为 true 时火焰找不到可燃物直接销毁；`fireSize` 增长乘以 `(1 - GetEffectiveVacuumForFire)`；真空 >0 时每 150 tick 受 `20 * vacuum` 点 Extinguish 伤害；雨（RainRate>0.01 且无顶/薄顶）概率受 10 点 Extinguish。
- `FireUtility.GetEffectiveVacuumForFire`（FireUtility.cs）：`map.Biome.inVacuum` 且 edifice 全实心时按屋顶情况取真空度，否则 `c.GetVacuum(map)`。**海底对应物：把 `GetVacuum` 换成"水深/淹没度"，火焰在水下自动熄灭——真空灭火的判定结构可原样照抄。**
- `FireUtility.ChanceToStartFireIn`：格内有 `filth.allowsFire==false` 的 filth 或地形/物品易燃性 <0.01 则无法起火（水地形易燃性 0 → 水上点不着火）。
- `FireArrow.Impact`（FireArrow.cs）：显式 `if (!terrain.IsWater)` 才 FloodFill 撒燃料 Filth_Fuel 并点火——武器级"水中失效"写法示例。`FlameThrower` 无显式水检查，靠易燃性兜底。

## 五、污物（Filth）与水/真空

- `FilthMaker.TryMakeFilth`（FilthMaker.cs）：**`c.GetTerrain(map).exposesToVacuum` 的地形直接拒绝生成污物**——"暴露在真空中的地面不留血迹"。海底类比：被淹没地形是否留污物（血、呕吐物）需要同样的开关，否则海底到处是血泊会很出戏。
- `FilthProperties`（FilthProperties.cs）：`rainWashes`（雨冲刷）、`disappearsInDays`、`placementMask`（FilthSourceFlags 位掩码）与地形 `filthAcceptanceMask` 双向匹配。`Filth.CanDropAt/CanMakeFilth` 还查 `StatDefOf.FilthMultiplier`（地形污物系数）。
- **海洋类比**：海底"水流冲刷污物"可仿 rainWashes 做一个淹没格定时清污。

## 六、建造/地形承载

- `GenConstruct`（GenConstruct.cs）：建造可放置性核心校验围绕 `terrainAffordanceNeed`（`GetTerrainAffordanceNeed(stuff)`）与 `item.GetAffordances(map)` / `terrainDef.affordances` 匹配；不匹配返回 "TerrainCannotSupport_TerrainAffordance"。替换地形时也校验旧建筑的 `terrainAffordanceNeeded`（CanReplace 相关，约 643 行处）。
- **海洋类比**：海底建筑（耐压壳、气密舱）与"只能建在海底岩层/只能建在已排水格"这类限制，正统做法就是注册自定义 TerrainAffordanceDef 并在地形 affordances 里声明——无需 patch。

## 七、其他 minor 发现

- `FertilityGrid`（FertilityGrid.cs）：肥力 = 地形 fertility，Biotech 污染格封顶 0.5；edifice 可用 `def.AffectsFertility/fertility` 覆盖（栽培箱模式）。海底作物/海藻养殖要么给海底地形 fertility，要么走 edifice 覆盖。
- `GeneDefOf`（GeneDefOf.cs）：Biotech 有 `WebbedPhalanges`（蹼指）基因条目——具体效果实现在这个文件之外（未验证），但说明原生基因体系里已有"水生适应"挂钩点，做"鳃/水下呼吸"基因时可参考其 Def 定义。
- `GameConditionDefOf`：GillRot、Drought、LavaFlow 均为 `[MayRequireOdyssey]`——Odyssey 已经把"水生生态事件"挂进了条件体系。
- `GatheringsUtility.ValidateGatheringSpot`：聚会点拒绝 `terrain.dangerous` 的格子——若把淹没地形标 dangerous，可自动阻止殖民者在露天水域开 party（附带效果，值得知道）。

## 覆盖说明

本片为 RimWorld 命名空间 F~G 字头共 200 个文件。其中约 55 个 `FloatMenuOptionProvider_*` 右键菜单 provider 经全文 grep 验证不含 water/vacuum/terrain/flood 相关逻辑（并完整抽读基类与 4 个代表文件），均为标准 UI 命令桥接；FocusStrengthOffset_*（冥想聚焦）、Gathering*/GatheringsUtility（聚会）、Gene_Hemogen*/Gene_Deathrest/GeneGizmo_*（血原质/死眠，Biotech）、Fleshbeast*/Fleshmass*（Anomaly 血肉兽）、GameComponent_*（Anomaly/头领/复制人/灵能仪式管理）经阅读确认与海洋主题无机制交集。未发现需要标「未验证」的关键结论，除 WebbedPhalanges 基因效果（实现不在本片）。
