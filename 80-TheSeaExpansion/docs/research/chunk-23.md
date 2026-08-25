# chunk-23 调研发现（RimWorld 组：Room* / Royal* / Scen* / Ship* / Signal* 家族）

## TL;DR

- **季节性洪水系统（Flood/SeasonalFlood）是"舱室进水"的直接模板**：洪水以临时地形（`SetTempTerrain(ShallowFloodwater)`）从河岸扩散、定时消退、气温低于 -7°C 直接冻结消失。海底殖民地"破洞进水/排水"可整套借用 tempTerrain 机制。（`SeasonalFlood.cs`）
- **Substructure（地基）体系确认存在且独立于地形**：`terrainGrid.FoundationAt(pos).IsSubstructure` 是 Odyssey 在不可建造地形（水面/真空）上铺建筑平台的机制，渲染层还有专门的 IndoorMask/GravshipMask shader。海底建筑平台直接对标。（`SectionLayer_GravshipHull.cs`、`SectionLayer_SubstructureProps.cs`）
- **场景系统（ScenPart）提供了海洋开局所需的全部挂点**：强制地图生成器+星球层（`ScenPart_ForcedMap`）、永久 GameCondition（`ScenPart_PermaGameCondition`，深海黑暗/低温可直接挂）、屏蔽冲突事件（`ScenPart_DisableIncident`，屏蔽热浪/寒流）、屏蔽地图生成步骤（`ScenPart_DisableMapGen`）、星球层配置（`ScenPart_PlanetLayer`）。
- **皇室援助有温度门槛，海底恒温 4°C 是冲突点**：`RoyalTitlePermitWorker.TemperatureIsAcceptable` 用 `faction.def.allowedArrivalTemperatureRange`（外扩 4°C）对比 `map.mapTemperature.SeasonalTemp`，不满足则援助不可用（"BadTemperature"）。需逐一核对各派系温度区间是否覆盖 4°C。（`RoyalTitlePermitWorker.cs`、`RoyalTitlePermitWorker_CallAid.cs`）
- **厚岩顶挡轨道打击、自然顶/厚顶挡穿梭机**：海底基地若头顶有厚岩层（海床岩层），轨道打击类援助/袭击天然被挡——"深度即护甲"的设计可以直接利用现有 roof 判定。（`RoyalTitlePermitWorker_OrbitalStrike.cs`、`RoyalTitlePermitWorker_CallShuttle.cs`、`ShipLandingArea.cs`）
- **雨中短路机制可类比为"水下/潮湿环境电气短路"**：`ShortCircuitUtility.TryShortCircuitInRain` 检查 `CompPowerTrader.Props.shortCircuitInRain` 并引发火焰爆炸，是"进水舱室电气设备短路"的现成模板。（`ShortCircuitUtility.cs`）
- **废墟"破洞房间"生成器**：`RoomPart_Breached` 在布局房间某面墙上炸开缺口+径向损伤+瓦砾污物，是"失压进水的海底设施/沉船遗迹"的直接生成模板。（`RoomPart_Breached.cs`）

## 水/洪水机制

### SeasonalFlood.cs（NOTE，强相关）
`SeasonalFlood : Flood`（Odyssey 限定）。关键事实（读到的）：
- 初始格选取：所有 `terrain.IsRiver` 且与非河岸相邻的格，每格带随机 `FloodWidthRange` 宽度。
- 扩散：`SpreadFlood` 不生成 Thing，而是 `terrainGrid.SetTempTerrain(cell, TerrainDefOf.ShallowFloodwater)`，并 `tempTerrain.QueueRemoveTerrain(...)` 定时恢复原地形。淹没 4~6 天（240000~360000 ticks）。
- 冻结：Tick 中若 `mapTemperature.OutdoorTemp < -7°C` 则整个洪水事件直接销毁（"洪水已冻结"消息）。

海洋类比：**"进水"不必做成气体/Thing 系统，临时地形 + 定时移除就是官方做法**。海底基地破洞进水可以：破洞→邻格 SetTempTerrain 为"淹没"地形→排水阀/泵移除时 QueueRemoveTerrain。另外"<-7°C 冻结"说明官方对低温水体有特殊分支——海底 4°C 不会触发，但这个分支证明温度-水体交互有先例。Flood 基类不在本片，未验证其扩散算法细节。

### SectionLayer_BridgeProps.cs（NOTE，借用）
桥的渲染层：桥可架在 Impassable 地形上（`passability == Impassable || SupportsStructureType(Bridge.terrainAffordanceNeeded)`），且 Odyssey 为桥提供了**太空变体贴图**（`def.spaceBridgePropsLoopGraphic`，`UseSpaceGraphics` 切换）。类比：海底走道/栈桥可用同一套"桥 + 环境变体贴图"机制，给桥 def 加"水下变体贴图"即可。

