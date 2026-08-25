# chunk-41 调研发现：RimWorld.QuestGen（任务脚本节点/部件，后半段）

本片是 QuestGen 程序集的后半段：QuestNode_*（脚本节点）、QuestPart_*（运行时部件）以及 Slate 基础设施。
绝大多数是通用任务脚手架（信号、延迟、文本解析、奖励），与海洋无关；真正的价值集中在三处：
**(1) Odyssey 行星层(PlanetLayer)任务选址机制 = 海底层任务/遗址的直接模板**；
**(2) 非自然黑暗 + 黑暗门控刷怪 = 深海低光照与深海生物的机制模板**；
**(3) 大量任务对"太空图"的显式排除/放行（canBeSpace、AcceptanceRequirementNotSpace、RandomSurfacePlayerHomeMap）= 海底殖民地会被同样排除的冲突点清单**。

## TL;DR

1. **小行星遗址任务 = 海底矿点遗址的完整模板**（`QuestNode_Root_Asteroid.cs`）：在玩家地块的"相邻行星层"上找格、生成 `SpaceMapParent` 世界对象、随机选一种可采矿物命名遗址、45-60 天超时。海底层照抄即可做"海底热液矿床/沉船打捞点"。
2. **任务选址已全面行星层化**（`QuestNode_Root_Site.cs`、`QuestNode_Root_Gravcore.cs`）：`canBeSpace`、`layerWhitelist/layerBlacklist`、`requireSameOrAdjacentLayer`、`PlanetLayer.FastTileFinder.Query(biomes, landmarks)` 都是现成参数——海底 Mod 只需新增自己的 PlanetLayerDef 并把海洋生物群系塞进这些白名单机制。
3. **任务接受的图层门槛节点已存在**（`QuestNode_RequirementsToAcceptPlanetLayer.cs`）：`canBeSpace` + 图层黑白名单，配合 `QuestNode_Root_MonolithMigration.cs` 里的 `quest.AcceptanceRequirementNotSpace(map.Parent)`，是"该任务能否发给海底殖民地"的官方开关位置。
4. **非自然黑暗事件 = 深海黑暗的机制范本**（`QuestNode_Root_UnnaturalDarkness.cs` + `QuestPart_DarkenMap.cs`）：`gameConditionManager.SetTargetBrightness(0f)` 把全图亮度压到 0，结束时恢复 1f 并把 GameCondition 设为非永久。深海"恒暗"可直接借这套亮度管道，而不必新造光照系统。
5. **黑暗门控刷怪机制**（`QuestPart_Noctoliths.cs`）：Noctol 只在 `map.glowGrid.GroundGlowAt(c) < 0.01f` 的格子上刷出，且用 `PawnGroundSpawner`（钻地延迟出现）——这就是"深海怪物只在无光区/从海床钻出"的原版实现。
6. **PitBurrow 伏兵 = 海床洞穴伏击模板**（`QuestPart_DistressCallAmbush.cs`）：求救信号任务在玩家进入地图后从 `PitBurrow` 里刷 Fleshbeast 波。海底版即"沉船探索惊动海床巢穴"。
7. **大量任务是"仅限地表"或"显式排除太空"**（冲突）：`QuestPart_SpawnMonolith` 用 `Find.RandomSurfacePlayerHomeMap`（巨石只去地表玩家家）；`WandererJoin.CanBeSpace` 默认 false；Mission 基类有 `canBeSpace` 字段。海底图若实现为非 Surface 层，这些任务会默认屏蔽——需逐一决定放行还是替换。
8. **Odyssey 地标(Landmark)清单里已有大量水体地标**（`QuestNode_Root_DistressCall.cs`、`QuestNode_Root_AncientComplex.cs`）：Oasis/Lake/LakeWithIsland/Pond/ToxicLake/Wetland/CoastalIsland/Peninsula 等，任务选址按地标过滤是官方做法——海底可加"海沟/热泉/沉船"类 LandmarkDef 复用同一管道。
9. **空降(Skyfaller)是任务送货/送人的统一入口**（`QuestNode_SpawnSkyfaller.cs`、`QuestNode_Raid.cs` 的 dropSpot 分支）：空投舱从天而降。海水里"天上掉下来"需要替换为潜艇/潜航舱到达模式，否则奖励舱穿海画面违和（冲突类，但机制上 DropPods 任务部件不查水体，能直接用）。
10. **多波次实体袭击编排模板**（`QuestNode_Root_SightstealerArrival.cs`）：嚎叫音效预警→延迟→分波 EdgeWalkIn 刷怪→AllEnemiesDefeated 信号收尾。深海版"利维坦多波袭击"可直接换 pawnGroupKind 复用。

