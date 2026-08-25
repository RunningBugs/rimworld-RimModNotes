# chunk-31 调研发现（Verse 组，A–D 字头 250 文件）

## TL;DR

本片最大收获：**Odyssey 的真空/气密体系在 Verse 层已经露出多个挂载点**，全部可直接类比为"海水/气密隔水"：

1. **`Building.IsAirtight` / `Building.ExchangeVacuum`**（`Building.cs:76-102`）——气密判定就在建筑基类上，支持 stuff 级气密（`isStuffableAirtight` + `stuffProps.isAirtight`）。"隔水建筑"完全可以复用同一套字段语义。
2. **`ConditionalStatAffecter_InSpace`**（`ConditionalStatAffecter_InSpace.cs`）——"在太空"的判定方式是 `Map.Tile.LayerDef.isSpace`，即按世界格地图层判定。海洋殖民地若用独立 MapLayer，可照写一个 `ConditionalStatAffecter_Underwater`。
3. **`CachedTempInfo`**（`CachedTempInfo.cs`）——房间缓存同时存 `temperature` 和 `vacuum` 两个 float：**真空度在引擎里是与温度并列的房间级模拟量**，海水淹没度可以照此建模。
4. **`CompAffectsSky`**（`CompAffectsSky.cs`）——单个建筑即可覆盖全图天空辉光/颜色，且自带 fade-in/hold/fade-out 动画 —— 深海"整体压暗+蓝绿色调"的现成工具。
5. **`CompGlower` 监听 `"Vacuum"` comp 信号**（`CompGlower.cs:167`）——真空状态变化会广播信号让光源刷新；且有 `darklightToggle`（暗光）机制，深海照明两条路都有官方先例。
6. **`AlternateGraphic.swimmingGraphicData`**（`AlternateGraphic.cs`）——官方 Pawn 贴图变体里已有"游泳中"贴图槽，水下移动表现有现成接口。
7. **`BodyPartDef.canBeVacuumBurnt`**（`BodyPartDef.cs:50`）——部位级"是否受真空灼伤"开关；类比可做部位级"耐压/抗溺水"。
8. **`BookUtility.IsValidBook` 用 `thing.VacuumConcernTo(pawn)`**（`BookUtility.cs:194`）——官方模式：凡是"东西暴露在真空里所以 pawn 不会去用"的判断收敛成一个扩展方法，海水版（物品在非气密区被水淹）可照抄此模式。

---

## 一、真空/气密体系（本片核心，全部强相关）

### Building.cs —— 气密判定的锚点
- `IsAirtight`（76-90 行）：`def.building.isAirtight` 直接气密；或 `def.building.isStuffableAirtight && Stuff.stuffProps.isAirtight`（材料级气密，如玻璃钢 vs 木墙）。
- `ExchangeVacuum`（92-102 行）：非气密建筑总是与周围交换真空；气密建筑只有 `def.building.alwaysExchangeVacuum` 为 true 才交换（气闸门类）。
- **类比结论**：海洋 Mod 的"隔水墙/气闸"不需要新造概念，同一组布尔语义（建筑级 + stuff 级 + 常开交换）照搬即可。具体真空模拟在别处（VacuumGrid/Room 级，见 CachedTempInfo），本文件只提供"这个建筑隔不隔"的判定接口。

### CachedTempInfo.cs —— 真空是房间级模拟量
- 结构体字段：`roomID, numCells, temperature, vacuum`。每个房间缓存温度**和真空度**。
- **类比结论**：海水淹没度若想做到"房间级"（抽干一个房间的水），这是最直接的参照——温度模拟怎么按房间算，海水就能怎么按房间算。

### ConditionalStatAffecter_InSpace / NotInSpace / InSunlight
- `InSpace.Applies`：`req.Thing.MapHeld.Tile.LayerDef.isSpace` —— 整个地图是不是太空由世界格所属 MapLayer 的 Def 决定。
- `NotInSpace` 直接取反继承；`InSunlight` 走 `Position.InSunlight(map)`（Biotech 限定）。
- **类比结论**：两条官方路径都可借用——(a) 若海底是独立 MapLayer，给 LayerDef 加判定即可让所有 "InSpace" 类条件属性有海底对应物；(b) 装备属性（潜水服水下加成）用 ConditionalStatAffecter 模式写。

### BookUtility.cs —— VacuumConcernTo 使用模式
- 选书时排除"对该 pawn 有真空顾虑"的物品（书在真空中、 pawn 过不去/不愿去拿）。
- **类比结论**：海水环境需要同款过滤器（"该物品泡在未抽干的海水里，陆地 pawn 不去取"），模式已验证。

### BodyPartDef.cs —— canBeVacuumBurnt
- 字段 `canBeVacuumBurnt = true`（第 50 行），真空灼伤按身体部位开关。
- **类比结论**：深海版可做"canBePressureCrushed / 溺水易感部位"级别的控制，粒度和官方一致。

### CompGlower.cs —— 真空信号与暗光
- `ReceiveCompSignal` 里 `"Vacuum"` 与其它电源/开关信号并列，触发 `UpdateLit(map)`（167 行附近）：真空变化会刷新光源状态。
- 内部枚举 `GlowerColorChangeType` 含 `DarklightToggle`；多选批量改色/切暗光。
- **类比结论**：(1) 海水淹没/排干事件可以照此广播 comp 信号刷新受影响事物；(2) 暗光（darklight，Anomaly 的"在黑暗中发光"）是深海照明的天然模板——深海大部分区域天然黑暗。

## 二、温度（4°C 恒温，直接相关）

