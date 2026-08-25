# chunk-17 调研发现（RimWorld 组，Misc* ~ Pawn_ConnectionsTracker）

本片 200 个文件，主体是：Need 家族、PawnColumnWorker 表格列 UI 家族（约 55 个）、Page_* 开局向导页面、Music* 音乐系统、Monolith/Obelisk/Nociosphere 等 Anomaly 实体辅助类。大部分与海洋无关；**最有价值的发现集中在 Odyssey 真空机制在「Pawn 生成 / 逐格环境判定」层面的落点**。

## TL;DR

1. **`PawnApparelGenerator.cs` 是「潜水服自动生成」的完整模板**：Odyssey 已为太空袭击者实现了"按地图环境免费补发防护装备"——`NeedVacuumResistance()` 判定 `tile.Tile.PrimaryBiome.inVacuum && pawn.ConcernedByVacuum`，然后 `AddFreeVacuumResistanceAsNeeded()` 循环补发带 `VacuumResistance` 的衣物（潜水服=真空服的直接类比）。海洋 Mod 的"袭击者穿潜水服"可完全照搬此模式。
2. **`PawnBreathMoteMaker.cs` 暴露了逐格真空判定 API**：`pawn.Position.GetVacuum(pawn.MapHeld) > 0f` 时会**抑制**呼吸白雾——证明 Odyssey 的真空是逐格（cell-level）数值而非纯地图级开关，且原版系统会逐格查询它。海洋的"逐格海水深度/淹没"应有同形 API。
3. **`Need_Indoors.cs` / `Need_Outdoors.cs` 是海底殖民地的心理冲突点**：两者都用 `Position.UsesOutdoorTemperature(map)` + 屋顶厚度判定室内/室外。海底殖民地若全图被视为"室外环境"，居民会永久困在室内 → `Need_Outdoors` 持续掉到 Entombed（幽闭），需要处理。
4. **`NoxiousHazeUtility.cs` 是"环境危害暴露判定"的范本**：物品看是否无屋顶、Pawn 看屋顶+`Room.PsychologicallyOutdoors`——这套"有顶即防护"的判定形状可直接套用到"海水是否涌入该格"。
5. **`NegativeFishingOutcomeDef.cs` 证实 Odyssey 已有钓鱼系统**，且有"负面钓鱼结果"Def（伤害类型+伤害范围+附加 Hediff），海洋 Mod 的捕鱼玩法可以直接扩展这套 Def。
6. **`MixedBiomeMapComponent.cs` 提供逐格双 biome 机制**（Odyssey）：`biomeGrid` 布尔数组 + `secondaryBiome`，`GetBiomeAt(cell)` 按格返回主/副 biome。可用于"海底热泉区/海沟"等图内混合生态。
7. **澄清一个命名陷阱**：`MoteBubble` / `MoteMaker` 与水下气泡无关——它们是头顶对话/思想气泡（`Mote_Speech`、`Mote_ThoughtBad` 等）。

---

## 一、真空机制在 Pawn 生成侧的落点（强相关，直接模板）

### PawnApparelGenerator.cs（1212 行，已通读）
- 内部类 `PossibleApparelSet` 管一套待生成衣物，含 `TotalInsulationCold`、`SatisfiesNeededWarmth()` 等。
- Odyssey 新增常量：`MinVacuumResistanceForFreeApparel = 0.9f`、`MinVacuumResistanceImprovement = 0.15f`、`DesiredVacuumResistance = 0.9f`；`freeVacuumResistanceApparelMaxPrice` 由 `Apparel_Vacsuit + Apparel_VacsuitHelmet` 市价 ×1.3 得出（仅 Odyssey 激活时）。
- `NeedVacuumResistance(pawn, request)`（L1103-1123）：`ModsConfig.OdysseyActive` → 取 `request.Tile`（无效则取玩家主家乡图 Tile）→ **`tile.Tile.PrimaryBiome.inVacuum`** 为真且 **`pawn.ConcernedByVacuum`** 为真才需要真空防护。→ **biome 级 `inVacuum` 布尔是驱动 AI 装备生成的开关；海洋 biome 若设自己的"溺水环境"标志，袭击者生成就会像真空图一样自动补装备**。
- `AddFreeVacuumResistanceAsNeeded(pawn)`（L349-442）：最多 10 轮，按 `Commonality/(Price²)` 加权随机挑带 `VacuumResistance ≥ 0.1`、`canBeGeneratedToSatisfyVacuumResistance` 的衣物，替换掉不兼容件，直到覆盖所有 `canBeVacuumBurnt` 且未被皮肤覆盖的外部身体部位（`GetUnprotectedVacuumParts()`，L464-506）。
- 类比结论：海洋 Mod 只需给潜水服类衣物加自己的抗性 stat + 在类似钩子（或 Harmony patch `NeedVacuumResistance`）里判定海洋 biome，袭击者/访客就会穿潜水服来。同文件里污染（`ToxicEnvironmentResistance`）是第二份同款范本（Biotech）。

