# chunk-08 调研发现（组：RimWorld，D 开头 + Dialog_* + Difficulty/Faction 家族）

本片 200 个文件，主体是 Designator_*（规划设计器）、Dialog_*（UI 对话框）、Drug*（药物政策）、Faction*（派系）四大家族。大部分是纯 UI/纯数据，但有若干与海洋强相关的机制杠杆。

## TL;DR

- **空投落点硬性排斥水域和真空地形**：`DropCellFinder.CanPhysicallyDropInto` 对 `terrain.IsWater` 和 `terrain.exposesToVacuum` 直接 return false → 全海图上海洋格没有任何合法空投点（贸易空投、袭击空投中心、难民降落全受影响），必须 patch。另有 `FindRaidDropCenterDistant(map, allowRoofed, allowWater=true)` 默认允许水面作为远距袭击空投中心。（源：`DropCellFinder.cs`）
- **Odyssey 已有咸水/淡水钓鱼区整套机制**：`Designator_ZoneAdd_Fishing` 用 `GetWaterBodyType`（Saltwater/Freshwater/None）区分水体、`waterBodyTracker.AnyFishPopulationAt` 检查鱼群、要求 Fishing 研究完成，且框选时咸水格与淡水格会被拆进不同 Zone。海洋 Mod 的海钓可直接站在这套机制上。（源：`Designator_ZoneAdd_Fishing.cs`）
- **派系到达/袭击的限制杠杆集中在 FactionDef**：`allowedArrivalTemperatureRange`、`arrivalModeWhitelist/Blacklist`、`layerWhitelist` 等 6 个 PlanetLayer 黑白名单、`raidArrivalLayerWhitelist/Blacklist`、`disallowedRaidStrategies`、`techLevel`、`earliestRaidDays` ——「海底只允许工业科技以上袭击」可通过这些 Def 字段 + 事件侧过滤实现。（源：`FactionDef.cs`）
- **水上地基（foundation/bridge）拆除有完整的支撑检查**：`terrainGrid.CanRemoveFoundationAt` + `WorkGiver_ConstructRemoveFoundation.AnyBuildingBlockingFoundationRemoval`（拆支撑上面建筑的地基会被拒绝并提示 "MessageCannotRemoveSupportingFoundation"）→ 海底平台/支柱建筑的直接模板。（源：`Designator_RemoveFoundation.cs`）
- **难度设置已有钓鱼产量系数**：`Difficulty.fishingYieldFactor`（含存档序列化、从 DifficultyDef 拷贝），海底殖民地经济平衡可挂这个旋钮。（源：`Difficulty.cs`、`DifficultyDef.cs`）
- 水相关视觉资源已存在：`EffecterDefOf` 中有 `PawnEmergeFromWater`、`PawnEmergeFromWaterLarge`、`WaterMist`、`Fishing` 等 Effecter。（源：`EffecterDefOf.cs`）

## 1. 空投/天降系统与水域的冲突（冲突类，必须处理）

**`DropCellFinder.cs`（655 行，全读）**

- `CanPhysicallyDropInto(c, map, canRoofPunch, allowedIndoors)` 是所有空投落点的基础验证器，硬性条件：
  - `!c.Walkable(map)` → false
  - `c.GetTerrain(map).exposesToVacuum` → false（Odyssey 真空地形排斥）
  - `c.GetTerrain(map).IsWater` → false（**水域排斥**）
  - 厚顶不可穿等
- `RandomDropSpot` 同样显式排除 `IsWater` 格（两条 fallback 都排）。
- `TryFindSafeLandingSpotCloseToColony` 的 SpotValidator 排除水域；`IsSafeDropSpot` 走 `IsGoodDropSpot`（含 Standable 检查，深水格 Standable=false）。
- 结论：海洋格地形必然是 `IsWater=true`，原版逻辑下**任何空投都无法落在海里**。海底 Mod 若希望「货运舱沉降到海底/袭击乘潜水舱抵达」，需要替换这套验证（或让海底地形 IsWater=false 而用别的方式表达海水，这与真空类比方案一致：Odyssey 真空地形同样被此函数排斥，说明官方对"致死环境地形"的统一处理就是禁止空投）。
- 例外：`FindRaidDropCenterDistant(map, allowRoofed=false, allowWater=true)` 的 `allowWater` 参数默认 true——远距袭击空投中心**允许**选在水面格。这是袭击"从海上而来"叙事的现成钩子。

**`DropPodIncoming.cs`（43 行，全读）**：空投舱撞击特效（ThrowDustPuff 尘土、ThrowLightningGlow、GenClamor 15 格声响）。尘土特效在水下违和，海洋 Mod 若复用空投舱需要换特效；`SpawnThings` 支持 `spawnWipeMode` 覆盖生成。

**`DropPodUtility.cs`（全读）**：`MakeDropPodAt` 支持按 faction.def（`dropPodActive`/`dropPodIncoming`）或 transporterDef 换空投舱外观 → 派系自定义"潜水补给舱"有现成 Def 钩子（与 FactionDef.dropPodActive/dropPodIncoming 呼应）。

## 2. 钓鱼机制（直接相关）

**`Designator_ZoneAdd_Fishing.cs`（全读）**

