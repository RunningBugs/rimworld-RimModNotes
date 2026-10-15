> 主代理游戏验证：`mod61-rev1248-k`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod61-rev1248-k`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# More Responsive Planet：1.7 beta 升级

本机基线 `1.7.4977 rev1248`，真实程序集/代码在 `RW/17`。packageId `RunningBugs.MoreResponsivePlanet`、Workshop `3523649764`、程序集名保持。当前为工作区适配产物，GUI/异步拖选尚未证明可用。

## 修改

新增独立1.7原源码副本：net10.0，锁定 Krafs.Rimworld.Ref `1.7.4977-alpha.1` / Lib.Harmony `3.0.0-rc.2`，显式 `harmony.PatchAll(typeof(MoreResponsivePlanetMod).Assembly)`。除此产品正文不变，无私有编译访问，不需Publicizer。About增1.7，README新增现代构建/验证限制；保留原1.6非缓存文件字节。未采用仅是备份的 LoadFolders.xml.bak；实际默认选择1.7+Common+根，不重复DLL。产物仅本DLL/deps.json。

## API、真实实现与语义

全静态清单 `logs/61-MoreResponsivePlanet-api-audit.json`，91个可识别直接外部成员，全部5份C#、继承、反射式Harmony、空XML、加载布局另审查。

- 5个实际目标保留：WorldSelector.WorldSelectorOnGUI、private HandleWorldClicks、Notify_DialogOpened，WorldDragBox.DragBoxOnGUI，WorldInterface.WorldInterfaceOnGUI。输入是独立WorldInterface.HandleLowPriorityInput→WorldSelectorOnGUI调用；即使拖动期间原WorldInterfaceOnGUI被跳过，低优先级选择输入/即时框绘制仍执行。前3个bool Prefix确实替换原方法，不只是无效入口。
- WorldSelector.SelectableObjectsUnderMouse overload/out bool、Select/Deselect/ClearSelection、SelectedTile、WorldObject.SelectableNow/ScreenPos/HiddenBehindTerrain/VisibleToCamera，ColonistBar/CameraJumper、PlanetTile/CaravanRightClick→FloatMenu/BestGotoDestNear/StartPath/gotoMote/Sound等都编译及metadata匹配。查看实际WorldSelector方法与原生循环选择实现；生成的实际Map/World适合测试命令/cycling，不能代替camera投影/UI命中。
- 原生拖选仍有colonist bar优先、neverMultiSelect/expanded icon、caravan优先及player caravan优先、80上限、空框回退单击。MOD现有实现仅使用SelectableNow+screen point+terrain、矩形宽/高>7、优先caravan，未完整复制原生多选路径；这些属于既有差异，不作为1.7接口变化顺手改写。
- 当前Waylith按帧最终鼠标位置发MouseDrag、不是每polling事件调用OnGUI（详见75升级记录）；不能沿用旧Unity输入性能结论。
- UnityStubs.GameObject/Create/AddComponent、Object.DontDestroyOnLoad/DestroyImmediate与private MonoBehaviour Update由真实Waylith.UnityCompat.UnityWorld支持。为明确这种Unity→Waylith变化，只读局部反编译该真实DLL：`logs/61-MoreResponsivePlanet-UnityWorld.cs`。AddComponent注册实例，Update逐active behaviours消息反射，private Update可发现；DontDestroyOnLoad迁移scene。完整日志含类型清单；没有修改引擎DLL。
- Texture2D.ReadPixels在当前实际UnityStubs读取ScreenCaptureBackend；active RenderTexture另走CameraTextureRenderer。无后端时仅WarnUnreadable并返回，MOD仍把_hasValidCache设true，所以Headless下该bool不证明截图。0x0屏幕/截帧时点/GUI.DrawTexture都需可视测试。截图销毁没有打包新的引擎依赖。

## 构建与有效验证材料

```bash
scripts/dotnet.sh build work/61-MoreResponsivePlanet/1.7/Source/mod.csproj -c Release --configfile .nuget/nuget.config
scripts/dotnet.sh build work/61-MoreResponsivePlanet/Tests/1.7/Mod61Checks.csproj -c Release --configfile .nuget/nuget.config
```

产品与检查0warnings/0errors，`logs/61-MoreResponsivePlanet-build.log`、`...-native-check-build.log`。DLL SHA256 `87b5210bfa66d4838815e4733354bac7c7780e5a537a75997cc713d9376ec6bb`。XML可解析；无既有测试。

`Tests/1.7/Mod61Checks.cs`仅游戏/Harmony编译引用，反射实际MOD。主代理可在主线程执行真实patch/active dispatcher注册、生成世界的真实WorldObjects、8组原native循环helper与MOD结果对照（不reversepatch）、实际命令prepare→apply的增量/替换、旧id/不等当前id防覆盖、真实ConcurrentQueue入队+实际dispatcher.Update主线程处理、真实mouse坐标Rect与Notify_DialogOpened结束状态；finally恢复selector/tile/drag/id。逻辑执行使用产品实现与真实原生类型，不是复刻实现；检查独立编译，运行尚待主代理。

Dispatcher检查手动调用其真实Update，明确不声称已通过自动逐帧调度；没有启动后台worker去做虚假无GPU投影。截图/GUI/worker同属核心可视功能，尚待专门验证。

## 未完成与现有风险

- 实际拖拽UI、截图正确像素/冻结/恢复、异步完整worker→真实帧dispatch→selection、shift input、colonist bar、expanded objects、右键caravan路径/对话框、世界layer切换及性能待测。
- 原worker在后台读取Find.WorldObjects.ToList/SelectedObjects.ToList、Input、Camera投影/WorldObject状态，并非main-thread冻结快照，无法证明线程安全；task取消仅检查drag ID，新StartDrag才增id，EndDrag/打开对话框不增id；没有停止线程生命周期。原有风险已发主代理，没有无证据称安全，也未扩大重构。
- 状态不是Scribe持久化数据；save/reload→静态dispatcher/worker跨场景状态未验证。未把编译成功或无红字视为完整功能通过。


## 2026-10-15 名称和描述版本修订

本轮在现有1.7适配基础上更新About名称/描述版本措辞，packageId/发布ID/支持标签/生产源码与DLL不改。

- 保留1.6历史动机，不虚构已证实的1.7性能收益；当前版本适配与验证范围分清。

语法/身份/版本声明与原产物一致已核验；属于元数据修改，不将其当新功能验证。详细回写审计见工作区expansion/metadata-changes.json。
