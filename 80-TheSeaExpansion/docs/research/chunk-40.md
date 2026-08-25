# chunk-40：RimWorld.QuestGen（任务生成引擎）调研

## TL;DR

1. **任务系统已内建"行星图层（PlanetLayer）"门控，是海底殖民地最直接的可借用机制**：`QuestNode_GetMap`、`QuestNode_GetNearbySettlement`、`QuestNode_GetSiteTile` 都带 `canBeSpace` 开关 + `layerWhitelist`/`layerBlacklist`（PlanetLayerDef 列表）；`QuestGen_Misc.AcceptanceRequirementLayer` 把图层要求作为任务接受条件。海底若实现为 Odyssey 式行星图层，原版任务的门控框架可整套照搬（源：`QuestNode_GetMap.cs`、`QuestGen_Misc.cs:232`）。
2. **默认排斥规则是冲突点**：`QuestGen.Generate()` 里，凡是 `everAcceptableInSpace=false` 且非 autoAccept 的任务脚本，都会自动追加 `AcceptanceRequirementNotSpace()`（拒绝对太空图生效）。海底若注册为新图层，需要确认该 QuestPart 的判定是否只排除 `isSpace`，否则会误伤海底殖民地（源：`QuestGen.cs:120-123`、`QuestGen_Misc.cs:243`）。
3. **"只刷工业科技以上袭击"在 QuestGen 里已有现成写法**：`QuestGen_Threat.Raid`、`QuestNode_GetExampleRaid`、`QuestNode_GenerateThreats` 生成示例袭击时用 `Find.FactionManager.GetFactions(..., TechLevel.Industrial)` 作为最低科技过滤；`QuestGen_Pawns` 的 pawn 选择参数有 `minTechLevel` 字段并按 `def.techLevel >= minTechLevel` 过滤（源：`QuestNode_GetExampleRaid.cs:29-31`、`QuestGen_Pawns.cs:47,192,262`）。
4. **任务型负面 GameCondition 是硬编码清单**：`QuestNode_GetRandomNegativeGameCondition` 写死 8 种（火山冬天/天气控制器/热浪/寒流/毒尘/心灵抑制/EMI/心灵低语），且热浪、寒流要走 `IncidentWorker_HeatWave/ColdSnap.IsTemperatureAppropriate(map)`。海底恒温 4°C 下这些判定会失真，且清单不含任何海洋事件——需要 patch 或提供自己的节点（源：`QuestNode_GetRandomNegativeGameCondition.cs:33-43,117-124`）。
5. **动物类任务全部从 Biome 取动物**：猎杀任务 `QuestNode_GetAnimalToHunt` 从 `map.Biome.AllWildAnimals` + 季节温度适宜性选动物；疯兽群任务 `QuestNode_ManhunterPack` 走 `AggressiveAnimalIncidentUtility.TryFindAggressiveAnimalKind(points, map)`。只要海底 BiomeDef 的野生动物表只列水生生物，这两类任务自动只刷水生生物——正合 Mod 目标（源：`QuestNode_GetAnimalToHunt.cs:40`、`QuestNode_ManhunterPack.cs:39-40`）。
6. **虫害任务依赖厚岩顶**：`QuestNode_Infestation` 和 `QuestGen_Get.GetMap(mustBeInfestable)` 都要求 `InfestationCellFinder.TryFindCell` 成功。海底如果没有"overhead mountain"式厚顶，虫害任务自动不会选海底图——是特性还是缺陷需设计决策（源：`QuestNode_Infestation.cs:39-40`、`QuestGen_Get.cs:28`）。
7. **Odyssey 的"引力引擎解锁全球到达"模式可类比"潜水艇/深潜器"**：`QuestNode_GetNearbySettlement` 里 `GravshipUtility.PlayerHasGravEngine()` 为真时跳过 `WorldReachability` 检查，任何定居点都可达；`QuestNode_HasGravEngine` 是任务分支条件节点。Mod 若做"潜艇"载具，可复刻这套任务侧钩子（源：`QuestNode_GetNearbySettlement.cs:83-86`、`QuestNode_HasGravEngine.cs`）。

## 本片覆盖的家族

RimWorld.QuestGen 全部 191 个文件：QuestGen 引擎核心（Slate/信号/文本规则）、约 150 个 QuestNode 脚本节点（XML 任务脚本的 DSL 节点）、以及 QuestGen_* 静态扩展类。绝大多数是通用 plumbing（数学/比较/列表/信号路由/信件/奖励），与海洋无关；有海洋价值的集中在"选图/选地块/选阵营/选动物/选事件"这几类节点。

## 主题一：行星图层门控（最强相关）

