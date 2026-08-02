# RimLocksmith 原版行为对照与过度修改审查

日期:2026-08-02
版本:RimWorld 1.6(本机 `RW/16/Assembly-CSharp.dll` 反编译核实)
结论先行:**当前实现用自研的 `LockPolicy` 整体替换了殖民地门的 `PawnCanOpen`,对原版的多条"特殊放行"路径没有照搬,导致敌人和若干边缘对象的行为与原版不一致。建议改为"postfix 只收窄"架构(见第 5 节),从结构上保证永远不放大原版权限。**

## 1. 原版 1.6 开门判定(反编译事实)

`RimWorld.Building_Door.PawnCanOpen`(RimWorld/Building_Door.cs:428),判定顺序:

```
1. !p.CanOpenDoors                                    → false
2. Map.Parent.doorsAlwaysOpenForPlayerPawns
   && p.Faction == Faction.OfPlayer                   → true   (仅 2 个任务地图设置:
                                                           WorshippedTerminal 黑客站点、
                                                           Gravcore 废弃定居点)
3. p.CanOpenAnyDoor                                   → true   (见 1.2)
4. p.FenceBlocked && !def.building.roamerCanOpen
   && (!roped || !PawnCanOpen(roper))                 → false  (递归检查牵绳者)
5. door.Faction == null                               → p.RaceProps.canOpenFactionlessDoors
6. p.guest != null && p.guest.Released                → true   (被释放的囚犯自行走出去)
7. CheckFaction(门类恒定 true)                        → GenAI.MachinesLike(door.Faction, p)
8. 否则                                                → true
```

### 1.1 `GenAI.MachinesLike`(Verse.AI/GenAI.cs:9)

- 无派系 && 非人形/野人 && (HostFaction ≠ 门派系 || 是囚犯) → false
- 是囚犯 && HostFaction == 门派系 → **false(囚犯平时开不了殖民地门)**
- pawn.Faction 与门派系敌对 → **false(敌人开不了殖民地门,只能砸)**
- 其余(visitor/ally/trader/殖民者/殖民地动物/机械体等)→ true

### 1.2 `Pawn.CanOpenAnyDoor` 的来源(Verse/Pawn.cs:470)

- `WildManUtility.WildManShouldReachOutsideNow`:野人(含被释放的野人囚犯)向外走;
- `lord.LordJob.CanOpenAnyDoor(p)`,原版覆盖为 true 的 LordJob:
  - **LordJob_PrisonBreak → true(越狱时囚犯可以开任何门!)**
  - **LordJob_SlaveRebellion → true(奴隶叛乱时可以开任何门!)**
  - LordJob_TradeWithColony → `p.FenceBlocked`(贸易队受围栏阻挡的对象放行)
  - LordJob_FormAndSendCaravan → `!p.FenceBlocked`(组建远行队时殖民者开任何门);
- `IsMutant && mutant.Def.canOpenAnyDoor`(异象变异体);
- `kindDef.canOpenAnyDoor`(特定 PawnKind,XML 级)。

### 1.3 `Pawn.CanOpenDoors`(Verse/Pawn.cs:454)

只看 `mutant.Def.canOpenDoors` 与 `kindDef.canOpenDoors` 两个 XML 标志,
**不是**智力/工具使用者判定。Mod 里直接调用了它,这一层无偏差。

注意(2026-08-02 核实):`PawnKindDef.canOpenDoors` 的 C# **默认值是 `true`**,
Core 全部 XML 均未覆盖 → **动物能否开门不由这个标志限制**。真正的限制在
`MachinesLike`(无派系/敌对派系 → false)与 `FenceBlocked` 规则:
野生动物(无派系)一律开不了殖民地门;殖民地动物(玩家派系)反而能开门,
拦住畜栏动物的是 FenceBlocked。

### 1.4 `Pawn.FenceBlocked`(Verse/Pawn.cs:404)

`Roamer && (CurJobDef == null || !CurJobDef.ignoreFenceBlocked)`,
其中 `Roamer => RoamMtbDays.HasValue`,`RoamMtbDays` 又被
`HediffSet.RemoveRoamMtb`(任一 hediff stage 的 `removeRoamMtb`)抑制。
注意 `CurJobDef.ignoreFenceBlocked` 是**按当前工作动态判定**的。

### 1.5 `Verse.PathUtility.GetDoorCost`(原版)

- NoPassClosedDoors 系:FreePassage ? 0 : MaxValue;
- PassAllDestroyableThings 系:开不了 → `costBlockedDoor + HP × costBlockedWallExtraPerHitPoint`
  (即"把门当可拆毁物"的有代价穿墙,breacher 用);
