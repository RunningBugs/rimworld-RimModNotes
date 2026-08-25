# chunk-10 调研发现（RimWorld 组：Gene*/GenStep_*/Gravship*/Goodwill/Hediff/Gizmo 等 200 文件）

## TL;DR

最重要的发现（每条注明源文件）：

1. **海底地图的生成模板就是 Odyssey 的太空图**：`GenStep_Space.cs` 把全图填成 `TerrainDefOf.Space` 并关闭 `regionAndRoomUpdater`；`GenStep_Asteroid.cs` 演示了完整流程——噪声生成中央"孤岛"、主岛外全部回填 Space 并销毁物品、岩石/洞穴/矿脉/废墟/太空残骸全套。「全图填水 + 海山孤岛」可以直接照抄这条管线。
2. **原版水覆盖格（WaterCovered）是"白板图"**：`GenStep_ElevationFertility.cs:81` 水覆盖时海拔强制 0（全平）；`GenStep_RocksFromGrid.cs:50` 和 `GenStep_RockChunks.cs` 对 WaterCovered 直接 return——不生成岩石、不刷矿。海底 Mod 的岩石/矿脉必须自己补。
3. **整图温度覆盖有官方做法**：`GenStep_OrbitalPlatform.cs` / `GenStep_OrbitalSatellite.cs` 在 `PostMapInitialized` 里调 `MapGenUtility.SetMapRoomTemperature(map, layoutDef, SpawnTemp)`（太空默认 -75°C）——海底恒温 ~4°C 照此办理；同时两文件还设置 `map.FogOfWarColor`（可给海底定制迷雾颜色）。
4. **排水的直接 API 实证**：`GenStep_EscapeShip.cs` 的 ScatterAt 里对每格调 `CompTerrainPumpDry.AffectCell(map, cell)` 把逃生舱区域的水抽干——「排水阀/干燥海底」机制的现成调用方式。
5. **深海低光照的冲突点**：`GenStep_Power.cs:40` 在生成带电力设施的据点时 `map.skyManager.ForceSetCurSkyGlow(1f)` 强制满天空亮度——海底图若套用据点生成会被强制变亮，需拦截。
6. **"只有工业科技以上袭击"的现成过滤写法**：`GenStep_Turrets.cs` 选炮塔派系时用 `(int)x.def.techLevel >= 4`（Industrial=4）做过滤。
7. **潜艇类比的核心是引力船全套**：`GravshipUtility.cs`（TravelTo/ArriveNewMap/AbandonMap/SettleTile/GenerateGravship 把船体地形、屋顶、气体网格全部打包带走）+ `GravshipPlacementUtility.cs`（落地时恢复地形/屋顶/**gasGrid**——逐格环境状态随载具迁移的先例）+ `GenStep_ReserveGravshipArea.cs`（落点成本评分：Space 格 +5、水格 +60、dangerous +80）。
8. **游泳表现已有基础**：`GraphicStateDefOf.Swimming` 图形状态存在（配合 `JobDriver_GoSwimming`、`PawnWaterRippleMaker`——后两者在本片之外）。
9. **深海热泉生态模板**：`GenStep_GeothermalVents.cs` 地热口按距离衰减概率在周围散布特定植物——热泉生物群落的直接类比。
10. **TileMutator 是海底行为的挂载管线**：5 个 `GenStep_Mutator*.cs` 依次调用 `TileMutatorDef.Worker` 的 GeneratePostElevationFertility / GeneratePostTerrain / GenerateCriticalStructures / GenerateNonCriticalStructures / GeneratePostFog——给"海洋格"做一个 TileMutator 即可插入生成的各个阶段。

## 一、海底地图生成总模板（太空图家族）

### GenStep_Space.cs（22 行，全读）
- `Generate`：仅 Odyssey 激活时执行；`map.regionAndRoomUpdater.Enabled = false`，然后逐格 `SetTerrain(allCell, TerrainDefOf.Space)`。
- 类比：海洋版图 = 逐格填"深水地形"。注意它**关掉了 region/room 更新器**——全图无房间语义；海底若要保持"非气密=海水"的区域语义，可能反而需要开着（或自行处理）。

### GenStep_Asteroid.cs（335 行，全读）/ GenStep_BasicAsteroid.cs
- 完整的小行星地图生成：
  - `GenerateAsteroidElevation`：以图中心 DistFromPoint + Perlin 混合噪声生成岛形（`Radius=0.224`，BasicAsteroid 仅改为 0.2）。
  - `SpawnAsteroid`：海拔>0.5 铺 Vacstone 自然地形，>0.7 且非洞穴放 Vacstone 岩体并盖 `RoofRockThin`；然后从图中心 flood-fill 出主岛，**主岛之外全部回填 `TerrainDefOf.Space`、去屋顶、销毁所有 Thing**。
  - `SpawnOres`：按 `mineableCounts` 配置刷矿脉，若 `SpaceMapParent.preciousResource` 指定则刷指定矿（贵重资源点机制）。
  - `GenerateCaveElevation`：`MapGenCavesUtility.GenerateCaves` 生成洞内隧道。
  - 可选生成废墟（`LayoutDefOf.SpaceRuins`）与太初树（清出 23×23 区域、铺 RichSoil、放密封箱）。
  - 结尾 `map.OrbitalDebris = OrbitalDebrisDefOf.Asteroid`（轨道残骸氛围）。
- 类比：这就是"海山/海底高地"地图——中央可建设海床 + 四周无底深渊/开阔海水。岛形噪声参数、主岛 flood-fill 回填、洞穴生成、矿脉配置全部可借用。

### GenStep_OrbitalWreck.cs / GenStep_OrbitalMechhive.cs / GenStep_OrbitalRelay.cs
- 太空"平台图"家族：`Chunks` 分区撒平台地形（`TerrainDefOf.OrbitalPlatform`/`MechanoidPlatform`）、`SpaceGenUtility.DamageHoles` 在平台上打洞（露出 Space）、`SpaceGenUtility.GenerateRing/GenerateConnectedPlatform` 造环形/延伸平台；`map.FogOfWarColor = ColorLibrary.SpaceHumanFog/SpaceMechFog` 定制迷雾色。
- 类比：海底"沉船残骸带/钻井平台"地图；DamageHoles 的思路反过来用就是"气密地板破洞漏海水"。

### GenStep_FrozenTerraformer.cs / GenStep_FrozenRuins.cs
- 噪声（Perlin + 距中心衰减）铺 Ice 地形、放 SolidIce 岩体；`EncaseStructuresInIce` 把废墟房间周围 24 格包进冰里、房间补 RoofRockThin。
- 类比（弱）：被沉积物/珊瑚掩埋的海底遗迹可用同一套"噪声包裹废墟"手法。

### GenStep_RemoveTinyIslands.cs
- 两遍 flood-fill：先找接触图边的最大可通行区；然后把面积 < 主区 1/20（或不触边且 < 1/2）的小可通行岛用相邻不可通行地形吞掉。
- 类比：海底地形生成后的碎岛清理可直接借用此算法。

### GenStep_Terrain.cs
- 逐格 `MapGenUtility.GetNaturalTerrainAt`（生物群落 terrainPatchMakers）铺自然地形；地形 `supportsRock=false` 上的 edifice 会被销毁并触发屋顶坍塌检查。
- 类比：海床土壤/沙地分布由 Biome 的 terrainPatchMakers 决定——自定义海底 Biome 的入口。

## 二、温度 / 光照 / 迷雾 / 天气

### GenStep_OrbitalPlatform.cs:72,254 / GenStep_OrbitalSatellite.cs:63,108
- `protected virtual float SpawnTemp => temperature ?? (-75f)`；`PostMapInitialized` 调 `MapGenUtility.SetMapRoomTemperature(map, layoutDef, SpawnTemp)`。
- **结论：太空图整图 -75°C 是在地图初始化后用工具方法统一写入房间温度的**，不是持续模拟。海底"室外恒温 4°C"可以在同类时机写入；但要注意这是"一次性设定"，长期恒温还得看 MapTemperature 的室外温度逻辑（本片外）。

### GenStep_Power.cs:40
- 生成（据点）电力设施前 `map.skyManager.ForceSetCurSkyGlow(1f)`——**强制当前天空亮度为满**。
- 冲突：海底低光照场景下，任何包含 GenStep_Power 的据点/站点生成都会把天光拉满，Mod 需要覆盖或事后还原。

### GenStep_Snow.cs
- 按当前 Twelfth 前后三个月平均温 <0°C 的数量决定初始积雪比例；**Odyssey 下季节温度 <0 且 `FreezeManager.CanEverFreeze(cell, map)` 的水格会被设为 `TerrainDefOf.ThinIce`**（水面结冰）；屋顶下不落雪；`meltSnowRadius>0` 的地形（如热水管）周围除雪。
- 类比：海底恒温 4°C 时初始雪=0、不结冰，天然兼容；反过来"海面结冰层"可用 ThinIce 机制。

### GenStep_Fog.cs / GenStep_FogSpace.cs
- 开局 `fogGrid.SetAllFogged()`，从玩家起点（或图边可达格）`FloodFillerFog.FloodUnfog`；`GenStep_Fog` 找起点用 `TraverseMode.NoPassClosedDoorsOrWater`（水本身在寻路语义里是特殊通行类别）；FogSpace 则从四角和 `rootsToUnfog` unfog。
- 类比：深海"视野极差"可以借用全雾 + 仅驻地周围 unfog 的做法；另外水已有独立的 TraverseMode 档位，潜水/潜艇寻路有挂点。

## 三、水体处理 / 排水

### GenStep_EscapeShip.cs（80 行，全读）
- CanScatterAt 要求 Heavy 承载或 `terrain.driesTo` 后是 Heavy；**ScatterAt 对区域内每格调 `CompTerrainPumpDry.AffectCell(map, item)`（并向周围随机扩 8 格）把水抽干**再生成飞船。
- **这是潮湿泵（MoisturePump）地形干燥 API 在地图生成阶段的实证用法**——海底"排水阀把舱内海水排干"的 Mod 机制应优先研究 `CompTerrainPumpDry`（本体在本片外）。

### 各 Scatterer 的避水惯例
- 大量散布器显式避水：`GenStep_Outpost.cs`（矩形内水格 >10% 拒绝）、`GenStep_AncientAltar`/`GenStep_ArchonexusResearchBuildings`（`terrain.IsWater || terrain.IsRoad` 视为碰撞）、`GenStep_ScatterAncientLandingPad/Mechs/UtilityBuilding`（`allowInWaterBiome = false`，说明基类 `GenStep_Scatterer` 有 `allowInWaterBiome` 开关）、`GenStep_BaseRuins.AvoidWaterRoads`（默认 true，`GenStep_AncientComplex` 覆盖为 false）。
- 结论：原版据点/废墟系统整体避水；海底殖民地想让这些内容出现在水下，需要逐个改校验或换 affordance 要求（很多处要求 `TerrainAffordanceDefOf.Heavy`/`Medium`，水地形通常不满足）。

## 四、载具类比：引力船 → 潜艇

### GravshipUtility.cs（919 行，全读）
- `TravelTo/ArriveNewMap/ArriveExistingMap`：船作为 WorldObject 在世界格间旅行，到达时按 `LayerDef.DefaultWorldObject` 建 MapParent、`GetOrGenerateMapUtility.GetOrGenerateMap(..., GetGenSteps(gravship))` 生成地图并注入两个 GenStep（ReserveGravshipArea + GravshipMarker）。
- `GenerateGravship`：把子结构格上的地形/屋顶/雪/沙/**gasGrid** 全部清除打包进 Gravship 对象；`AbandonMap` 遗弃旧图。
- `PreLaunchConfirmation`：起飞前列出"将被留下的人员/动物/建筑/断开的子结构"——潜艇出航确认对话框的直接参照。
- 类比：潜艇/潜水钟载具可整套复用这个世界对象旅行 + 落图生成管线。

### GravshipPlacementUtility.cs（读关键部分）
- `PlaceGravshipInMap` 落地时依次恢复：地基（foundation 地形层）、地形、**气体网格 `map.gasGrid.SetDirect(cell, value)`**、区域/指令、Thing、Pawn、屋顶。
- **重点：逐格气体状态随船保存/恢复**——Odyssey 的真空的载体就是 gasGrid 一类的逐格网格。海洋 Mod 的"海水淹没格"状态可仿照此模式（自定义网格 + 随载具迁移）。
- `ClearArea`/`GetReplacementTerrain`：降落清场时把不可走/危险地形替换为 Biome 中等承载地形，`TerrainDefOf.Space` 不替换（返回 null）；`terrain.gravshipReplacementTerrain` 字段可指定替换地形。

### GenStep_ReserveGravshipArea.cs（全读）/ GenStep_GravshipMarker.cs / GravshipLandingMarker.cs
- `CostToPlaceAt` 落点评分：越界/禁建区=∞，UsedRects +100000，屋顶 +20，雾 +20，天然岩 +80/其他建筑 +40，**Space 地形 +5（全 Space 再加 10000），不可通行 +200，dangerous +80，水 +60**，缺 Light/Medium/Heavy 承载 +30。
- 类比：潜艇着海底/玩家选降落点的评分函数模板；也显示原版对"落在水上"是惩罚而非禁止。

## 五、袭击 / 威胁

### GenStep_Turrets.cs
- 炮塔派系选取：`!x.defeated && x.HostileTo(player) && !x.Hidden && (int)x.def.techLevel >= 4 && !x.temporary`——**techLevel>=4（Industrial）过滤的实例**，正合"只有工业科技以上的袭击"。

### GenStep_Ambush.cs（+_Edge/_Hidden 变体）
- 埋伏触发器三件套（RectTrigger + TriggerUnfogged + SignalAction_Ambush）；埋伏类型随机：Manhunters / Mechanoids（需点数够生成机械组）/ Normal（人族）。
- 类比：海底废墟探索的"惊动守卫"机制可直接套用；也可替换为水生猛兽群。

### GenStep_MechCluster / GenStep_SleepingMechanoids / GenStep_ConditionCauser
- 休眠机械群、天气控制器站点；ConditionCauser 是"全图游戏状态（含黑暗、毒尘等）"的生成入口——深海想做"海底火山灰/浑水"类全图 condition 时的生成侧参照。

## 六、生态（动物 / 植物）

### GenStep_Animals.cs
- 循环 `wildAnimalSpawner.SpawnRandomWildAnimalAt(loc)` 直到 `AnimalEcosystemFull`。
- 挂钩点：初始动物由 `WildAnimalSpawner`（本片外）按 Biome 决定——海底自定义 Biome 只配水生动物即可实现"只刷水生生物"；本片证实入口在 Biome/Spawner，不在 GenStep。

### GenStep_SpecialTrees.cs
- `if (map.Biome.isExtremeBiome) return;`——**极端生态直接跳过特殊树**；另有肥沃度、PsychologicallyOutdoors、距人造建筑等一串校验。
- 类比：海底 Biome 标记 isExtremeBiome 可自动屏蔽仙树/先驱树等地表内容。

### GenStep_GeothermalVents.cs
- 地热口周围按 `densityByDistance` 曲线（随距离衰减）概率散布 `floraToScatter` 植物，`CanEverPlantAt(..., checkMapTemperature: true)`。
- **深海热泉生态群落的直接模板**：热泉 + 周围管虫/硫细菌群落。

### GenStep_Plants.cs / GenStep_CaveHives.cs / GenStep_UndercaveInterest.cs
- UndercaveInterest（237 行全读）：地下洞穴随机 3-5 个兴趣点（蘑菇地、化合燃料发电机+燃料、虫巢+虫胶、尸体+装备、尸体堆、沉睡 fleshbeast）——**海底兴趣点（鲸落、沉箱、热泉矿）的散布模板**：选点（距边>5、距出口>10、互距>10）→ IrregularLump 清场 → 按类型生成。
- CaveHives：虫巢要求洞穴格（Caves>0）且远离派系基地 50 格——海底若要有"海怪巢穴"可参照。

## 七、TileMutator 挂载管线（重要）

`GenStep_MutatorPostElevationFertility` / `GenStep_MutatorPostTerrain` / `GenStep_MutatorCriticalStructures` / `GenStep_MutatorNonCriticalStructures` / `GenStep_MutatorFinal`：各 16 行，全是同一模式——遍历 `map.TileInfo.Mutators` 调 `mutator.Worker?.Generate*(map)`。

- **结论：TileMutatorDef.Worker 提供 5 个地图生成阶段钩子**，世界格被标记 mutator 后自动生效。海洋格做成 TileMutator（或自定义 Biome+Mutator 组合）是最省力的注入方式。另见 `GenStep_ScatterGeysers`（mutator 可乘 geyserCountFactor/chunkDensityFactor/junkDensityFactor 等密度系数）。

### GenStep_ScenParts.cs
- `Find.Scenario.GenerateIntoMap(map)`——场景（Scenario）自定义生成物的总入口；海洋开局场景（潜艇坠毁开局等）从这里挂。

## 八、其它值得一提

- **GraphicStateDefOf.cs**：`Swimming` GraphicState 已存在（游泳专用渲染状态；配套 `JobDriver_GoSwimming`、`PawnWaterRippleMaker` 在片外）——水中 pawn 的表现层有官方先例。
- **GenStep_FindPlayerStartSpot.cs**：玩家起点选最大连通开阔区中央、要求 Heavy 承载、无顶；`map.wasSpawnedViaGravShipLanding` 时跳过（船落点即起点）。
- **GenStep_Settlement.cs / GenStep_SettlementPawnsLoot.cs**：派系据点 BaseGen 管线（`symbolStack.Push("settlement")`），帝国据点强制王座室+停机坪——若海洋里要有"海底海盗前哨"需换符号或接受其陆地校验。
- **GenStep_Roads.cs**：按世界格道路连接生成图内道路，`tuning.costWater = 30` 寻路避水；海洋格无道路连接时整步跳过。
- **GenStep_GravshipWreckage.cs**：坠船点（弹坑 3-6、油渍/炸痕、船体碎片、GravEngine、尸体、机械守卫 LordJob_DefendPoint）——"沉船宝藏点"的地表版参照。
- **GenHostility.cs / GenLeaving.cs / GenLocalDate.cs / GenWorld.cs / GlobalControls.cs**：读过的通用系统（敌对判定、摧毁掉落、按经度的本地日期、世界图取格、右上角 UI），与海洋无直接类比点，PASS。

## 本片覆盖家族说明

本片 200 个文件中约 105 个是 `GenStep_*`（地图生成步骤），其余为 Gene 基因系统（11）、Gravship 引力船（8）、Goodwill 好感（8）、Hediff 健康（14）、Gizmo/UI（10）、History 记录（6）及散落工具类。基因/健康/UI/好感家族经通读与海洋主题无类比点；价值集中在 GenStep 的太空图/水处理/温度/生态部分与 Gravship 的旅行-落地管线。