- `QuestNode_GetMap.cs`：`IsAcceptableMap` 依次排除 PocketMap、`!canBeSpace && Tile.LayerDef.isSpace`、`mustBeInfestable && 无虫害格`、以及 `layerWhitelist`/`layerBlacklist` 不含 `map.Tile.LayerDef` 的图。**结论**：任务选图已是图层感知的，海底图层可以直接用白/黑名单进任务 XML。
- `QuestGen_Get.cs`（静态版 GetMap）：`canBeSpace=false` 时 `return !m.Tile.LayerDef.isSpace`。
- `QuestNode_GetNearbySettlement.cs`：找附近可贸易定居点时同样支持 `canBeSpace`、图层白/黑名单、`requireSameOrAdjacentLayer`（用 `Layer.DirectConnectionTo` 判邻接）；另输出 `canCaravan`（要求同图层且 `LayerDef.SurfaceTiles`）。**类比**：海底图层与地表是否"DirectConnection"决定了哪些任务能跨海投放。
- `QuestNode_GetSiteTile.cs`：任务站点选地块，`canSelectSpace` 开关 + Odyssey 地标（`allowedLandmarks`、Odyssey 激活时默认 0.5 概率选地标格）；底层 `TileFinder.TryFindNewSiteTile(..., canSelectSpace)`。**类比**：海底遗迹/沉船站点可用同一入口 + 自定义 LandmarkDef。
- `QuestGen_Misc.cs:232` `AcceptanceRequirementLayer(canBeSpace, whitelist, blacklist)` → `QuestPart_RequirementsToAcceptPlanetLayer`；`:243` `AcceptanceRequirementNotSpace()`。
- `QuestGen.cs:120`：`if (!root.everAcceptableInSpace && !root.autoAccept) quest.AcceptanceRequirementNotSpace();` —— **默认拒绝**是引擎级行为，Mod 需验证 `QuestPart_RequirementsToAcceptPlanetLayer` 对新图层的判定（该 QuestPart 不在本片，未验证）。

## 主题二：袭击/威胁生成与科技过滤

- `QuestGen_Threat.cs`：`Raid()` 组装 `IncidentParms`（forced、points 下限 `faction.def.MinPointsToGeneratePawnGroup`、到达方式/策略解析、walkIn 时用 walkInSpot）；`RandomRaid()` 同理。
- `QuestNode_GetExampleRaid.cs:29-31` 与 `QuestNode_GenerateThreats.cs:53-55`：未指定阵营时从 `GetFactions(allowHidden:false, allowDefeated:false, allowNonHumanlike:true, TechLevel.Industrial)` 且敌对的阵营里随机——**任务袭击示例本来就不要工业以下阵营**。
- `QuestGen_Pawns.cs`：`PawnGenerationRequest`（QuestGen 自定义参数类）含 `minTechLevel`；`:192` pawnKind 过滤 `(int)x.def.techLevel >= (int)parms.minTechLevel`；`:262` 已有 pawn 的阵营科技过低也会被排除。
- `MechhiveUtility.cs`：机械巢袭击固定 `Faction.OfMechanoids` + `EdgeDrop` + `ImmediateAttack`，`canRoofPunch=false`，points 下限取机械族 `MinPointsToGeneratePawnGroup(Combat)`。**类比**：海底版"固定高威胁事件"（如深海巨兽袭击）可照这个 26 行的模板写。
- `QuestNode_Incident.cs` / `QuestNode_CreateIncidents.cs`：通用"在任务信号上触发指定 IncidentDef"节点；CreateIncidents 支持按 intervalTicks 在任务持续期内周期性触发（围攻潮的模板）。两者都 forced=true、target=slate 的 map。

## 主题三：动物/生物任务 → 水生生物

- `QuestNode_GetAnimalToHunt.cs`：从 `map.Biome.AllWildAnimals` 过滤 `map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(race)` 且地图上有野生个体的动物，按难度曲线加权随机。**海底恒温 4°C 会参与这里的温度适宜性判定**，水生动物的耐受区间要覆盖 4°C。
- `QuestNode_ManhunterPack.cs`：`AggressiveAnimalIncidentUtility.TryFindAggressiveAnimalKind(points, map)`（该工具类不在本片，未验证其内部是否也是 Biome 驱动——大概率是）。
- `QuestNode_GetAnimalKindByPoints.cs`：从全部 PawnKindDef 里按 `RaceProps.Animal && combatPower < points` 随机，**不按 Biome 过滤**——若被海洋任务复用需注意它会选出陆生动物（冲突点，轻微）。
- `QuestNode_GetPawnKind.cs` 的 `anyAnimal` 选项同样全表随机（有 `onlyAllowedFleshType` 可限）。

## 主题四：温度/GameCondition 事件 → 冲突