## 主题一：Odyssey 行星层选址机制（海底层任务的核心模板）

### QuestNode_Root_Asteroid.cs（NOTE，强相关）
- 小行星任务根节点。关键流程：`TryFindSiteTile` 先 `TileFinder.TryFindRandomPlayerTile(canBeSpace: true)` 拿玩家地块作原点，再 `Find.WorldGrid.TryGetFirstAdjacentLayerOfDef(origin, layerDef, out layer)` 拿到**相邻的目标行星层**，然后 `layer.FastTileFinder.Query(new TileQueryParams(origin, 1, 3)).TryRandomElement()` 在 1-3 格内选点。
- 生成 `SpaceMapParent`（worldObjectDef 由 XML 注入），随机选 mineable 设置 `preciousResource` 和名字（"AsteroidName" + 资源名），挂 45-60 天 WorldObjectTimeout，地图移除即成功。
- **类比**：把 layerDef 换成"海底行星层"、worldObjectDef 换成"海底矿脉/沉船"、mineables 换成海底矿物，整个节点就是海底版的资源点任务。这是全片最直接的可借用模板。

### QuestNode_Root_Site.cs（NOTE，强相关）
- 通用单 SitePart 遗址节点，字段即选址参数全集：`canBeSpace`、`requireSameOrAdjacentLayer`（默认 true，用 `planetLayer.DirectConnectionTo(origin.Layer)` 判断相邻）、`layerWhitelist/layerBlacklist`、`allowedBiomes`、`allowedLandmarks`、`maxHilliness`、`distanceFromColonyRange`。
- `TryGetLayer` 先验证原点所在层，不通过则遍历 `Find.WorldGrid.PlanetLayers` 找一个满足 Validator 的层；`TryFindSiteTile` 用 `layer.FastTileFinder.Query(query, allowedBiomes, allowedLandmarks, desperate)` 带"放宽版"二次查询。
- **类比**：XML 驱动即可生成海底遗址任务（把白名单指向海底层）。这是 Mod 兼容性最好的一点：不用写 C#，改 Def 即可。

### QuestNode_Root_Gravcore.cs 及 11 个子类（基类 NOTE，子类 PASS）
- 重力芯系列任务（AbandonedSettlement/AncientReactor/AncientStockpile/CrashedMechanoidPlatform/FrozenTerraformer/InsectLair/MechanoidRelay/Mechhive/OrbitalAncientPlatform/OrbitalMechanoidPlatform/Gravship_Wreckage）共用基类的 `TryFindSiteTile`：`canBeSpace: true` 找原点→`TryGetFirstAdjacentLayerOfDef` 找指定层→FastTileFinder 按生物群系白名单(`allowedBiomes`)+地标模式(Required/Forbidden，50% 概率)+可达性选格，失败再放宽距离。
- 子类本体都是"GenerateSite + SpawnWorldObject + 奖励 Gravcore/GravlitePanel + 进场信 + MapRemoved 结束"的样板。
- **类比**：这整套就是"在特殊图层上批量铺系列遗址任务"的工厂模式。海底 Mod 若要加"深渊遗址系列任务链"，照抄基类即可。子类文件本身无独立海洋价值（PASS）。

### QuestNode_RequirementsToAcceptPlanetLayer.cs（NOTE，强相关）
- 任务接受门槛：向 quest 加 `QuestPart_RequirementsToAcceptPlanetLayer`，参数 `canBeSpace`、`layerWhitelist`、`layerBlacklist`，取 slate 的 map。
- **类比**：现成的"此任务不发到海底殖民地/仅发到海底殖民地"开关，XML 可配。海洋 Mod 若要屏蔽某些陆基任务，应优先复用此节点而非 patch。

