# chunk-34 调研发现（Verse 组：Hediff 家族 + H~L 基础设施）

本片 250 个文件，主体是 **Hediff（健康状态）系统全家桶**（HediffComp_*/HediffGiver_*/Hediff_* 子类）加上 H~L 字母段的基础设施（接口、语言 worker、UI Listing、日志、按键、数学结构）。Hediff 系统恰恰是"海水环境伤害"最该落脚的系统——Odyssey 的真空伤害本身就是用 Hediff 实现的，本片直接读到了它的完整源码。

## TL;DR

1. **真空暴露整套 Hediff 机制已读到实现细节，是"溺水/水压"伤害的直接模板**：`HediffComp_VacuumExposure`（severity 只在脱离真空时恢复）+ `HediffGiver_VacuumBurn`（按真空度曲线对随机可烧伤部位打 VacuumBurn 伤害）+ `HediffStage.preventVacuumBurns` 标志（hediff 阶段级免疫真空烧伤）。（源：HediffComp_VacuumExposure.cs、HediffGiver_VacuumBurn.cs、HediffStage.cs）
2. **低温症 HediffGiver_Hypothermia 读到了全部数值公式**：4°C 海底恒温下殖民者会持续累积低温症，是海洋 Mod 默认就要面对的常驻威胁；其"回暖恢复 + 冻伤掉部位"两段式逻辑可直接参考或需要针对性屏蔽/改造。（源：HediffGiver_Hypothermia.cs）
3. **HediffGiver_Terrain 证明"站某地形上周期性受伤"有现成框架**：TerrainDef 上的 `burnDamage`/`burnIntervalTicks`/`ignitePawnsIntervalTicks` 字段驱动，可类比为"海底热泉/腐蚀性淤泥"等地形伤害，纯 XML 可配。（源：HediffGiver_Terrain.cs）
4. **HediffComp_RemoveIfApparelDropped：装备脱下即移除 hediff**——"潜水服=抗水压 hediff 来源，破洞/脱下立刻开始溺水"的经典设计有现成 comp。（源：HediffComp_RemoveIfApparelDropped.cs）
5. **气体 severity comp 族（SeverityFromGas / SeverityFromGasDensityDirect）展示了"环境浓度→hediff severity"的两种范式**，含 `exposureStatFactor` 抗性属性减免——类比"氧气浓度/麻醉气体"，是把 SeaWater 当作类 Gas 系统时的现成参考。（源：HediffComp_SeverityFromGas.cs、HediffComp_SeverityFromGasDensityDirect.cs）
6. **LavaFXComponent（Odyssey）是按地形挂环境粒子 FX 的 MapComponent 模板**——CellFXSpawner 对每格 LavaDeep/LavaShallow 地形定期喷火花烟雾；海底气泡、浮游生物光点等环境 FX 可照抄此结构。（源：LavaFXComponent.cs）

---

## 一、真空伤害全家桶（=海水致死的直接模板）

### HediffComp_VacuumExposure.cs（NOTE，强相关）
- 每 60 tick 检查一次：若 pawn 已生成、所在格 `GetVacuum(map) >= 0.5f` 且 `VacuumResistance < 1f`，则 severity 增量为 0（暴露中不恢复也不加重——加重由 VacuumBurn 的直伤负责）；否则按 `severityPerSecondUnexposed` 恢复。
- 关键设计结论：**Odyssey 把"暴露"拆成了两层——直伤（burn）走 HediffGiver，慢性指标（VacuumExposure hediff，应该对应减压/缺氧类 debuff）走 comp 恢复**。海洋 Mod 可同样拆成"溺水直伤"+"减压症/缺氧 hediff"。
- 门控：`ModsConfig.OdysseyActive` 硬编码——Mod 自己实现时不需要这种 DLC 门控。

### HediffGiver_VacuumBurn.cs（NOTE，强相关）
- 前提：`pawn.Map.Biome.inVacuum && pawn.HarmedByVacuum`（Pawn 级属性，水生生物/机械天然免疫的挂点）。
- 频率曲线 `VacuumSecondsBurnRate`：真空度 0.5 → 每 20 秒一次，1.0 → 每 5 秒一次；每次 1~2 点 `DamageDefOf.VacuumBurn`。
- `VacuumUtility.TryGetVacuumBurnablePart` 选取可被真空伤的部位（大概率是外露肉体部位，本片外，未验证）。
- **类比：海水致死可以完全复刻这个 giver——"溺水" giver：按水深/暴露度曲线每 N 秒造成 drowning 伤害；用 pawn 自定义属性（如 AquaticBreathing）替代 HarmedByVacuum。**

