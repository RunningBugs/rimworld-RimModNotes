# chunk-26 调研发现（RimWorld 组：SurgeryOutcome* / Tale* / Terrain* / ThingSetMaker* / ThinkNode_Conditional* / Thought* 家族）

## TL;DR

- **原生已有"海洋深水/浅水"地形 Def 引用**：`TerrainDefOf.WaterOceanDeep / WaterOceanShallow`（TerrainDefOf.cs），且 Odyssey 新增了 `ShallowFloodwater / MarshFlood`（临时洪水地形）、`ToxicWaterShallow/Deep`、`HotSpring`、`DryLakeBed`、`Space`——水体与"临时水地形"的 Def 层基础设施比预想完整。
- **游泳寻路已有实现**：`SwimPathFinder.TryFindSwimPath`（SwimPathFinder.cs）——以 `terrain.IsWater` 且 `toxicBuildupFactor == 0` 为可游条件，用 `GenSight.LineOfSight` 做水中步进（最多 12 节点、步距 1~3 格）。目前只被 `JoyGiver_GoSwimming`（娱乐游泳）和调试工具调用，可直接借为水生生物/潜水员的水中移动模板。
- **临时地形系统可借作"排水/回灌"机制**：`TempTerrainManager`（TempTerrainManager.cs）支持 `QueueRemoveTerrain(cell, tick)` 定时移除临时地形，并在 Odyssey 下挂载 `FreezeManager` 做每格稳态效果——"海水淹没是临时地形、排水阀排队移除"的原生范本。
- **袭击装备科技过滤逻辑明确**：`TechLevelUtility.CanSpawnWithEquipmentFrom`（TechLevelUtility.cs）逐档规定各科技等级能穿什么装备（Industrial 只穿 Industrial，Spacer 可穿 Industrial+Spacer），"只允许工业以上袭击"可直接在 faction/pawnKind 层用此体系约束。
- **服装已带"环境防护"装备属性先例**：`ThingStuffPair` 缓存 `VacuumResistance`（Odyssey）与 `ToxicEnvironmentResistance`（Biotech）两个 equippedStatOffset（ThingStuffPair.cs）——"潜水服/抗压服"照此加一个 stat offset 即可被装备生成与加权逻辑消费。
- **温度系 ThinkNode 是"只刷水生生物"的天然筛子**：`ThinkNode_ConditionalAnimalWrongSeason`（SeasonAcceptableFor）、`ThinkNode_ConditionalDangerousTemperature`（SafeTemperatureRange 之外）、`ThinkNode_ConditionalOutdoorTemperature`（UsesOutdoorTemperature）——海底恒温 4°C 会让安全温度范围不含 4°C 的动物全部处于"错误季节/危险温度"状态而被 AI 驱离；水生动物只需把安全温度范围覆盖 4°C 即可留下。
- **地形斑块生成器可直接生成海底地貌**：`TerrainPatchMaker`（Perlin 噪声 + 阈值映射 + `isPond` 标记 + `minSize` 泛洪校验 + 肥力过滤）是海沙/海泥/热泉区斑块的原生模板。
- **天降事件内容生成器与海底语境有交互点**：`ThingSetMaker_ResourcePod` 有 `preventSpawningInResourcePod` 标记、`ThingSetMaker_Meteorite` 有 `mineablePreventMeteorite` 标记——海底落物事件可用同款排除标记控制掉落池。

## 水体与地形

### TerrainDefOf.cs — 水体地形 Def 清单（直接相关）
原生 DefOf 已含 `WaterDeep / WaterShallow / WaterMovingChestDeep / WaterMovingShallow`，以及**海洋专属** `WaterOceanDeep / WaterOceanShallow`。Odyssey 追加：`ShallowFloodwater`、`MarshFlood`（洪水类临时地形）、`ToxicWaterShallow / ToxicWaterDeep`、`Space`（真空气格地形）、`HotSpring`、`DryLakeBed`、`Lava*`、`Substructure`。结论：海洋格地图生成不必从零造地形 Def，WaterOcean* 已存在；"非气密区域充满海水"若用地形表达，可参考 `Space` 地形的整套配套（该地形在 Odyssey 中是致死环境格的载体，具体致死逻辑在其他分片的 Vacuum 系统里）。