### 冲突点：地表/太空限定（NOTE × 3）
- `QuestNode_Root_MonolithMigration.cs`：`quest.AcceptanceRequirementNotSpace(map.Parent)`——Anomaly 主线明确拒收太空图；海底图若被视作非地表，同样被拒。
- `QuestPart_SpawnMonolith.cs`：`Find.RandomSurfacePlayerHomeMap`——巨石只刷在**地表**玩家家；海底-only 存档里该方法返回值行为未验证，可能直接 Fail（"Tried to spawn monolith but no map found"）。
- `QuestNode_Root_WandererJoin.cs`：`protected virtual bool CanBeSpace => false`；`QuestNode_Root_Mission.cs`：`public bool canBeSpace;`；`QuestNode_Root_PollutionDump/PollutionRaid/PollutionRetaliation.cs`：`QuestGen_Get.GetMap(mustBeInfestable: false, null, canBeSpace: true)`——原版到处用 canBeSpace 旗标做图层分流，海底层需要同等的 canBeOcean 语义（或者复用 isSpace 风格在 PlanetLayerDef 上加标记，未在本片中验证 PlanetLayerDef 的字段）。

## 主题二：黑暗与深海生物（借用）

### QuestNode_Root_UnnaturalDarkness.cs + QuestPart_DarkenMap.cs（NOTE × 2，强相关）
- 事件流程：先发 UnnaturalDarkness GameCondition（permanent）→ 初始阶段 0.5-0.75 天→ 主阶段开始时 `QuestPart_DarkenMap` 调 `mapParent.Map.gameConditionManager.SetTargetBrightness(0f)` 把全图亮度归零 → Skyfaller 砸下 1-3 个 Noctolith → `QuestPart_RandomWaves`(mtb 36h) + 一波 EdgeWalkInDarkness 的 Noctol 袭击 → 6-8 天后结束，DarkenMap.Cleanup 里 `SetTargetBrightness(1f)` 恢复并把 GameCondition 设为非永久 301 ticks 收尾。
- **类比**：深海"室外光照极低"不必新写光照系统——GameConditionManager 的目标亮度管道就是官方压光手段；恒暗海底可以把 target brightness 常驻低位（需与 GameCondition_UnnaturalDarkness 的实际亮度曲线配合，亮度如何叠加未在本片验证）。

### QuestPart_Noctoliths.cs（NOTE，强相关）
- 打爆/打伤 Noctolith 时刷 Noctol 援军。刷怪格校验 `ValidSpawnCell`：不越界、不迷雾、可站立、不在 Portal 区，且**严格模式要求 `map.glowGrid.GroundGlowAt(c) < 0.01f`（无光才刷）**；刷出用 `PawnGroundSpawner`（60-120 tick 钻地延迟，Noctolith_Destroyed 音效）。
- **类比**：两条都是海底直接可用的原语——"深海捕食者只在无光区出现"和"生物从海床钻出"（PawnGroundSpawner 与 PitBurrow 同为钻出式入场）。

### QuestPart_DistressCallAmbush.cs（NOTE，相关）
- DistressCall 任务（75% 概率）在 MapGenerated 后 2400-4800 tick 发信号，从地图上未迷雾的 `PitBurrow` 生成 Fleshbeast 波（点数按 AmbushPointsCurve）。
- **类比**：探索型遗址的"惊动巢穴"伏击模板；海底沉船探索可复用 PitBurrow 或自定义海床巢穴 ThingDef。

### QuestNode_Root_SightstealerArrival.cs（NOTE，相关）
- 多波袭击编排：低威胁(<400 点)直接一波 EdgeWalkInDistributed；高威胁：第一波→嚎叫音效(Sightstealer_DistantHowl)+预警信→0.2-1.5 天后第二波（带 shard 掉落）→50% 概率第三波。每波挂 `QuestPart_SightstealerWave`（LordJob_SightstealerAssault）并按 waveTag.AllEnemiesDefeated 信号收尾。
- **类比**：把 PawnGroupKindDefOf.Sightstealers 换成自定义水生 pawnGroupKind、嚎叫换成水下声呐音效，就是"深海兽群多波袭击"。代码无任何地表假设。

## 主题三：到达模式与空袭/空投（冲突与借用）

### QuestNode_Raid.cs（NOTE，相关）
- 任务袭击节点，包一层 `QuestPart_Incident`(IncidentDefOf.RaidEnemy)。关键点：`incidentParms.raidArrivalMode.walkIn` 为真时走 `walkInSpot`（边缘走进），否则走 `dropSpot`（空投）；`canTimeoutOrFlee` 依赖 `map.CanEverExit`。
- **类比**：海底图上的袭击者"边缘走进"意味着从海里游进来——需要水生 pawnKind 或自定义 ArrivalMode；空投(dropSpot)穿海是画面/逻辑冲突点。`map.CanEverExit` 在海底图上的取值会影响袭击者撤退行为（未验证，建议查 Map.CanEverExit 实现）。

