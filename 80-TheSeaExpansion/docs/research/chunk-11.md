# chunk-11 调研发现（RimWorld 组：History* / Hive* / Horror* / Ideo* / I*接口 / Incident* 家族，共 200 文件）

本片主体是**游戏事件（Incident）系统全家**——这正是"海底殖民地该刷什么事件、屏蔽什么事件"的核心改造面，价值密度很高。Ideo*/接口/History 家族基本无关。

## TL;DR（最重要发现）

1. **水中袭击已有完整官方模板**：`IncidentWorker_DevourerWaterAssault.cs` 直接把 `parms.raidArrivalMode = PawnsArrivalModeDefOf.EmergeFromWater`（吞噬者从水中钻出袭击），配上 LordJob + 信件即成事件。海底"水生生物袭击"照抄此结构即可。
2. **事件过滤的合法后门**：`IncidentWorker.CanFireNow`（`IncidentWorker.cs:35-154`）内建 `allowedBiomes/disallowedBiomes`（按世界格 PrimaryBiome 过滤）、`layerWhitelist/layerBlacklist` + `PlanetLayerDef.onlyAllowWhitelistedIncidents`（若海洋做成独立 PlanetLayer，可白名单化整层事件）。**纯 XML 即可屏蔽绝大多数陆生事件，不用 Harmony**。
3. **寒流在 4°C 海底会误触发**：`IncidentWorker_ColdSnap.IsTemperatureAppropriate` 要求 `SeasonalTemp ∈ (0,15)`——海底恒温 4°C 正好落在区间内；热浪要求 `≥20` 不会触发。寒流必须显式屏蔽（biome 黑名单）。
4. **极光事件自动免疫永暗地图**：`IncidentWorker_Aurora.CanFireNowSub` 检查 `!map.GameConditionManager.IsAlwaysDarkOutside`——深海若设成永暗（类比非自然黑暗），极光类天空事件自动不刷，这是"环境属性自动门控事件"的正面范例。
5. **派系到访/袭击有温度门控**：`IncidentWorker_PawnsArrive.FactionCanBeGroupSource` 要求 `f.def.allowedArrivalTemperatureRange` 同时涵盖 `OutdoorTemp` 和 `SeasonalTemp`——4°C 海底会自动筛掉大批派系；"只允许工业科技以上袭击"可叠加 `raidArrivalLayerWhitelist`（`IncidentWorker_RaidEnemy.cs:24-30`）实现。
6. **太空层袭击权重已有特例可抄**：`IncidentWorker_Raid.ModeWeight`（`IncidentWorker_Raid.cs:44-51`）对 `LayerDef.isSpace` 的图用 `Mathf.Max(weight, mode.minSpaceSelectionWeight)` 保证空投类抵达方式的最低权重——海洋层可照搬此模式给"潜水抵达"加权。
7. **Odyssey 已有"需要鱼"的 GameCondition 门控**：`IncidentWorker_MakeGameCondition.CanFireNowSub` 末尾检查 `gameConditionDef.requireFish && !map.waterBodyTracker.AnyBodyContainsFish`——海洋事件/条件可直接借用 `waterBodyTracker` 这套水体鱼类追踪。
8. **野生动物事件全部走 biome 动物表 + 海岸钩子**：`IncidentWorker_HerdMigration.IsValidBiomeAnimal` 里 `map.TileInfo.IsCoastal` 时会额外查 `BiomeDef.CommonalityOfCoastalAnimal`——官方已有"海岸动物"区分；海底 biome 只要定义自己的 `AllWildAnimals`，迁徙/猎杀群/丧尸动物群（`IncidentWorker_ShamblerSwarmAnimals` 会按 biome 动物变丧尸——海底自动变"丧尸鱼群"）等事件自动生效。但 `SeasonAndOutdoorTemperatureAcceptableFor` 在 4°C 下会过滤掉大多数动物，水生动物需自定义温度舒适区。
9. **虫巢/侵染机制是"深海巢穴"直接模板**：`Hive.cs`+`HiveUtility.cs`+`IncidentWorker_Infestation.cs`（CompSpawnerPawn 刷怪、CompSpawnerHives 扩散、InsectSludge 地形、GlowPod 发光荚——发光荚本身就是深海照明的风味参照）。注意 `InfestationCellFinder` 要求山体厚顶，海底图没有，侵染事件会找不到格子自动不刷；要做"海床巢穴"需自写 cell finder。
10. **天坠物家族 → "海面沉降物"类比**：`IncidentWorker_MeteoriteImpact`/`ShipChunkDrop`/`ResourcePodCrash`/`CrashedShipPart`/`OrbitalDebris` 全走 Skyfaller 从天而降——剧情上可改为"从海面沉下来的残骸/补给舱"（换抵达物 ThingDef 与贴图，机制全保留）。

