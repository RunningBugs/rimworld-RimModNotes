# chunk-01 调研发现（组：RimWorld，A 字母段 200 文件）

## TL;DR

1. **`Biome.inVacuum` 是奥德赛真空环境的总开关，本片中已有三处消费它的实际代码**——能力禁用（`Ability.cs:178`）、暖衣警报屏蔽（`Alert_NeedWarmClothes.cs:168`）、真空暴露警报（`Alert_LowOxygen.cs`，配合 `Position.GetVacuum(map)` 与 `HediffDefOf.VacuumExposure`）。海底 Mod 若定义自己的 `BiomeDef` 标志（如 `underwater`），这三处就是"哪些系统会读这个标志"的直接范例。
2. **服装系统已为"环境防护服"留好生成钩子**：`ApparelProperties.canBeGeneratedToSatisfyVacuumResistance`（太空服）与 `canBeGeneratedToSatisfyToxicEnvironmentResistance` 并列存在——潜水服可直接类比太空服，靠 StatDef + 该字段进入 AI 的"按需着装"逻辑（`ApparelProperties.cs:43-45`）。
3. **猎杀/袭击类事件的动物筛选管道在 `AggressiveAnimalIncidentUtility`**：从 `map.Biomes.SelectMany(AllWildAnimals)` 取候选，沿海格额外放行 `CommonalityOfCoastalAnimal`，并按地块温度过滤。"海底只刷水生生物"只需保证海底 BiomeDef 的 `wildAnimals`/coastal 列表里只有水生动物，事件层无需改动（`AggressiveAnimalIncidentUtility.cs:46-61,167-171`）。
4. **警报框架极易扩展**：`Alert` 基类 + `AlertReport.CulpritsAre(...)` 工厂 + `requireRoyalty/Ideology/Biotech/Anomaly` 门控字段。"壳体进水/结构失压"警报可照抄 `Alert_ToxicBuildup`（hediff 严重度阈值）或 `Alert_LowOxygen`（逐格环境值 + hediff 阶段）模式（`Alert.cs`、`AlertReport.cs`）。
5. **`AncientHatch : MapPortal`** 是奥德赛"地表舱门→口袋地图"的入口实现（黑客解锁 + `GenStep_AncientStockpile` 生成内部），是"海底沉船/海沟洞穴入口"的直接模板（`AncientHatch.cs`）。
6. 环境音按 Biome 驱动：`AmbientSoundManager` 读 `BiomeDef.soundsAmbient` 与 `noAmbientWind`——海底 Biome 可纯 XML 配置水下环境音并关掉风声（`AmbientSoundManager.cs:36-64`）。

## 1. 真空/氧气机制的消费点（本片最有价值）

### Alert_LowOxygen.cs（54 行，全文已读）
- `Alert_Critical` 子类。仅 `ModsConfig.OdysseyActive` 时生效。
- 判定链：遍历玩家派系所有已生成 Pawn → `item.Position.GetVacuum(item.Map) < 0.5f` 说明该格真空度低（有气）则跳过；否则查 `HediffDefOf.VacuumExposure` 且 `CurStageIndex >= 2` 才报警。
- 类比：**"海水暴露"警报的逐行模板**。海底版应为"该格海水值 >= 阈值 且 DrowningExposure hediff 到达某阶段"。关键 API：`IntVec3.GetVacuum(Map)` 返回 float——暗示真空是按格连续值（0~1）而非布尔，海水可照此设计。

### Ability.cs（1322 行，读关键段）
- `CanCast`（`Ability.cs:150-191`）里有三层环境门控，全是现成挂载点：
  - `pawn.MapHeld.IsPocketMap` + `VerbProperties.useableInPocketMaps` → 口袋地图禁用；
  - `pawn.MapHeld.Biome.inVacuum` + `VerbProperties.useableInVacuum` → 真空禁用（返回 "CannotFunctionInVacuum"）；
  - `LayerPreventsCast(tile)` → 星球层禁用。
