# chunk-32 调研发现（Verse: DamageWorker_D~G 开头家族）

## TL;DR

- **`District.ExposedVacuumCount`（District.cs:287）是"海水泄漏检测"的最直接模板**：district（房间/区域连通块）已经内置了"暴露于真空的格数"的增量缓存计数——判定条件是"无屋顶 **或** 地形 `exposesToVacuum`"。海底 Mod 的"气密性/进水"判定几乎可以照搬这套机制。
- **`ExitMapGrid`（ExitMapGrid.cs:50）证明真空 biome 会关掉"地图边缘离开"网格**：`map.Biome.inVacuum` 时 `MapUsesExitGridNow` 直接返回 false。海底 biome 同样需要屏蔽边缘撤离（不能游泳走出地图）。
- **`FreezeManager` 是 Odyssey 水面结冰系统**（FreezeManager.cs）：-7°C 以下水面逐步冻结成 `ThinIce`、2°C 以上融化。海底恒温 4°C 下应天然不触发，但要确认海水地形的 `canFreeze` 标记；这套"水体随温度改变地形"的机制本身也可借做海底特殊事件（如热泉口）。
- **`DarklightUtility` / `DarknessSource` 是黑暗/暗光机制的核心 Verse 侧实现**：暗光（darklight）是一种特定色相的光色判定 + 非自然黑暗作为寻路布尔网格（`SimpleBoolPathFinderDataSource`），深海低光照与"发光生物照明"可整套借用。
- **`Explosion` 已有水面差异化先例**（Explosion.cs:314）：爆炸后生成物有 `postExplosionSpawnThingDefWater` 水格专用变体，说明"同一效果在水格走另一套参数"是官方认可的扩展点。
- **`DeepResourceGrid` 深钻资源网格**：按格存储矿产 def+数量，配深扫描仪/深钻。海底矿床（锰结核、热泉硫化物）可直接复用这套地下资源体系。
- **`FishShadowComponent`**：Odyssey 水体鱼影 fleck 氛围组件，海底可直接借用做海面阴影/鱼群氛围。

本片其余 242 个文件为 Debug 工具、Dialog UI、DirectXml 序列化、Fleck 系统、Def 基础设施、近战伤害 Worker 等，与海洋语境无可类比点（PASS/SKIP）。

---

## 1. 真空暴露计数 → 海水暴露计数（强相关，直接模板）

**District.cs:287 `ExposedVacuumCount(int threshold)`**

- District 是 Region 系统里的连通块（房间即同 RegionType 的 district 集合）。
- 该方法增量枚举 district 内所有格子，统计"无屋顶 **或** 地形 `terrain.exposesToVacuum`"的格数，带 `cachedExposedCount`/`cachedExposedState` 枚举器缓存，支持阈值提前退出。
- 类比结论：这就是 Odyssey 用来判断"房间是否漏气"的底层。海底 Mod 的"气密建筑隔绝海水/破洞进水"需要完全同构的一套：地形侧加一个 `exposesToSeawater`（或复用同一字段的语义），district 级计数暴露格 → 决定该连通块是否被海水淹没。`TerrainDef.exposesToVacuum` 字段的存在说明地形可以声明自己"破坏气密"——海底地形（如矿脉挖开后的海床）可照样声明。

## 2. 地图边缘撤离网格与真空 biome（强相关，行为需照做）

**ExitMapGrid.cs**

- `MapUsesExitGridNow`（:38）逐条排除：玩家主基地、pocket map、**`map.Biome.inVacuum`**、未打赢的商队战场、可重组商队的地图，都不显示/不使用边缘撤离网格。
- 网格本体是距地图边缘 2 格内、`CanBeSeenOver` 且对边缘格有 LOS 的格子（:189 `IsGoodExitCell`）。
- 类比结论：海底 biome 应当像真空 biome 一样有一个等价开关（`BiomeDef` 扩展字段），禁用边缘离开——潜水殖民地不能"走到地图边缘撤离"，撤离应走潜水艇/上升舱。这证明 BiomeDef 级字段控制地图级移动规则是既有模式。

## 3. 水面结冰系统 FreezeManager（相关：冲突屏蔽 + 机制模板）

**FreezeManager.cs**

- 每格稳态 effect（`DoCellSteadyEffects`，需 OdysseyActive）驱动水面结冰/融冰：
  - 结冰：温度 ≤ -7°C 且地形 `canFreeze`、非四面环水；概率随温度与水体已冻比例（`waterBody.numCellsFrozen/numCellsCanFreeze` 的 10 次幂）增长，四面环冰则必冻；冻结是半径 2.9 的成片 `SetTempTerrain(ThinIce)`。
  - 融冰：温度 ≥ 2°C 的镜像逻辑。
