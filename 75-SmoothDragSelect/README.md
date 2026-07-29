# 流畅拖拽选择 Smooth Drag Select

修复高回报率鼠标拖拽时（如按住左键拖框选择）游戏卡顿的问题。

## 原理

鼠标拖动时每秒产生数十至数百个 MouseDrag/MouseMove 事件，Unity IMGUI 对每个事件都完整执行一遍 OnGUI 界面树（殖民者栏、主按钮、窗口栈、各 MOD 窗口）。实测本机一趟全量遍历约 20ms：帧率下降 → 每帧排队事件增多 → 全量遍历次数增多 → 帧率进一步崩塌（死亡螺旋，实测曾出现单帧 5.7 秒全耗在 GUI 上）。挂在 OnGUI 上的 FPS 计数器会因此显示虚高数值（如 900+），并非真实渲染帧率。

本 MOD 在 `UIRoot_Play.UIRootOnGUI` 入口对 MouseDrag/MouseMove 事件做**自适应限流**：限流间隔 = 平滑帧时间 ÷ 2，夹在 60Hz（帧率正常，无感）与 10Hz（帧率崩塌，止损）之间。画面更新仍由每帧必到的 Repaint 承担，鼠标按下/松开、键盘、滚轮事件一律不限流，功能无变化。

## 设置

- **Enable mouse event throttling**（默认开）：限流总开关，关闭可对比原版行为。
- **Log frame profiling**（默认关）：向日志输出慢帧分解（update/ticks/mapUpdate/drawMapMesh/drawDynamicThings/guiPhase）和每次拖拽的统计行，仅用于诊断。

## 构建

```bash
cd 1.6/Source && dotnet build -c Release
```

产物输出到 `1.6/Assemblies/MouseEventThrottle.dll`。
