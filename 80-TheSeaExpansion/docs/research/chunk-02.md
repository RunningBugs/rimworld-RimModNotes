# chunk-02 调研发现（RimWorld 命名空间，A~Building_Turret，共 200 文件）

## TL;DR

1. **`BiomeDef` 自带海洋扩展需要的几乎全部开关**（`BiomeDef.cs`）：`isWaterBiome`、`inVacuum`、`disableSkyLighting`、`disableShadows`、`constantOutdoorTemperature`（可空 float，恒定室外温度）、`biomeMapConditions`、`canBuildBase`、`canAutoChoose`、`impassable`、`onlyAllowWhitelistedArrivalModes`、`wildAnimalsCanWanderInto`、`fishTypes`/`maxFishPopulation`、全套水体地形字段。海底生态域**大概率可以纯 XML + 少量 patch 定义出来**。
2. **`BiomeConditionMapComponent`**（`BiomeConditionMapComponent.cs`）：地图生成时把 `biomeMapConditions` 注册为**永久 GameCondition**——深海永久黑暗/恒温的官方挂钩点，零代码可用。
3. **`Building_LifeSupportUnit`**（`Building_LifeSupportUnit.cs`）：Odyssey 的生命维持装置=「真空泵」实体模板——房间气密且处于真空生态域时按房间面积抽低 `Room.Vacuum`，同时兼任暖气（20°C/30 能量每秒）。**「排水阀/供氧机」的直接克隆对象**。
4. **气密性是 def 级开关**（`BuildingProperties.cs`）：`isAirtight`、`isStuffableAirtight`、`alwaysExchangeVacuum`、`canExchangeVacuum` 都是 XML 字段；`Building_Door.ExchangeVacuum`（`Building_Door.cs:59`）实现了「门开着才交换真空」——**水密门/气闸的直接类比**。
5. **`Building_GravEngine.GetOrbitalWarnings()`**（`Building_GravEngine.cs:600`）：发射前检查全船房间 `VacuumUtility.IsRoomAirtight`，并按 250 格/暖气、400 格/氧泵的密度校验生命维持覆盖——可整套借用为海底基地「耐压/排水覆盖度」检查 UI 模式。
6. **温度三件套**（`Building_TempControl.cs`/`Building_Heater.cs`/`Building_Cooler.cs`）：房间温控全走 `GenTemperature.ControlTemperatureTempChange`；海底恒温 4°C 只需 `constantOutdoorTemperature=4`，暖气逻辑现成。
7. **冲突项**：`Bombardment`（轨道轰炸，`Bombardment.cs`）在海底主题上说不通，且会向无厚顶格点火——海底图应考虑屏蔽该类轨道事件。
8. **袭击破墙**（`BreachingUtility.cs`）：破墙 AI 以「挡住突破路径的玩家建筑」为目标——海底墙被打破=灌水，现有破墙机制天然制造「外壳被凿穿」的恐怖感，无需改动。

---

## 一、生态域定义（BiomeDef 家族）——本片最大收获

### BiomeDef.cs（546 行，全读）
`BiomeDef` 是 Def 类，以下字段全部可在 XML 里设置，逐条对应海洋需求：