- 依赖 `WaterBody`（`c.GetWaterBody(map)` 的 `numCellsFrozen`/`numCellsCanFreeze`）。
- 类比结论：
  1. 冲突面：海底室外恒温 ~4°C，高于冰点阈值，天然不会结冰——但前提是海底地形不触发别的稳态 effect；若 Mod 自定义"海水地形"，注意 `canFreeze` 应设 false。
  2. 模板面："水体按环境温度成片改变临时地形"整套模式（WaterBody 统计 + 径向扩散 + 临时地形）可借来做海底特有现象，例如热泉口周围沉积、油污扩散等。

## 4. 黑暗与暗光（相关：深海低光照借用）

**DarklightUtility.cs**

- 暗光判定是纯粹的颜色启发式（IsDarklight：绿/蓝分量接近、红分量低于其一半），默认暗光色 `Color32(78,226,229)` 青绿色。
- `IsDarklightAt`：格内有屋顶 + `PsychGlowAt >= 1`（即满发光）+ 视觉光色为暗光色 → 视为暗光格。
- 类比结论：深海"生物荧光照明"可以直接定义为暗光色系的 glower，无需新机制；黑暗族类/趋光生物的判定都会自动生效。

**DarknessSource.cs**

- `SimpleBoolPathFinderDataSource` 子类：把"非自然黑暗格"喂给寻路系统作为布尔代价源；仅在 `GameCondition_UnnaturalDarkness` 激活时计算，监听 `map.events.GlowChanged` 做增量更新。
- 类比结论：如果深海低光要影响寻路（如人形生物避开无灯深海区），这就是挂接点；同时它也是"全图常驻黑暗条件"的既有先例——深海可以把低光做成一个常驻 GameCondition，而不是地图生成时一次性烘培。

## 5. 爆炸的水面差异化先例（相关：扩展点先例）

**Explosion.cs:314**

- 爆炸落格若地形 `IsWater`，优先生成 `postExplosionSpawnThingDefWater`（否则 fallback 到普通 spawn def）；注意 `chanceToStartFire` 仍会对水格调 `FireUtility.TryStartFireIn`（由 FireUtility 内部决定是否失败，爆炸层不管）。
- 类比结论：Def 层已有"水格变体"字段，海底 Mod 的爆炸物可以直接利用；若要做"水下爆炸不生成火焰/烟雾而是气泡"，这个字段 + DamageWorker 覆写（参考 `DamageWorker_Vaporize` 的模式）就是路径。

## 6. 深钻资源网格 → 海底矿床（相关：直接复用）

**DeepResourceGrid.cs**

- 两个 ushort 网格（def shortHash + 数量，上限 65535/格），CellBoolDrawer 做覆盖层；`AnyActiveDeepScannersOnMap` 控制可见性；`DrawPlacingMouseAttachments` 在深钻放置时显示脚下资源余量。
- 类比结论：海底特色资源（锰结核、海底油田、热泉硫化物）无需自研系统，深钻 + 深扫描仪体系直接可用，只需要地图生成时填这个网格。这也是"工业科技以上袭击"语境下海底经济支柱的现成方案。

## 7. 鱼影氛围组件（直接相关：氛围/生态）

**FishShadowComponent.cs**

- MapComponent（需 OdysseyActive）：遍历 `map.waterBodyTracker.Bodies`，对 `HasFish` 且 ≥12 格的水体按面积比例每 450 tick 尝试在纯水格（8 邻全为水）生成 `FishShadow`/`FishShadowReverse` fleck（随机缩放 0.4~1.1x、旋转 75°/s）。
- 类比结论：海底殖民地里这是现成的氛围系统——前提是海底的"水面"仍以 WaterBody 形式存在（比如海面层/采光井）。若海底地图全是"水下"，这个组件的 `IsWater` 判定可以反过来给"海床上方游过的鱼影"提供模板。

## 8. 顺带排除项（读过但无类比价值）

- `DamageWorker_Nerve/Scratch/Stab/Stun/MiningBomb`：近战/神经伤害 Worker，纯伤害数值逻辑。MiningBomb 仅体现 Odyssey 的"矿用炸药对 Mineable 走 buildingDamageFactor"特例，海底挖矿无额外启发。
- `DeathActionWorker_*`（Simple/Vanish/Divide）：死亡时播 fleck/生 filth/分裂成 fleshbeast（Anomaly），纯生物死亡演出框架——若要加"水生生物死亡上浮/溶解"可用同框架，但属常规扩展点，不单列。
- `DangerUtility`/`Danger`：寻路危险度枚举 + 温度伤害对玩家派系 pawn 的危险度豁免（`HasTemperatureInjury` 且 faction 有适温房间时 Danger.None）——说明"环境致死"是通过 Region.DangerFor 影响寻路的，海底海水致死区也会走这条路（Region 层，不在本片）。
- `FogGrid`/`FogSource`/`FloodFillerFog`：战争迷雾系统，与海洋无特殊关联。
- `EnvironmentStatsDrawer`：房间统计悬浮窗 UI。
- 其余 Debug*、Dialog_*、DirectXml*、Fleck*、Def* 基础设施文件：见覆盖表。