### HediffStage.preventVacuumBurns（NOTE，借用）
- HediffStage.cs:80 的布尔字段：某 hediff 到达某阶段即免疫真空烧伤（例：Odyssey 的真空抗性 hediff）。
- **类比：潜水装备/基因给的 hediff 在其 stage 上挂等价标志即可实现"防水压"，这是最省事的免疫机制。**

## 二、温度伤害（海底 4°C 恒温的冲突与参考）

### HediffGiver_Hypothermia.cs（NOTE，冲突+参考）
- 公式：低于安全温度下限时 severity 增速 = `|温差| * 6.45E-05`/tick（下限 0.00075）；回暖（高于舒适下限或站在 `heatPerTick > 0` 的地形上）按当前 severity 的 2.7% 衰减（clamp 0.0015~0.015）。
- 冻伤：非虫族、气温 < 0°C、severity > 0.37 时按概率对 `AllPartsVulnerableToFrostbite` 部位造成 Frostbite 伤害（部位 HP 的 50%）。
- 虫族用单独的 `hediffInsectoid`。
- **结论：4°C 室外会让未防护殖民者稳步累积低温症；这与海洋设定自洽（海水本来就冷），但如果 Mod 想让"气密建筑内温暖、室外寒冷"成为核心生存压力，这个 giver 原生支持，不需要新代码；只需要控制地图温度系统。**

### HediffGiver_Heat.cs（PASS 偏 NOTE）
- 高温镜像逻辑，含 150°C 以上直接 Burn 伤害的极端段。海底场景基本用不到（除非做海底热泉地图），登记为参考。

## 三、环境浓度 → Hediff 的通用范式（气体 comp 族）

### HediffComp_SeverityFromGas.cs + Properties（NOTE，借用）
- 站在含 `gasType` 的格子上：severity += `(密度/255) * severityGasDensityFactor`，再乘 `1 - pawn.GetStatValue(exposureStatFactor)`（**抗性属性减免是 XML 可配的**）；不在气体中则按 `severityNotExposed` 变化（可配负数=恢复）。
- **如果把"海水"实现为类 Gas 的网格系统（而非真空系统），这个 comp 就是现成的溺水/中毒 hediff 驱动器，`exposureStatFactor` 直接对应"潜水服密封度"属性。**

### HediffComp_SeverityFromGasDensityDirect.cs + Properties（NOTE，借用）
- 另一范式：`densityStages` 列表把气体密度百分比直接映射到 hediff 的 stage 序号（stage = 命中的档位 + 1），离开气体即 CompShouldRemove。适合"缺氧程度分级"这类瞬时状态。

## 四、地形伤害框架

### HediffGiver_Terrain.cs（NOTE，直接相关）
- 读 TerrainDef 字段：`ignitePawnsIntervalTicks`（MTB 点燃 pawn）、`burnDamage` + `burnIntervalTicks`（MTB 造成 Burn 伤害，至少 3 点，部位限定 Bottom/Outside）。
- **这是熔岩地形的实现，纯 XML 驱动。海洋类比：腐蚀性海水、海底热泉喷口、毒海葵地形——直接定义 TerrainDef 字段即可获得"站在上面受伤"的行为，无需写代码。**

## 五、装备/条件联动 hediff comp（潜水装备设计的零件箱）

- **HediffComp_RemoveIfApparelDropped.cs（NOTE）**：`CompShouldRemove => !pawn.apparel.Wearing(wornApparel)`。hediff 绑定具体装备实例，脱下即消失。潜水服供氧 hediff 的现成实现方式。
- **HediffComp_ReactOnDamage.cs（PASS）**：受特定 DamageDef 伤害时触发（加 hediff / 呕吐）。可类比"被深水生物攻击时感染寄生虫"，通用性一般。
- **HediffComp_ReplaceHediff.cs（PASS）**：severity 到阈值后把自身替换成别的 hediff（可配消息/信件）。"轻度减压症→重度减压症"可用，但 HediffStage 分阶段通常更简单。
- **HediffComp_RemoveIfOtherHediff.cs（PASS）**：存在另一 hediff 时（可限 stage、可 MTB 延迟）移除自身。
- **HediffComp_TendDuration.cs（PASS 偏 NOTE）**：治疗体系核心 comp——tendQuality 带 ±0.25 随机方差、`severityPerDayTended * tendQuality` 恢复速度、TendTicksFull/Overlap 计时。溺水伤害若做成"可治疗"（如减压病需要进加压舱治疗），走这套原生体系即可。
- **HediffComp_SeverityPerDay / SeverityPerSecond（SKIP/PASS）**：标准 severity 增减 comp，支持 range 随机、minAge、mechanitorFactor。基础零件。
- **HediffComp_SelfHeal / SkillDecay / ThoughtSetter / RecoveryThought / VerbGiver / Randomizer 系（SKIP）**：通用零件，无海洋特异性。