- `Visible` 要求 `ModsConfig.OdysseyActive` 且 `ResearchProjectDefOf.Fishing.IsFinished`（研究门控）。
- `CanDesignateCell`：`c.GetWaterBodyType(map) == WaterBodyType.None`、地形 Impassable、或 `!map.waterBodyTracker.AnyFishPopulationAt(c)` 都拒绝 → 鱼群是按水体（waterBodyTracker）追踪的，海图上"鱼群密度/洄游"玩法要动的是 waterBodyTracker（不在本片）。
- `DesignateMultiCell` 把框选格按 Saltwater/Freshwater 分两批，分别进不同的 Zone_Fishing（咸水区和淡水区不合并）→ 海图全咸水，行为自然正确。
- `Designator_ZoneAdd_Fishing_Expand.cs` 只是扩展区的子类。

**`Difficulty.cs` / `DifficultyDef.cs`**：`fishingYieldFactor` 是难度级钓鱼产量系数（默认 1，参与存档与难度预设拷贝）。同文件还有 `allowExtremeWeatherIncidents`（海底恒温剧情需要屏蔽热浪/寒流事件时的难度侧开关参照）、`AllowedBy(DifficultyConditionConfig)` 的 `extremeWeatherIncidentsDisabled` 判定路径（`DifficultyConditionConfig.cs` 是对应的纯数据配置类）。

**`Designator_ZoneAdd_Growing.cs`（全读）**：种植区要求 `c.GetFertility(map) >= 0.5`（Biotech）或土豆的 fertilityMin。海底泥沙地形肥力若为 0 则无法划种植区 → 海底农业要么做肥沃海泥地形，要么全靠水培。

## 3. 派系/袭击限制杠杆（借用类）

**`FactionDef.cs`（扫字段定义）** 关键字段：

- `TechLevel techLevel`：派系科技等级——「只有工业科技以上袭击」的过滤依据。
- `FloatRange allowedArrivalTemperatureRange`（默认 ±1000）：派系到达的环境温度门槛——海底 4°C 恒温下可天然屏蔽掉有温度要求的派系/事件。
- `arrivalModeWhitelist/Blacklist`（PawnsArrivalModeDef）：限制到达方式（步行/空投/轨道）。
- `layerWhitelist/layerBlacklist`、`arrivalLayerWhitelist/Blacklist`、`neutralArrivalLayerWhitelist/Blacklist`、`raidArrivalLayerWhitelist/Blacklist`（PlanetLayerDef，Odyssey 星球图层机制）：若海洋实现为独立 planet layer，这 6 个名单是官方的分层到达/袭击过滤渠道。
- `disallowedRaidStrategies`、`disallowedRaidAgeRestrictions`、`canSiege`、`canStageAttacks`、`raidsForbidden`、`earliestRaidDays`、`dropPodActive/dropPodIncoming`（自定义空投舱）。

`Faction.cs / FactionGenerator.cs / FactionManager.cs` 等运行时类未发现水/真空相关逻辑（grep 无命中），袭击科技等级过滤的实际判定应在 RaidStrategy/IncidentWorker 侧（不在本片）。

## 4. 水上建筑与地基（直接相关）

**`Designator_RemoveFoundation.cs`（全读）**：

- 同一设计器在 Odyssey 下叫「移除地基」，无 Odyssey 叫「移除桥梁」（label/desc 按 `ModsConfig.OdysseyActive` 切换）。
- `CanDesignateCell`：`terrainGrid.CanRemoveFoundationAt(c)` + 上方有 Fillage=Full 且 Impassable 的建筑时拒绝 + `WorkGiver_ConstructRemoveFoundation.AnyBuildingBlockingFoundationRemoval` 阻止拆"支撑着别的建筑"的地基。
- 海洋类比：海底平台/支柱完全可复用 foundation 地形机制（在深水/海底不可建造格上铺可建地基），拆除时的支撑检查逻辑现成。

## 5. 其他小发现

- **`EffecterDefOf.cs`**：存在 `PawnEmergeFromWater`、`PawnEmergeFromWaterLarge`、`WaterMist`、`UndercaveMapAmbienceWater`、`Fishing` EffecterDef —— 生物从水中钻出（袭击/伏击）、水面雾气、钓鱼特效都有现成资源。
- **`Dialog_BeginGravshipLaunch.cs`（全读）**：引力船发射是一个 Ritual（`RitualBehaviorWorker_GravshipLaunch`），发射前对话框带「强制访客离开 / 带上动物 / 带上机械体」复选框 → 「潜艇载着全殖民地迁徙」的完整 UI+流程模板。`Dialog_NamePlayerGravship.cs` 是给船命名（RulePackDefOf.NamerGravship），可类比给潜艇命名。
- **`Dialog_FormCaravan.cs`**：远行队面板用 `map.Biome` 算日采集食物（ForagedFoodPerDayCalculator）——海洋生态 biome 的采集量在此链路生效（仅登记，未深挖）。
- **`Dialog_Options.cs`**：仅有 `Prefs.GravshipCutscenes` 开关，与海洋无关。
- **`DiplomacyTuning.cs`**：`Goodwill_MemberKidnappedOnGravship = -10`，纯数值常量，无关。

## 覆盖说明

- Designator_* 其余（Plan/Plants/Smooth/Strip/Tame/Uninstall/Zone 等）均为通用规划工具，读关键文件（`Designator_Plants.cs`、`Designator_SmoothSurface.cs`、`Designator_Tame.cs`、`Designator_ZoneAdd_Growing.cs` 等）确认与海洋无关；余者按类定义扫描登记 SKIP。
- Dialog_* 约 85 个全是 UI 窗口（存档列表、异种胚、仪式、政策管理等），类定义扫描确认无环境/水/真空逻辑，SKIP。
- Drug*（10 个）、Entity*（Anomaly 实体法典）、DifficultyDefOf、DutyDefOf、Direction8Way、Duplicate*、DyingRevenant 等均为纯数据/纯枚举/纯机制与海洋无交集。