| 字段 | 实际语义（读到的） | 海洋类比 |
|---|---|---|
| `inVacuum` | 真空生态域标记，被全代码库 ~20 处引用（`Building_LifeSupportUnit`、`CompPowerPlantWind`、`JobGiver_OptimizeApparel` 等） | 「海水致死环境」总开关；mod 要么复用 `inVacuum`（坐享全部现有致死/气密/服装逻辑），要么自建平行标记并 patch 这些点 |
| `constantOutdoorTemperature` (`float?`) | 非空时室外温度恒定（`MapGenUtility.cs:987` 读取） | **海底恒温 4°C 一行 XML 解决** |
| `disableSkyLighting` / `disableShadows` | 关闭天空光照/阴影 | **深海低光照一行 XML** |
| `biomeMapConditions` (`List<GameConditionDef>`) | 见下方 MapComponent | 永久黑暗/环境 condition |
| `isWaterBiome` | 水体生态域标记 | 直接可用 |
| `canBuildBase` / `canAutoChoose` / `impassable` / `canExitMap` / `onlyAllowWhitelistedArrivalModes` | 分别控制能否定居/能否被自动选中/世界图不可通行/能否出图/到达方式白名单 | 原版 Ocean 靠这组字段禁止定居；mod 的海底生态域要反向打开 `canBuildBase` |
| `wildAnimalsCanWanderInto` / `animalDensity` | 野生动物游荡/密度 | 「只刷水生生物」靠 `wildAnimals`/`coastalWildAnimals` 列表控制 |
| `coastalWildAnimals` | Odyssey 新增：沿海动物表，`ShouldSpawnAnimalOnCoast` 比较海岸/普通 commonality（`BiomeDef.cs:414`） | 水生动物刷新的现成管道 |
| `fishTypes` (`BiomeFishTypes`) / `maxFishPopulation` | 钓鱼鱼类表 | 海底捕鱼内容入口 |
| `waterShallowTerrain`/`waterDeepTerrain`/`oceanShallowTerrain`/`oceanDeepTerrain`/`waterMovingShallowTerrain`/`waterMovingChestDeepTerrain`/`mudTerrain` 等 | 生态域覆盖默认水体地形 | 海底地形替换 |
| `terrainsByFertility` + `TerrainForAffordance()` (`BiomeDef.cs:329`) | 按肥力/affordance 选地形，缺省回落 `TerrainDefOf.Soil` | 海底沉积物地形 |
| `geyserCountFactor` | 间歇泉数量系数 | 海底热泉数量 |
| `hasBedrock` / `noGravel` / `hasVirtualPlants` / `forageability` / `foragedFood` | 基岩/砾石/虚拟植物/采集 | 海底资源细节 |
| `noAmbientWind` | 无环境风 | 风力发电在海底本就无效（参考 `CompPowerPlantWind` 对 `inVacuum` 的处理） |
| `settleWarning` (`[MustTranslate]`) | 定居警告文本 | 「此处为深海……」警告 |
| `fogOfWarColor` / `orbitalDebris` | 战争迷雾颜色/轨道残骸 | 深海迷雾色调 |
| `layerWhitelist`/`layerBlacklist` | 星球层过滤 | 海底仍在地表，无需新层 |

注意 `DrawMaterial`（`BiomeDef.cs:190`）对 `Ocean`/`Lake` 特判用 `WorldOcean` 材质——新海底生态域在世界地图上的渲染会走 `WorldTerrain` 或 `WorldIce` 分支，可能需要 patch 或接受现有外观。

### BiomeConditionMapComponent.cs（23 行，全读）
`MapGenerated()` 里遍历 `map.Biome.biomeMapConditions`，用 `GameConditionMaker.MakeConditionPermanent` 注册为永久 condition。**这是「深海永久黑暗/非自然黑暗类效果」的官方注入点，纯 XML 驱动**。

### BiomeWorker.cs / BiomeWorker_Ocean.cs / BiomeWorker_Space.cs / BiomeWorker_SeaIce.cs
- `BiomeWorker.GetScore(biome, tile, planetTile)` 决定世界生成时生态域放置；`CanPlaceOnLayer` 默认按 `layerWhitelist/Blacklist` 过滤（`BiomeWorker.cs:10`）。
- `BiomeWorker_Ocean.GetScore`：非 `tile.WaterCovered` 返回 -100，否则 0——即 Ocean 铺满所有水覆格。
- `BiomeWorker_Space`：`CanPlaceOnLayer` 硬编码 `layer.Def == PlanetLayerDefOf.Orbit`，`GetScore` 恒 -100（只由轨道层逻辑放置）。
- 其余陆地 worker 全部以水覆返回 -100。
- **结论**：若新增「深海」生态域想在世界图特定海域出现（而非替换整个 Ocean），需自定义 `BiomeWorker` 子类在 `WaterCovered` 格给出比 Ocean 更高的正分（参考 `BiomeWorker_SeaIce` 用 Perlin 噪声限制分布区域，`BiomeWorker_SeaIce.cs:27` 的 `AllowedAt` 模式）。

### BiomeFishTypes.cs（16 行，全读）
纯数据：`freshwater_Common/saltwater_Common/freshwater_Uncommon/saltwater_Uncommon` 四个 `FishChance` 列表 + `rareCatchesSetMaker`（ThingSetMakerDef）。**海水的钓鱼鱼类表走 `saltwater_*`**，海底 mod 的渔获内容直接在这里挂。

### BiomeDefOf.cs
含 `Ocean`、`Lake`、`[MayRequireOdyssey] Space`、`Orbit`、`Glowforest`、`[MayRequireAnomaly] Undercave` 等引用——确认 Odyssey 的 Space/Orbit 与普通 Ocean 是三个不同生态域；`Undercave`（洞穴）的存在说明「无光地下」已有先例（未验证其 XML 里是否用 `disableSkyLighting`，超出本片范围）。

