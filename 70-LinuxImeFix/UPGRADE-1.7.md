> 主代理游戏验证：`mod70-dubs-ibus-x11-v1`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod70-dubs-ibus-x11-v1`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod70-ibus-x11-v5`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod70-ibus-x11-v5`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod70-rev1248-ah`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod70-rev1248-ah`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod70-dubs-rev1248`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod70-dubs-rev1248`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod70-rev1248-ag`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod70-rev1248-ag`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod70-rev1248-t`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod70-rev1248-t`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 升级记录

目标 `1.7.4977 rev1248`。**独立managed/native构建及有效逻辑/原生白盒测试通过，真实桌面输入验证待执行**。原1.6所有源/DLL/.so及用户内容保留，packageId/发布ID不变；About添加1.7并更新中英文版本说明，新1.7目录独立加载。

## 构建依赖/真实引擎API

net10.0，精准Krafs.Rimworld.Ref1.7.4977-alpha.1与Lib.Harmony3.0.0-rc.2；显式PatchAll(typeof(Start).Assembly)。当前需要的所有game/GUI/TextEditor/Input/Waylith.Instance.Headless API均已核对实际rev1248源码并能用精准包编译。alpha1缺新的Input.imeCompositionMode getter/Keyboard.onIMECompositionChange等接口，但本MOD只使用原已存在setter，不机械增加快照引用或改成无用新事件。没有直接private外部成员编译访问，无需Publicizer。

已枚举完整静态API引用api-references.json；动态接口包括dlopen/dlsym/libdl.so.2、完整15个native ABI exports、GUIUtility.GetStateObject(TextEditor)/controlID/position/text/cursorIndex/selectIndex、可选DubsMintMenus.DubGUI.InputField旧8/当前1.7完整9参数、所有实际Harmony目标和本MOD私有state字段。Native FindLibrary从实际Assembly.Location旁找.so，内存加载时回退所属ModContentPack.RootDir树；当前普通加载先选1.7，旧跨版本fallback风险记录，不修改无关功能。

最新真实 Source/Widgets.TextField/TextArea与DevGUI.TextField仍调用Unity GUI文本框，UIRoot_Play/Entry UI事件后置仍有效。UnityStubs/GUI.cs:973/1004/1023真正存取TextEditor位置/文本/selection；位置保持本地rect，原RectLooksLike匹配仍有语义依据；TextEditor写回caret会被下一次TextFieldImpl的_syncedEditor流程接收。GUI.matrix/depth、UI.GUIToScreenPoint、Font/Color/QuickGUI等仍存在，实际字体/缩放/候选位置须渲染确认。

## 必要最小行为变化

- actual EngineApplication.Instance.Headless==true（Waylith源码:104/126）时不Load原生IBus、不建候选renderer；纯Headless没有键盘/window，启动必须空闲。真实Linux有窗口保留IBus功能，未禁用产品功能或吞掉测试。
- **修复实际1.7 modifier输入缺口**：Bootstrap/Imgui/ImguiDispatch.cs:ControlKeys/GameKeys及:276–333仅派发14控制键/75游戏键，未包含Shift/Ctrl/Alt；字符从:285–287以KeyCode.None单独派发。因此旧Event.keyCode modifier pressed/released处理无法得到切换IBus/Rime所需Shift边沿。新在已聚焦且native ready的文本框中读取6个modifier实际Input.GetKey held state，首次焦点只snapshot，之后只转发变化并设置IBusReleaseMask，正确保留双Shift和Ctrl/Alt。Input.cs:254–260明确这些modifier不被TextFocus吞掉，无需internal getter/新IME API。字符与Backspace等原流程不重复发送。
- 提交选择区替换逻辑抽成同语义private InsertCommit，使用BCL Math.Clamp，便于独立验证Unicode UTF-16 caret/反向/越界选择；旧版本不动，没有大重构。
- native C源逐字不改，重编独立.so；D-Bus解析/候选/UTF-8/Focus/SetCursor原逻辑保留，15导出匹配，不用新的native ABI冒充运行成功。

## 已执行测试与命令

`bash scripts/dotnet.sh build work/70-LinuxImeFix/1.7/Source/mod.csproj -c Release --no-restore`：0warning/error（build.log）。gcc -shared -fPIC -O2 -Wall -Wextra 原生构建0警告，系统既有libdbus-1/pthread，仅项目输出（native-build.log），未全局安装或改系统环境。

`bash scripts/dotnet.sh run --project work/70-LinuxImeFix/Tests/Unit/Unit.csproj -c Release --no-restore`：passed（unit-tests.log）。6个实际生产commit范围/Unicode/双向selection/越界caret病例，8个实际IBus keysym转换边界（Backspace优先、编辑键/Enter/字符/空键）；8个**实际UnityStubs InputSystem raw key状态**到生产ForwardModifierChanges的transition/mask/Release/双Shift/重复抑制病例，native delegate接受端只记录消息，不连接D-Bus。这证明轮询算法实际读到未被focused text吞掉的modifier，不假报输入法/GUI端到端成功。

旧有效native_whitebox_tests复制到Tests/1.7并指向新源码、fixture目录在项目，parser/UTF-8/IBus地址优先/partial init/focus no-reply逻辑passed（native-tests.log），13原源约束passed（source-invariants.log）。ctypes仅dlopen检查全部15导出，不调用init/key/焦点，不碰用户桌面（native-abi.log）。原脚本包含真实desktop smoke不能直接运行；本轮没有运行真实IBus集成。source hashes在native-source-review.json。

## root运行与GUI观察入口

Tests/1.7/Mod70Checks.cs编译0warning/error，仅game/Harmony；检查5类实际text/root具体targets已注册、Headless的native loadAttempted确实false。它是加载/安全门禁验证，**不代表IME功能**。

设置环境 `RW17_IME_UI=1` 时同场景会添加真实Game Window（Mod70GuiProbe），以真正Widgets.TextField创建已命名自动聚焦输入框；观察真实OnGUI经过生产prefix/postfix后的text/native preedit/candidate count/ready/focus，将变化写 `SaveData/rw17-ime-ui.json`。它不注入假commit/键、不直接调用Native.ProcessKey，根代理在专用DISPLAY通过真实X11输入触发GLFW/GameGUI。目标：IBus/Rime真实nihao+Space提交“你好”、首次候选/preedit非空、Shift切换英/中模式、Backspace组词、选区替换/光标位置、失焦/再焦、Dubs可选字段和UIScale/屏幕边缘/顶层renderer；需要实际截图与输入数据证据，文本json不能替代候选窗口像素正确性。

root必须dbus-run-session+独立IBus daemon/engine、**显式IBUS_ADDRESS**和专用XDG_DATA/CONFIG/CACHE/TMPDIR、专用Xvfb DISPLAY（env -u WAYLAND_DISPLAY, SDL_VIDEODRIVER=x11）运行。native旧fallback会扫描HOME/.config/ibus，单XDG隔离不足，所以未指定dedicated address禁止真实GUI probe；不要改HOME、用户真实D-Bus/IME/词典配置。若只测无IBus窗口启动，仍给独立无服务socket地址以安全失败。父代理负责串行游戏和native集成。本代理没启动游戏，实际startup/D-Bus/候选绘制/真实输入/平台window/DLC/其他UI mod及reload仍pending。运行唯一mandatoryMOD brrainz.harmony；DubsMintMenus可选测试时额外启用dubwise.dubsmintmenus。

## 联合验证发现的可选Dubs接口变化

首个58MOD联合 `combined-all-rev1248` 确实启用Dubs后，type存在但原8参数TargetMethod返回null，type-only Prepare放行导致整个PatchAll失败。单70 headless旧通过无法证明可选组合。

实际当前Dubs1.7 DLL：`/Data/SteamLibrary/steamapps/workshop/content/294100/1446523594/1.7/Assemblies/DubsMintMenus.dll`，SHA256 `5a4b08042c147e502e933539ccc736b52c74d732f486ce2e992f575107618d6b`，与20的固定引用一致。局部真实反编译 `logs/70-LinuxImeFix/DubGUI.actual-1.7.cs:172` 表明接口为public static bool InputField(string name, Rect rect, ref string buff, Texture2D icon=null, int max=999, bool readOnly=false, bool forceFocus=false, bool ShowName=false, **string ghost=null**)。真实方法直接调用GUI.TextField(val,buff,max,Text.CurTextAreaStyle)，不是Widgets.TextField；ghost仅空文本提示绘制，icon/ShowName对应文本Rect计算与原70 helper相同，因此专用patch仍必要，不以撤去功能规避加载失败。当前Dubs还有其它实际窗口调用该InputField（20已审查两个dialog/bill窗口）。

仅修改新1.7的Dubs可选block：优先准确9参数，保留旧8参数fallback，type缺失安全返回null；Prepare判TargetMethod是否真正可解析，不再仅判type。Prefix/Postfix/Rect/IBus其它managed/native行为不改，没有引入Dubs硬DLL引用/mandatory dependency或复制其DLL。新产品SHA见results；native.so原SHA及C源逐字保持，所有旧1.6保持。

`logs/70-LinuxImeFix/dubs-build.log`与dubs-checks-build.log均0/0。有效Unit重新执行原22个实际managed逻辑病例仍过；新增2个接口病例先验证真的未加载Dubs时Prepare=false/Target=null，再只加载当前真实Dubs程序集（不运行图形cctor/不伪造stub），验证真实9参数bool/static/ref buff/optional ghost解析匹配，全部过：dubs-unit-tests.log。13结构不变量仍过(dubs-source-invariants.log)，这不替代GUI功能。更新完整静态API清单api-references-dubs.json。无需重跑未改native代码测试。

更新Mod70Checks：有当前Dubs时要求准确方法且确实注册本MOD的具体prefix+postfix；不存在时要求无可选target。不能把未知signature静默skip记成Dubs功能已通过。真实GUI probe新增测试选项 `RW17_IME_UI=1 RW17_IME_DUBS_UI=1`，同窗口/同坐标直接调用当前真实Dubs.InputField并从ref buff取回，artifact标出field类型；未造输入、不直接调自有prefix或native ProcessKey。root/A在专用namespace+DISPLAY+IBUS_ADDRESS用真实键盘执行，可与普通Widgets场景比较。

root尚需重跑70+Dubs实际加载/注册与全部联合，并按GUI条件测试真实Dubs中文组合/候选/提交。历史单70通过保留，不覆盖为新版组合通过；native bridge及其它1.7modifier修复未动，本代理未启动游戏/读用户凭据或接触桌面IBus。

## 真实GUI焦点诊断（未据诊断修改产品）

root后续 `mod70-dubs-rev1248` 已验证真实可选Dubs具体prefix/postfix注册，`combined-all-rev1248-v2` 已通过组合加载/Def/Map smoke；这两者仍不能替代实际输入法功能。

隔离GUI `mod70-ibus-x11-v2` 确实启动GPU/window、原生IBus ready且输入框已聚焦（命名control=-2147483648合法）。1.7 GUIUtility.GetStateObject按(Type,controlID)缓存全局TextEditor，TryGetActiveTextEditor只排除0、接受该负ID；Waylith ImguiDispatch先UIRootOnGUI后ProcessPendingWindows，现有FrameEndRefresh则在keyboardControl/实际editor仍一致时保留focus。单看调用顺序不能证明帧间FocusOut。

GUI测试观察器仅在RW17_IME_UI窗口存在时安装，准确转发原pFocusIn/pFocusOut/pReset委托各一次并统计实际生产调用，关闭窗口finally恢复原委托；不自己请求焦点、注入按键/commit或更改native C/ABI。这样不会漏掉.NET10内联的小FocusIn/Out包装。artifact记录focusedControl、sawFocused标志、真实cached TextEditor ID/rect、名称及最多64条原事件/keyboard/native调用trace；计数从native Init之后开始，不能把Init内部第一次FocusIn算进其中。独立checks构建0warning/error：`logs/70-LinuxImeFix/focus-observation-checks-build.log`。

另发现测试命令行的具体误判风险：本机 `ibus version` 为1.5.34-rc2，官方同tag [tools/main.vala](https://raw.githubusercontent.com/ibus/ibus/1.5.34-rc2/tools/main.vala) 的get_set_engine先SetGlobalEngine和GetGlobalEngine，再根据use-system-keyboard-layout决定是否exec_setxkbmap；该函数在layout/variant/option均默认、没有新增参数时静默返回1。本机只读 `/usr/share/ibus/component/rime.xml` 确实layout=default。v2全部切换命令返回1且stdout/stderr空，可能实际上已成功选中Rime但被布局尾步骤误判；需根代理在真实隔离namespace每次选中后独立查询实际 `ibus engine`，不能仅靠退出码断定native焦点失效。本轮只作源码诊断，不把推断写成功能通过，不修改用户真实IBus设置。官方源快照在logs/70-LinuxImeFix/ibus-official/。

v3 `mod70-ibus-x11-v3` 真实证实上述CLI误判：setter=1但独立getter=0/rime；整个输入阶段focus-in=1、focus-out=0、reset=0，实际editor/control/name稳定，因此本场景没有帧间丢焦点。真实候选最多5、原生preedit与Space提交均发生，但严格“你好”断言仍失败，text为“nih奥”。私有D-Bus完整记录表明n/i/h于07:18:05.618–.622调用真实Rime，隔离Rime初次部署日志至07:18:05.940（3tasks3success、0failure）才ready，前三键在部署期间被引擎拒绝成为nih；之后a/o进入中文模式并真实提交奥。记录在 `logs/70-LinuxImeFix/focus-protocol-review.json`。这是测试首次部署前置不足，下一轮应在发送任何键前等待实际部署结束，不能修改产品吞掉原生未消费键或删去完整中文断言。产品managed/native仍保持原SHA；真实完整中文提交/Shift等最终门槛待root重新运行。

## 真实短Shift在慢帧中的必要修复

v4 `mod70-ibus-x11-v4` 在等待真实Rime部署完成后，实际nihao→非空preedit/最多5个候选→Space完整提交“你好”全部通过，focus-in=1/out=0。仅物理Shift切Latin失败：XTest确实按住Shift_L 0.35秒，私有D-Bus全部ProcessKeyEvent只有n/i/h/a/o/Space/x，完全没有65505/65506或对应Release。因此不能归因Rime schema（实际default.yaml的Shift_L=inline_ascii），MOD未转发边沿。

真实GlfwInputBridge在PollEvents中逐个调用InputSystem.QueueKeyChange；UnityStubs.ButtonControl同时保留WasPressedThisFrame、WasReleasedThisFrame至BeginFrame，即使最终isPressed=false。Input.GetKeyDown/GetKeyUp公开接口对303..312的modifiers不被TextFocus过滤。原ForwardModifierChanges只看GetKey终态，按下/释放同一慢帧时无变化，整次短按被丢掉；v4 Space处理07:23:29.670、x处理07:23:30.638之间恰有真实Shift短按07:23:29.736–30.086。

最小新1.7修改仅在该轮询函数补处理公开Down+Up两个真实边沿、按modifier/Time.frameCount去重，并在首次focus快照忽略获得focus前同帧积累的边沿。终态released按下→释放，终态held释放→重新按下；保留原长按/双Shift/释放掩码/未消费字符路径。没有延长测试按键、吞键、改Rime配置或替换native桥/Dubs/Harmony。边沿flags每modifier/frame只能保留一个完整对，源码注明极端一帧多次完整短按需改用有序事件流的边界，不为此引入新hook。

有效回归先运行真实UnityStubs.QueueKeyChange(true→false同帧)，旧生产算法严格失败：`logs/70-LinuxImeFix/shift-tap-before.log`。新生产算法的实际同帧短按、held释放/重按、清空frame边沿后不重播、重复OnGUI轮询，以及原24个实际managed病例均通过：`shift-tap-after.log`；记录27个病例，不连接用户/测试IBus。产品build和GUIchecks build均0warning/error（shift-tap-build.log/shift-tap-checks-build.log），原13结构约束仍过（shift-tap-source-invariants.log），最新全部静态API引用清单api-references-shift-tap.json。无需publicizer/额外依赖，native C/.so/15导出和所有旧版本保持。

最新managed DLL SHA256 `a4235ffe7b6601b1fa79a603af1511c9fe1634d554fc3308752463e406d3c388`；native仍为 `5ae63b6b1581357f8d780eca1ded3dee36195743434433fddceb9c2dfe6e1335`。原GUIprobe/launcher真实Shift→Latin x断言不变，等待root串行v5实际验证；回归通过不是输入法全部功能已通过。

最终v5 `mod70-ibus-x11-v5` 由root在真实隔离GPU/X11/Rime环境执行通过（testing/ime-game-v5/result.json passed=true、runner=0）：真实nihao/preedit/5候选、Space→“你好”、物理Shift→Latin x得到“你好x”、物理Backspace→“你好”。私有总线真实65505两请求state=1及1073741825（0x40000001）同一慢帧完整发送并收到Rime回包，证明短按修复确实经过原生桥；FocusIn=1、FocusOut=0、Reset=0保持。协议摘录/GUI真实状态见 `logs/70-LinuxImeFix/shift-tap-runtime-v5.json`，首键发生在实际部署完成之后也有launcher时间证据。没有假commit、强制nativefocus或放宽中文/Shift目标。

以上通过范围为真实Widgets文本框的中文组合/候选数据/空格提交/Shift英文切换/Backspace。像素级候选位置/前后遮挡、UIScale/屏幕边缘、选区/光标替换、Shift切回中文与双Shift/快捷键、实际Dubs窗口中文输入/布局、真实Wayland和reload仍需分别验证，不能从单X11窗口推导全部平台/UI兼容。根代理负责受控回写新DLL/源码/Tests/记录；本代理未启动游戏或修改原版环境。

主代理实际GUI最终复核：mod70-ibus-x11-v5/ime-game-v5整个结果true，exit0(35.04s)、零ERROR（console与Player.log）；真实D-Bus/Rime候选5、nihao+Space提交你好、Shift+x为你好x、BackSpace回你好。Shift同一帧两物理边沿均真实到达IBus，未fake提交/焦点；HOME不改、物理用户.config映射项目夹具。部署3tasks成功之后9.5ms才发首键，UI/native焦点稳定1in/0out。其他platform/选区/candidate像素、Dubs GUI另列范围。

实际Dubs GUI最终验证：ime-dubs-v1/mod70-dubs-ibus-x11-v1，全部输入/候选/真实Shift/退格与部署先后断言true、exit0、console零ERROR。field来源明确actual DubsMintMenus.DubGUI.InputField，不以只注册代替输入。Code/输入库不再修改；像素定位、选区、Wayland/SteamDeck以及每帧多次完整tap仍未覆盖。
