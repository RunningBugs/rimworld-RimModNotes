> 主代理游戏验证：`mod01-rev1248-a`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod01-rev1248-a`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod01-wave1`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod01-wave1`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# AlertUtility：1.7 beta 升级记录

状态：已适配、编译与独立字符串单元测试通过；游戏/GUI/存档验证待执行。只修改 work 完整副本。

基线 1.7.4977 rev926；packageId RunningBugs.AlertUtility；发布 ID 3023033362。保留名称、所有 1.4/1.5/1.6 文件及存档字段。

## 变更

- 新增独立 1.7 文件树，net10.0，Krafs.Rimworld.Ref 1.7.4977-alpha.1 / Lib.Harmony 3.0.0-rc.2；DLL 仍名 AlertUtility.dll，引用不复制到输出。
- 原启动已经显式 PatchAll(Assembly.GetExecutingAssembly())，无需改动。删除仅 1.7 项目的 HugsLib 编译引用：两份源码没有使用任何 HugsLib 符号，About 也无运行依赖，原引用来自模板；未机械升级该依赖。
- 删除 1.7 TimerSetWindow.cs 未使用的 System.Runtime.InteropServices.WindowsRuntime using；没有 P/Invoke 或 Windows 原生功能，不能把旧 using 误判成 native 依赖。
- supportedVersions 添加 1.7；显式 LoadFolders 增加 v1.7=/+1.7，保留原 v1.4/1.5/1.6，不加载旧 DLL。

## API/语义审查

全部静态类型及成员引用记录在 logs/01-AlertUtility/api-references.json，覆盖编译器属性和泛型签名。无 transpiler；唯一名称式 Harmony 目标为 PlaySettings.DoPlaySettingsGlobalControls(WidgetRow,bool)，当前签名及 GlobalControlsUtility 调用仍存在（RW/17/Source/RimWorld/PlaySettings.cs:108；GlobalControlsUtility.cs:16）。Postfix 的 row/worldView 注入保持；worldView 分支继续不显示按钮。实际 GUI 绘制/点击只能用可视测试确认。

WorldComponent 的构造、WorldComponentTick、ExposeData 仍为同样接口，World.Tick 经 WorldComponentUtility 调用组件（RW/17/Source/RimWorld/Planet/World.cs:220）。每 60 ticks 检查到期，ReceiveLetter(TaggedString,TaggedString,LetterDef,...) 后 Pause，并移除已通知事件；未来事件保留。Find.World/TickManager/LetterStack、TicksGame、Pause、NeutralEvent 均存在（TickManager.cs:53/:241；LetterStack.cs:50）。组件是游戏实际创建的对象，不能仅用直接调用静态过滤方法充当生命周期验证。

Event 的 Scribe_Values(int/string) 与主组件 Scribe_Collections.Look(... LookMode.Deep) 接口仍可编译，键 RunningBugs.AlertUtility.Event.ticks/message 与 RunningBugs.AlertUtility.events 完整保留。事件列表仍是静态字段，这是已有设计；本次不改变新世界/加载生命周期行为，保存重载仍须实测。

窗口继承 Window；InitialSize、SetInitialSizeAndPosition、PreOpen、DoWindowContents、PostClose override 签名仍存在。全部 WindowStack(IsOpen/Add/TryRemove)、WidgetRow.ToggleableIcon、ContentFinder<Texture2D>.Get、Widgets(Label/ButtonImage/ButtonInvisible/ButtonText/BeginScrollView/EndScrollView)、Listing_Standard、Text/GenUI/UI、UnityStubs Rect/Vector2/Color/GUI 引用已通过精确 1.7 编译并列入审查日志。GUI.DrawTexture 与 GUI.color 运行语义、字体、图标、滚动、输入字段及窗口布局未在无窗口模式验证；不声称这些渲染路径均兼容。

时间单位仍取 GenDate.TicksPerHour=2500、TicksPerDay=60000、TicksPerYear=3600000（RW/17/Source/RimWorld/GenDate.cs），自有速度映射保持原值。StringExt.Truncate 为独立纯逻辑；RectExt/getMultiplier、RadioButtonDraw/Labeled/group、DrawAlreadySetAlarms、DrawWindow 与各 UI 入口均保留原实现；窗口相关函数不在普通 .NET 环境假装执行。

1.7/Defs/LetterDef.xml 的 success_letter 使用的 color/flashColor/flashInterval/bounce/arriveSound 仍对应 LetterDef 字段（RW/17/Source/Verse/LetterDef.cs:11）；该模板 Def 本身未被定时逻辑使用，仍保留。Patch XML 与版本语言文件为空。根多语言 Keyed 文件和 UI/timer_mail 贴图完整保留，TimerTimeOut 格式参数未改变；XML 全部解析通过。

## 验证

生产构建与 Tests/1.7/checks.csproj 场景代码编译均 0 warnings / 0 errors。Tests/Unit/Unit.csproj 实际运行生产 StringExt.Truncate，7 个边界断言通过（null、空、短于/等于限制、截断、0、负限制异常），无复刻实现。

可复现命令（工作区）：

```bash
bash scripts/dotnet.sh restore work/01-AlertUtility/1.7/Source/AlertUtility.csproj --configfile .nuget/nuget.config
bash scripts/dotnet.sh build work/01-AlertUtility/1.7/Source/AlertUtility.csproj --no-restore -c Release
bash scripts/dotnet.sh run --project work/01-AlertUtility/Tests/Unit/Unit.csproj --no-restore -c Release
bash scripts/dotnet.sh build work/01-AlertUtility/Tests/1.7/checks.csproj --no-restore -c Release
```

游戏断言 Tests/1.7/Mod01Checks.cs：检查具体工具栏 Postfix 和 WorldComponent 实例；反射调用真实 Add/GetEvents，创建到期/未来事件，调用真实组件 tick，验证非 60-tick 边界不触发、边界发送 NeutralEvent/暂停/删除到期项且保留未来项、重复调用不重复发信；同时检查真实 getMultiplier 的五种单位。finally 恢复静态事件、时间/速度和测试信件。此源码编译通过但未实际游戏执行。UI 点击/布局、取消事件、速度暂停显示、Scribe 保存重载、第三方组合仍未测。

DLL SHA-256：de029b5c6d8619652f6984aa420161dfda6b34ede53f818d94db8b36dcdfadae。元数据审查 126 个成员/72 个类型。日志目录 logs/01-AlertUtility/；旧文件逐字节保留检查及 XML 解析通过。

## 首轮真实场景失败与intro状态修正

主代理testing/cases/mod01-wave1加载/工具栏注册/WorldComponent/时间单位已进入场景，但复合到期断言失败，未标产品bug。实际Root_Play.cs:50/:51在生成地图后设置0.5秒fade；Game.cs:182的PlayerHasControl在fade期间false，TickManager.cs:162/:219/:241的Pause因此拒绝切换。首次MapComponentUpdate虽Playing/无LongEvent，仍可能处于该intro阶段。

修正场景保留到期删除、未来保留、唯一NeutralEvent信件、不重复与暂停所有断言，并拆分失败信息。测试先记录fading/player_control/speed，暂存ScreenFader四项颜色/时间字段，通过公共SetColor(clear)结束intro，确认实际PlayerHasControl，再从Normal验证真实Pause；finally还原fade、prePause/time、事件及测试信件。生产Alarm/DLL未修改；新的场景0warnings/0errors，根因属于源码支持的时序解释，待新用例日志确认entry值和全套断言。

完整保存/新进程重载：combined-save-rev1248 → combined-reload-rev1248，两次exit0、无新增ERROR；真实计时器(消息+tick)、Pawn引用物品策略(工业药7)、研究Def队列、世界诅咒activationID在6个断言中保留。一次性夹具由本轮生成，不使用真实用户存档；不声称跨1.6存档迁移。证据位于工作区testing/cases两用例的rw17-save-result.json。