### TempTerrainManager.cs / TerrainRemoveTick.cs — 临时地形生命周期（借用：排水/回灌）
- 每 tick 随机抽 `map.Area * 0.0006` 个格子做"稳态效果"（`DoCellSteadyEffects`），Odyssey 激活时委托给 `FreezeManager`（结冰/融化类逐格效果）。
- `QueueRemoveTerrain(cell, tick)` 用优先队列按 tick 定时调用 `map.terrainGrid.RemoveTempTerrain(cell)` 移除临时地形，全套可序列化。
- 类比：Mod 的"排水阀抽干某房间海水"可实现为临时海水地形 + 排队移除；"舱体破裂回灌"则是反向添加。FreezeManager 的挂载方式（`ModsConfig.OdysseyActive` 条件构造）也是"按 Mod 激活挂子系统"的范例。

### TerrainPatchMaker.cs / TerrainThreshold.cs / TerrainTemplateDef.cs — 地形生成（借用：海底地貌）
`TerrainPatchMaker` 用 Perlin 噪声（frequency/lacunarity/persistence/octaves 可配）+ `TerrainThreshold` 的 [min,max] 噪声值区间映射到 TerrainDef；支持 `minFertility/maxFertility` 过滤、`minSize` 泛洪填充校验最小斑块、`isPond` 标记。海底的沙地/泥地/矿脉区/热泉区斑块可直接用这套生成。`TerrainTemplateDef` 是可染色地板模板（`TerrainDefGenerator_Carpet` 按 ColorDef 展开），与海洋基本无关，仅说明地形也可由生成器批量派生。

### TerrainAffordanceDefOf.cs — 地形能力标签（直接相关）
affordance 列表：`Light / Medium / Heavy / SmoothableStone / MovingFluid / Bridgeable / Walkable`。`MovingFluid`（流体）、`Bridgeable`（可架桥）是水体地形的核心标签：建筑的 `terrainAffordanceNeeded` 与地形的 affordances 匹配决定能否建造（参见 TerrainDefGenerator_Carpet 里生成地毯时设 `terrainAffordanceNeeded = Heavy`）。海底建筑规则（"只能建在气密结构内/海床上"）应通过自定义 affordance 或复用现有标签实现。

## 水中移动

### SwimPathFinder.cs — 游泳路径搜索（直接相关，强）
`TryFindSwimPath(pawn, root, out result)`：从 root 出发做最多 12 步的贪心水中游走，每步候选格要求：`InBounds && Standable && !IsForbidden && terrain.toxicBuildupFactor == 0 && terrain.IsWater && GenSight.LineOfSight(当前格, 候选格) && !KnownDangerAt`。评分：基础 10000，曼哈顿距离 >40 格按 70→40 衰减，急转弯（>110°）乘 0.01，后半段向外走乘 1.5 加成。当前调用方只有 `JoyGiver_GoSwimming`（殖民者游泳娱乐，不在本片）和 `DebugToolsTrailer`。结论：这是"水中局部游走"而非全图 A*，适合水生生物闲逛/逃窜；长距离水中通勤仍需普通 PathFinder + 水域 pathCost。毒水不可游（toxicBuildupFactor 过滤）值得照搬到"污水/酸液海"。

## 科技等级与袭击

### TechLevel.cs / TechLevelUtility.cs — 装备科技过滤（借用：工业+袭击限定）
`CanSpawnWithEquipmentFrom(pawnLevel, gearLevel)` 的实际规则：Undefined 一律 false；Neolithic 可穿 ≤(int)2（即 Undefined/Animal/Neolithic）；Medieval ≤3；Industrial **只穿 Industrial**；Spacer 可穿 Spacer 或 Industrial；Ultra 可穿 Ultra 或 Spacer；Archotech 只穿 Archotech。`IsNeolithicOrWorse` = (int)techLevel ≤ 2。结论："海底只来工业以上袭击"不需要新机制——控制 faction 的 techLevel + 装备生成走此函数即可；注意 Industrial 档不穿 Medieval 以下装备这条不对称规则。

### ThingStuffPair.cs — 装备环境防护属性先例（借用：潜水服/抗压服）
构造函数缓存 5 个值：`Price / InsulationCold / InsulationHeat / ToxicEnvironmentResistance（Biotech 限定）/ VacuumResistance（Odyssey 限定）`，后两者取自 `thing.equippedStatOffsets.GetStatOffsetFromList(...)`。结论：Mod 定义 `WaterPressureResistance` 之类的 equipped stat offset 后，ThingStuffPair 模式的缓存/加权可原样复用（注意此处两个 DLC 属性是按 ModsConfig 门控的，Mod 应自行决定是否门控）。

## 动物 AI 与温度筛选

