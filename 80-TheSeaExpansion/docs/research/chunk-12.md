# chunk-12 调研发现（RimWorld 组，字母段 IncidentWorker_W* ~ JobDriver_De*）

## TL;DR

- **钓鱼系统完整存在且是海洋直接资源**：`ITab_Fishing.cs` 证实 1.6 有 `Zone_Fishing` + `WaterBody` 鱼群模拟（种群数量/最大种群/常见与稀有鱼列表/最低种群百分比阈值），海底殖民地可直接把周边水域划为钓鱼区当食物来源。
- **虫害事件在海底地图上永远不会触发**：`InfestationCellFinder.cs` 的刷巢评分硬性要求厚岩顶（`isThickRoof`）+ 山体得分 ≥ 0.17，海底图无山即无虫害——既是好消息（不用防）也是设计缺口（需要"海底版巢穴事件"替代）。
- **"走进来"类事件普遍没有环境致死检查**：`IncidentWorker_WandererJoin.cs` 只检查边缘格可达性（`CanReachColony`），流浪者会径直走进海水淹死；`IncidentWorker_WildManWandersIn.cs` 有温度/有毒尘埃门控可抄作模板，海底同类事件必须补"海水致死"检查。
- **MapPortal 是现成的"副本入口/潜艇码头"模式**：`InsectLairEntrance.cs`（进入巨型虫巢副本图）+ `ITab_ContentsMapPortal.cs`（portal 装载清单 UI + `JobDriver_HaulToPortal` 搬运 job）整套可借用为沉船/海沟探索入口。
- **间歇蒸汽喷射器是"海底热液喷口"的现成模板**：`IntermittentSteamSprayer.cs` 间歇喷粒子 + `GenTemperature.PushHeat(40f)` 局部加热，可在恒温 4°C 海底做热泉 POI。
- **雪/沙网格有专门的清除工作**：`JobDriver_ClearSnowAndSand.cs` 操作 `map.snowGrid`/`sandGrid` 深度，海底图天然有 sandGrid，"清理淤泥/沉积物"工作可照抄。

## 海洋直接相关

### 钓鱼区 UI（ITab_Fishing.cs）
`Zone_Fishing` 的检查面板。关键事实（读到的，非猜的）：
- 钓鱼区绑定 `WaterBody`：`zone.Cells[0].GetWaterBody(zone.Map)`。
- `WaterBody` 有 `Population`/`MaxPopulation`（鱼群数量模拟）、`CommonFishIncludingExtras` 与 `UncommonFish`（鱼种 ThingDef 列表，直接列在 UI 里）。
- 区设置：`FishRepeatMode`（重复次数/目标数量/永久）、`targetPopulationPct` 滑条（最低种群百分比，下限 `10f / MaxPopulation`）、`pauseWhenSatisfied`/`unpauseAtCount`。
- 鱼类种群文本走 `FishingUtility.FishPopulationLabel`。
- 海洋价值：海底殖民地捕鱼经济可直接复用；需要确认 `Zone_Fishing` 的可划区域地形条件（Zone_Fishing 类不在本片，未验证）以及海底地形是否算水域。

### 清雪/清沙 Job（JobDriver_ClearSnowAndSand.cs）
- 工作量 = `50f * (snowGrid.GetDepth + sandGrid.GetDepth)`，完成后把两网格深度清零；按雪/沙哪个深选 `EffecterDefOf.ClearSnow/ClearSand` 特效。
- `JobDriver_AffectFloor`（本片）与 `JobDriver_ConstructFinishFrame`（本片）在铺地板/建造地形时也会顺手把 snowGrid/sandGrid 清零（`clearSnow` 分支）。
- 海洋价值：海底图的 sandGrid 天然存在，"清淤/平整海床"工作、建造时自动清沉积物都有现成模式。

## 事件/袭击门控（冲突与借用）

### 虫害刷点（InfestationCellFinder.cs、InfestationUtility.cs）
`InfestationCellFinder.GetScoreAt` 硬性条件（任一不满足得 0 分）：
- 距殖民地建筑 ≤ 30 格；格可行走、未迷雾、无阻挡物；
- **必须有顶且是厚顶**（`cell.GetRoof(map).isThickRoof`）；
- 封闭区域大小 ≥ 2；**温度 ≥ -17°C**（`MinCellTempToSpawnHive`）；
- **山体得分 ≥ 0.17**（周围自然岩 `isNaturalRock` 每格 +1、厚顶每格 +0.5，采样 700 格半径模式每 10 取 1）。
- 加分项：距无顶区远（直线距离×区域 Dijkstra 距离，^1.55）、**光照低**（`Lerp(1, 0.18, GroundGlowAt)`，越暗越好）、温度在 -17~-7°C 之间越高越好。
- 总分 < 7.5 不刷。`InfestationUtility.SpawnTunnels/SpawnJellyTunnels` 是生成入口（TunnelHiveSpawner / TunnelJellySpawner），fallback 模式可放宽到"附近 3 格内有厚顶"甚至忽略顶。
- 海洋价值（冲突类）：海底图没有自然山/厚岩顶 → 常规虫害事件永远刷不出；海底恒温 4°C 满足温度下限，不是屏蔽因素。若要"海底巢穴袭击"，需要自定义刷点逻辑（例如以"海床洞穴/沉船"替代厚顶条件）；若不要，则无需任何屏蔽补丁。

