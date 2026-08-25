# RimLocksmith 边缘锁匠 - 方案固定文档

## 目标

RimLocksmith 边缘锁匠是一个 RimWorld 1.6 门锁/门禁 Mod，用来替代 Locks2 的常用功能，并修复其已知问题。

核心目标：

1. 功能范围与 Locks2 大体一致，第一版先覆盖基础群体开关、默认配置、批量配置、内置汉化。
2. 修复 Locks2 `LockComp.CompGetGizmosExtra()` 在 `config == null` 时抛 `NullReferenceException` 的架构问题。
3. 默认配置尽量贴近游戏原版门权限；但玩家显式配置后，由 RimLocksmith 接管关闭门的开门权限。
4. 已打开的门不干涉，保持游戏原版“开着的门可自由通过”的行为。
5. 非殖民地门完全旁路，不显示配置，不改开门/寻路逻辑。

## 名称

- Mod 名称：`RimLocksmith 边缘锁匠`
- 目录：`72-RimLocksmith`
- packageId：`runningbugs.rimlocksmith`
- 程序集/根命名空间：`RunningBugs.RimLocksmith`

## 兼容策略

不做运行时 ThingDef 扫描，不猜测某个 ThingDef 是否“像门”。

第一版只通过 XML 显式给门 ThingDef 加 comp 和 ITab，覆盖 Locks2 已覆盖的门类：

- 原版 `Building_Door`
- 原版 `Building_MultiTileDoor`
- Doors Expanded: `DoorsExpanded.Building_DoorExpanded`
- Linkable Doors: `LinkableDoors.Building_LinkableDoor`
- Architect Expanded Fences: `BuildLib.Building_Gate`
- Save Our Ship: `Building_ShipAirlock`

将来要支持更多 Mod 门时，显式增加 XML patch，不引入自动猜测。

## 所有权边界

只有 `door.Faction == Faction.OfPlayer` 的门算“殖民地门”。

非殖民地门，包括 factionless 门：

- 不显示 RimLocksmith tab；
- 不初始化默认配置；
- 不参与设置页 apply all；
- Harmony patch 直接旁路，保持原版逻辑；
- 即使历史上保存过 RimLocksmith comp，也不生效。

## 权限语义

只控制“关闭的殖民地门是否允许某类 Pawn 主动开门”。

- 门已打开 / FreePassage：不干涉原版通行。
- **玩家派系单位由设置项门控**：设置项「管理殖民地单位」（`ManageColonyPawns`，默认启用）开启时，殖民者/奴隶/殖民地动物/殖民地机械体四类开关出现在门配置中，可逐类收窄；关闭时玩家派系单位（含精神崩溃、叛变中的单位）一律免管，跟随原版——原版已有对应控制机制：禁用门（`ForbidUtility.CaresAboutForbidden` + `Building_Door.IsForbiddenToPass`，覆盖殖民者/奴隶/殖民地动物；精神崩溃者原版禁用门也不管，Mod 同样不碰）、区域管制、畜栏、机械师体系。
- **敌对/囚犯/无派系单位免管**：原版 `GenAI.MachinesLike` 本来就拒绝他们开殖民地门，跟随原版即可，无需开关。
- 配置禁止访客：访客、商队（含驮兽）、外来动物/机械体不能打开关闭的殖民地门。

不做独立 `CanPass` 配置；UI 语义统一为“允许开门”。

## 源码依据

RimWorld 1.6 关键源码点：

- `RimWorld/Building_Door.cs:428-459`：`PawnCanOpen` 先检查 `p.CanOpenDoors`、`CanOpenAnyDoor`、动物/fence 逻辑；门 `Faction == null` 时使用 `p.RaceProps.canOpenFactionlessDoors`；玩家门/派系门最后通过 `GenAI.MachinesLike(base.Faction, p)` 判断。
- `Verse.AI/GenAI.cs:9-24`：`MachinesLike` 会拒绝敌对 faction pawn，也会拒绝被该 faction 关押的囚犯；非敌对 visitor/ally/trader 通常允许。
- `Verse/Pawn.cs:518-528`：`IsColonist` 要求玩家 faction、人类、非不安全奴隶。
- `Verse/Pawn.cs:624-634`：`IsPrisonerOfColony` 来自 `guest.IsPrisoner && guest.HostFaction.IsPlayer`。
- `Verse/Pawn.cs:648-658`：`IsSlaveOfColony` 来自 `IsSlave && Faction.IsPlayer`。
- `Verse/Pawn.cs:794-808`：`IsColonyMech` 要求 Biotech、mechanoid、玩家 faction、无 mental state；因此敌对/异常机械体不进入 colony mech 默认允许类别。

因此 RimLocksmith 的管理面收窄为唯一的「访客」开关：非敌对 visitor/trader/商队/外来动物在原版会被允许开门，Mod 可以收窄为禁止；其余所有单位要么原版自拒（敌对/囚犯/无派系），要么原版自理（玩家派系），都无需 Mod 介入。

注意：源码调研显示，普通玩家 faction 动物在 `GenAI.MachinesLike(Faction.OfPlayer, pawn)` 层面会被原版允许开殖民地门，另有 `FenceBlocked` / `roamerCanOpen` 例外。玩家派系动物免管，这套原版行为自动完整保留。

## 动物、畜栏与普通门

原版畜栏有两套相关逻辑：