### ThinkNode_ConditionalAnimalWrongSeason.cs（相关：只刷水生生物）
动物专属条件：`!pawn.Map.mapTemperature.SeasonAcceptableFor(pawn.def)`（Verse 层 MapTemperature.cs:91，比对动物 def 的温度适宜范围与地图当前温度）。海底恒温 4°C 时，安全范围不含 4°C 的野生动物会持续满足此条件（在 think tree 中通常接"离开地图"分支）——这是"只留水生动物"的原生筛选通道；水生动物 def 把舒适/安全温度范围写宽即可豁免。

### ThinkNode_ConditionalDangerousTemperature.cs / ThinkNode_ConditionalOutdoorTemperature.cs（相关）
- 前者：`!pawn.SafeTemperatureRange().Includes(pawn.AmbientTemperature)`——任何 pawn（含袭击者）处于危险温度时的 AI 开关。4°C 对普通人类袭击者不在致命范围，但若 Mod 想表现"海水失温更快"，需在 AmbientTemperature 或 SafeTemperatureRange 侧动手。
- 后者：`pawn.Position.UsesOutdoorTemperature(pawn.Map)`——气密室内温度判定就是走这个；Mod 的"气密建筑隔绝海水恒温"与室外 4°C 天然兼容此判定。

## 天降事件内容生成

### ThingSetMaker_Meteorite.cs（借用/冲突：海底陨石）
从 `nonSmoothedMineables`（`mineable && !building.mineablePreventMeteorite && !IsSmoothed`）里按 40%/35%/25% 权重选普通矿/廉价矿石/贵重矿石（BaseMarketValue 5 分界），生成 8~20 个矿块建筑并设 `canChangeTerrainOnDestroyed = false`。`mineablePreventMeteorite` 标记可用于排除 Mod 不想掉落的矿。

### ThingSetMaker_ResourcePod.cs（借用：海底补给舱）
掉落池过滤：`Item && TraderCanSell && 非装备 && 1 ≤ BaseMarketValue < 40 && !preventSpawningInResourcePod && 无 CompHatcher`；总价值 150~600 银、最多 7 堆、单堆 20~40。`preventSpawningInResourcePod` 是现成的掉落排除标记。

### ThingSetMaker_RefugeePod.cs（冲突提示：海底难民舱）
生成 `PawnKindDefOf.SpaceRefugee`，`HealthUtility.DamageUntilDowned` 保证倒地（最多重试 10 次）。倒地难民若落在深水致死环境，存活/救援逻辑需要 Mod 自己处理——此生成器不做任何地形检查。

## 本片其余覆盖（无海洋价值，一句话说明）

- **SurgeryOutcome\*（9 个）**：手术结果体系（成功/失败/死亡/附带 Hediff/异种胚复杂度 comp），纯医疗，与海洋无关。
- **Tale\*（24 个）**：艺术品故事（tale）记录与文本生成；`TaleData_Surroundings` 会记录 biome/天气/房间属性/温度/雪深进艺术品描述（海底殖民地艺术品会引用自定义 biome 名，属自然兼容，不算发现）。
- **Thought\*（17 个）**：心情/想法系统（含 Ideology 仪式、角色、建筑需求想法），通用框架无水域交互。
- **ThinkNode_Conditional\* 其余（约 70 个）**：通用 AI 条件（ faction/倒地/燃烧/需求/职责等），除上述温度系 3 个外均无海洋类比点；`ThinkNode_ConditionalCanReachMapEdge.allowFlyOutInstead` 支持飞行离图，海底语境无对应。
- **ThingSetMaker\* 其余（约 20 个）**：通用物品集生成框架（按价值/营养/堆数/子树/条件包装等）。
- **ThingDefGenerator_\*（5 个）**：蓝图/框架、尸体、肉、神经训练器、技术蓝图 ThingDef 批量派生。
- **Targeter / TargetHighlighter / TargetingParameters**：瞄准 UI 与目标过滤参数。
- **Tattoo\*、TexCommand、TextGenerationPurpose、ThingCategoryDefOf、ThingDefOf、ThingOverlaysHandle、ThingRequiringRoyalPermissionUtility、ThingSelectionUtility、ThingStyleHelper、TechprintUtility、TendUtility、TerrorUtility、TameUtility、TerrainDefGenerator_\***：纹身/UI 贴图/DefOf/皇室权限/选择过滤/样式/技术蓝图/医疗包扎/奴隶恐吓/驯服警告，均与海洋无关（TameUtility.CanTame 只查 Wildness/Scaria/Dryad，驯服水生动物无需改它）。
