# chunk-25 发现文档（RimWorld：Stat* / Stock* / Storyteller* / Storage* / Surgery* 等家族）

本片覆盖 RimWorld 命名空间下 `StatPart_*`、`StatWorker_*`、`StockGenerator_*`、`Storyteller(Comp)*`、`Storage*`、`SurgeryOutcomeComp_*`、`Style*`、`St*` 杂项共 200 个文件。大头是「属性系统」与「叙事者事件调度」两个家族。

## TL;DR

1. **按星球图层调节事件频率的 Odyssey 先例**：`StorytellerComp_RandomMain.cs` 用 `target.Tile.LayerDef.isSpace` 判定太空层，命中则 MTB ×`spaceMtbDayFactor`（默认 2）、并施加 `spaceMinSpacingDays`（默认 2 天）最小间隔；还用 `IncidentTargetTagDefOf.Map_RaidBeacon` 跳过 ThreatBig。海洋层可以照抄这套「按 LayerDef 特判 + IncidentTargetTag 过滤」的模式（另见 `StorytellerCompProperties_RandomMain.cs` 的字段定义）。
2. **真空抗性 Stat 已存在，是"水下呼吸/耐压"属性的直接模板**：`StatWorker_VacuumResistance.cs` —— `VacuumResistance` 只对血肉 pawn 显示；`breathesAir == false` 的变异体直接返回 1.0（完全免疫）。
3. **海底恒温 4°C 直接踩到工作台惩罚线（冲突）**：`StatPart_WorkTableTemperature.cs` —— 温度 <9°C 或 >35°C 时全工作台速度 ×0.7。海底室外 4°C ⇒ 所有露天/未加热房间内工作台永久吃 ×0.7，必须加热或改判定。
4. **低温压制加成在海底恒成立（冲突）**：`StatWorker_ColdContainmentBonus.cs` —— 冷藏对实体收容强度的加成走 `ContainmentUtility.TryGetColdContainmentBonus`；4°C 恒温下海床地图等于白送冷藏加成，Anomaly 收容难度失衡。
5. **每格持续环境效果的挂载框架**：`SteadyEnvironmentEffects.cs` —— 雪/沙积累与按温度消融、无顶物品退化、`terrain.heatPerTick` 向房间注热、 terrain 引燃半径、雨中短路、`gameConditionManager.DoSteadyEffects` 与 `GasUtility.DoSteadyEffects` 都在此逐格调用。海水的"浸泡退化/沉积/水压效果"应挂在这里。
6. **"暴露即退化"StatPart 三件套模板**：`StatPart_ToxicFallout.cs`（无顶 + 毒尘 condition 时退化倍率）、`StatPart_NoxiousHaze.cs`、`StatPart_Pollution.cs` —— 都靠 `def.deteriorateFromEnvironmentalEffects` + 暴露判定。换成"泡在未气密海水中"就是现成模板。
7. **房间结构完整度评分模板（耐压壳类比）**：`StatWorker_ContainmentStrength.cs` —— 收容强度 = f(墙HP、门HP、照明、未封顶 −30、地板地形自带 ContainmentStrength、PsychologicallyOutdoors)。做"舱体水密/耐压评分"可整套借用。
8. **BiomeDef 是海洋生态事件的总闸门**：`StorytellerComp_CategoryIndividualMTBByBiome.cs` 按 `incidentDef.mtbDaysByBiome` 查当前格 `PrimaryBiome`；`StorytellerComp_Disease.cs` 用 `biome.diseaseMtbDays` 与 `biome.CommonalityOfDisease(d)`；`StorytellerComp_SingleOnceFixed.cs` 支持 `skipIfOnExtremeBiome`（`Biome.isExtremeBiome`）。自定义海洋 biome 即可控制事件频率/疾病谱/跳过特定事件。
9. **商人动物库存自带温度过滤（冲突 + 利用点）**：`StockGenerator_Animals.cs` —— `checkTemperature` 时用 `Find.World.tileTemperatures.SeasonAndOutdoorTemperatureAcceptableFor(tile, kind.race)` 筛掉温度不适宜的动物；海底 4°C 下温带动物全被过滤。同时 `tradeTagsSell/tradeTagsBuy` 机制允许加"海洋动物"标签做专营商人。
10. **每建筑独立 MTB 的"钻探引怪"模板**：`StorytellerComp_DeepDrillInfestation.cs` —— 每台可用深钻按 `baseMtbDaysPerDrill / difficulty.deepDrillInfestationChanceFactor` 独立掷 MTB 触发虫害。海底采矿/钻油引海怪直接套用。
11. **随年份拉长的资源掉落曲线模板**：`StorytellerComp_ShipChunkDrop.cs` —— MTB 随游戏年数走 SimpleCurve（20→135 天）。海洋版"漂流残骸/浮货"事件可复用。
12. **陆地动物海底觅食问题有 Odyssey 先例**：`StatWorker_ForagedNutritionPerDay.cs` —— 学会 `TrainableDefOf.Forage` 的动物按 `BodySize × 0.6` 产出每日觅食营养（Odyssey 限定）。海底地图上放牧动物吃什么是必答题，此机制是答题框架。
13. **地形标签移速修正**：`StatPart_TerrainMoveSpeed.cs`（仅解释层，但证实 `PawnKindDef.moveSpeedFactorByTerrainTag` 存在）—— 非水生生物进水域地形减速可做在 kindDef 层。

