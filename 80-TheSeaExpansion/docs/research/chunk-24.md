# chunk-24 源码调研发现（RimWorld 命名空间：Signal*、SitePart*、Skill*、Skyfaller*、SpecialThingFilterWorker_*、StageTrigger*、Stat* 等）

## TL;DR

- **SpaceGenUtility.cs 是 Odyssey「轨道平台/太空遗迹」地图生成工具**：`GenerateHole` 把整片矩形替换成 `TerrainDefOf.Space`（真空地形）并掀掉屋顶，`DamageEdges`/`DamageHoles`/`ScatterExtrusions` 在平台边缘挖洞、生成凸出结构。这套「在可建造基底上挖出致死环境空洞」的生成器，几乎可以直接类比成「在海床上生成充满海水的深渊裂缝/洞穴」。
- **SitePartWorker_ConditionCauser 全家（含 SunBlocker/ClimateAdjuster/WeatherController）是「世界据点向周围地图施加游戏状态」的完整管线**：遮蔽阳光（SunBlocker→黑暗）、温度偏移（ClimateAdjuster）、强制天气（WeatherController）。深海低光照、海底恒温、屏蔽天气事件三件事都能借这条管线实现或需要针对它做屏蔽。
- **SitePartDef.minFactionTechLevel + FactionCanOwn** 提供了「据点只允许某科技等级以上派系拥有」的原生开关，正好对应「海底只刷工业科技以上袭击」的需求。
- **SitePartWorker_RaidSource** 是「世界地图上的袭击源据点按 MTB 周期性对玩家殖民地发动 RaidEnemy」的现成实现（袭击点数 ×0.6，两次袭击间隔 ≥90000 tick）。
- **StatDefOf 确认存在 `VacuumResistance`（Odyssey 真空抗性）、`FishingSpeed`、`FishingYield`、`ComfyTemperatureMin/Max`**：真空抗性是「水下抗压/潜水服」属性的直接模板；捕鱼速度/产量是海底食物获取的核心属性。
- **StatPart_EnvironmentalEffects**：物品腐坏度受屋顶/户外/雨量影响，且**地形 def 的 `extraDeteriorationFactor` 直接叠加进腐坏因子**——给「海水地形」设这个字段即可让泡在水里的物品加速腐坏，零代码。
- **StatPart_Glow**：按 `glowGrid.GroundGlowAt` 光照曲线缩放属性（用于工作速度等），深海低光照惩罚的原生挂点；`ignoreIfPrefersDarkness` 已有对黑暗模因/基因的豁免逻辑。
- **Skyfaller.cs**：空投舱撞击时会 `HitRoof()` 用 `RoofCollapserImmediate.DropRoofInCells` 砸穿屋顶（glass/薄顶有 `soundPunchThrough` 音效）——类比到海底：空投/物资从海面沉降到海底、砸穿基地外壳（漏水！）的整套表现管线可借用。
- **SketchBuildable.ForceTerrainAffordance**：生成 Sketch（遗迹蓝图）时遇到水格自动铺 `AncientBridge`/`AncientHeavyBridge`（Odyssey 地基），水面上生成遗迹的原生处理方式。
- **SkillTuning.XpPerTickFishing = 0.025**：捕鱼是原生工作类型（1.6/Odyssey），吃 Animals 技能经验。
- **SoundDefOf 有 `Ambient_Space`（太空环境音）、`EmergeFromWater`、`Interact_CatchFish`**：海底环境音与水下音效有现成锚点。

## 世界地图据点系统（Site / SitePart）——海底站点与袭击来源

### SitePartDef.cs
关键字段（全部读自源码）：
- `conditionCauserDef`：该据点携带的「状态引发器」建筑 def（配合 `CompProperties_CausesGameCondition.worldRange` 决定影响半径）。
- `minFactionTechLevel`（`TechLevel`，默认 Undefined）：`FactionCanOwn()` 里检查 `faction.def.techLevel < minFactionTechLevel` 则不允许拥有。**这就是「只有工业科技以上的袭击」在据点层面的原生实现点**。
- `gravShipsCanLandOn`（Odyssey 重力船可否降落）、`forceMutators`（TileMutatorDef 列表，强制地图变异器）、`badEvenIfNoMap`、`leaveAbandonedSettlement`。
- `FactionCanOwn()` 排除玩家派系/已灭派系/隐藏派系，并调用 `Worker.FactionCanOwn(faction)` 留给子类。
- `CompatibleWith()` 用 tags/excludesTags 做据点部件互斥。

