# chunk-20 调研发现：QuestPart / Quest / Raid 家族

本片覆盖 RimWorld 命名空间下几乎全部 `QuestPart_*`（任务部件）、`Quest*`（任务框架）、`QuickSearch*`（UI）、`Raid*`（袭击定义）共 200 个文件。绝大多数 QuestPart 是"信号进→做事→信号出"的 XML 可拼装脚本积木，与海洋无直接关系；但**任务/袭击的"地图与星球层合法性"门控体系**是海洋 Mod 可直接借用的现成框架。

## TL;DR

1. **任务按星球层(PlanetLayer)过滤的机制已内置**（`QuestScriptDef.cs`）：Def 上有 `layerWhitelist` / `layerBlacklist` / `everAcceptableInSpace` / `neverPossibleInSpace` / `canOccurOnAllPlanetLayers` 字段，`CanQuestOccurOnTile()` 逐层检查。海洋若实现为新 PlanetLayer，可直接用 XML 把不适合海底的任务（如需要 drop pod、虫害、地面袭击的任务）黑名单掉——零代码。
2. **袭击策略(RaidStrategyDef)也有 `layerWhitelist`/`layerBlacklist`**（`RaidStrategyDef.cs:17-19`）：可以用纯 XML 禁止"工兵破墙""中心空投"等策略出现在海洋层，实现"只有工业科技以上的合理袭击"。
3. **到达方式有自动回退链**（`QuestPart_PawnsArrive.cs:87-103`）：指定的 `PawnsArrivalModeDef` 在目标地图上 `CanUseOnMap` 不通过时，会遍历所有 `canBeBackup=true` 的模式挑一个能用的；全都不行则 Log.Error 放弃。海洋地图需要让所有地面走入/空投模式的 `CanUseOnMap` 返回 false，并提供一个"潜水/潜艇到达"的 backup 模式，否则任务人物会 Log.Error 消失或以不合理方式上岸。
4. **任务虫害 Infestation 无视屋顶要求**（`QuestPart_Infestation.cs:89`）：`InfestationUtility.SpawnTunnels(..., spawnAnywhereIfNoGoodCell: true, ignoreRoofedRequirement: true, ...)`——任务驱动的虫巢可以刷在任何地方，海底地图必须在更外层（任务层黑名单或 InfestationUtility patch）拦截，否则海底长虫巢。
5. **奖励/补给几乎全靠轨道空投**（`QuestPart_DropPods.cs`、`QuestPart_GiveNearPawn.cs:188`、`QuestPart_LendColonistsToFaction.cs:98`）：走 `DropPodUtility.DropThingGroupsNear` / `DropCellFinder`，落点要求 `!x.Roofed(map)`。海底"空投穿水+穿顶"是冲突点：要么让海底建筑屋顶不被视为 Roofed（影响气密语义，危险），要么 patch 空投落点/到达表现（如换成货运潜艇）。
6. **世界据点与迁殖民地选址不考虑海洋**（`QuestPart_SpawnWorldObject.cs:54` 用 `TileFinder.TryFindNewSiteTile`；`QuestPart_NewColony.cs:52` 用 `MoveColonyUtility.PickNewColonyTile`）：海洋格加入世界地图后，任务据点可能刷在海里、Archonexus 迁殖民可能让玩家选中海洋格——需确认 TileFinder/PickNewColonyTile 的可通行性谓词是否会把海洋格排除（本片未覆盖这两个工具类，**未验证**）。
7. **任务触发的 GameCondition 自带地图适用性检查与跨地图回退**（`QuestPart_GameCondition.cs:48-66`）：`gameCondition.CanApplyOnMap(map)` 不通过时会遍历所有玩家殖民地找一张能用的图。海洋地图若让"热浪/寒流"类 condition 的 `CanApplyOnMap` 返回 false，任务会自动把条件转投到别的殖民地——这正是"海底恒温屏蔽温度事件"想要的语义挂点。

## 1. 星球层门控：海洋任务过滤的直接模板（强相关）

### QuestScriptDef.cs（任务定义本体）
关键字段（第 86-96 行，全部 XML 可填）：
- `everAcceptableInSpace` / `neverPossibleInSpace`：Odyssey 为太空层加的两个开关，语义是"非 autoAccept 的任务默认不许在太空层接受/发生"。
- `layerWhitelist` / `layerBlacklist`（`List<PlanetLayerDef>`）：任意层通用过滤。
- `canOccurOnAllPlanetLayers`、`endOnColonyMove`。

