# Simple GUI Library and Gallery — 1.7

状态：适配/编译完成，实际Map与GUI验证待root。packageId `RunningBugs.GUI`，无直属发布ID，按2026-10-15用户追加范围升级完整自用实现；不是因README模板/空XML跳过实际库。标题及描述包含1.7，保留原1.4/1.5支持，不虚加1.6。

独立复制当前1.5四个C#到1.7。新工程net10.0、固定Krafs.Rimworld.Ref1.7.4977-alpha.1/Lib.Harmony3.0.0-rc.2；唯一生产代码改动为 `PatchAll(typeof(Start).Assembly)`。不用Publicizer；HugsLib在源码/旧DLL均无调用，新工程不继续引入无用包。旧版本/Common/原资源保持，默认加载1.7、Common和根，只有自有`1.7/Assemblies/SimpleGUI.dll`，没有deps或依赖DLL。

基线1.7.4977 rev1248：完整新旧引用见工作区`expansion/logs/09-GUILib/api-{original,1.7}.json`。逐项核对真正PlaySettings.DoPlaySettingsGlobalControls(WidgetRow,bool)及MapInterface→global controls调用链、WidgetRow.ToggleableIcon(ref bool,Texture2D,string,SoundDef,string)、WindowStack.IsOpen/Add/TryRemove(Type,bool)。旧Postfix仍是真实入口，保留worldView guard、静态flag和可配置callback。没有其它反射/transpiler/存档字段。

SampleWindow的Window继承/drag/resize、QuickSearchWidget和filter、DefDatabase、Rect.TopPartPixels/BottomPartPixels、Text.LineHeight、Widgets.DefLabelWithIcon/scroll/highlight/ButtonInvisible在实际源码仍存在。UnityStubs GUI窗口路径可执行，不用RenderTexture合成；真正像素与物理交互分开验证。Logger三个方法和Caller* metadata保留，启动/配置消息不删。Common Def/Patch/语言XML本身为空合法；WindowIcon来自Common，原缺失SampleWindowTooltip翻译键/empty-search无行/最多51行/selected矩形缓存等既有行为保持，不借升级修画廊设计。

构建：在升级工作区执行` scripts/dotnet.sh restore expansion/work/09-GUILib/1.7/Source/mod.csproj --configfile .nuget/nuget.config`，再` scripts/dotnet.sh build expansion/work/09-GUILib/1.7/Source/mod.csproj --no-restore -c Release`；build.log记录0warnings/0errors。API audit、XML与单DLL输出检查通过。

测试专用`Tests/1.7/Mod09Checks.cs`（RW17.Validation.Mod09Checks）已独立编译0/0，**已由root实际native场景执行通过**。实际Map上检查具体Harmony Postfix/真实启动注册，调用原生PlaySettings方法开/关/去重窗口、world guard和真实库消费者callback，调用真实gallery内容并仅观察DefLabelWithIcon（原方法照常执行）断言实际Def搜索/顺序/51cap/空结果和无点击无选择，直接验证自有GetRow/GapVertical几何。所有短期观察器finally具体unpatch，恢复原配置/flag/Event/窗口。

root可在专用渲染场景设置`RW17_GUI09_RENDER=1`，驱动最后用真实注册action留下gallery并设置真实搜索steel，输出RW17_GUI09_RENDER_READY供root GPU截图。默认不留新窗口，测试DLL不发布。未测物理点击/拖拽/resize/scroll/键盘搜索、真实pixels和与任意第三方GUI库的组合；没有序列化接口需要伪造单测。

当前实际native证据：`testing/cases/expansion-gui09-letters27-stats73/SaveData/rw17-map.json`对应检查passed，整case退出0/19.4s，仅Headless Resolution基线；保留真实GPU/物理UI边界。


## 主代理最终覆盖证据（2026-10-15）

前段编译/待运行说明属于代理阶段；以下为最终实测覆盖，未列工作流、物理交互、其它三方组合与跨1.6存档仍保留边界。
- expansion-gui09-letters27-stats73：19.4秒，正常exit0/未超时，相关实际检查通过；仅原版Headless 0x0 resolution基线ERROR。
- expansion-gallery09-render：63.47秒，正常exit0/未超时，相关实际检查通过；无ERROR。

四个本地fork及整个RimModNotes/工作区完整备份验证已完成，见expansion/backup.json；旧版/ID/原用户改动保留。所有游戏测试完全隔离，未修改用户配置/存档，未提交/推送。主代理将在整合后核验受控回写哈希与实际本地路径。


主代理已回写至实际本地目录：/home/lisanhu/mine/workspace/rimworld/RimModNotes/09-GUILib。回写后 expansion-local-selfuse-final 使用实际本地新1.7路径再次验证，相关检查通过且正常退出、无超时；Headless仅基线0x0 ERROR，渲染组合无ERROR。原始文件保留与显式清单哈希均复验；本轮未commit/push/Workshop发布。完整结果见 /home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/expansion/REPORT-1.7.md。