### SitePartWorker.cs（基类）
- `PostDestroy()`：若 `leaveAbandonedSettlement` 且在地表层，摧毁后在原地留下 `AbandonedSettlement` 世界对象（归玩家）。
- 钩子齐全：`PostMapGenerate(map)`、`SitePartWorkerTick`、`Notify_SiteMapAboutToBeRemoved` 等，自定义海底站点部件直接继承即可。

### SitePartWorker_ConditionCauser.cs（+ 四个子类）
- `Init()`：据点生成时 `ThingMaker.MakeThing(conditionCauserDef)`，设派系，`CompCauseGameCondition.RandomizeSettings(site)` 并 `LinkWithSite(site)`——**世界据点→范围游戏状态**的绑定管线。
- 威胁标签附带 `ConditionCauserRadius`（影响半径）；选中时画世界范围圈。
- **SunBlocker**（SitePartWorker_ConditionCauser_SunBlocker.cs）：遮蔽阳光的据点。`IsAvailable()` 里有现成的「玩家是黑暗/穴居模因则不生成此据点」的豁免——深海殖民地天然黑暗，这类「对玩家有利环境则不再刷对应 debuff 据点」的判断逻辑可直接照抄。
- **ClimateAdjuster**：comp 是 `CompCauseGameCondition_TemperatureOffset`，任务描述里格式化温度偏移量。类比：海底恒温 4°C 可以用同类 comp 实现，反之气候调节器事件在海底是冲突项需要屏蔽。
- **WeatherController**：comp 是 `CompCauseGameCondition_ForceWeather`，强制天气。海底应屏蔽天气（水下无雨无雪），这条是冲突类参考。
- PsychicDroner / PsychicSuppressor：仅性别规则文本差异，机制同上，海洋无特殊价值。

### SitePartWorker_RaidSource.cs（继承 Outpost）
- 每个玩家殖民地地图，据点每 2500 tick 哈希间隔判定一次 MTB（`QuestTuning.PointsToRaidSourceRaidsMTBDaysCurve.Evaluate(threatPoints)`），触发 `IncidentDefOf.RaidEnemy`，袭击点数 ×0.6，成功后记 `lastRaidTick`，90000 tick 内不再袭击。
- **海底版「海盗前哨站定期袭击海底殖民地」的完整模板**，且 RaidEnemy 本身会走派系科技等级过滤。

### SitePartWorker_WorkSite.cs（+ Farming/Hunting/Logging/Mining）
- 抽象基类：`CanSpawnOn(tile)` 控制站点能刷在哪种世界格；`LootThings(tile)` 按格子产出战利品表；`FactionCanOwn` 检查派系 PawnGroupMaker 与意识形态戒律（DisallowedPrecepts）。
- 各子类的刷出条件全部基于**世界格属性**：Farming 看 `PrimaryBiome.allowFarmingCamps` + 季节温度过滤作物；Hunting 看 `AnimalDensity > Desert` 且战利品来自该生态 `AllWildAnimals` 的皮革；Logging 看 `TreeDensity`；Mining 看 `hilliness`（山地 3–4 级）。
- **类比**：海底资源站点（深海热泉矿区、渔场）可以照这个模式写 `CanSpawnOn`（看海洋生态/地形）+ 自定义战利品表。Hunting 版还演示了「战利品从该格生态的动物列表推导」，对「只刷水生生物」的战利品推导是直接参考。