### PawnBreathMoteMaker.cs（49 行，已通读）
- 寒冷呼吸白雾：仅 Humanlike、非机械、非 Shambler，且 **`!(ModsConfig.OdysseyActive && pawn.Position.GetVacuum(pawn.MapHeld) > 0f)`** —— 真空格里不喷白雾。
- 触发还要求 `pawn.AmbientTemperature < 0f`（`MaxBreathTemperature = 0f`）。
- 海洋结论两点：a) `GetVacuum(IntVec3, Map)` 是逐格 API，证明真空是格级数据（类比：海洋需要 `GetWaterDepth(cell, map)` 同形物）；b) 海底恒温 4°C > 0°C，呼吸白雾在海底永远不会出现（ trivia，无需处理）。

### PawnCapacityWorker_Breathing.cs（25 行，已通读）
- Breathing 能力 = `BreathingSource`(肺) × `BreathingPathway`(气管) × `BreathingSourceCage`(胸廓) 三个 body part tag 效率连乘。
- 类比：溺水/减压病 Hediff 只要削 Breathing 相关部件效率即可走原版能力管线；`PawnCapacityWorker_Moving`/`_Consciousness` 也会按 Breathing 插值（各 20% 权重），溺水会自然传导到移动和意识。不需要新机制，加 Hediff 即可。

## 二、室内/室外心理需求与"暴露判定"（冲突 + 借用）

### Need_Indoors.cs / Need_Outdoors.cs（均全文通读）
- 两者核心判定相同：`bool outdoor = !pawn.Spawned || pawn.Position.UsesOutdoorTemperature(pawn.Map)`，再看 `GetRoof()` 是否 `isThickRoof`。
- `Need_Outdoors`：长期不在"真室外"会持续下降 → CabinFeverLight/Severe → Trapped → Entombed；睡床只减缓（×0.2）。**厚屋顶下反而 -0.45/interval，海底基地全是人工厚顶 => 全体幽闭**。这是海底殖民地必须面对的平衡问题（原版真空图同样有此问题，Odyssey 未见在本文件内特判）。
- `Need_Indoors` 反向：在室内持续涨满。海底殖民者 Indoors 永远满、Outdoors 永远掉——只刷一半。
- `Need.cs` 基类 `IsFrozen`（已读）：Suspended / freezeWhileSleeping / 精神崩溃 / dormant / 不可交互时冻结，与真空无关——Odyssey 没有在这里为真空特判需求冻结。

### NoxiousHazeUtility.cs（54 行，已通读）
- `IsExposedToNoxiousHaze(thing, cell, map)`：GameCondition 激活且未被其它 condition 遮蔽 → 物品类只看 `!cell.Roofed(map)`；Pawn/建筑看屋顶，有顶时再查 `cell.GetRoom(map).PsychologicallyOutdoors`；`pawn.kindDef.immuneToGameConditionEffects` 可免疫。
- 类比：**"海水危害是否作用于该格"的判定可直接套这个三段式**（无顶=暴露；有顶=看房间密闭性；kindDef 级免疫标志=水生生物免疫海水）。`TryGetNoxiousHazeMTB` 还示范了按世界污染分数算 MTB 的条件触发。