- PassDoors:开不了 → **150**;
- ByPawn:开不了 → canBashDoors ? 300 : MaxValue。

## 2. Mod 当前逻辑(实际实现,非设计意图)

- XML patch 给 `Building_Door`/`Building_MultiTileDoor` 及 4 个 Mod 门类的
  ThingDef 注入 `CompRimLocksmithDoor` + ITab → **所有受支持门类一律带 comp**。
- `Patch_BuildingDoor_PawnCanOpen`(prefix)→ `RimLocksmithUtility.TryAllowsOpen`:
  殖民地门(Faction.OfPlayer)+ pawn 非空 + 门未开 + 有 comp → **完全接管**,
  用 `LockPolicy` 结果替换原版返回值。comp 没有存档配置时 `EnsureConfig()`
  现造一份默认配置 → **未配置过的门也从第一天起被接管**(DESIGN.md 写的是
  "默认贴近原版;显式配置后才接管",实现与意图不符)。
- `LockPolicy` 顺序:`!CanOpenDoors → false`;fence-blocked 规则;按
  `AccessCategory` 查 11 个布尔配置。
- `PawnAccessFactsFactory` 分类顺序:Hostile(派系敌对)> Prisoner > Slave >
  ColonyMech > ColonyAnimal(玩家派系动物)> Colonist(玩家派系)> **WildAnimal
  (其余一切动物,含商队驮兽!)** > Trader > Guest/Ally(好感 75 分界)> Other。
- `IsFenceBlockedRoamer` 是手工复制:`RaceProps.FenceBlocked`(= 定义层
  Roamer)+ 反射扫描 hediff stage 的 removeRoamMtb + 硬编码
  "SentienceCatalyst"。**漏掉了 `CurJobDef.ignoreFenceBlocked` 动态判定**;
  牵绳者只查 `roper.CanOpenDoors`,原版是递归完整 `PawnCanOpen(roper)`。
- `Patch_PathUtility_GetDoorCost`(prefix):除 NoPassClosedDoors 系和
  ByPawn+禁门 两条早退外,其余模式也套用 LockPolicy。

## 3. 偏差清单:Mod 阻止了原版明确放行的对象

以下均为"Mod 默认配置下即生效",不需要用户改任何设置:

| # | 对象 | 原版行为 | Mod 行为 | 影响 |
|---|------|----------|----------|------|
| A1 | 越狱中的囚犯 | `LordJob_PrisonBreak.CanOpenAnyDoor → true`,可以开任何门 | 分类 Prisoner,默认 `AllowPrisoners=false`,**开不了门** | 越狱形同虚设,囚犯被关死在门内 |
| A2 | 叛乱中的奴隶 | `LordJob_SlaveRebellion.CanOpenAnyDoor → true` | 默认 Slave=true 碰巧一致;但用户关掉 AllowSlaves 后叛乱奴隶也开不了门 | 用户配置会误伤原版叛乱机制 |
| A3 | 被释放的囚犯 | `guest.Released → true`,自行开门走出地图 | 分类 Prisoner(或 Hostile),默认 false,**开不了门** | 释放的囚犯卡在基地里 |
| A4 | 向外走的野人 | `WildManShouldReachOutsideNow → CanOpenAnyDoor` | 无派系 → 分类 Other,默认 `AllowOthers=false` | 野人走不出去 |
| A5 | canOpenAnyDoor 的变异体/特定 PawnKind | 原版放行 | 分类 Hostile/Other,默认 false | 异象相关敌人/单位行为被改变 |
| A6 | 商队/访客的动物 | `MachinesLike`:非敌对派系 → true(能否开还受 kindDef.canOpenDoors 约束) | 分类顺序里 `RaceProps.Animal` 先于派系访客判定 → **归为 WildAnimal**,默认 false | 商队驮兽等一律按"野生动物"处理,分类错误 |
| A7 | `CurJobDef.ignoreFenceBlocked` 的动物 | 做特定工作时不受围栏/门限制 | 手工复制的 FenceBlocked 不含工作判定 | 边缘偏差,随工作闪现 |
| A8 | doorsAlwaysOpenForPlayerPawns 任务地图 | 玩家 pawn 无条件开门 | 用户若禁殖民者则连这些地图也禁 | 边缘,仅 2 个任务地图 |

## 4. 偏差清单:Mod(经配置)放大了原版权限 + 寻路层偏差

