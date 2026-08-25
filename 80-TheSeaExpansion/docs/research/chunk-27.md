# chunk-27 调研发现：Thought / ThoughtWorker 家族（心情想法系统）

本片 200 个文件全部是 RimWorld 的「想法（Thought）」系统：`Thought_*`（想法实例）+ `ThoughtWorker_*`（每 tick 判定想法是否激活的 Worker）。与海洋 Mod 的关系不在某个具体想法，而在两点：(a) 海底环境会直接触发/冲突一批现有的环境类想法（黑暗、寒冷、幽居、无阳光）；(b) 若要新增"深海/海水"相关心情，本片提供了完整的扩展模板。

## TL;DR

1. **`ThoughtWorker_Precept_NoFish`** —— Odyssey 已有"鱼类戒律"想法，直接读 `p.MapHeld.waterBodyTracker.AnyBodyContainsFish`：官方水域追踪器存在且记录"水体是否含鱼"，海底地图的渔业/吃鱼戒律可直接挂钩。（直接相关）
2. **海底恒温 4°C 会全员常驻 Cold 想法**：`ThoughtWorker_Cold` 按 `ComfyTemperatureMin - AmbientTemperature` 差值分 4 档（<10/20/30°C），Ideology 的 `Temperature_Tough` 戒律可减 2 档 —— Mod 需要决定保留（穿保温服解决）、给海底地图专用戒律、还是用 nullifyingPrecepts 屏蔽。（冲突类）
3. **海底 = 永远无阳光**：`ThoughtWorker_InSunlight`（Biotech 日光需求基因用）读 `Position.InSunlight(map)`；`ThoughtWorker_Dark` 用 `needs.mood.recentMemory.TicksSinceLastLight > 240` tick 判定"身处黑暗"。海底室外永远黑暗 → 这两个想法的行为需要专门设计（永久黑暗惩罚 or 用 `Ideo.IdeoPrefersDarkness()` 类机制豁免）。（冲突+借用）
4. **海底殖民地 = 全室内/全有顶**，穴居者（Undergrounder）三个 Worker 的判定全部基于 `UsesOutdoorTemperature` / `roofGrid.RoofAt` / `isNatural` —— 海底气密建筑天然满足"室内+人工顶"，穴居者特性在海底纯增益；反之普通殖民者的 `NeedOutdoors`（Cabin Fever，Entombed/Trapped 档）会长期积累，这是海底基地的核心心情压力来源。（直接相关）
5. **`GameConditionManager.IsAlwaysDarkOutside` 标志已存在**（`ThoughtWorker_Aurora` 里读到）：官方已有"地图永远黑暗"的条件机制，海底地图可直接借用让极光/日蚀类想法统一失效。（借用）
6. **新增"海水暴露"心情的模板齐全**：`ThoughtWorker_NoxiousHaze`（环境危害想法 + `ToxicResistance`/`ToxicEnvironmentResistance` stat 免疫 + `immuneToGameConditionEffects`）是"泡水致死/不适"想法的直接仿造对象；`ThoughtWorker_DeadlifeDust` 演示了按 `Position.GasDensity(map, GasType)` 密度分 3 档的写法（若海水用气体系统实现）。

## 主题一：光照 / 黑暗类想法（深海低光照）

| 文件 | 机制 | 海洋类比 |
|---|---|---|
| `ThoughtWorker_Dark.cs` | 醒着 + `TicksSinceLastLight > 240` + 非盲 + 戒律不偏好黑暗 → 激活"黑暗"负面想法 | 海底室外永远满足 → 常驻惩罚，需用 `IdeoPrefersDarkness()` 类似钩子或新 nullifying 机制处理 |
| `ThoughtWorker_InSunlight.cs` | Biotech 限定；`p.Position.InSunlight(p.Map)` 为真则激活（日光浴基因正面想法） | 海底永不触发；若有"喜阳"基因殖民者会持续缺失该增益 |
| `ThoughtWorker_Precept_Eclipse.cs` | 戒律想法：醒着、非盲、房间 `PsychologicallyOutdoors`、地图 `GameConditionDefOf.Eclipse` 激活 → 日蚀心情 | "室外 + 黑暗条件 → 心情"的现成模板；可仿造一个永久性的"深海幽暗"条件想法 |
| `ThoughtWorker_Precept_Darklight.cs` | `DarklightUtility.IsDarklightAt(pos, map)` → 暗光戒律心情 | 若 Mod 加"发光海洋植物/探照灯"可复用 glowGrid 体系 |
| `ThoughtWorker_Precept_IndoorLight.cs` | `glowGrid.PsychGlowAt(pos) != PsychGlow.Dark && Roofed && !IsDarklightAt` → 室内光照心情 | 海底基地人工照明直接走这条现有逻辑，无需改动 |
| `ThoughtWorker_Aurora.cs` | 继承 `ThoughtWorker_GameCondition`；先查 `map.GameConditionManager?.IsAlwaysDarkOutside == true` 则强制不激活；再要求无顶、非盲 | 证明官方有"全图永夜"开关；海底地图挂上后，极光类天空想法自动失效 —— 值得抄 |
| `ThoughtWorker_IsDayForNightOwl.cs` / `ThoughtWorker_IsNightForNightOwl.cs` | 仅按 `GenLocalDate.HourInteger(p)` 时段判定（11-17 / 23-5），与光照无关 | 夜猫子特性在海底照常工作，无冲突 |

## 主题二：温度类想法（海底恒温 4°C）

