> 主代理游戏验证：`mod75-rev1248-j`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod75-rev1248-j`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# Smooth Drag Select：1.7 beta 升级

packageId `RunningBugs.MouseEventThrottle`、程序集 `MouseEventThrottle`、Workshop `3772595392` 保持。基线 `1.7.4977 rev1248`、真实 `RW/17`。原 `1.6` 非缓存文件逐个字节相同，新增独立 1.7 net10.0，锁定 Ref `1.7.4977-alpha.1` / Harmony `3.0.0-rc.2`；只在新 C# 改显式 `.PatchAll(typeof(MouseEventThrottleMod).Assembly)`，未改变原限流/探针算法，也不需 Publicizer。About 增加1.7及引擎行为备注；README补充1.7与结果限制。

## 原版语义变化：不能照搬旧性能结论

当前真实 `RW/17/Bootstrap/RimWorldWaylith/Imgui/ImguiDispatch.cs`：DispatchInput 按每帧最终鼠标位置差处理，对三个按键各最多一次 MouseDrag，先发 Layout；此入口没有 MouseMove 分发。Draw 另发 Layout/Repaint。`UnityStubs/.../InputRuntime.QueueMouseMove` 更新累积鼠标状态；不是每个 polling event 都调用整棵 OnGUI。

所以旧 Unity 1.6 的高回报率鼠标“单帧数百次完整 GUI”的根因/测量不代表1.7。保留原自适应 gate 能限制实际 MouseDrag pass，但 Layout 并未限流，在新引擎下是否更快、是否削减拖动响应必须可视测试。没有为求编译成功静默删除功能，也没有宣称不再需要或已经带来性能收益。此发现已发主代理，供输入相关 MOD 共享。

## API 与生命周期

静态清单 `logs/75-SmoothDragSelect-api-audit.json`（23个可识别直接外部成员）。完整源码、所有 Harmony 类型/名称、UI时钟/事件、Scribe另审查：

- 7 个真实目标保留签名：UIRoot_Play.UIRootOnGUI、Root.Update、Root.OnGUI、TickManager.TickManagerUpdate、Map.MapUpdate、MapDrawer.DrawMapMesh、DynamicDrawManager.DrawDynamicThings。UIRoot 多个前缀分别为 gate 和 DragStats；prefix/postfix 的 `__state` long 匹配。actual Root.OnGUI 继续调用 virtual UIRootOnGUI 后执行 fader/camera，拦 UIRoot_Play 不会拦整 Root。
- 真实 UIRoot_Play 仍拥有完整 window/按钮/Selector/designator 输入树。gate仅拦 MouseDrag/MouseMove，按下/抬起/键盘/滚轮/Layout/Repaint 均保留，不伪称真实输入路径无风险。
- 当前 UnityStubs Event.current 是 ThreadStatic native compatibility Event，type 可能因 backend hit-clip 映射 Ignore；Time.realtimeSinceStartupAsDouble 映射 UnityRuntime.Current.Time，Input.GetMouseButton 为真实按键状态。以上都不是未实现空桩；没有使用新增 IME 接口。
- Verse.Text.StartOfOnGUI 仍重置 Font，异常 wrap/anchor 会记录原版 ErrorOnce；fixture保持正常 wrap/anchor，只检查字体重置，不刻意制造原版错误以干扰错误比较。
- 原 FrameProfiler/DragStats 仍依据实际 Root.Update 和 Stopwatch，不用GUI次数当帧数。Headless不绘制GUI，不能证明draw profiler/拖拽报告真实出现。现有 setting Scribe 键 `throttleEnabled`/`profilingEnabled` 不变。
- 默认版本选择 1.7+根，根没有 Assemblies、无LoadFolders，不加载旧1.6同名DLL；输出仅MOD DLL/deps.json，不复制游戏/Harmony。

## 编译与供主代理执行的检查

```bash
scripts/dotnet.sh build work/75-SmoothDragSelect/1.7/Source/mod.csproj -c Release --configfile .nuget/nuget.config
scripts/dotnet.sh build work/75-SmoothDragSelect/Tests/1.7/Mod75Checks.csproj -c Release --configfile .nuget/nuget.config
```

产品/检查 0 warnings、0 errors，日志 `logs/75-SmoothDragSelect-build.log`、`...-native-check-build.log`。DLL SHA256 `f2e2abf46ea3e04582392514ac2c4cbac2436bf5c80e727d96c473ea59ea160f`，XML可解析。

无既有有效测试；未拆算法/制造假时钟或假Event来复刻实现。`Tests/1.7/Mod75Checks.cs` 使用真实游戏/Harmony API及反射读取MOD，在完整主线程上验证：7实际patch注册，真实Event各强制保留类型/null，两种mouse event 使用当前Time的已过/立即重复间隔、counter、真实Text字体重置、全局关闭、健康帧下限，以及对 actual Find.UIRoot.UIRootOnGUI 的安全“应被拦截”调用，通过只读临时具体Postfix观察 Harmony `__runOriginal=false`。观察器不改变原方法结果，无reversepatch；finally仅移除自己的具体观察方法，恢复Event/settings/gate/text。未调用需绘制的允许GUI树。

检查已独立编译，实际运行仍待主代理。不能把此数据/真实skip断言当高回报率输入/选择框流畅度/窗口拖动功能通过。

## 必要后续

可视比较启用/禁用下实际框选、MouseUp最终选中、窗口/滚动条/殖民者重排、跨UI事件、低/高帧率输入延迟，以及 profiler frame/drag统计。性能需固定地图/鼠标回报率/引擎版本，至少比较原版与MOD；旧后期用户存档不得直接使用。本适配未进行保存重载/GUI性能实测。