## 一、事件框架与过滤机制（怎么屏蔽/放行事件）

### IncidentWorker（基类，`IncidentWorker.cs`）
`CanFireNow` 完整过滤链（未 `forced` 时依序检查）：
- `def.TargetAllowed(target)`（targetTags 与 `IIncidentTarget.IncidentTargetTags()` 求交，见 `IncidentTargetTagDefOf`：World / Map_PlayerHome / Map_Misc / Map_RaidBeacon）
- 难度设置、`earliestDay`、`min/maxThreatPoints`
- **`allowedBiomes`/`disallowedBiomes`**：按 `Find.WorldGrid[target.Tile].PrimaryBiome` 过滤——海底 biome 把这个用上即可屏蔽陆地专属事件
- **PlanetLayer 三层门控**：`layerWhitelist`、`layerBlacklist`、`canOccurOnAllPlanetLayers` + `layerDef.onlyAllowWhitelistedIncidents`（`IncidentWorker.cs:78-93`）——Odyssey 太空层就是这样把普通事件挡掉的，海洋若做成独立 PlanetLayer 可获得同等隔离强度
- `ScenPart_DisableIncident`、`minPopulation`、`minRefireDays`（含 `refireCheckTags` 关联事件）、Anomaly 威胁等级门控
- **地图上任何 `preventIncidents` 的 GameCondition 会全局停事件**（:121-130）——可做一个"海底风暴"condition 顺带停其他事件

另有同 tick 缓存（`lastCheckCanRunTick`）与 `FiredTooRecently` 冷却机制。

### IncidentDef（`IncidentDef.cs`）字段要点
`workerClass/category/targetTags/baseChance/pointsScaleable/minThreatPoints/maxThreatPoints/allowedBiomes/disallowedBiomes/layerWhitelist/layerBlacklist/canOccurOnAllPlanetLayers/gameCondition/durationDays/diseaseIncident/diseaseBiomeRecords/mtbDaysByBiome/treeDef/treeGenStepDef/mechClusterBuilding/questScriptDef`。其中 **`mtbDaysByBiome` 和 `diseaseBiomeRecords` 是按 biome 配置的**——海底 biome 需要自己的疾病频率表，否则无疾病或沿用默认。`ConfigErrors` 强制：allowed 与 disallowed 不能同时定义。

### IncidentParms（`IncidentParms.cs`）
事件参数包：`target/points/faction/raidStrategy/raidArrivalMode/spawnCenter/infestationLocOverride/pawnGroupKind/traderKind/customLetter*/silent/sendLetter/bypassStorytellerSettings` 等。自定义事件 worker 直接读写这些字段即可（如 DevourerWaterAssault 覆写 raidArrivalMode）。

### IIncidentTarget（`IIncidentTarget.cs`）
事件目标接口：`Tile / StoryState / GameConditionManager / PlayerWealthForStoryteller / IncidentTargetTags()`。Map、Caravan、World 都实现它——海底地图无需新实现，但若做"潜艇航行中"之类的自定义世界对象可参照。

### 调度（参考，无需改动）
- `IncidentQueue.cs`：QueuedIncident 按 FireTick 排序，失败可 retry（每 833 tick 重试一次）。
- `IncidentCycleUtility.cs`：按 on/off 天数周期 + 最小间隔生成确定性（种子化）事件命中表。
- `IncidentWorker_GiveQuest.cs`：任务发放事件，quests 也有 `layerWhitelist/everAcceptableInSpace/onlyAllowWhitelistedQuests` 门控（:51-78）——海洋层对任务同样生效。