## 建筑平台 / 气密（Odyssey Substructure）

### SectionLayer_GravshipHull.cs、SectionLayer_SubstructureProps.cs（NOTE，直接相关）
两个都是渲染层（SectionLayer），但证实了以下机制存在：
- `TerrainGrid.FoundationAt(pos)` 返回"地基"地形，`TerrainDef.IsSubstructure` 标记——Odyssey 的 Substructure 是铺在水面/真空等不可建造地形上的建筑地基，与表层地形分离存储。
- `SectionLayer_GravshipMask.IsValidSubstructure(c)` 静态判定（本片未见实现，未验证）。
- `ShaderDatabase.IndoorMaskMasked` / `GravshipMaskMasked` shader：船体角件根据是否屋顶覆盖（`c.Roofed(map)`）切换室内遮罩渲染——即"室内外观区分"在渲染层就有支持。
- SubstructureProps 层为地基边缘画 O/U/角/平边收边条（`Terrain/Surfaces/Substructure/SubstructureBorder_*`），说明地基有完整的边缘美化体系。

海洋类比：海底殖民地"在海床上铺设可建平台"应直接复用 Substructure 地基体系而非新造地形系统； gravship 船体=气密外壳的渲染/遮罩方案也可照搬到"耐压壳体"。

## 场景系统（海洋开局的挂点）

### ScenPart_ForcedMap.cs（NOTE，直接相关）
场景部件：`PostWorldGenerate` 中把 `GameInitData.startingTile` 强制设为指定 `PlanetLayerDef` 上最近的可用格，并强制 `mapGeneratorDef`。即"海洋格开局"可以纯 Def 实现：自定义 MapGeneratorDef（海底地图）+ 指向海洋层/海洋格的 ScenPart_ForcedMap。

### ScenPart_PermaGameCondition.cs（NOTE，直接相关）
`GenerateIntoMap` 中 `GameConditionMaker.MakeConditionPermanent(gameCondition)` 注册永久 GameCondition，候选过滤 `d.canBePermanent`。深海"永久黑暗/低光照"只要定义一个 canBePermanent 的 GameConditionDef 即可用现有场景部件挂载，无需新代码。

### ScenPart_DisableIncident.cs / ScenPart_DisableMapGen.cs（NOTE，冲突屏蔽）
- `ScenPart_DisableIncident : ScenPart_IncidentBase`：场景级屏蔽指定事件——海底屏蔽热浪/寒流/日蚀等温度、光照事件的官方通道。
- `ScenPart_DisableMapGen`：屏蔽指定地图生成步骤（如 scatter 陆生动物），海洋地图生成器可借此关掉陆生内容。

### ScenPart_PlanetLayer.cs（NOTE，弱相关）
Odyssey 星球层的场景编辑部件：layer + settingsDef + tag + 层间连接（`LayerConnection`）。读了一半（前 120/392 行），主要看到了配置 UI 和序列化；说明"层"是场景一等公民。若海洋做成独立星球层（而非 Surface 上的水格），这是配置入口。后半未细读，未验证。

## 皇室援助 / 轨道与空投（冲突+借用）

### RoyalTitlePermitWorker.cs（NOTE，冲突类）
基类 `AidDisabled_NewTemp`：`temperatureMatters && !TemperatureIsAcceptable(map, faction)` → 援助被禁，原因 "BadTemperature"。判定：`faction.def.allowedArrivalTemperatureRange.ExpandedBy(-4f).Includes(map.mapTemperature.SeasonalTemp)`。
海底恒温 ~4°C：若某派系 allowedArrivalTemperatureRange 下限高于 0°C（如部落可能无所谓，帝国需查 Def），则该派系援助在海底永久不可用。**需要核对各 FactionDef 的温度区间**（本片不含 Def XML，未验证具体数值）。

### RoyalTitlePermitWorker_CallAid.cs（NOTE）
`RoyalAid.overrideAcceptableTemperatureRange`（FloatRange?）可覆盖上述温度判定；另外空投格要求 `DropCellFinder.CanPhysicallyDropInto(..., canRoofPunch: true)`。说明每个援助 permit 可以单独豁免温度——海洋适配时不必改基类，改 Def 即可。