## 二、气密/真空 → 水密/海水（核心机制类比）

### Building_LifeSupportUnit.cs（52 行，全读）★强相关
Odyssey 生命维持装置，每个 TickRare：
- `ComputeTempChange()`：把所在房间温度向 20°C 调（30 能量/秒，效率随环境温度衰减，同暖气算法）；
- `ComputeVacuum()`（`Building_LifeSupportUnit.cs:44`）：**当且仅当 `Map.Biome.inVacuum && !Room.ExposedToSpace`**，按 `100/Room.CellCount × 0.05 × 4.167` 每 rare tick 降低 `Room.Vacuum`。
- 这就是「气密房间内抽真空/供氧」的完整实现：房间越大单台越慢。**海底 mod 的「排水泵/供氧机」可直接克隆此类**，把 `Room.Vacuum` 换成自定义的 `Room` 水位属性，或干脆复用真空系统本身。

### BuildingProperties.cs（681 行，读字段表）★强相关
`def.building` 的属性包，与气密相关的 XML 字段：
- `isAirtight`（`:154`）、`isStuffableAirtight`（`:156`）：墙/门的气密标记；
- `alwaysExchangeVacuum`（`:57`）、`canExchangeVacuum`（`:59`）：是否总是/可以交换真空（如栏杆）；
- 另有 `biomeSpecific`（`:148`，生态域限定建筑）、`canLandGravshipOn`、`naturalTerrain`、`leaveTerrain`、`allowAutoroof`、`roofCollapseDamageMultiplier` 等海洋可能有用的字段。
- **结论**：海底 mod 的「耐压墙/外壳」在 XML 层打 `isAirtight` 即可接入现有真空隔离系统；真正的气密判定算法在 `VacuumUtility`（Verse 侧，本片外，未验证细节）。

### Building_Door.cs（621 行，读前 200 行+关键属性）
- `ExchangeVacuum` 重写（`:59`）：基类不交换真空时，门**开着才交换真空，关着隔绝**——气闸/水密门的现成语义。
- 开门 45 tick、关门延迟 110 tick、`ContainmentBreached`（开门超 600 tick 算收容突破，`:149`）。
- **类比**：海底基地开门瞬间「海水涌入连通区」的时序直接由这套门 tick 逻辑承载；`Building_MultiTileDoor`/`Building_SupportedDoor`/`Building_HackableDoor`/`Building_JammedDoor` 都只是绘制/锁定变体，无语义扩展。

### Building_GravEngine.cs（663 行，读发射检查段）
`GetOrbitalWarnings()`（`:600`）遍历 `validSubstructure` 覆盖的房间，跳过 `UsesOutdoorTemperature` 和门厅，用 `VacuumUtility.IsRoomAirtight(room)` 判气密；统计暖气（`Building_Heater` 或 `heatPerSecond>20` 的 CompHeatPusher）与 `CompOxygenPusher` 数量，密度不足（<格子数/250、/400）就发红字警告。
- **借用价值**：这是「密封环境生命维持覆盖度自检」的完整 UI 模式，海底 mod 可仿造一个「耐压完整性/排水能力检查」。

## 三、温度 / 光照 / 农业

### Building_TempControl.cs / Building_Heater.cs / Building_Cooler.cs
- `Building_TempControl` 只是持有 `CompTempControl`+`CompPowerTrader` 的薄基类。
- `Building_Heater.TickRare`：环境温度 <20°C 时满效率，`GenTemperature.ControlTemperatureTempChange` 算增量后直接 `GetRoom().Temperature += num2`。
- `Building_Cooler.TickRare`：冷热两侧各取一格，向热侧 `GenTemperature.PushHeat`，效率随温差衰减（每 130°C 降满）。
- **结论**：海底恒温 4°C 下取暖全靠这套现成逻辑；冷机在海底无用武之地（环境温度永远低于目标），无需屏蔽但玩家也不会造。

### Building_SunLamp.cs（35 行，全读）
实体类只有一个「按光照半径建种植区」的 gizmo；光照本体在 CompGlower/ThingDef（本片外）。配合 `disableSkyLighting`，**深海农业=室内太阳灯+水培**，无需新机制。