### 虫茧污染事件（IncidentWorker_WastepackInfestation.cs）
- 需 Biotech、`Faction.OfInsects` 非空；全局概率乘难度系数 `wastepackInfestationChanceFactor`；刷点走 `CocoonInfestationUtility.CanSpawnCocoonAt`（该工具类不在本片，未验证其地形条件）。
- 海洋价值：若海底不产污染（wastepack 体系），此事件无由触发；确认 `CocoonInfestationUtility` 的条件需要别的分片。

### 流浪者/野人走进来（IncidentWorker_WandererJoin.cs、IncidentWorker_WildManWandersIn.cs）
- WandererJoin：入口格只要求 `map.reachability.CanReachColony(c) && !c.Fogged(map)`——**没有任何环境/致死检查**。海底直接后果：流浪者在海水（致死环境）里生成并死亡。必须补丁或禁用。
- WildManWandersIn：**有环境门控模板可抄**——ToxicFallout 或 NoxiousHaze 活跃时不刷；`map.mapTemperature.SeasonAcceptableFor(ThingDefOf.Human)` 为 false 不刷；还要存在非玩家人类派系。
- 注：`SeasonAcceptableFor` 的实现在 MapTemperature（本片外，未验证具体温度窗口）。若它以人类舒适温度判断，海底恒温 4°C 可能让该事件天然不触发——需要在 MapTemperature 所在分片确认。
- IncidentWorker_WanderersSkylanterns（天灯访客）只是 VisitorGroup 加礼物，无环境检查，同需处理。

## 可借用的机制模板

### MapPortal 副本入口（InsectLairEntrance.cs、ITab_ContentsMapPortal.cs）
- `InsectLairEntrance : MapPortal`：首次进入发信件（含 Gravcore 提示变体）；响应 "Sealed" comp 信号播放填坑特效。`MapPortal` 基类在别的文件（未验证细节）。
- `ITab_ContentsMapPortal`：portal 装载 UI——`leftToLoad`（TransferableOneWay 清单）、`LoadInProgress` 时才可见、移除条目会结束对应的 `JobDriver_HaulToPortal` 搬运 job、把 pawn 从 `LordJob_LoadAndEnterTransporters` 移除。
- 海洋价值：沉船/海沟/热泉区副本入口、基地间的"潜水舱门"都可照此做：一张图上的建筑 → 进入另一张图，带装载流程。

### 间歇蒸汽喷射器（IntermittentSteamSprayer.cs）
- 独立工具类（被建筑持有）：间隔 500~2000 tick 随机喷一次，每次持续 200~500 tick；喷时按 `SprayThickness`(0.6) 概率抛 `FleckMaker.ThrowAirPuffUp`，每 20 tick `GenTemperature.PushHeat(parent, 40f)` 加热；有开始/结束回调。
- 海洋价值：海底热液喷口 = 自然热源 + 视觉点缀，在恒温 4°C 海底可做"围绕热泉建站"的 POI；类与具体建筑解耦，直接可实例化复用。

### 昆虫伏击（InsectAmbush.cs）
- 遗迹里的伏击触发器：每 60 tick 扫半径 5~8 格内有无玩家殖民者且有视线；触发后按 `AmbushThreatPointsCurve`（100→100、1000→300、5000→1500 点）评估当前威胁点数，刷 `Faction.OfInsects` 战斗组，用 `PawnGroundSpawner`（240~480 tick 钻地延迟）破土而出，发大威胁信件并建 AssaultColony lord。
- 海洋价值：把 PawnKind 换成水生生物、spawner 特效换成"破沙/翻泥"，就是现成的"海底沉船探索伏击"模板。

### 灭火 Job 无环境检查（JobDriver_BeatFire.cs）
- 拍火 job 只做寻路 + `pawn.natives.TryBeatFire`，不判断环境。水下"火不应存在"必须在 Fire 生成/蔓延层（Verse 命名空间，本片外）处理，本片无新信息，仅确认 job 层不用改。

### 血族死亡眠的日照检查（JobDriver_Deathrest.cs）
- 去床前 fail 条件遍历床位 `item.InSunlight(Bed.Map)`，晒到太阳就拒绝并提示。深海低光照下血族死亡眠不受限——对海底玩法是个小利好；同时证明 `InSunlight` 是现成的"该格是否被阳光直射"API，海底mod若做"光照不足"debuff 可反用此 API。

## 本片覆盖的其余家族（均无海洋价值）

- **Instruction_\*（23 个）**：新手教程课程（建房、划区、采矿、打倒袭击者等），仅 TutorialMode 用。
- **InteractionWorker_\*（19 个）+ Interaction 基础设施**：社交互动（闲聊/求婚/招募/劝降/越狱煽动等），纯社交数值，与环境无关。
- **ITab_\*（约 40 个）**：检查面板 UI（角色/健康/需求/囚犯/栏圈/存储/基因/研究笔记等），纯界面。
- **JobDriver_\* 其余（约 35 个）**：搬运、仪式、育儿、基因、机械、拆除等具体工作，无环境交互。
- **I\* 接口 / \*DefOf / 枚举 / 纯数据类（约 43 个）**：定义文件。
