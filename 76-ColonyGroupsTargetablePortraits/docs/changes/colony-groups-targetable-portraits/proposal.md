---
status: draft
created: 2026-07-29
---
# Colony Groups 头像可作为能力选目标入口

## 为什么（动机、背景）

[LTO] Colony Groups（packageId `DerekBickley.LTOColonyGroupsFinal`，程序集 `TacticalGroups.dll`）替换了
vanilla 的顶部 ColonistBar，但它替换后头像栏失去了一个 vanilla 能力：在能力（如 Anomaly 异常医术）
进入选目标模式时，直接点击头像完成对对应小人的目标选择。

根因（已通过反编译核实，RimWorld 1.6）：

- 选目标由 `RimWorld.Targeter` 驱动，其 `CurrentTargetUnderMouse` 首先调用
  `RimWorld.ColonistBar.TryGetEntryAt(UI.MousePositionOnUIInverted, out entry)` 询问鼠标是否在头像上，
  命中后经 `ValidateTarget → OrderForceTarget → StopTargeting` 完成确认（含高亮、音效、shift 连选）。
- TG 用 Harmony prefix 顶掉了 `ColonistBar.ColonistBarOnGUI` 等绘制方法，但**没有 patch
  `TryGetEntryAt`**，命中测试仍使用 vanilla ColonistBar 的 `cachedDrawLocs`（不再更新），故点头像永远落空。
- [NL] Dynamic Portraits 之所以兼容，是因为它只 transpile `ColonistBarColonistDrawer.DrawColonist`
  的绘制、不改布局，vanilla 命中逻辑天然有效。

TG 侧已有的现成资产（均为 public）：

- `TacticUtils.TacticalColonistBar`（静态字段）→ `TacticalColonistBar` 实例
- `TacticalColonistBar.TryGetEntryAt(Vector2, out Entry)`：主栏头像命中测试（`DrawLocs` + `Entries`）
- `TacticalColonistBar.TryGetGroupPawnAt(Vector2, out Pawn)`：悬停展开的组弹窗小头像命中测试
  （基于 `ColonistGroup.pawnRects`，弹窗关闭时自动不命中）

组弹窗（悬停展开的小头像网格）不是 `Verse.Window`，在 BeforeMainTabs 阶段由 IMGUI 直接绘制，
单击左键不被任何人消费，事件可直达 `Targeter.ProcessInputEvents`。

## 做什么（范围，含明确不做的部分）

新建 MOD `76-ColonyGroupsTargetablePortraits`（沿用仓库编号惯例）。
packageId `RunningBugs.ColonyGroupsTargetablePortraits`（沿用仓库 `RunningBugs.*` 惯例），
AssemblyName/RootNamespace `CGTargetablePortraits`。

当前仅构建并声明 1.6（`supportedVersions` 只标 1.6 是为了简单）。跨版本兼容性已核实：
方案所依赖的游戏 API（`ColonistBar.TryGetEntryAt` 签名、`Entry(Pawn, Map, int)` 构造器）在
1.0→1.6 逐字节一致；TG 侧三个入口（`TacticUtils.TacticalColonistBar`、`TryGetEntryAt`、
`TryGetGroupPawnAt`）在 TG 的 1.2–1.6 五个 DLL 中同名同签名。**同一套源码无需修改即可在
1.2–1.6 编译通过**；将来要支持旧版本，只需增加对应版本目录、引用对应版本的 Krafs.Rimworld.Ref
与 TG DLL，并加版本标签，无需改动代码。旧版本实际生效与否需游戏内冒烟测试
（1.6 已确认 `Targeter` 会查询 `ColonistBar.TryGetEntryAt`；旧版 Targeter 行为未从方法体验证，
若不查询则 patch 无害但不生效）。

实现方式：一个 Harmony prefix patch，目标 `RimWorld.ColonistBar.TryGetEntryAt(Vector2, out ColonistBar.Entry)`：

1. 取 `TacticUtils.TacticalColonistBar`，为 null 则放行原逻辑（兜底）；
2. 调 TG `TryGetEntryAt` 命中主栏头像 → 构造 `new ColonistBar.Entry(tgEntry.pawn, tgEntry.map, tgEntry.group)`，
   返回 true，阻断原方法；
3. 未命中则调 TG `TryGetGroupPawnAt` 命中悬停组弹窗头像 → 构造 `new ColonistBar.Entry(pawn, pawn.Map, 0)`，
   返回 true，阻断原方法；
4. 都不命中 → 返回 false，阻断原方法（TG 环境下 vanilla 命中测试本就无意义）。

其余行为（目标校验与提示、悬停高亮、确认音效、shift 连选、Esc/右键取消）全部由 vanilla `Targeter` 自动完成。

工程约定（照搬 51-RACursePatch）：

- `About/About.xml`：`supportedVersions` 1.6；`loadAfter`: `brrainz.harmony`、`DerekBickley.LTOColonyGroupsFinal`
- `1.6/Source/mod.csproj`：net472 + Krafs.Publicizer 2.* + Krafs.Rimworld.Ref 1.6.4488-beta +
  Lib.Harmony 2.3.*（本 MOD 不用 HugsLib API，不引用）；
  以 `<Reference>` + `<Private>false</Private>` 引用拷入 Source 目录的 `TacticalGroups.dll`（仅编译期，不分发）
- 编译输出至 `1.6/Assemblies/`
- 部署：软链接 `~/mine/workspace/rimworld/RimModNotes/76-ColonyGroupsTargetablePortraits`
  → `/Data/SteamLibrary/steamapps/common/RimWorld/Mods/76-ColonyGroupsTargetablePortraits`

明确不做：

- 不支持右键组图标打开 `MainFloatMenu` 菜单期间的弹窗头像选目标（vanilla `Window` 会在
  WindowStack 阶段吞掉所有 MouseDown；如需覆盖须另加对 `ColonistGroup.DrawColonist` 的
  targeting 分支并手动复制 vanilla 确认流程——已评估为不值得）
- 不支持世界地图能力（`WorldTargeter` 是另一套链路）
- 不写自动化测试（UI 交互类 patch，与仓库其他 MOD 一致，进游戏手动验证）

验证清单（游戏内）：

1. 激活异常医术 → 点顶部头像 → 正确对该小人排队施法
2. 悬停展开组弹窗 → 点弹窗小头像 → 同样生效
3. shift + 点头像 → 连续多目标选择（MultiSelect 能力）
4. Esc / 右键 → 取消选目标
5. 非选目标状态下，头像原有点击行为（选中、双击跳镜头、右键菜单、拖拽排序）不受影响

## 待定问题

无。