### Building_PlantGrower.cs（123 行，全读）
水培种植箱：断电时每 TickRare 对箱上植物造成 1 点 `Rotting` 伤害（`:71`）；检查 `PlantUtility.GrowthSeasonNow` 显示季节提示。**海底断电=作物死**的惩罚已经内置；这也提示 mod 需要保证海底电力稳定性或提供替代农业。

### Building_SteamGeyser.cs / Building_GeothermalVent.cs
间歇泉：未被地热发电机压住时间歇喷蒸汽（`IntermittentSteamSprayer`），喷发时调用 `WeatherBuildupUtility.AddSnowRadial` 化雪（**海底无雪，此调用无害但无意义**）。`Building_GeothermalVent` 是空标记类。配合 `BiomeDef.geyserCountFactor`，**「海底热泉发电」可直接复用地热体系**——是海底最合理的基础电力来源（风力/太阳能均不可用）。

## 四、袭击 / 事件（冲突与借用）

### BreachingUtility.cs（407 行，读关键部分）
破墙 AI：`ShouldBreachBuilding`（`:97`）——可破坏的、属于玩家或无派系的、且 `BlocksBreaching`（挡住突破路径）的 Edifice 会成为目标；含采矿、远程破墙站位（`BreachRangedCastPositionFinder`）、护送半径等。
- **类比**：海底基地的耐压外壳就是破墙目标——袭击者凿穿外墙=海水涌入，「只有工业以上袭击」时破墙攻城锤/破墙斧正好是最威胁。机制零改动即有戏剧性。

### Bombardment.cs（237 行，全读）⚠冲突
轨道轰炸：从 z=60 高度落弹、`GenExplosion.DoExplosion` 轰炸，且每 20 tick 向随机格（无厚顶）点火（`StartRandomFire`，`:190`）。
- **冲突点**：海底世界里「从轨道落下的炮弹」穿透海水在主题上不成立；且海底室内火可以烧，但开阔「海水区」点火逻辑会出戏。建议 mod 在海底图屏蔽 `OrbitalStrike` 类 incident（具体 incident 过滤点在本片外，未验证）。

### 其他袭击相关（PASS，记一笔）
- `Building_Trap*`（陷阱家族）、`Building_Turret`（炮塔基类）、`Building_Battery`（火焰伤害 5% 几率点燃爆炸）——全部与环境无关，海底照用。
- `BossgroupWorker`/`BossgroupDef`：Boss 召唤战 worker，与海洋无交互。

## 五、本片覆盖的其余家族（均无海洋价值）

- **Bill 家族**（Bill.cs、Bill_Production、Bill_Medical、Bill_Mech、BillStack、BillUtility 等 18 个文件）：生产/医疗/机甲工单系统，纯工坊逻辑。
- **Book 家族**（BookOutcomeDoer/Properties/UI 12 个文件）：书籍阅读收益。
- **Baby/Backstory/Beard/Bed/Beauty/Body 家族**：婴儿玩耍、背景故事、外观、床位、美观度、体型——纯数据或 UI。
- **Building 杂项**：床、书架、储物架、培养舱、基因装配/提取、机甲充电/培育、收容平台、棺材/坟墓、通讯台、交易信标、穿梭机、发射舱、陷阱、王座、乐器、发酵桶（复用通用温度系统）、古代遗迹系列（AncientVent 会按 GameCondition 开关特效，算遗迹+环境联动的小先例）等——均为与环境无关的家具/设备逻辑。
- **Autotests_*、Autosaver、AutoUndrafter、AutoHomeAreaMaker、AvoidGridTuning、Bezier**：测试/系统/数学工具。
- **Blueprint 家族**（Blueprint.cs、Blueprint_Build、Blueprint_Door、Blueprint_Install、Blueprint_Storage*）：建造蓝图的样式/材料/安装逻辑，地形可否建造的判定在 `GenConstruct.CanPlaceBlueprintAt`（本片外，未验证）。

## 未验证 / 片外线索
- 真空的致死伤害、`VacuumUtility.IsRoomAirtight` 算法、`Room.Vacuum` 语义在 Verse 命名空间（本片外）。
- `inVacuum` 的全部消费点（本片 grep 到的有 `Ability.cs`、`FireUtility.cs`、`JobGiver_OptimizeApparel.cs`、`PawnApparelGenerator.cs`、`CompLowPowerInSpace.cs`、`MapGenUtility.cs` 等）——若 mod 自建「海水」标记而非复用 `inVacuum`，这些都是 patch 点清单。
- 原版 Ocean 生态域 XML（`canBuildBase=false` 等假设值）未在本片范围内，需查 Defs XML。