## 二、温度/天气/光照类事件（海底恒温 4°C + 低光照下的冲突与屏蔽）

| 事件 | 文件 | 海底行为 | 处置 |
|---|---|---|---|
| 寒流 ColdSnap | `IncidentWorker_ColdSnap.cs` | `SeasonalTemp ∈ (0,15)` → 4°C **满足条件会触发**，GameCondition 会压低室外温 | 必须屏蔽（disallowedBiomes 或场景设置） |
| 热浪 HeatWave | `IncidentWorker_HeatWave.cs` | `SeasonalTemp ≥ 20` → 不触发 | 天然免疫 |
| 旱灾 Drought | `IncidentWorker_MakeInitialDrought.cs` | 永久 GameCondition | 海底逻辑矛盾，应屏蔽（`IncidentDefOf.Drought` 为 MayRequireOdyssey） |
| 闪暴风 Flashstorm | `IncidentWorker_Flashstorm.cs` | 直接注册 GameCondition_Flashstorm，无温度检查 | 应屏蔽（海底无雷暴）；注意它还会在雨率>0.1 时强制切天气 |
| 极光 Aurora | `IncidentWorker_Aurora.cs` | 检查 `!IsAlwaysDarkOutside` + 天光<0.5 | **永暗海底自动免疫，范例级设计** |
| 死亡 pall/非自然黑暗 | `IncidentWorker_DeathPall.cs` | MakeGameCondition 子类 | 深海低光照主题可借用该 condition 机制 |
| 岩浆流/岩浆涌出 | `IncidentWorker_LavaFlow.cs` / `IncidentWorker_LavaEmergence.cs` | 注册永久 condition / 在图中心 spawn `ThingDefOf.LavaEmergence` | **海底热泉（hydrothermal vent）的直接类比模板**：一个 spawner Thing + 永久 condition 的组合 |
| 季节洪水 | `IncidentWorker_SeasonalFlooding.cs` | 要求 SurfaceTile + 有河流 + 室外温>-7 + 春季 | 海底无河流自动不刷；其 `ThingDefOf.SeasonalFlood` spawner 模式可参考 |
| 心灵 drone/抚慰 | `IncidentWorker_PsychicDrone.cs` / `PsychicSoothe` / `PsychicEmanation` | 只要求有殖民者，与环境无关 | 海底照常用 |

### GameCondition 发放总线（`IncidentWorker_MakeGameCondition.cs`）
- 检查同名 condition 互斥、`CanCoexistWith`、`preventIncidents`
- **`gameConditionDef.requireFish` 门控**：`!(target is Map { waterBodyTracker: not null }) || !map.waterBodyTracker.AnyBodyContainsFish` 时不发——Odyssey 的鱼类水体追踪系统已被事件系统消费，海洋 mod 做"渔汛/鱼群迁徙"condition 可直接挂这个字段

## 三、袭击与到访（"只有工业科技以上袭击"的实现抓手）

### 水中袭击模板（核心）
`IncidentWorker_DevourerWaterAssault.cs`：faction=OfEntities，`raidArrivalMode=EmergeFromWater`，points×0.7，保底 minPoints×2，`LordJob_DevourerAssault`，概率挂 shard。与陆地版 `IncidentWorker_DevourerAssault.cs`（EdgeWalkIn）唯一差别就是抵达方式——**证明"换 raidArrivalMode 即可把任意袭击变成水生版"**。`IncidentWorker_ChimeraAssault/GorehulkAssault/GhoulAttack/HateChanters` 等同构。