### 其余 SitePartWorker
- Manhunters：`ManhunterPackGenStepUtility.TryGetAnimalsKind(threatPoints, tile, out animalKind)` 按威胁点数和格子选动物——动物选择走格子生态，海洋格自然会选到水生动物（前提是该工具的过滤逻辑支持，此处未验证）。
- MechCluster / SleepingMechanoids：`IsAvailable()` 要求 `Faction.OfMechanoids != null`，最低威胁点数 750 / 派系最低战斗组点数。机械族在海底的合理性由 Mod 自定，机制上无阻碍。
- Outpost：威胁点→战利品价值曲线 `ThreatPointsLootMarketValue`（100pt→200$、10000pt→2000$）。
- Turrets：炮塔数 = 威胁点 / 小炮塔 combatPower，钳制 2–11，迫击炮 0–1。
- WorkSite 的 `PointsMarketValue` 曲线：100pt→200$、800pt→2000$、10000pt→5000$。
- AncientAltar / AncientComplex / WorshippedTerminal / AlphaThrumboSighting：任务据点部件，海洋无直接价值。

## 地图生成：SpaceGenUtility.cs（Odyssey 太空图）——海底地图生成的头号模板

逐方法读到的行为：
- `GenerateHole(map, rect)`：摧毁矩形内所有 Thing，地形设为 `TerrainDefOf.Space`，屋顶设为 null。**「把一片区域变回致死环境」的原语**，海底版就是设成「海水地形/深水格」。
- `DamageEdges` / `GenerateEdgeCut` / `GenerateExtraDamage` / `DamageHoles`：沿平台边缘啃出随机形状的缺口、在内部按密度（每百格 0.05 个）挖 6–12 格大小的洞，洞边缘用 `MapGenUtility.DoRectEdgeLumps` 做不规则化——把「完整的轨道平台」破损化。类比：海床基地遗迹被海水侵蚀出破洞。
- `ScatterExtrusions`：从已有地块边缘向外生成 8–15 长、带间隙的条状 `TerrainDefOf.OrbitalPlatform` 凸出结构。
- `ChunksSetSparsley`：洪泛填充按覆盖率标记 chunk。
- 大量 IntRange/FloatRange 参数都有默认值，可整体照搬再调参。

相关：`SketchResolverDefOf` 里 Odyssey 新增的 sketch 生成器（AncientHatch 古代舱口、Gravship、CerebrexCore、Mechhive* 等）是遗迹/前哨 sketch 的入口清单，海底遗迹生成可参考。

## Sketch（遗迹蓝图）与水

- **SketchBuildable.ForceTerrainAffordance()**：当 sketch 建筑要落在水格（`TerrainAt(item).IsWater`）时，不铺普通地形而是 `SetFoundation(AncientBridge)`（Odyssey 激活时用 `AncientHeavyBridge`）。即原版对「水面上生成遗迹建筑」的答案是**自动垫地基桥**——海底 Mod 若想在海床生成沉船/遗迹，要么照此垫地基，要么改用水下专用地形。
- Sketch.cs / SketchTerrain.cs / SketchThing.cs：sketch 容器与单实体生成逻辑（SpawnOrder：地形 1.0、Thing 2.0），本身与海洋无关，PASS。

## Skyfaller（空投舱）——从海面到海底的物资/袭击投送

- 撞击时序：`ticksToImpact` 倒数，≤15 tick 时 `HitRoof()`——若占格有屋顶，播放该 RoofDef 的 `soundPunchThrough` 音效并用 `RoofCollapserImmediate.DropRoofInCells` 塌落周边屋顶（`minimalRoofDestruction` 控制是否扩一圈；有 pawn 或承重建筑的格不塌）。
- `Impact()`：可选爆炸（`explosionDamage/Radius/Factor`）→ `SpawnThings()` 把内装物放出（爆炸会把实心建筑下的地形也摧毁 `Notify_TerrainDestroyed`）→ 可选弹片（SkyfallerShrapnelUtility，前方 6 格/后方 30 格定向散布钢渣+瓦砾）→ 相机震动、音效、impactLetter。
- 类比：海底殖民地「从海面沉降下来的补给舱/袭击舱」可整个复用 Skyfaller，只需替换贴图动画（下沉而非坠落）；砸穿屋顶→**砸穿气密外壳导致海水灌入**是现成玩法的嫁接点。`SkyfallerUtility.CanPossiblyFallOnColonist` 是袭击 AI 评估砸到殖民者的工具。`Skyfaller_FlyingPawn`（飞行单位降落动画）与海洋无关。