## 一、叙事者事件调度（Storyteller 家族）——海洋事件调控

- `StorytellerComp.cs`（基类）：所有 comp 走 `MakeIntervalIncidents(IIncidentTarget)`；`UsableIncidentsInCategory` 会调 `incident.Worker.CanFireNow(parms)`；`props.allowedTargetTags / disallowedTargetTags`（见 `StorytellerCompProperties.cs`）是 comp 级目标过滤——海洋地图可通过自定义 `IncidentTargetTagDef` 精确开关哪些 comp 生效。另有 `enableIfAnyModActive / disableIfAnyModActive` 按 DLC 开关。
- `StorytellerComp_RandomMain.cs`：Randy 风格主事件源。**关键**：`target.Tile.Valid && target.Tile.LayerDef.isSpace` 时 `mtbDays *= spaceMtbDayFactor`（2）且距上次事件不足 `spaceMinSpacingDays`（2 天）直接跳过；`skipThreatBigIfRaidBeacon` + `Map_RaidBeacon` tag 可压大威胁。海洋层的"事件更稀疏"照此办理。
- `StorytellerComp_ThreatsGenerator.cs`：威胁事件委托给 `ThreatsGenerator`（本片外）。PASS。
- `StorytellerComp_CategoryMTB.cs` / `_SingleMTB` / `_OnOffCycle` / `_RandomQuest` / `_RandomEpicQuest` / `_RefiringUniqueQuest` / `_ImportantQuest` / `_ClassicIntro` / `_FactionInteraction` / `_MechanitorComplexQuest` / `_MonolithMigration` / `_DissolutionTriggered` / `_GauranlenPodSpawn`：标准调度模式（MTB / on-off 周期 / 一次性 / 触发式），本身无环境判定，但都是"新增海洋事件"时可套用的骨架。其中 `_FactionInteraction` 有 `minWealth`、`minDanger` 门槛。
- `StorytellerComp_Disease.cs`：疾病 MTB = `biome.diseaseMtbDays × difficulty.diseaseIntervalFactor`（ caravan ×4），按 `biome.CommonalityOfDisease(d)` 加权选病种。海洋 biome 的疾病谱完全在 XML 层可控。
- `StorytellerComp_NoxiousHaze.cs`：以 `WorldPollutionUtility.CalculateNearbyPollutionScore(map.Tile)` 驱动 condition 的 MTB，并带"距上次触发"的最小/最大阈值窗口。可类比为"水质/赤潮"类海洋 condition 的触发器模板。
- `StorytellerComp_WorkSite.cs`：Odyssey 工地任务事件，10 天后按 `QuestNode_Root_WorkSite.BestAppearanceFrequency()` 缩放 MTB。海洋版"沉船打捞点/海床矿点"任务事件可仿。
- `Storyteller.cs` / `StorytellerUtility.cs` / `StoryState.cs`：调度核心与威胁点数计算（`DefaultThreatPointsNow` 基于财富等），通用，无环境特判。`StorytellerUI.cs` 里有难度开关 `allowExtremeWeatherIncidents`——极端天气类事件在难度层就能全局关掉，海底屏蔽热浪/寒流可借这个 Difficulty 字段思路。
- `StoryWatcher_Adaptation.cs` / `_PopAdaptation`：适应性与人口意图，通用难度曲线，与海洋无关。

## 二、属性系统（StatPart/StatWorker）——环境暴露与退化