### RoyalTitlePermitWorker_OrbitalStrike.cs / CallShuttle.cs（NOTE，借用）
- 轨道打击：目标格 `roof.isThickRoof` 则不可选（但 `temperatureMatters: false`——轨道打击不受温度限制）。
- 穿梭机：`roof.isNatural || roof.isThickRoof` 阻挡降落点。
类比："海床上方有厚岩层/足够水深 → 轨道武器与空投不可用"，深度防护可直接映射到现有 roof 体系；反过来 Mod 若想表现"深水阻挡轨道打击"，可在 roof 判定旁加水深判定。

### ShipLandingArea.cs（NOTE，借用）
降落区清空判定：屋顶（任何 roof）阻挡、非 Pawn 的占位物/Skyfaller 阻挡、`beacons[0].Props.landingAreaTerrainSupport`（TerrainAffordanceDef）地形支持度检查。水下着陆/潜艇泊位：地形 affordance 检查是现成的"只允许落在特定海底地形"挂点。

## 电气 / 潮湿

### ShortCircuitUtility.cs（NOTE，借用）
- `TryShortCircuitInRain(Thing)`：`CompPowerTrader.PowerOn && Props.shortCircuitInRain`，或电池存电 >100 → 火焰爆炸（半径 1.9）+ 信件。即"潮湿环境电器短路"已是参数化机制（per-ThingDef 开关 `shortCircuitInRain`）。
- 电池联网短路：按 `Sqrt(总储能)*0.05`（clamp 1.5~14.9）算爆炸半径。
海洋类比：进水舱室内的通电设备短路，直接复用 `shortCircuitInRain` 式开关 + 自定义触发源（淹没地形判定）即可，数值公式可照搬。

## 地图生成 / 废墟

### RoomPart_Breached.cs（NOTE，直接相关）
`FillOnPost` 的房间部件：选一面不邻接"不可拆墙房间"的边墙，中段 2~4 格直接 Destroy、两端角格打到 40%~80% HP，再对缺口周围径向 2~4 格内建筑打折 HP，最后撒 `Filth_RubbleBuilding` 两团 + `Filth_BlastMark`。是"海底失压设施/沉船残骸"房间生成的直接模板——破洞+损伤+瓦砾三步走。

### RoomPart 家族其余（PASS）
AncientEngine/AncientWeaponStorage/Barricades/CenterSpace/ConnectConduits/CornerThing/Corpse/Crate/Dormant*Cluster/FillPadding/GestationTanks/Gore/InsectHive/SentryDrone 均为布局房间的物品/威胁填充器，机制通用（找随机格→生成），无海洋特异说法。ConnectConduits 会把房内发电机和用电器用 HiddenConduit 连起来再断开用户侧——海底遗迹"断电的旧电网"氛围可用，但属通用技巧。

### RoomRequirement_TerrainWithTags.cs（NOTE，借用）
房间要求：房间内**每格**地形的 `tags` 必须命中指定 tag 集合。类比：可做"必须整体建在海床沉积物上"的特殊房间/建筑要求（如水培舱必须落在海底热泉地形 tag 上）。

### RoomRoleWorker / RoomStatWorker 家族（PASS）
房间职能打分与房间属性统计，全部为通用计数逻辑；`RoomStatWorker_Space` 对 `PsychologicallyOutdoors` 房间直接给 350 分——海底"露天"（非气密）区域不会被当房间，无需处理。

## 袭击相关

- `SappersUtility.cs`（PASS）：sapper 资格=canBeSapper+拆建筑武器+采矿≥4。纯 AI 资格判定，无地形/水体交互。
- `SiegeBlueprintPlacer.cs`（PASS）：围攻炮兵选址要求可达、无屋顶。海底围攻如何发生属事件层问题，本片无特殊机制。
- `SignalAction_Infestation.cs`（PASS）：任务信号触发虫巢，`ignoreRoofedRequirement` 字段存在；虫巢=陆生威胁，海洋语境应整体替换为水生威胁，无借用价值。
- `SignalAction_Ambush/Delay/DormancyWakeUp/Incident`（PASS）：任务脚本信号动作，通用。

## 其他读过但无海洋价值（PASS 要点）

- `SeasonUtility.cs`：季节纯由纬度+年进度曲线算出（含 PermanentSummer/Winter）；海底"无季节"需要在上层（温度/事件）屏蔽，季节系统本身不用改。
- `RottableUtility.cs`：腐败阶段查询工具，无温度/水逻辑（逻辑在 CompRottable，不在本片）。
- `Scenario.cs/ScenarioMaker.cs` 等场景框架、`Selector.cs` 选择器、`Screen_Credits.cs`、`ShipJob*` 运输船状态机、`ShipUtility.cs`（结局飞船部件完整性检查）、`ShuttleIncoming.cs`、`RoyalTitle*` 其余（头衔数据/继承/食物要求）——均为通用框架，类比不出海洋特异说法。