- 类比：火焰类/飞行类能力在水下应禁用——奥德赛已用 `VerbProperties` 上一个 bool + Biome 上一个 bool 解决，海底可加 `useableUnderwater` 同款字段（需 patch VerbProperties，或复用 inVacuum 思路由 Biome 标志驱动）。

### Alert_NeedWarmClothes.cs（196 行，全文已读）
- `MapWithMissingWarmClothes`（`:162-178`）第一行过滤就是 `map.IsPlayerHome && !map.Biome.inVacuum`——**真空 Biome 直接跳过暖衣检查**（太空不需要棉袄，需要太空服）。
- 类比：海底恒温 4°C 时，"暖衣警报"到底该不该响，取决于你是否想让保温成为海底机制；无论哪种，`Biome.inVacuum` 这个"按 Biome 屏蔽警报"的写法就是答案。该片还展示了 `ComfyTemperatureMin` 与 `Insulation_Cold` 的统计链路（`:43-60`），低温（4°C）在海底是真实威胁，见 §5。

### ApparelProperties.cs（258 行，全文已读）
- 三个并列布尔字段：`canBeGeneratedToSatisfyWarmth` / `canBeGeneratedToSatisfyVacuumResistance` / `canBeGeneratedToSatisfyToxicEnvironmentResistance`（`:41-45`），默认全 true。
- 结论：AI"为应对环境而搜刮/着装"的生成逻辑按这三个开关过滤服装；**潜水服 = 新增一个环境抗性 StatDef + 对应开关的同类机制**，太空服（VacuumResistance）已是完整先例。另有 `immuneToToxGasExposure`（`:69`）——单件服装完全免疫某环境暴露的开关，潜水服"免疫海水暴露"可对标。

## 2. 警报框架（Alert.cs / AlertReport.cs，全文已读）

- `Alert` 抽象基类：子类只需实现 `GetReport()`；`Recalculate()` 缓存 active/label。门控字段 `requireRoyalty/requireIdeology/requireBiotech/requireAnomaly`（`Alert.cs:18-24`）在 `EnabledWithActiveExpansions` 里统一判定——**Mod 自定义警报无需 DLC 门控，直接继承即可**；优先级用 `defaultPriority`（`Alert_Critical` 基类 = 红色高优先级）。
- `AlertReport`：struct，`CulpritsAre(List<Thing>/List<Pawn>/...)` 工厂 + 点击警报跳转到肇事目标（`Alert.OnClick` 用 `CameraJumper.TryJumpAndSelect` 循环跳转）。
- **Alert_ToxicBuildup.cs**（全文已读）：hediff 严重度阈值警报——`GetFirstHediffOfDef(HediffDefOf.ToxicBuildup).Severity >= 0.6f`。类比"水压伤/溺水积累"警报，30 行可写完。
- **Alert_UndercaveUnstable.cs**（全文已读）：`PitGate.IsCollapsing` + `TicksUntilCollapse` 的倒计时警报，`requireAnomaly = true`。机制是 Anomaly 专属，但"结构崩塌倒计时 + 点击跳转"模式可直接借给"壳体破损/舱段失压倒计时"。
- **Alert_CannotBeUsedRoofed.cs**（全文已读）：遍历所有 `!canBeUsedUnderRoof` 的 ThingDef，检查是否被屋顶覆盖（`RoofUtility.IsAnyCellUnderRoof`）。类比：海底可能需要"该建筑不能暴露于海水"的限制——造一个 `canBeUsedUnderwater` 同款 ThingDef 字段 + 同款警报即可。
- `AlertsReadout.cs` 为警报栏 UI 容器（PASS，Mod 加警报不需要碰它——vanilla 自动发现 Alert 子类；此点为框架惯例，本片未逐行验证其发现机制）。

## 3. 动物刷新与猎杀事件