- `QuestNode_GetRandomNegativeGameCondition.cs`：硬编码 8 条件 + challengeRating 过滤；`PossibleNow` 会查对应 IncidentDef 的难度禁用/最早天数/冷却，且热浪/寒流额外查温度适宜性。海底恒温下热浪永远"appropriate"与否取决于 `IsTemperatureAppropriate` 实现（本片外，未验证），但**清单本身不含海洋事件**，任务奖励/惩罚型状况需要 Mod 自己扩。
- `QuestNode_GameCondition.cs`：通用施加 GameCondition 节点，可 targetWorld（全球）或 map；`gameCondition.RandomizeSettings(points, map, ...)`。
- `QuestNode_GetPlantPlayerCanHarvest.cs`：种植任务用 `Find.World.tileTemperatures.GetSeasonalTemp(map.Tile)` + 未来 15 天日变化推温度区间，要求植物 min/max 生长温度覆盖（±5°C 缓冲）。**海底地图的世界格温度是否等于水温 4°C 取决于 Mod 实现**；若海底图不在正常温度系统里，此节点行为未验证。

## 主题五：虫害（Infestation）

- `QuestNode_Infestation.cs`：TestRun 要求 `allowViolentQuests`、`Faction.OfInsects != null`、`InfestationCellFinder.TryFindCell(map)` 成功。
- `QuestNode_GetHivesCountFromPoints.cs`：巢数 = points × `IncidentWorker_Infestation.PointsFactorCurve` / 220，至少 1。
- `QuestGen_Get.cs` / `QuestNode_GetMap.cs`：`mustBeInfestable` 选图参数。
- **类比**：海底若无厚岩顶则无虫害——与"只有工业+袭击"的目标方向一致（虫害本就是替代威胁），但若想要"海沟虫巢"事件需另做。

## 主题六：抵达/降落方式

- `QuestGen_Misc.cs:304` `ResolveArrivalMode`：请求的到达方式 `Worker.CanUseOnTile(tile)` 不可用时会遍历所有 PawnsArrivalModeDef 找第一个可用的兜底。**类比**：空投舱能否落在海底格由到达方式 Worker 的 CanUseOnTile 决定，Mod 可通过自定义到达方式（如"深水潜航投放"）接入，原版兜底逻辑保证不会完全失效。
- `QuestNode_GetDropSpot.cs`：空投点要求 `Standable && !Roofed && !Fogged && 距边缘≥minDist && CanReachColony`。**若 Mod 把"海水"实现为屋顶（roof），空投点会找不到**——实现海水时应避免把开阔海域标记为 Roofed，或提供自定义 drop spot 逻辑。
- `QuestNode_GetWalkInSpot.cs`：步行进入点取边缘格，三级降级（可达殖民地→未迷雾→任意）。
- `QuestNode_DropPods.cs` / `QuestGen_Misc.DropPods`：通用空投 QuestPart 组装，无地形判断（判断在 Worker/落点选择侧）。

## 主题七：其他有类比价值的点

- `QuestNode_HasGravEngine.cs` + `QuestNode_GetNearbySettlement.cs:83`：Odyssey"有引力引擎则全球可达"的任务侧钩子；潜艇载具可复刻。
- `QuestNode_EndGame_ShipEscape_FindShipTile.cs`：结局飞船降落点要求 `PrimaryBiome.canBuildBase && canAutoChoose` 且无世界物体；若海洋 biome `canBuildBase=false` 则天然不会被选为飞船落点（无需额外处理）。
- `QuestNode_GenerateSite.cs` / `QuestGen_Sites.cs`：站点生成支持隐藏 SitePart（`PossibleUnknownThreatMarker`）、`allowViolentQuests=false` 时拒绝带 `wantsThreatPoints` 的站点部件——纯框架，海底站点直接可用。
- `QuestNode_ExpansionActive.cs` / `QuestNode_ModIsActive.cs`：按 modID 分支的任务节点，Mod 自己的任务脚本可用同款条件（通过 ModIsActive）。

## 明确无关的家族（PASS 摘要）

- 数学/比较/逻辑节点（Add/Divide/Multiply/Clamp/Equal/Greater/Less/IsTrue/IsNull/Chance/LoopCount 等约 40 个）：纯 Slate 运算。
- 信号路由节点（AnySignal/AllSignals/Signal 系列、QuestGen_Signal.cs）：任务内事件总线。
- 信件/消息/奖励/好感/王位/寄宿/穿梭机/运输船节点：组装 QuestPart 的薄壳，无环境判定。
- Monument（纪念碑）系列节点：Sketch 生成与资源统计，无地形前提。
- `QuestGenUtility.cs`：信号命名、Slate 变量、语法文本解析，纯工具。