`CanQuestOccurOnTile()`（第 202-226 行）逻辑：层不在白名单→false；在黑名单→false；非 autoAccept 且非 everAcceptableInSpace 且层 `isSpace`→false；neverPossibleInSpace 且 isSpace→false。

`TryFindNewSuitableMapParentForRetarget()`（第 228-245 行）：任务重定向时先找满足层条件的玩家殖民地，再找"有玩家 GravEngine 的地图"（Odyssey 重力船逻辑）。**类比**：海洋 Mod 若有"海洋殖民地"概念且不是通过 PlanetLayer 实现，这个重定向方法可能永远找不到海洋地图（它只看层条件+重力引擎），需要 patch 或用层方案。

**结论**：如果海洋做成 PlanetLayer，绝大多数"哪些任务能在海洋发生"的需求可以用纯 XML 解决，这是本片最重要的发现。

### QuestPart_RequirementsToAcceptPlanetLayer.cs（任务接受条件部件）
任务级别（非 Def 级别）的接受条件：`canBeSpace`、`layerWhitelist`、`layerBlacklist`；`CanAccept()` 里目标图为空时若 `canBeSpace` 直接通过，否则找任一非太空层的玩家殖民地，找不到就返回 "QuestNotSpace" 拒绝理由。**类比**：海洋版可照抄此模式写一个 `RequirementsToAcceptOceanLayer`，或者在 XML 里直接用本部件把海洋层列入黑名单。

### QuestUtility.cs（任务工具类，1012 行）
主要是 pawn-quest 记账（IsQuestLodger / GetExtraFaction 等），与海洋无关；唯一相关点：`CanAcceptQuest(Quest)`（第 268 行起）聚合所有 `QuestPart_RequirementsToAccept` 部件的判定——即上面第 2 条的汇总入口。其余 900 行已扫读，无海洋相关内容。

## 2. 到达方式回退链（强相关，海洋到达方式的挂点）

### QuestPart_PawnsArrive.cs
`Notify_QuestSignalReceived`（第 57-135 行）：
1. 目标 mapParent 失效时调用 `quest.TryFindNewSuitableMapParentForRetarget()` 重定向；
2. 用指定的 `arrivalMode`（默认 `PawnsArrivalModeDefOf.EdgeWalkIn`）；
3. `!Worker.CanUseOnMap(map)` → 遍历所有 Def 找 `canBeBackup && CanUseOnMap` 的替代；
4. 还没有 → `Log.Error` 且人物**直接不生成**。

**类比结论**：海洋地图上若 EdgeWalkIn 等模式的 `CanUseOnMap` 返回 false（这大概率要在 PawnsArrivalModeWorker 层 patch，本片未覆盖该 worker），而又没有注册海洋自己的 backup 到达模式，任务给的援军/难民会静默丢失并报错。海洋 Mod 应提供一个 `canBeBackup=true` 的自定义到达模式（潜水钟/潜艇/海边缘游入）。

### QuestPart_RandomRaid.cs
任务触发的随机袭击：`IncidentDefOf.RaidEnemy`/`RaidFriendly`，`forced=true`，可走 `incidentParms.raidArrivalMode` / `raidStrategy` 覆盖；执行前过 `incidentDef.Worker.CanFireNow(incidentParms)`（第 105 行）。**类比**：袭击最终仍走 IncidentWorker 管线，海洋的"只有工业以上袭击"既可以在 RaidStrategyDef 层黑名单（见下），也可以在 IncidentWorker.CanFireNow 层拦截——两个挂点都真实存在。

### RaidStrategyDef.cs
`layerWhitelist`/`layerBlacklist`（第 17-19 行）+ `arriveModes`（袭击策略绑定的到达方式列表）+ `pawnsCanBringFood`。XML 即可把"中心空投(ImmediateAssault 中心投放)""工兵"等策略从海洋层剔除。

## 3. 空投/天降物管线（冲突类）

- `QuestPart_DropPods.cs`（378 行，通读）：奖励空投走 `DropPodUtility.DropThingGroupsNear`/`DropThingsNear`；落点选择（第 184-190 行附近）优先 `DropCellFinder.TradeDropSpot(map)`，否则找 `x.Standable(map) && !x.Roofed(map) && (allowFogged || !x.Fogged(map)) && map.reachability.CanReachColony(x)` 的随机格，兜底 `DropCellFinder.RandomDropSpot`。
- `QuestPart_GiveNearPawn.cs:188`、`QuestPart_LendColonistsToFaction.cs:98,108`：同样 `DropPodUtility.DropThingsNear(..., canRoofPunch: false, ...)`。
- `QuestPart_Skyfaller.cs`：`SkyfallerMaker.SpawnSkyfaller(skyfallerDef, items, dropSpot, map)`，无屋顶检查，直接砸。
- `QuestPart_SpawnThing.cs`（第 99-114 行）：落点链 `TryFindShipLandingArea → TryFindSafeLandingSpotCloseToColony → FindSafeLandingSpotNearAvoidingHostiles → FindSafeLandingSpot → RandomDropSpot + TryFindDropSpotNear(allowIndoors: false)`。
- `QuestPart_SpawnSpaceDrone.cs`：`DropCellFinder.FindSafeLandingSpot(...)` 找 35/15/25 半径内安全落点。
- `QuestPart_MechCluster.cs`：`MechClusterUtility.FindClusterPosition` + `SpawnCluster(..., dropInPods: true, ...)`——机械集群也是空投入场。