### ObeliskActivityWorker.cs / PawnActivityWorker.cs（均已读）
- Anomaly 方尖碑/实体的活跃度：`thing.IsOutside() && comp.Props.changePerDayOutside.HasValue` 时额外增长，UI 文案 key 是 **`NotInSealedRoom`**（"不在密封房间"）。
- 类比：原版已有"密封房间抑制某效果"的完整 comp 模式（CompActivity），海洋的"该设备在海水环境中失效/腐蚀"可用同款 `changePerDayOutside` 思路（字段名里 Outside 实际是 IsOutside() = UsesOutdoorTemperature 语义）。

## 三、地图/biome/世界层结构（借用）

### MixedBiomeMapComponent.cs（46 行，已通读）
- MapComponent，`bool[] biomeGrid`（每格一个 bit）+ `BiomeDef secondaryBiome`；`GetBiomeAt(cell)`：Odyssey 未激活或格不在 grid 内 → `map.Biome`，否则 secondary。`ExposeData` 存档。
- 类比：海底图内"热泉区/正常海床"混合生态、或"浅滩-深海"过渡的现成机制；只要各系统查询 biome 时走 `GetBiomeAt` 而非 `map.Biome` 就生效（哪些系统走了它需查调用方，本片未覆盖——**未验证**）。

### OrbitalTransition.cs（26 行，已通读）
- 音乐切换器，展示官方判断"当前在太空图"的写法：遍历 `Find.Maps`，任一 `map.Tile.LayerDef.isSpace` 即真。
- 类比：**`Tile.LayerDef.isSpace` 是 Odyssey 给 PlanetLayer 打的环境标志**，海洋若新增"海面/海底"层或只想检测，可找 `PlanetLayerDef` 上的同类标志位；音乐系统也可照 `MusicTransition` 子类加"深海氛围音"。

### OrbitLayer.cs（31 行，已通读）
- `PlanetLayer` 子类，仅覆写 `CanSelectLayer()`：层上没有任何 WorldObject 时拒绝选中（"CannotSelectOrbitReason"）。
- 类比：如果海洋 Mod 不加新层而是复用主层，此文件只说明 PlanetLayer 可子类化并控制可选性——备用知识。

### MTBByBiome.cs（22 行，已通读）
- 极小数据类：`BiomeDef biome + float mtbDays`，自定义 XML 解析（节点名=biome Def 交叉引用，值=mtb）。
- 类比：IncidentDef 等处用 `List<MTBByBiome>` 按 biome 调事件频率——海洋 biome 的事件频率调参直接填 XML 即可，零代码。

## 四、钓鱼（直接相关）

### NegativeFishingOutcomeDef.cs（24 行，已通读）
- Odyssey 已有钓鱼系统；本 Def 描述**负面钓鱼结果**：`fishType`（咬钩的鱼）、`letterLabel/letterText/letterDef`（信件）、`damageDef + damageAmountRange`（对钓鱼者造成伤害）、`addsHediff + hediffSeverity`（附加 Hediff，例如被鱼咬伤）。
- 类比：海洋 Mod 的"钓到危险深海生物"只需新增此 Def 的 XML；钓鱼主逻辑（Job/WorkGiver）在别的文件，本片未覆盖——**未验证**。

## 五、事件/袭击/世界交互类比（中等价值）

### PassingShip.cs / PassingShipManager.cs（均全文通读）
- 过路商船：Map 级管理器持有 `List<PassingShip>`，每艘 `ticksUntilDeparture = 40000` 倒计时离开；通讯走 `Building_CommsConsole`（`CommFloatMenuOption` 要求有通电的 `OrbitalTradeBeacon` 才能交易），离开后若殖民地有通讯台则弹"离开通讯范围"消息。
- 类比：**"海面船只经过海底殖民地头顶"** 的完美模板——纯通讯交互、不落地、限时离开，全程不需要船真的在图上出现。海底贸易/求救信号可直接复用这套 manager。