### AggressiveAnimalIncidentUtility.cs（173 行，全文已读）
- 猎杀事件（manhunter）选种管道：`TryFindAggressiveAnimalKind(points, map, out kind)`：
  1. 优先 Odyssey 的 `TileMutatorDefOf.AnimalHabitat` 栖息地动物（50% 概率）；
  2. 从 `map.Biomes.SelectMany(b => b.AllWildAnimals)` 过滤 `CanArriveManhunter`（`RaceProps.Animal && canArriveManhunter && CanPassFences`）与污染变体；
  3. 兜底按全 DefDatabase + 地块温度 `SeasonAndOutdoorTemperatureAcceptableFor` 过滤。
- 沿海判定：`map.TileInfo.IsCoastal` 时允许 `CommonalityOfCoastalAnimal(k) > 0` 的动物（`:167-171`）——**vanilla 已有"海洋生物上岸"事件通道**（海岸格刷海洋动物袭击）。
- 数量：`points / combatPower`，clamp 到 [2, 100]。
- 类比结论："海底只刷水生生物袭击"在事件层零改动——只要海底 BiomeDef 的动物列表干净；但要注意第 3 步兜底是全表 + 温度过滤，4°C 恒温下温度过滤形同虚设，若想绝对排除非水生动物需 patch 兜底分支或保证前两级必命中。

### AnimalBiomeRecord.cs（17 行，SKIP 级数据类，此处登记用途）
- `biome + commonality` 的 XML 自定义加载载体（`PawnKindDef.wildBiomes` 等元素）。给水生动物配置"只在海底 Biome 出现"就是往它身上写数据，无代码工作。

## 4. 环境音

### AmbientSoundManager.cs（66 行，全文已读）
- 切图时重建 Sustainer：`BiomeDef.soundsAmbient` 列表逐个 `TrySpawnSustainer(SoundInfo.OnCamera())`；`biome.noAmbientWind == true` 时不播/停掉海拔风声。
- 结论：海底氛围（低沉水声、鲸歌）纯 Def 配置即可：`soundsAmbient` + `noAmbientWind=true`。零代码。

## 5. 温度

### Alert_Hypothermia.cs（经 grep 读关键行）
- `:18` 判定 `!item.SafeTemperatureRange().Includes(item.AmbientTemperature)` 即报危急。
- 结论：**海底室外恒温 4°C 低于人类舒适下限，未防护殖民者会持续触发低温症警报与 hediff**——这符合"室外致死/危险"设计，无需屏蔽；但动物（`Alert_HypothermicAnimals`）同样会报，水生生物的 `SafeTemperatureRange` 要在 Def 里调好。`Alert_Heatstroke`/`Alert_GhoulHypothermia` 同族，PASS。
- 注：暖衣警报会被 `Biome.inVacuum` 式标志屏蔽（见 §1），但低温 hediff 本身不走 Biome 标志，走实际温度——恒温 4°C 靠世界格温度/Biome 温度参数实现即可，事件层的热浪/寒流冲突不在本片范围。

## 6. 口袋地图入口

### AncientHatch.cs（92 行，全文已读）
- `MapPortal` 子类：`CompHackable` 解锁（未解锁 `IsEnterable` 返回 false），`GetExtraGenSteps` 用 `GenStepDefOf.AncientStockpile` + 可选 `LayoutDef` 生成内部空间，`stockpileType` 决定战利品类型。双贴图（关/开）在 `SpawnSetup` 里构造。
- 类比：**"海底沉船舱门/深海热泉洞穴入口"的完整模板**——入口建筑 + 口袋地图生成 + 解锁条件，全在一个 92 行类里。

## 未覆盖说明

本片其余 ~180 个文件为：单一用途 Alert（医疗/奴隶/机械体/异象/王室/育儿等，全部 PASS）、Ability/Apparel/Area 家族的 Def/DefOf/枚举/数据载体（SKIP）、异象（Anomaly）专属工具（PASS，与海洋无类比点）。所有 PASS/SKIP 判定基于类声明全文 grep + Vacuum/Oxygen/Water/Coastal/Temperature/Weather/Roof/Biome/TechLevel 等主题关键词全片扫描，未发现遗漏的相关命中。