| # | 场景 | 原版行为 | Mod 行为 | 说明 |
|---|------|----------|----------|------|
| B1 | **AllowHostiles=true** | 敌人开不了殖民地门,只能砸(MachinesLike) | 敌人直接开门进入 | **用户本次反馈的问题**:设置后敌人行为被改变 |
| B2 | AllowPrisoners=true | 囚犯平时开不了门(仅越狱时能开) | 囚犯随时开门 | 同类问题 |
| B3 | GetDoorCost,PassAllDestroyableThings(breacher 拆毁寻路) | 开不了 → `costBlockedDoor + HP×系数`(有代价) | LockPolicy 拒绝 → 300 或 **MaxValue**(视为不可通过) | breacher 把殖民地门当墙,拆墙路线选择被改变 |
| B4 | GetDoorCost,PassDoors | 开不了 → 150 | 拒绝 → 300/MaxValue | 少量寻路代价漂移 |

无偏差的类别(默认配置下与原版一致,可配置收窄是 Mod 的正当功能):
Colonist、Slave(默认)、ColonyAnimal、ColonyMechanoid、Guest、Ally、Trader(人形)、
WildAnimal(原版本来就被 MachinesLike 的无派系/敌对规则拦住,开不了殖民地门)。

### 4.1 野生动物的原版行为核实(2026-08-02 补充)

问题:野生动物(尤其 manhunter 狂暴动物)在原版能否自己开殖民地门?

结论:**不能,跟随原版是安全的**,依据:

1. `PawnKindDef.canOpenDoors` C# 默认值为 `true`,Core XML 全部未覆盖
   → 动物开不开门不由此标志决定;
2. 野生动物无派系 → `MachinesLike` 第一条(无派系 && 非人形 &&
   HostFaction ≠ 门派系 → false)拦死;昆虫等有 hostile 派系的被敌对规则拦死;
3. manhunter 没有专属的 `LordJob.CanOpenAnyDoor` 覆盖(原版仅 PrisonBreak /
   SlaveRebellion / TradeWithColony / FormAndSendCaravan 四个),
   狂暴动物同样只能砸门。

因此 WildAnimal 列入"跟随原版"白名单外类别(第 5 节),即使保留
`AllowWildAnimals=true` 的旧配置也不再生效,狂暴症不会因此获得开门能力。

## 5. 建议的修正设计:postfix + 只收窄 + 类别白名单

**核心原则:Mod 只能在原版结果上收窄权限,永远不许放大;只有殖民地内部类别可配置,其余一律跟随原版。**

1. `PawnCanOpen` 改 **postfix**:`__result = __result && PolicyAllows(category, config)`。
   - 原版的 CanOpenAnyDoor / guest.Released / doorsAlwaysOpen / FenceBlocked /
     MachinesLike 全部自动保留,A1–A8 一次性清零,且永远不怕 1.7 再改判定顺序
     (消除手工复制原版逻辑的全部漂移面,`IsFenceBlockedRoamer` 整个可删)。
   - Hostile 原版必为 false → 敌人行为**结构性**回到原版(B1 消除)。
2. 类别白名单:仅 `Colonist / Slave / ColonyAnimal / ColonyMechanoid /
   Guest / Ally / Trader` 参与配置;`Hostile / Prisoner / WildAnimal / Other`
   不查配置,直接放行原版结果。
   - 同时修掉 A6:判定动物类别前先判"非敌对派系访客",或按 lord/TraderKind
     修正商队动物归类。
3. `GetDoorCost` patch:ByPawn 模式下 vanilla 内部会回调已被 postfix 收窄的
   `door.PawnCanOpen`,理论上可以**整个删除**;PassDoors/PassAllDestroyableThings
   不再被误伤(B3/B4 消除)。删除前需验证:被拒殖民者的区域可达性
   (reachability)与`NotifyChanged` 清缓存行为不变。
4. 配置兼容:`AllowHostiles/AllowPrisoners/AllowWildAnimals/AllowOthers`
   四个布尔字段保留在 LockConfigData 与 scribe 中(旧存档不炸),UI 移除,
   代码不再读取。
5. 白盒测试(tests/)按新语义重写:LockPolicy 输入从"绝对判定"改为
   "在原版结果上收窄"。

## 6. 待验证清单(修正实施后)

- 越狱囚犯能开殖民地门;释放囚犯能自行走出去;野人能走出去。
- 敌人(含 canOpenAnyDoor 变异体)行为与未装 Mod 完全一致(开不了就砸)。
- 商队人形可进、驮兽表现与原版一致。
- 被拒殖民者:门视为不可通过、自动绕行,无误报"无法到达"。
- DoorsExpanded 等 Mod 门:postfix 叠加在其 PawnCanOpen 之上同样只收窄。