- `ThoughtWorker_Cold.cs`：`num = ComfyTemperatureMin - AmbientTemperature`，差值 <10/20/30 对应 stage 0/1/2/3；`PreceptDefOf.Temperature_Tough` 戒律把 stage 减 2（可减到不激活）。
- `ThoughtWorker_Hot.cs`：对称逻辑，用 `ComfyTemperatureMax`；海底不会触发。
- 结论：海底室外 4°C 相对人类舒适下限（约 16°C+装备）差值 ~12°C → 全员室外常驻 stage 1 "冷"。选项：(a) 不管，让潜水服/加热服解决（装备推高 ComfyTemperatureMin）；(b) 给海底剧本发 Temperature_Tough 戒律；(c) 用 `ThoughtDef.nullifyingPrecepts` / `nullifyingHediffs`（见主题五）挂一个"深海适应"豁免。注意判定读的是 `p.AmbientTemperature`，气密建筑内正常供暖后想法自然消失，机制本身是自洽的。

## 主题三：室内/室外/穴居（海底基地 = 封闭空间）

- `ThoughtWorker_IsIndoorsForUndergrounder.cs`：核心静态方法 `IsAwakeAndIndoors(p, out isNaturalRoof)`：`Position.UsesOutdoorTemperature(map)` 为 false + 有顶 → 室内；`roofDef.isNatural` 区分自然顶/人工顶。
- `ThoughtWorker_IsUndergroundForUndergrounder.cs`：室内 + **自然**顶（山体下）→ 穴居者正面想法。海底基地是人工顶 → 这条不触发，只触发"室内"那条。
- `ThoughtWorker_IsOutdoorsForUndergrounder.cs`：`UsesOutdoorTemperature || !Roofed` → 穴居者负面想法。海底"室外"（非气密区）会触发 —— 语义完美吻合。
- `ThoughtWorker_NeedIndoors.cs` / `ThoughtWorker_NeedOutdoors.cs`：读 `needs.indoors/outdoors.CurCategory` 映射 stage；NeedOutdoors 的档含 `Entombed/Trapped/CabinFeverSevere` —— **海底全封闭殖民地会让普通殖民者长期 Cabin Fever**，这是海底玩法最重要的心情矛盾点（也是设计点：可类比给"海洋爱好"特性豁免）。
- `ThoughtWorker_NeedRoomSize.cs`：附带发现 `p.needs.PrefersIndoors`（戒律驱动）可豁免狭窄房间惩罚 —— 又一个"偏好室内"的官方钩子。

## 主题四：Odyssey 鱼类 / 水体

- `ThoughtWorker_Precept_NoFish.cs`（15 行，全文已读）：`ModsConfig.OdysseyActive` 限定；`!p.MapHeld.waterBodyTracker.AnyBodyContainsFish` 时激活"没有鱼"戒律想法。
  - **关键结论**：Odyssey 在 Map 上挂了 `waterBodyTracker`，提供 `AnyBodyContainsFish` 属性。海洋 Mod 的渔业资源、捕鱼点、吃鱼戒律都应该对接这个官方追踪器而不是自建一套。

## 主题五：想法系统核心（新增"深海/海水"想法的扩展点）

- `ThoughtWorker.cs`：抽象基类，`CurrentState(p)` → `FailState`（`ThoughtUtility.CanGetThought` 过滤）→ `CurrentStateInternal`（子类实现）→ `def.invert` 后处理。新增想法 Worker 只需继承并实现一个方法。
- `Thought_Situational.cs`：情境想法实例；`RecalculateState()` 里 Worker 给 stage；`def.producesMemoryThought` 支持"情境结束后转成记忆想法"（例如"泡了海水 → 获得持续 1 天的后怕"）。
- `ThoughtUtility.cs`：`CanGetThought` 的过滤器链（developmentalStage、gender、minExpectation、requiredTraits/Hediffs/Genes…）；`ThoughtNullified` 支持五种豁免：**nullifyingTraits / nullifyingHediffs / nullifyingOwnTales / nullifyingPrecepts / nullifyingGenes**（+ `neverNullifyIfAnyTrait` 反豁免）—— 全 XML 驱动，Mod 做"深海适应基因/戒律豁免负面海洋想法"零代码。
- `Thought_Memory.cs`：记忆想法基类（age/duration/stackLimit/lerpMoodToZero 衰减）。
- `ThoughtWorker_GameCondition.cs`：地图或世界 GameCondition 激活 → 想法激活，最薄的"环境事件→心情"桥。
- `ThoughtWorker_HateChantDrone.cs`：`gameConditionManager.GetActiveCondition<GameCondition_HateChantDrone>().level` 枚举映射 5 档 stage —— "条件强度→想法分档"模板（可仿造"洋流强度/水压"心情）。
- `ThoughtWorker_NoxiousHaze.cs`：危害环境想法的完整免疫检查链：`ThoughtUtility.NullifyingGene`（注意：这里 NullifyingGene 反而**强制激活**，是基因自带的"虽然免疫但仍在意"语义）、`ToxicResistance >= 1`、`ToxicEnvironmentResistance >= 1`、`kindDef.immuneToGameConditionEffects`。仿造"海水暴露不适"想法时，这套 stat 命名模式可直接平移（如自定义 `SeawaterExposureResistance`）。

## 其余文件

- SKIP（纯数据/极 trivial）：`ThoughtStage`（数据类）、`ThoughtState`（状态结构体）、`ThoughtMaker`（工厂）、`ThoughtToAddToAll`（结构体）。
- PASS（173 个）：社交评价类（_Social、乱伦、情人、种族/异种、蒙面/裸体戒律系列）、儿童/成长舱系列、义体/致盲戒律系列、乐器、王室头衔、Anomaly/Biotech 专属等 —— 逐文件读过，机制均为"人际关系/装备/基因/房间评分 → 心情"，与海洋语境无任何类比点。