### OrbitalDebrisSpawner.cs（70 行，已通读）
- 一个 Thing：延迟 3-12 秒后启动，5 秒内随机节奏（`Rand.DynamicChance`）砸下 12-16 颗 `MeteoriteCraterIncoming`（SkyfallerMaker），落点用 `CellFinderLoose.TryFindSkyfallerCell`（不允许屋顶格/物品/建筑）。
- 类比：**"海面沉落物/坠物事件"**（货柜落水、沉船碎片砸到海床基地外围）直接换 Skyfaller def 即可。

### MoveColonyUtility.cs（410 行，已通读）
- Archonexus 搬迁殖民地的完整流程；选址入口 `PickNewColonyTile` 用 **`TileFinder.IsValidTileForNewSettlement(tile, reason)`** 校验——**海洋格能否建殖民地/搬迁，闸门在这个 TileFinder 方法里**（不在本片，属 Verse/Planet 侧，需其它分片确认）。新定居点的 WorldObjectDef 取 `tile.LayerDef.SettlementWorldObjectDef`（L162），再次印证 LayerDef 是环境行为的数据源。

### Page_SelectStartingSite.cs（284 行，读头部）
- 开局选点 Page（world 地图上点格），标题已有备用 key `"SelectLandingSite"`（Odyssey 着陆语义）。具体哪些格可选同样取决于 TileFinder 校验——**细节未验证**。

## 六、覆盖层与表现层（弱相关，备查）

- **OverlayDrawer.cs / OverlayTypes.cs / OverlayDrawHandler.cs**（均已读）：Thing 头顶状态图标系统。`OverlayTypes` 是 `[Flags]` 枚举，位已用到 0x400（ForbiddenAtomizer）——**mod 无法安全地往枚举里加新位**（枚举值硬编码在 DrawAllOverlays 的 switch/位运算里），海洋的"舱体进水/失压"警示图标需要自绘或 patch `DrawAllOverlays`。绘制走 `DrawBatch` 实例化 + 脉冲透明度，可作性能范本。
- **MoteBubble.cs / MoteMaker.cs**（均已读/查）：**命名陷阱**——"Bubble"是思想/对话气泡（Mote_Speech、Mote_ThoughtGood/Bad、Mote_ForceJob），与水下气泡无关。水下气泡效果需走 FleckMaker（Verse 侧，本片未覆盖）。
- **MoteProgressBar.cs**：附着在 Thing 上的进度条 mote（排水阀排水进度可用，`MoteProgressBarAlwaysVisible` 变体不缩放隐藏）——弱相关，备查。

## 七、明确无价值家族（PASS/SKIP 说明）

- **Need 家族其余**（Food/Rest/Joy/Mood/Comfort/Beauty/Chemical/Authority/Deathrest/KillThirst/Learning/MechEnergy/Play/RoomSize/Sadism/Seeker/Suppression 等）：纯数值需求，与环境无关。
- **PawnColumnWorker_\*（54 个）+ PawnColumnDef(Generator)**：Animals/Work 等表格的列 UI，纯界面。
- **Page_\*（13 个）**：开局向导/MOD 配置界面（Page_ModsConfig 1303 行也是纯 UI）。
- **Music\***：音乐播放/序列/切换数据与状态机（OrbitalTransition 除外，见上）。
- **Monolith\*/Obelisk\*/Nociosphere\*/Mutant\*/NuzzleUtility**：Anomaly DLC 实体辅助与变异系统（ObeliskActivityWorker 的密封房间语义除外，见上）。
- **Name\*/PawnBio\*/PawnAddictionHediffsGenerator/PawnCacheRenderer 等**：命名、背景故事、成瘾生成、头像渲染，无关。
- **MonumentMarker.cs / MonumentMarkerUtility.cs**（741 行已通读）：Royalty 纪念碑任务（Sketch 蓝图核对），机制完整但与海洋无任何类比点。
- **MiscDebugDrawer / ModList / NativeVerbPropertiesDatabase / PassAllQuestPartUtility / OutpostSitePartUtility 等**：调试/序列化/小工具。