1. 畜栏封闭性计算：`Verse/AnimalPenEnclosureCalculator.cs:49-61` 中 `RoamerCanPass(Building_Door door)` 在门不是 `FreePassage` 时只看 `door.def.building.roamerCanOpen`。普通门没有 `roamerCanOpen`，所以被视为畜栏边界；`AnimalFlap` 在 XML 中有 `<roamerCanOpen>true</roamerCanOpen>`，所以可被畜栏动物通过。
2. 动物实际开门：`Building_Door.PawnCanOpen` 有 `p.RaceProps.FenceBlocked && !def.building.roamerCanOpen` 检查。需要畜栏管理的 roamer/fence-blocked 动物不能自己开普通门；如果被能开门的 pawn 牵着，则递归检查牵绳 pawn，可通过普通门。

RimLocksmith 的 postfix 只收窄架构天然保留这套原版行为：殖民地动物完全免管，畜栏/牵绳语义与原版完全一致（Locks2 的 prefix 接管曾绕过 `FenceBlocked` 检查导致动物跑出畜栏，本 Mod 无此问题）。

## 默认规则开关

- 殖民者：默认允许（受「管理殖民地单位」设置项门控）
- 奴隶：默认允许（受「管理殖民地单位」设置项门控）
- 殖民地动物：默认允许（受门控）；但 FenceBlocked 畜栏动物仍不能自己开普通门（原版限制自动保留）
- 殖民地机械体：默认允许（受门控），分全部/仅受控/禁止三档
- 访客：默认允许（恒可配置）；商队全员（商人/护卫/驮兽）与访客同属此类，外来动物/机械体同此类

以下类别均不可配置，跟随原版：

- 囚犯/敌人/野生动物/其他——原版 `MachinesLike` 本来就拒绝，等同禁止，无需开关
- 异象实体：异象控制功能暂禁用（`ANOMALY-CONTROL(disabled)` 注释保留），与普通 pawn 同等分类

测试必须覆盖所有类别，并覆盖“默认”和“用户显式覆盖”两种行为。

## 商队与访客

商队不单设类别。原版来访商队只有商人本人有 `TraderKind`，护卫/搬运工没有，驮兽更是动物——因此商队全员与访客统一归 Guest 一类，由「访客」开关一并控制。

原版 `LordJob_TradeWithColony.CanOpenAnyDoor` 对 `FenceBlocked` pawn 返回 `true`，即商队驮兽（muffalo 等）在原版可开任意门。RimLocksmith 对此做唯一例外处理：pawn 的 `CanOpenAnyDoor` 若来自商队 LordJob，不享受豁免，仍受访客开关控制；其余原版特殊放行（越狱/叛乱/远行队/野人/变异 kind）一律保留。

## 异象实体

异象控制功能暂禁用（代码以 `ANOMALY-CONTROL(disabled)` 注释保留，含设置项 `ControlAnomalies`、配置项 `AllowAnomalies`、分类分支与 UI 行；`AnomalyUtil` 类预留）。禁用期间异象实体与普通 pawn 同等分类：玩家派系食尸鬼/蹒跚怪等归入殖民者，敌对异象实体归入敌人（原版本来就拒绝；revenant 等 `canOpenAnyDoor` 的按原版特权放行）。

预留的启用语义：设置项开启后，敌对异象实体仍跟随原版；非敌对异象实体归入 Anomaly 类别，由门配置中的「异象实体」开关控制（默认允许）。

## 原版明示放行（豁免清单）

以下情形 `ShouldDeny` 一律不拦，与原版对齐：

- `pawn.CanOpenAnyDoor`：越狱/叛乱/玩家远行队/野人逃跑/变异 kind（商队 LordJob 驮兽除外，见上节）；
- `pawn.guest.Released`：被释放的囚犯离场（原版 `Building_Door.PawnCanOpen` 明示放行）；不豁免会导致禁用访客后释放的囚犯被门困住。

注：被捕的殖民者（玩家派系 + `IsPrisonerOfColony`）在 1.6 原版确实存在（逮捕不改派系，见 `JobDriver_TakeToBed.CheckMakeTakeePrisoner` → `Pawn_GuestTracker.CapturedBy`）；但原版 `MachinesLike` 对殖民地囚犯一律拒绝，分类顺序对行为无影响。

## Mod 设置页

设置页不涉及当前选择对象，只提供全局操作：

- 默认规则 preset 设置；
- 新建门是否使用默认规则；
- 是否管理殖民地单位（默认启用；禁用后玩家派系四类开关不生效，玩家单位一律遵循原版规则）；
- 应用默认到所有未设置过的殖民地门；
- 应用默认到所有殖民地门；
- 重置默认配置。

`userConfigured` 语义：

- 新建门自动使用默认配置：`false`
- 玩家在 tab 中手动修改：`true`
- 应用默认到所有未设置过的殖民地门：覆盖 `false` 的门，仍为 `false`
- 应用默认到所有殖民地门：全部覆盖，全部设为 `false`

## ITab 行为

- 单选殖民地门时显示。
- 多选时，只有当选中对象全部是门，且其中至少一扇殖民地门可配置，才显示。
- 批量设置只作用于殖民地门；非殖民地门显示为忽略数量，不修改。
- 多扇门配置不同则显示 mixed 状态；打开 tab 不自动统一，只有用户显式点击某项才批量写入。
- 从一扇殖民地门切换到另一扇殖民地门时，RimLocksmith tab 应保持打开，符合游戏内其他 tab 的默认体验。

## 未来扩展记录

第一版不做“门组/规则组”。未来可能加入命名配置：

- 用户创建多个命名 config/preset；
- 每扇门选择一个 group name；
- 修改 group 后引用它的门自动更新；
- 支持从 group 脱离并复制成独立配置。

第一版数据结构预留 `linkedPresetId`，但不启用。