### 派系过滤链
- `IncidentWorker_PawnsArrive.FactionCanBeGroupSource`（:17-53）：排除 player/defeated/temporary；**`allowedArrivalTemperatureRange` 需同时含 OutdoorTemp 与 SeasonalTemp**（desperate 二轮放宽温度）；`arrivalLayerWhitelist/Blacklist`；`onlyAllowWhitelistedArrivals` 时强制白名单；`MustHaveSettlementOnLayer`（子类可覆写）要求派系在该层有定居点——海洋层定居点隔离的关键字段。
- `IncidentWorker_RaidEnemy.FactionCanBeGroupSource`（:9-41）：再加 `raidArrivalLayerWhitelist/Blacklist`（派系 def 级）+ 至少一个可用 RaidStrategy。→ "只允许工业科技以上派系袭击"= 给原始派系 def 加 arrival/raidArrival 层黑名单，或让海洋层 `onlyAllowWhitelistedArrivals`。
- `IncidentWorker_Raid.ModeWeight`（:44-51）：**`LayerDef.isSpace` 时对每个抵达方式取 `max(weight, minSpaceSelectionWeight)`**——Odyssey 为太空层写的特例，海洋层想要"潜水艇/水下抵达"提权需 Harmony 类似钩子（isSpace 是硬编码）。
- `IncidentWorker_Raid.AdjustedRaidPoints`（:191-211）：`points *= target.Tile.LayerDef.raidPointsFactor`——**海洋层可在 PlanetLayerDef 上直接配 raidPointsFactor 调整袭击强度**，纯 Def 级。
- 友军支援 `IncidentWorker_RaidFriendly`：要求图上有 >120 combatPower 的活跃敌人才触发。

### 商队/访客/贸易
- `IncidentWorker_NeutralGroup`：额外检查 `preventNeutralVisitors` condition 与 `neutralArrivalLayerWhitelist/Blacklist`；`TryResolveParmsGeneral` 用 `RCellFinder.TryFindRandomPawnEntryCell`（陆地边缘进入）——海底若全图是水，边缘走入格找不到则整个事件静默失败，需要自定义进入点。
- `IncidentWorker_TraderCaravanArrival` / `VisitorGroup` / `TravelerGroup` / `CaravanArrivalTributeCollector`：同族，均走边缘进入 + LordJob。**海底需要全部替换为"潜艇抵达"版本或屏蔽**。
- `IncidentWorker_OrbitalTraderArrival.cs`：只需任何通电通讯台，贸易商船挂在 `passingShipManager`——**海底完全可用，是海底贸易主通道**（改名"过路货船"即可）。`IncidentWorker_RansomDemand.cs` 同理只需通讯台，海底可用。
- `IncidentWorker_MechCluster.cs`：机械集群空投（dropInPods），无温度/地形门控——海底可作为主要高阶威胁保留。
- `IncidentWorker_CrashedShipPart.cs`：落点校验拒绝 natural roof、要求 `DropCellFinder.SkyfallerCanLandAt`；随带休眠机械族护卫——海底版"沉船残骸"直接改 Skyfaller 表现即可。

### 伏击/商队事件（世界地图旅行侧）
`IncidentWorker_Ambush.cs`（基类）+ `Ambush_EnemyFaction` / `Ambush_ManhunterPack` / `CaravanDemand` / `CaravanMeeting`：对商队目标用 `CaravanIncidentUtility.SetupCaravanAttackMap` 现场生成 100×100 临时战斗图。海底世界格旅行时被伏击会生成陆地临时图——**需要为海洋格定制伏击图生成**（这是 worldgen 侧的活，事件入口在这里）。

## 四、生物事件（只刷水生生物的实现面）

