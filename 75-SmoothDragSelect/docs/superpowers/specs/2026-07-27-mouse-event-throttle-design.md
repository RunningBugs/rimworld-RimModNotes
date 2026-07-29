# Mouse Event Throttle 设计文档

日期：2026-07-27
状态：已批准（范围 A：仅事件限流）

## 问题

游戏后期单位多、按住左键拖框选择时，白框一卡一卡；拖拽期间 OnGUI 系 FPS 计数器显示 900+。

## 根因（源码确认，游戏版本 1.6.4871）

1. 高回报率鼠标（500–1000Hz）在拖动/移动时每秒产生数百个 `MouseDrag`/`MouseMove` 事件。
2. Unity IMGUI 对每个事件都完整调用一遍 `Root.OnGUI → UIRoot_Play.UIRootOnGUI`，整棵 UI 树（殖民者栏、主按钮栏、窗口栈、MOD 窗口等）逐事件执行。每次 1–2ms × 900 次/秒即可吃光主线程，真实渲染帧率崩塌，白框（仅 Repaint 上屏）随之卡顿。
3. OnGUI 系 FPS 计数器统计的是每秒 OnGUI 调用次数，被事件流刷到 900+，造成"高 FPS 却卡"的假象。
4. 佐证：原版 `AlertsReadout.AlertsReadoutOnGUI`、`MouseoverReadout`、`ThingOverlays`、`BeautyDrawer`、`DispenseAllThingTooltips` 均已自行过滤到 Repaint；未过滤的重量级 handler 仅剩 `ColonistBar.ColonistBarOnGUI` 与 `MainButtonsRoot.MainButtonsOnGUI`。
5. 已排除：原版 Selector 拖拽期间只画框不做选择计算；Achtung/TacticalGroups 等 MOD 无拖拽期重计算；FPS Stabilizer 只改 TickManager 帧预算常量，与本问题无关；Player.log 无每帧异常。

## 方案（范围 A）

对未做事件过滤的重量级 OnGUI handler 加 Harmony 前缀：当 `Event.current.type` 为 `MouseDrag` 或 `MouseMove` 时跳过本次执行（return false）。视觉更新由每帧必到的 Repaint 事件承担，功能无损。

目标方法：

- `RimWorld.ColonistBar.ColonistBarOnGUI` — 跳过条件再加 `!ReorderableWidget.Dragging`（殖民者栏拖拽重排需要 MouseDrag 事件）。
- `RimWorld.MainButtonsRoot.MainButtonsOnGUI` — 直接跳过；热键走 KeyDown 事件，不受影响。

明确不做（用户已裁量）：

- 不动 `Selector.HandleMapClicks` 等输入路径（保证功能正确性）。
- 不动 `WindowStackOnGUI`（避免影响拖窗口/滚动条；若验证后仍有残余卡顿，可用 `Selector.dragBox.active` 作为安全守卫再加）。
- 不做松手后的事件时间戳取点修正（原方案 B）。
- 不做后台线程/分帧选择计算。

## 诊断输出

为便于游戏内验证，MOD 在拖拽结束后向日志写一行汇总：拖拽时长、OnGUI 通过次数/秒、真实渲染帧数/秒、被跳过的通过次数。实现：

- `UIRoot_Play.UIRootOnGUI` 前缀计数 OnGUI 通过次数；
- `Verse.Root.Update` 后缀计数真实帧；
- 在 `Selector.dragBox.active` 期间累计，松开左键且拖拽超过 1 秒时 `Log.Message` 一行。

## 交付

- 目录 `75-SmoothDragSelect/`（显示名 Smooth Drag Select；About/About.xml、1.6/Source、1.6/Assemblies），结构仿 `14-SurgeryNeverFail`。
- csproj：net472，`Krafs.Rimworld.Ref 1.6.4871`（与游戏本体一致），`Lib.Harmony 2.3.1.1`。
- 编译后软链接到 `/Data/SteamLibrary/steamapps/common/RimWorld/Mods/75-SmoothDragSelect`。

## 验证

1. 游戏内启用 MOD，进后期存档；
2. 按住左键连续拖框：白框应明显变顺滑，松开后日志出现拖拽汇总行；
3. 殖民者栏拖拽重排、底部按钮、双击选中等功能回归测试。

## 迭代记录（最终状态）

- v1（殖民者栏+主按钮限流）：证据不足，慢帧时整个拖拽发生在单帧内。
- v2（UIRootOnGUI 总闸门 45Hz）：有改善，但实测每趟全量遍历 ~20ms × 45Hz 仍吃光帧预算。
- v3（分帧探针）：定位 `guiPhase` 主导慢帧（单帧 5.7 秒全在 GUI），并发现 `drawDynamicThings`（pawn 渲染）尖刺属于后期整体性能问题，不在本 MOD 范围。
- v4（自适应闸门）：限流间隔 = 平滑帧时间 ÷ 2，夹在 60Hz~10Hz；游戏内验证"基本可用"。
- 收尾：探针日志收进 MOD 设置（`Log frame profiling`，默认关），限流总开关默认开；入口由 StaticConstructorOnStartup 改为标准 Mod 类构造。