## Stat 系统——水下生存相关的属性挂点

- **StatDefOf**（grep 确认）：`VacuumResistance`（:162，Odyssey 真空抗性——潜水服/抗压服属性直接照抄）、`FishingSpeed`（:241）、`FishingYield`（:244）、`ComfyTemperatureMin/Max`（:56/58）、`MaxFlightTime`/`FlightCooldown`。
- **StatPart_EnvironmentalEffects.cs**：腐坏因子 = 未加顶 `factorOffsetUnroofed` + 户外室温房间 `factorOffsetOutdoors` + **地形 `extraDeteriorationFactor`**，未加顶时再 ×(1→5 随雨量)，被建筑遮挡 ×`protectedByEdificeFactor`。**给海水地形设 `extraDeteriorationFactor` 即可让水中物品加速腐坏**；注意海底「室外」没有雨，雨量乘子恒为 1（若天气被屏蔽的话）。
- **StatPart_Glow.cs**：`factorFromGlowCurve.Evaluate(glowGrid.GroundGlowAt(pos))` 缩放属性；`ignoreIfPrefersDarkness` 对黑暗模因 / 无黑暗敏感基因的 pawn 豁免。深海低光照下工作速度惩罚的直接挂点。
- **StatPart_Difficulty_FishingYield.cs**：捕鱼产量乘 `Find.Storyteller.difficulty.fishingYieldFactor`（难度设置里有捕鱼产量系数）。另有 ButcherYield 同款；MiningYield 版是空实现（占位）。
- **SkillTuning.XpPerTickFishing = 0.025**：捕鱼给（Animals）技能经验的原生速率。SkillDefOf 无独立 Fishing 技能——捕鱼归 Animals。
- StatDef.parts 管线本身（StatPart/StatPart_Curve 等）是通用框架，与海洋无关。

## 声音

- **SoundDefOf**（grep 确认）：`Ambient_Space`（:219，太空环境音，海底环境音可直接新建同款 SongDef/SoundDef）、`EmergeFromWater`（:337，出水音效——原版已有水下生物/涉水相关音效锚点）、`Interact_CatchFish`（:706，捕鱼交互音）。
- SoundParamSource_TimeOfDay：按当前地图小时数给音效供参数，海洋无关。

## 其他（弱相关 / 一句话）

- **StageFailTrigger_TargetNotLit.cs**：仪式失败触发器——目标（火盆类）的 CompRefuelable 没燃料即失败。是「仪式需要光源/燃料」的现成触发器，若海底做「照明相关的仪式/机制」可参考；本身属 Ideology 仪式系统。
- SituationalThoughtHandler.cs：心情/社交情境想法的缓存与刷新框架（每 100 tick 重算），具体「黑暗/潮湿」想法在 ThoughtWorker 里（不在本片），本片 PASS。
- SmokepopBelt.cs：被敌对炮塔子弹擦过时放 BlindSmoke 烟雾（`AnyGas(GasType.BlindSmoke)` 检查）——水下烟雾弹合理性是设计问题，机制上气体系统与地形无耦合，PASS。
- SkipUtility.cs：Anomaly 传送（skip）工具，海洋无关。
- SpecialThingFilterWorker_*（48 个）：存储/配方过滤器的琐碎实现，全部与海洋无关。
- Stage 触发器其余（约 20 个）：仪式 lord 触发器，全部 PASS。
- SlaveRebellionUtility / SocialCardUtility / SocialInteractionUtility / SpectatorCellFinder / SpouseRelationUtility / SolidBioDatabase / SkillUI / StatDrawEntry / StatDisplayOrder 等：UI、社交、仪式观众找位、背景故事库等，均 PASS。

*覆盖范围：chunk-24 全部 200 个文件（Signal*、Site*、Sketch*、Skill*、Skip*、Skyfaller*、Sl*、Sm*、So*、Sp*、Stage*、Stat* 家族）。大文件（Sketch.cs 1084 行、SocialCardUtility 1132 行等）按类定义+核心方法阅读，结论均基于实际读到的内容；Manhunters 动物选择的水生过滤路径标记为「未验证」。*