- **动物表来源**：`IncidentWorker_HerdMigration.TryFindAnimalKind` 从 `map.Biomes.SelectMany(AllWildAnimals)` 按 `CommonalityOfAnimal`（污染时 `CommonalityOfPollutionAnimal`）抽；**`map.TileInfo.IsCoastal` 时额外并上 `CommonalityOfCoastalAnimal`**——官方已区分海岸动物，海底 biome 自定义动物表即可让迁徙事件刷鱼群。但 `IsValidAnimal` 还要求 `mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(race)`，4°C 下多数陆地动物被过滤——**水生动物 race 需配低温舒适区，否则迁徙/自驯事件全灭**。
- `IncidentWorker_FarmAnimalsWanderIn.cs`：硬编码 `tradeTags.Contains("AnimalFarm")` + Wildness<0.35 + 温度可接受——海底想要"驯顺海洋生物上门"需给水生物种打 AnimalFarm 贸易标签。
- `IncidentWorker_SelfTame.cs`：对图上任意无派系动物生效——海底刷的水生动物可直接自驯，无冲突。
- `IncidentWorker_ThrumboPasses.cs` / `Alphabeavers`：温度门控 + 边缘进入的范例；`exitMapAfterTick` 限时停留模式可借用做"洄游鱼群"。
- `IncidentWorker_AggressiveAnimals` / `AnimalInsanityMass` / `AnimalInsanitySingle`：猎杀群用 `AggressiveAnimalIncidentUtility.TryFindAggressiveAnimalKind`（吃 biome 表）；群体发疯直接在地图现有动物里挑——海底自动作用于水生动物，无冲突。
- `IncidentWorker_ShamblerSwarmAnimals.cs`：**按 biome 动物表生成丧尸版本地动物**（2% 嵌合体）——海底自动得到"丧尸鱼群"，零改动。
- `IncidentWorker_SightstealerSwarm.cs`：窃视者（畏光、喜暗的 Anomaly 实体）EdgeWalkInDistributed 袭击——深海低光照主题风味绝配，可保留/仿制。

## 五、虫巢/侵染/钻出（深海巢穴模板）

- `Hive.cs`：Building + CompSpawnerPawn（初始 200 点、上限 500 点、半径 2 刷怪）+ CompCanBeDormant（未被雾覆盖时唤醒）；Odyssey 加 Locust 兵种。`PreventPlayerSellingThingsNearby` 等细节齐全。
- `HiveUtility.cs`：`SpawnHive/SpawnHives`（`TraverseMode.NoPassClosedDoorsOrWater` 找扩散格）、`SpawnExtras` 铺 InsectSludge 地形（`GenConstruct.CanBuildOnTerrain` 校验）+ 20% Filth_Slime + **GlowPod 发光荚（深海生物照明直接借用）** + EggSac。
- `IncidentWorker_Infestation.cs`：上限 30 巢，点数曲线 0.7→0.45（5000 点），`InfestationCellFinder.TryFindCell` 要求厚岩顶——**海底图无岩顶，侵染自动不刷**；要做"海床巢穴事件"必须自写 cell finder（找热泉/海沟格）。
- `IncidentWorker_DeepDrillInfestation.cs`：`CompCreatesInfestations` 门控（`GetUsableDeepDrills`）+ TunnelHiveSpawner（points×0.3~0.6，clamp 200~1000）——海底钻探引发巢穴的现成机制，保留即可。
- `IncidentWorker_FleshbeastAttack.cs`：PitBurrow 从地下钻出 fleshbeast（`LargeBuildingSpawnParms` 找格、Outdoors 限定、延迟 420 tick 涌出）——**"海底泥沙中钻出怪物"的直接模板**；`IncidentWorker_PitGate.cs`/`FleshmassHeart.cs` 同用 `LargeBuildingCellFinder` 大型建筑刷点框架。

## 六、植物/资源事件

- `IncidentWorker_CropBlight.cs`：枯萎病按**房间**扩散（同 Room 内半径 11×点数系数，8 格内 100%）——海底室内农场的病害平衡参照。
- `IncidentWorker_AmbrosiaSprout.cs` / `BoomshroomSprout`：要求 `PsychologicallyOutdoors` + 生长季 + 肥力 + Room≥64 格——海底室外全是"海水环境"，陆生萌发事件应屏蔽，但模式可抄做"深海热泉菌群/发光藻萌发"。
- `IncidentWorker_SpecialTreeSpawn.cs`（基类）+ `AnimaTreeSpawn/PoluxTreeSpawn/HarbingerTreeSpawn`：`GenStep_SpecialTrees.CanSpawnAt` + `DesiredTreeCountForMap` 配额 + **`map.Biome.isExtremeBiome` 直接拒刷**——若海底 biome 标 extreme，这类事件自动免疫。
- `IncidentWorker_GauranlenPodSpawn.cs`：`IsGoodBiome` 示范了用 IncidentDef 自己的 allowed/disallowedBiomes 做运行时判断。
- 天坠资源：`MeteoriteImpact`（TryFindSkyfallerCell 要 Light 地形承载 + 站立格计数）、`ShipChunkDrop`（随游戏时间衰减数量 1.2→0.1）、`ResourcePodCrash`（RandomDropSpot + DropPodUtility）、`OrbitalDebris`（spawn 一个 `OrbitalDebrisSpawner` Thing 代发）——全部可改皮为"海面沉降"。