**类比结论**：整条任务物资/威胁入场管线建立在"从天上掉下来"之上，落点逻辑集中在 `DropCellFinder`（本片未覆盖，**未验证**其对水体的处理）。海底场景两个选择：(a) 接受"轨道空投砸穿海水"并处理屋顶/水体表现；(b) patch DropCellFinder 与各到达 worker 换成海上来的运输工具。另外落点谓词普遍要求 `!Roofed`——如果气密建筑的屋顶在海底算作 Roofed，空投会自然避开建筑落在"海里"，物品落在致死海水中拿不到，需要设计"打捞"或让空投直接进接收区。

## 4. 虫害与 GameCondition（冲突类+挂点）

- `QuestPart_Infestation.cs:89`：任务虫害 `SpawnTunnels(hivesCount, map, spawnAnywhereIfNoGoodCell: true, ignoreRoofedRequirement: true, tag, overrideLoc)`——两个参数都强制放宽，**海底无法靠"没有厚岩顶"天然免疫任务虫害**，必须用第 1 节的任务层黑名单或在 InfestationUtility 层 patch。
- `QuestPart_GameCondition.cs`：`CanApplyOnMap` 检查 + 跨玩家地图回退（第 48-66 行）；`targetWorld` 时直接注册到世界。这是"海洋地图屏蔽地表气候事件"的现成语义挂点：condition 不适用就换地图，而不是硬刷。
- `QuestPart_Incident.cs`：任务触发任意 IncidentDef 的通用部件，强制 `forced=true` 并过 `CanFireNow`；同样带 mapParent 重定向。海洋屏蔽某类事件时，若只拦 Worker.CanFireNow，任务线会静默跳过该事件（不报错）——设计任务时需注意别让关键剧情事件被静默吞掉。

## 5. 世界地图选址（潜在冲突，未验证）

- `QuestPart_SpawnWorldObject.cs:44-68`：任务据点落位用 `TileFinder.TryFindNewSiteTile`，若目标格已有世界对象则用 `TryFindPassableTileWithTraversalDistance` 微调。海洋格是否会被 TryFindNewSiteTile 选中取决于 TileFinder 的谓词（本片未覆盖，**未验证**）。
- `QuestPart_NewColony.cs`：Archonexus 式整体迁殖民地，`MoveColonyUtility.PickNewColonyTile` 让玩家在世界地图选格→`MoveColonyAndReset` 建定居点。若海洋 Mod 允许海洋格降落，这条路径理论上天然支持"迁殖民到海底"；反之若不允许，要确认选格 UI 会排除海洋格（未验证）。

## 6. 框架结论

- `QuestPart.cs`：抽象基类（IExposable, ILoadReferenceable），含 `SignalListenMode` 枚举、`QuestLookTargets`/`InvolvedFactions` 虚属性、`Notify_QuestSignalReceived` 钩子。整个 QuestPart 体系是"XML 脚本 + C# 部件类"，**mod 可以自由新增 QuestPart 子类并在自己的 QuestScriptDef 里引用**——海洋专属任务（如"打捞沉船""修复海底电缆"）不需要 patch 任何 vanilla 代码，写新部件+新脚本 Def 即可。
- 其余 ~180 个文件（Filter_*、Pass_*、Faction/Thought/Hediff/皇家好感/穿梭机/运输舱等）均为任务内部状态机积木，机制与地图环境完全解耦，类比到海洋无任何增量信息（详见覆盖表 PASS/SKIP）。

## 未覆盖/待其他分片验证的依赖

- `DropCellFinder` / `DropPodUtility` / `InfestationUtility` / `TileFinder` / `MoveColonyUtility` / `PawnsArrivalModeWorker` / `ThreatsGenerator`：本片只读到调用点，这些工具类本体在其他分片，海洋相关结论（落点是否避水、海洋格是否可通行等）需以那些分片为准。