- **暴露退化三件套**（TL;DR #6）：`StatPart_ToxicFallout` / `StatPart_NoxiousHaze` / `StatPart_Pollution`。判定要素：ThingDef 勾选 `deteriorateFromEnvironmentalEffects`、位置无顶（`PositionHeld.Roofed`）或处于污染格/毒气霾暴露（`NoxiousHazeUtility.IsExposedToNoxiousHaze`、`Position.IsPolluted`）。给退化速率乘倍率。"物品泡在海水里加速损坏"照此实现。
- `SteadyEnvironmentEffects.cs`（TL;DR #5）：这是地图级逐格 tick 的环境效果总线。已验证行为：每 tick 处理 `map.Area × 0.0006` 格；雪/沙只在无顶格积累（Perlin 噪声调制）；温度 >0°C 按 `MeltAmountAt` 消融（10°C 以下平方衰减）；`room.UsesOutdoorTemperature` 区分室内外；`terrain.heatPerTick` 向房间注热；>240°C 概率自燃；雨中短路走 `ShortCircuitUtility.TryShortCircuitInRain`；物品退化走 `FinalDeteriorationRate`（无顶/室外温度/地形 `extraDeteriorationFactor` 都会写进退化原因列表）；结尾调 `gameConditionManager.DoSteadyEffects` 与 `GasUtility.DoSteadyEffects`。海洋 Mod 的"海水格持续效果"应在此并列注入。
- **室内外判定 StatPart**：`StatPart_Outdoors`（`room.OutdoorsForWork` 或无顶即算室外，室内/外两个倍率字段）、`StatPart_WorkTableOutdoors`（`PsychologicallyOutdoors` ⇒ 工作台 ×0.8）、`StatPart_WorkTableRoomRole`（工作台不在指定房间角色时乘 `workTableNotInRoomRoleFactor`）。海底"半开放舱室"的房间语义会直接影响这些惩罚。
- `StatPart_WorkTableTemperature`（TL;DR #3）：硬编码 9°C/35°C 阈值，越界 ×0.7，`GenTemperature.GetTemperatureForCell` 取温。
- `StatWorker_ColdContainmentBonus`（TL;DR #4）与 `StatWorker_ContainmentStrength`（TL;DR #7）：Anomaly 收容体系。后者把"房间结构"量化为数值：墙 HP、门 HP、照明、未封顶 −30、地板地形 statBases 里的 ContainmentStrength 全计入，`PsychologicallyOutdoors` 的房间直接不合格——这就是"耐压壳完整度"的现成公式。
- `StatWorker_VacuumResistance`（TL;DR #2）：`ShouldShowFor` 限血肉 pawn；不呼吸空气的变异体直接 1.0。加"潜水/耐压"属性时照抄。
- `StatPart_TerrainMoveSpeed`（TL;DR #13）：本体只是报告解释，实际修正在别处（未在本片），但确认 `PawnKindDef.moveSpeedFactorByTerrainTag`（`Dictionary<string,float>`，按 TerrainTag 翻译键展示）是官方机制。
- `StatWorker_ForagedNutritionPerDay`（TL;DR #12）：Odyssey 的 Forage 训练，动物每日觅食营养 = `BodySize × 0.6`。
- 其余 StatPart/StatWorker（心情、疼痛、疲劳、品质、材料、近战 DPS、市值、奴隶、机械师等约 60 个）均为通用数值修饰，与海洋语境无交集，详见覆盖表。

## 三、贸易库存（StockGenerator 家族）

- `StockGenerator_Animals.cs`（TL;DR #9）：温度过滤 + 野性度选择曲线（0 野性权重 100 → 1.0 野性权重 2）+ tradeTags 买卖配对。海底商人卖什么动物，温度过滤是第一个要处理的点（`SeasonAndOutdoorTemperatureAcceptableFor` 用的是世界格季节温度，海洋格的温度模型直接决定货架）。
- `StockGenerator.cs` 基类有 `maxTechLevelGenerate / maxTechLevelBuy` 字段——"只刷工业科技以上"的贸易侧也可在 stock 层控制。
- 其余（Tag/Category/SingleDef/Slaves/Techprints/Tomes/ReinforcedBarrels 等）为通用库存生成器，无关。

## 四、世界地图与地基

- `SurfaceLayer.cs`：地表图层把 elevation/hilliness/temperature/rainfall/swampiness/pollution/河流/道路/TileMutator 序列化成 byte 数组存档；注意 `TilesToRawData` 里 `WaterCovered` 格直接存原 elevation（允许负值海深），陆格 clamp 到 ≥1——**世界格已原生支持负海拔（水覆格）**，海床深度数据可存在 elevation 里。
- `SubstructureGrid.cs`：Odyssey 引力船"子结构"网格——`terrainGrid.FoundationAt(index)?.IsSubstructure` 判定地基地形，GravEngine 通过 `CompSubstructureFootprint` 半径连接地基并双色（连接/未连接）可视化，`StatDefOf.SubstructureSupport` 限制规模。类比：海床建筑需要"打地基/耐压支座"时，这套"支撑源建筑 + 地基地形 + 连接可视化 + 支撑上限"的整套机制可借用。
- `StructureLayoutDef.cs`：遗迹/结构布局 Def，可指定墙/门/灯/地板 terrainDef、`surroundingTerrainDef`、散置 prefab。海底废墟（沉船、失事设施）生成可走 LayoutDef 管线。

## 五、无关家族（已扫读，确认无海洋价值）

- `SurgeryOutcomeComp_*`（10 个）：手术成功率修饰（床/房间品质、药品、年龄曲线等），无环境维度。
- `Storage*` / `StoreUtility`：存储区/过滤器/搬运目标，纯物流。
- `Style*` / `StuffCategory*`：发型纹身与材料类别，纯数据。
- `Study*` / `Stat*` 基础设施（StatRequest/StatUtility/StatsReportUtility 等）：属性系统管道，无环境逻辑。
- `StealAI*` / `StunHandler` / `StrengthWatcher` / `StrikeRecord`：袭击 AI、眩晕、强度监视、雷击记录，无关。