- `IncidentWorker_ShortCircuit.cs`：短路事件，随机挑 `ShortCircuitUtility.GetShortCircuitablePowerConduits` 的电缆炸——海底"电力+海水"主题的风味冲突点，可考虑保留（水下短路更危险）或替换为专属事件。
- 疾病事件（`IncidentWorker_Disease.cs` 及 `DiseaseHuman`/`DiseaseAnimal`）：受害者筛选排除低温休眠舱/生物塑形舱内 pawn，发病比例由 `diseaseVictimFractionRange` 控制；按 biome 的发病率表在 `IncidentDef.diseaseBiomeRecords`——海底 biome 需自配疾病表。

## 七、Anomaly/其他事件（简表）

- `IncidentWorker_Obelisk.cs`：`ChanceFactorNow` 按图上已有方尖碑数 ×0.7/个 动态调权——"已存在实体影响事件权重"的范例。
- `IncidentWorker_PsychicRitualSiege/ShamblerAssault/EntitySwarm` 等：RaidEnemy 子类覆写 faction/strategy 的范式。
- `IncidentWorker_MetalhorrorImplantation`（感染路径）、`Revenant/Emergence`（物品延迟孵化）、`UnnaturalCorpseArrival`、`GoldenCubeArrival`（MeteoriteImpact 子类+指定殖民者 CubeInterest）、`Nociosphere`、`VoidCuriosity`、`HateChanters`、`HarbingerTreeProvoked`、`InsectJelly`（InfestationCellFinder 同限岩顶）、`GameEndedWanderersJoin`（结局后初始小人不带温度检查直接边缘刷入）——均读过，机制已明，海底无特殊冲突，大多走 EdgeWalkIn 需注意进入格。

## 八、杂项

- `HorrorRelaxTransition.cs`：MusicTransition 检查 pocket map 是 Labyrinth/**Undercave** 或图上有 PitGate/FleshmassHeart/Noctolith 时切恐怖音乐——**深海地图氛围音乐的挂点**（自定义 MusicTransition 子类即可）。`HorrorTensionTransition.cs` 检查 UnnaturalDarkness/DeathPall/BloodRain condition。
- `IdeoBuildingPresenceDemand.cs`：意识形态"建筑存在需求"+房间要求（`RoomRequirement` 列表、期望等级门控、Alert 联动）——若做海洋主题模因（如要求"深海祭坛"），机制在此；`IdeoFoundation` 有 `InitialVeneratedAnimalsCountRange`（尊崇动物），海洋模因可尊崇水生动物。
- `HumanEmbryo/HumanOvum`（生育）、`History*`（历史曲线/事件记录）、其余 Ideo*（意识形态生成/UI/重构）、全部 I* 接口、各 DefOf/枚举——与海洋无关，已登记覆盖表。
- `HistoryEventsManager.cs`：precept 用的事件统计（RecordEvent→IdeoUtility.Notify_HistoryEvent），若加自定义海洋 HistoryEvent（如"猎杀深海巨兽"）走这里。

*注：本片未发现与"气密/海水环境模拟"直接相关的代码（那在 Verse 组的 Room/Region 系）；事件层与水体的唯一官方交集是 `EmergeFromWater` 抵达方式、`requireFish` 门控、`CommonalityOfCoastalAnimal`、`IsCoastal` 这四处分立钩子。*
