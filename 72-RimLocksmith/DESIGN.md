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

第一版只控制“关闭的殖民地门是否允许某类 Pawn 主动开门”。

- 门已打开 / FreePassage：不干涉原版通行。
- 用户配置允许敌人：敌人可以打开关闭的殖民地门。
- 用户配置禁止殖民者：殖民者不能打开关闭的殖民地门。
- 默认 preset 参考原版语义，但配置后可覆盖原版。

第一版不做独立 `CanPass` 配置；UI 语义统一为“允许开门”。

## 源码依据

RimWorld 1.6 关键源码点：

- `RimWorld/Building_Door.cs:428-459`：`PawnCanOpen` 先检查 `p.CanOpenDoors`、`CanOpenAnyDoor`、动物/fence 逻辑；门 `Faction == null` 时使用 `p.RaceProps.canOpenFactionlessDoors`；玩家门/派系门最后通过 `GenAI.MachinesLike(base.Faction, p)` 判断。
- `Verse.AI/GenAI.cs:9-24`：`MachinesLike` 会拒绝敌对 faction pawn，也会拒绝被该 faction 关押的囚犯；非敌对 visitor/ally/trader 通常允许。
- `Verse/Pawn.cs:481-490`：`IsColonist` 要求玩家 faction、人类、非不安全奴隶。
- `Verse/Pawn.cs:587-596`：`IsPrisonerOfColony` 来自 `guest.IsPrisoner && guest.HostFaction.IsPlayer`。
- `Verse/Pawn.cs:611-620`：`IsSlaveOfColony` 来自 `IsSlave && Faction.IsPlayer`。
- `Verse/Pawn.cs:757-770`：`IsColonyMech` 要求 Biotech、mechanoid、玩家 faction、无 mental state；因此敌对/异常机械体不进入 colony mech 默认允许类别。

因此第一版默认 preset 采用“正常殖民者/奴隶/殖民地机械体/访客/盟友/商队允许，囚犯/敌人/野生动物禁止”的原版近似语义；但玩家显式配置后，RimLocksmith 可以覆盖关闭殖民地门的开门权限。

注意：源码调研显示，普通玩家 faction 动物在 `GenAI.MachinesLike(Faction.OfPlayer, pawn)` 层面通常会被原版允许开殖民地门，另有 `FenceBlocked` / `roamerCanOpen` 例外。为了与原版完全一致，RimLocksmith 默认允许殖民地动物，但额外保留原版的 `FenceBlocked && !roamerCanOpen` 限制：畜栏动物不能自己开普通门。

## 动物、畜栏与普通门

原版畜栏有两套相关逻辑：

1. 畜栏封闭性计算：`Verse/AnimalPenEnclosureCalculator.cs:49-61` 中 `RoamerCanPass(Building_Door door)` 在门不是 `FreePassage` 时只看 `door.def.building.roamerCanOpen`。普通门没有 `roamerCanOpen`，所以被视为畜栏边界；`AnimalFlap` 在 XML 中有 `<roamerCanOpen>true</roamerCanOpen>`，所以可被畜栏动物通过。
2. 动物实际开门：`Building_Door.PawnCanOpen` 有 `p.RaceProps.FenceBlocked && !def.building.roamerCanOpen` 检查。需要畜栏管理的 roamer/fence-blocked 动物不能自己开普通门；如果被能开门的 pawn 牵着，则递归检查牵绳 pawn，可通过普通门。

Locks2 的问题是它用 Prefix 完全接管 `PawnCanOpen`，且动物规则默认允许殖民地动物，于是绕过了原版 `FenceBlocked && !roamerCanOpen` 检查：畜栏系统仍认为普通门是边界，但动物实际寻路认为自己能开门，导致动物跑出只有普通门的房间。

RimLocksmith 为了更贴近原版，即使用户把“殖民地动物”设为允许，也仍保留这条原版限制：`FenceBlocked` 动物只能自己开 `roamerCanOpen` 门；普通门需要被能开门的 pawn 牵着才允许通过。

## 默认规则开关

参考 Locks2 的基础规则类别，第一版提供：

- 殖民者：默认允许
- 奴隶：默认允许；叛变/敌对奴隶按分类进入敌对逻辑
- 囚犯：默认禁止
- 殖民地动物：默认允许；但 FenceBlocked 畜栏动物仍不能自己开普通门，只能开 roamerCanOpen 门或被能开门的 pawn 牵过普通门
- 殖民地机械体：默认允许
- 访客：默认允许
- 盟友：默认允许
- 商队：默认允许
- 敌人：默认禁止
- 野生动物：默认禁止
- 其他：默认禁止

测试必须覆盖所有类别，并覆盖“默认”和“用户显式覆盖”两种行为。

## Mod 设置页

设置页不涉及当前选择对象，只提供全局操作：

- 默认规则 preset 设置；
- 新建门是否使用默认规则；
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