## 六、黑暗与光照暴露（Anomaly 机制，深海低光照可借用）

### Hediff_DarknessExposure.cs（NOTE，借用）
- 非自然黑暗中不定时（首击 480~720 tick，之后 180~300 tick）对 pawn 造成 2.5~5.5 的随机撕咬/抓/割伤害——**"黑暗中有东西攻击你"的叙事化实现**，带玩家通知信件、倒地后暂停、离开黑暗即移除。
- 硬绑 `GameCondition_UnnaturalDarkness` 与 Anomaly DLC（`ModLister.CheckAnomaly`）。
- **类比：深海掠食者袭击可借用这个"环境 hediff 周期性凭空造成伤害 + 首次信件警告"的叙事模板（需自己实现，不能直接用，DLC 门控硬编码）。**

### Hediff_LightExposure.cs（PASS 偏 NOTE）
- 每 60 tick 按 `PsychGlowAt != Dark` 增减 severity（亮 +0.4/秒级，暗 -0.25）。给怕光生物用的。**反向类比：如果做"深渊生物怕光"，可直接用；对人类殖民者的"深海幽闭/缺光抑郁"则更适合用 Thought/GameCondition 而非 hediff。**

### Hediff_FrenzyField.cs / Hediff_SleepSuppression.cs（PASS 偏 NOTE）
- "诱导器光环"模式：建筑/生物给周围 pawn 加 hediff，`lastTickInRangeOfInducer + 60` 缓冲窗口内在范围内 severity 上升、离开下降。**可类比"发光水母光环/声呐干扰场"，模式通用。**

## 七、环境粒子 FX 模板

### LavaFXComponent.cs（NOTE，借用）
- Odyssey 的 MapComponent：构造时订阅 `map.events.TerrainChanged`，维护两个 `CellFXSpawner`（深/浅熔岩各一，每格每秒 0.015 次），按地形类型（`TerrainDefOf.LavaDeep` / TempTerrain `LavaShallow`）把格子加入/移出 spawner，FX 为 MicroSparks + 自定义 LavaSmoke fleck。
- **海底气泡串、热泉微粒、发光浮游生物等"贴地形的环境粒子"照此结构实现即可；CellFXSpawner 类在别的分片（本片外，未验证）。**

## 八、其余覆盖说明（PASS/SKIP 家族）

- **Hediff 核心类**（Hediff.cs、HediffSet.cs、HediffDef.cs、HediffStage.cs、HediffWithComps/Parents/Target、HediffUtility、HediffStatsUtility、HediffMaker、HediffGiverUtility、ImmunityHandler/Record、InjuryProps）：全部读过，是通用健康系统基础设施。HediffDef 的字段清单（`lethalSeverity`、`stages`、`hediffGivers`、`comps`、`preventsDeath` 等）确认了新环境伤害 hediff 全部可 XML 定义。无海洋特异发现，PASS。
- **DLC 专属 hediff 子类**（Cube 系、DeathRefusal 系、Deathrest 系、Metalhorror、Shambler、Scaria、Psychic 系、Psylink、Mechlink、Pregnant/Labor、Inhumanized、DuplicateSickness、MeatHunger、SentienceCatalyst、ShardHolder、PorcupineQuill、Hangover、HemogenCraving、VatLearning、VatGrowing）：均为 Royalty/Biotech/Anomaly 内容逻辑，与海洋无交集，PASS。
- **InfectionPathway 系**（1.6 感染途径系统：InfectionPathway/Utility、HediffInfectionPathway）：记录"感染从哪种途径/来源生物来的"并带过期时间，用于疾病解释文案。理论上可给"海洋寄生虫感染"标来源，但价值边际，PASS。
- **H~L 基础设施**：接口族（IExposable/IThingHolder/IVerbOwner 等约 25 个）、IntVec2/3、IntRange、IntGrid、LinkGrid、按键系统（KeyBinding*/KeyPrefs*）、语言系统（LanguageWorker 全族 19 个 + LanguageDatabase 等）、UI（Listing 族、InspectTab*、ImmediateWindow、Letter 族）、日志（Log*）、存档（LoadSaveMode、LookMode、LoadIDsWantedBank、LoadedObjectDirectory）、ListerThings/ListerBuildings、JobMaker/JobDef 等——全部与海洋玩法无交集，SKIP/PASS。