### QuestNode_SpawnSkyfaller.cs（NOTE，冲突类）
- 一切"从天上下来"的统一入口（空投舱、NoctolithIncoming、ShipChunkIncoming、SpacedroneIncoming 都是 Skyfaller）。支持 tryLandInShipLandingZone（优先落船舰着陆区）。
- **类比**：海底殖民地接收任务奖励/袭击增援若仍走 Skyfaller，会出现"舱体从海面砸进海底"——机制可用（SkyfallerMaker 不查水体），视觉与设定需替换为潜艇/货运潜航器类 ThingDef。

### 其余到达相关（PASS，但记录了结论）
- `QuestNode_PawnsArrive.cs`：walkIn 时用 walkInSpot，否则默认 EdgeWalkIn——同 Raid 的边缘进入问题。
- `QuestNode_Root_MechanoidSignal.cs`：GravEngine 随 ShipChunkIncoming_SmallExplosion 天降，选格用 `DropCellFinder.IsGoodDropSpot`——空投选格逻辑，海底同样面临穿海问题。
- `QuestPart_AddShipJob_*`（8 个文件）：TransportShip 的任务调度（Arrive/FlyAway/Unload/Wait*），纯运输船脚手架，无海洋类比价值。

## 主题四：地标与生物群系过滤（借用）

### QuestNode_Root_DistressCall.cs（NOTE，相关）
- `AllowedLandmarks`（Odyssey 激活时）：Oasis、Lake、LakeWithIsland(s)、Pond、DryLake、ToxicLake、Wetland、HotSprings、CoastalIsland、Peninsula、Valley、Cavern、Chasm、Cliffs、Hollow、TerraformingScar、Dunes——**水体类地标是任务选址的一等过滤条件**；`TileFinder.TryFindNewSiteTile(..., allowedLandmarks, 0.5f 选中地标概率, canSelectComboLandmarks: true)`。
- **类比**：海底世界特征（海沟、热液口、沉船带）应实现为 LandmarkDef，任务选址管道直接兼容。`QuestNode_Root_AncientComplex.cs` 有一份几乎相同的地标清单（多 IceDunes/LavaFlow/AncientSmokeVent/AncientToxVent），PASS 但结论相同。

## 本片其余内容（PASS/SKIP 概览）

- **通用脚手架**（PASS）：信号/延迟/分支/变量节点（Signal、SignalActivable、Sequence、RandomNode、Set/Unset、SetAndRestore、Subtract、SplitRandomly、Prefix、SubScript、SlateDump、RuntimeLog、QuestUnique、ViolentQuestsAllowed 等约 60 个）——任务逻辑管道，无海洋类比。
- **文本/语法**（PASS）：ResolveQuestName/Description/TextNow/TextRequests、TextRules、SlateRef、SlateRefUtility、Slate、QuestTextRequest、StageSignals、TextRulesTarget、SlateNames——Slate 是任务变量字典（前缀作用域机制），纯基础设施。
- **Royalty/Ideology/Biotech 任务**（PASS）：BestowingCeremony、RequirementsToAccept*(Bedroom/ColonistWithTitle/Research)、RequireRoyalFavorFromFaction、Hospitality_Refugee、RelicHunt、ReliquaryPilgrims、Hack_*、Mechanitor*、Sanguophage*、TradeRequest_*、Beggars 等——剧情任务，与海洋无机制交集。
- **Anomaly 主线其余部件**（PASS）：MetalHell（跳过场景）、MonolithTwisting/SetMonolithGleaming/StructureSpawned（VoidMetal 扩散特效）、EntityArrival（PitBurrow 波次，235 行读了类结构与常量）、VoidAwakening/VoidMonolith（204 行，读关键部分）——特效与单图事件，无海底直接对应。
- **纯数据类**（SKIP × 6）：ShipPawnGroup、ShipPawnOption、StageSignals、TextRulesTarget（枚举）、SlateNames（常量）、QuestTextRequest（3 字段数据类）。