### CompHeatPusher / CompHeatPusherPowered / CompProperties_HeatPusher
- 每 60 tick 调 `GenTemperature.PushHeat(position, map, heatPerSecond)`；有 `heatPushMaxTemperature/heatPushMinTemperature` 工作区间；Powered 版本叠加电力/燃料/故障/开关检查。
- **类比结论**：海底恒温 4°C 意味着供暖是常驻需求，这就是加热器 comp 本身——无需改动，只需注意"室外"（非气密区）在海洋里应视为不可加热（类比真空中温度模拟的处理，需查 GenTemperature 对真空的处理，本片未覆盖，未验证）。

### CompTemperatureDamaged / CompProperties_TemperatureDamaged
- 每 250 tick 检查 `AmbientTemperature` 是否超出 `safeTemperatureRange`（默认 -30~30），超出则吃 `DamageDefOf.Deterioration` 伤害。
- **类比结论**：(a) 海底 4°C 本身在安全区内，不会误伤；(b) 该 comp 是"环境不适就缓慢损坏"的模板，可仿写"非耐压物品在深海缓慢损坏"（把温度判定换成水压/淹没判定）。

## 三、光照与天空（深海低光照，直接相关）

### CompAffectsSky / CompProperties_AffectsSky
- 建筑级天空覆盖：`SkyTarget(glow, skyColors, lightsourceShineSize, lightsourceShineIntensity)`，props 有 `lerpDarken`。
- `StartFadeInHoldFadeOut(fadeIn, hold, fadeOut, target)` 提供完整的淡入-保持-淡出动画（日蚀类事件用）。
- **类比结论**：深海"永久暮色/黑暗"若用 GameCondition 全局做不够灵活时，单个建筑（如"深渊黑烟囱"生态区）即可局部改变天空颜色与辉光，且带过渡动画。

### BloodRainUtility.cs —— "暴露于室外 + 游戏条件 → hediff" 模板
- `ExposedToBloodRain`：有活动 GameCondition、未被其它条件压制、持续 2000 tick 后、且 `!Position.Roofed(map)`（头顶无屋顶才算暴露）。
- 满足则给人/动物加 `BloodRage` hediff。
- **类比结论**：这就是"暴露致死/受影响"的标准实现——海洋版："处于非气密（未隔水）格 + 非水生 → 加'溺水/水压'hediff"，判定结构（条件管理器 + 暴露判定 + 种族过滤）逐行可仿。

## 四、贴图与表现

### AlternateGraphic.cs —— 游泳贴图槽
- 字段：`swimmingTexPath` / `swimmingGraphicData`，方法 `GetSwimmingGraphic(other)`（基于已有 graphic 拷贝覆盖 texPath/颜色）。
- **类比结论**：官方已经为"pawn 在游泳"准备了贴图变体管线（与 rotting/dessicated 并列），水生生物和水下移动直接可用。

### CompWindSource / CompProperties_WindSource（弱相关）
- 只有一个 `float wind` 字段的 comp（props 无字段）。
- **类比结论**：风力源的极简模板；若做"洋流发电机"，"水流强度源"可照此实现（一个 float + 消费方查询）。

## 五、屋顶/封顶（海底气密室需要"顶"）

### AutoBuildRoofAreaSetter.cs
- 房间形成后自动给房间生成 BuildRoof 区域：排除 TouchMapEdge、RegionCount>26、CellCount>320、门道的房间；要求边界格有玩家势力的屋顶支撑物；`def.building.allowAutoroof` 可禁。
- **类比结论**：海底殖民地"封顶"沿用屋顶系统即可（屋顶本来就是隔真空的必要件，大概率也隔海水——屋顶与真空的关系在别处，本片未验证）。自动屋顶逻辑不用动。

## 六、火焰/爆炸（冲突类：水下要灭火）

### DamageDef.cs
- 关键字段：`igniteChanceByTargetFlammability`（按目标可燃性点燃）、`igniteCellChance`、`explosionSnowMeltAmount`、`isExplosive` 系列、mote/fleck 特效字段。
- **类比结论**：水下火焰应无法存在（Fire 本身是否能在水下生成需查 Fire/GenSpawn 条件，本片未验证）；自定义水下伤害 Def 时把 ignite* 置 0、`explosionCellFleck` 换成气泡类特效即可。DamageWorker_Extinguish（灭火伤害类型）说明"灭火"本身就是一种伤害实现，排水也可类比。

## 七、基础设施（海水网格的存储模板）

### BoolGrid.cs / ByteGrid.cs
- 标准的全图逐格 bool/byte 存储：`IExposable`、按 `CellIndicesUtility.CellToIndex` 索引、BoolGrid 带 TrueCount 缓存。
- **类比结论**："海水格"（每格水量/是否淹没）最简实现就是一张 ByteGrid（0-255 水位）；真空网格大概率也是类似结构（具体实现不在本片，未验证）。

### AnimalPen 家族（PASS，但留一句）
- AnimalPenManager/各 Calculator 实现"围栏圈养+封闭性判定+食物统计"。水生养殖若走"围网海域"概念，封闭性判定（AnimalPenEnclosureCalculator 基于 Region 泛洪）是现成的；但围栏设施本身要求陆地建造，直接用会有地形冲突，故整体 PASS，仅记录其 Region 泛洪封闭判定算法可复用。

## 未验证事项（留给其它分片）
- 真空网格本体（VacuumGrid 或 Room.vacuum 的计算处）不在本片。
- 屋顶是否参与真空/海水隔绝，需查 RoofGrid 与真空模拟的交互。
- Fire 的生成条件是否检查地形/真空，需查 Fire.cs / FireUtility。
- `GenTemperature.PushHeat` 在真空格的行为（决定"海水区不可加热"怎么实现）。
