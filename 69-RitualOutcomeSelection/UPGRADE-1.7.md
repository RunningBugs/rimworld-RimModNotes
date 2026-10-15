> 主代理游戏验证：`mod69-rev1248-b`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod69-rev1248-b`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 beta 升级记录

状态：已适配，实际功能/UI 验证未完成。当前固定基线 `1.7.4977 rev1248`；发布 ID `3543090225`；packageId `RunningBugs.RitualOutcomeSelection`。更新途中旧 rev926 审查已按新完整 Source 复核，不把旧运行证据冒充新修订。

新增独立 1.7/Source/DLL、net10.0，锁定 Ref 1.7.4977-alpha.1、Publicizer 2.3.2、Harmony 3.0.0-rc.2。新公开成员仍仅 Dialog_BeginRitual.ritual/outcome、LordJob_Ritual.RitualFinished 三项，策略 IgnoresAccessChecksTo；不改游戏 DLL、不公开整个引用。生产源码仅 PatchAll(typeof(Start).Assembly)，保留原一次性选择/取消/弹窗逻辑，About 添加 1.7，旧 1.6/Common/备份逐字节保持，默认版本目录加载单份同名 DLL。

完整源码/API 审查：启动 Harmony/Log/Color；GetOutcome postfix 的质量/ritual/worker/result；LordJob_Ritual.ApplyOutcome Prefix 与 protected virtual RitualFinished；Window.Close Postfix、Dialog_BeginRitual protected ritual/outcome、Precept_Ritual.outcomeEffect.def、WindowStack.Add；窗口 ctor/InitialSize/forcePause/draggable、DoWindowContents、Text.Font、Rect/Vector2、Widgets.Label/RadioButtonLabeled/ButtonText、RitualOutcomePossibility.Label/positivityIndex、outcomeChances 列表、Translate、Messages/SilentInput、Close。无 transpiler、自有 Def/Scribe 或第三方 DLL。

真实 rev1248 GetOutcome(float,LordJob_Ritual) 仍由 FromQuality.Apply 和相关派生 worker 调用，原方法先算随机 outcome，再 postfix 一次性覆盖并清空字段。ApplyOutcome(float,bool,bool,bool) 的 cancelled 命名/类型及 RitualFinished(float,bool) 保持：未满足时间/关键阶段或取消时不完成，Prefix 因此清除挂起选择。Window.Close(bool) 仍通过 WindowStack.TryRemove，BeginRitual 开始回调也使用此路径，protected outcome 字段仍可读取。部分派生 worker 可重写 GetOutcome 并有专用逻辑，未声称所有绕开 base 的路径均可强制。

当前 UnityStubs GUI/Input/Font 与 rev926 有实现更新。已阅读新 Widgets 的 RadioButtonLabeled、ButtonText（经 Invisible/GUI/输入路径）及 UnityStubs.GUI 的 ButtonImpl，签名满足旧源码，但点击、字体/布局不能由 headless 调用证明。完整差异索引见工作区 shared/revision-source-diff.json，真实源码为 RW/17/UnityStubs、Source。

保持原有功能边界：选择存储在全局静态字段而非逐仪式对象；任意 BeginRitual 关闭（包括取消）都会开选择窗口，默认 positivityIndex=0；不借迁移重构这些设计。多并发仪式、无 outcome 定义的 BeginRitual 和非质量 worker 需要具体游戏场景，未静态假定无风险。双语 Keyed/空 Common Def/Patch 不改。

已执行精准依赖 restore/Release build，0 warnings/errors（logs/69-RitualOutcomeSelection/restore.log、build.log）。命令 `scripts/dotnet.sh restore work/69-RitualOutcomeSelection/1.7/Source/mod.csproj --configfile .nuget/nuget.config`，随后 build --no-restore --configuration Release。原/新完整 metadata 引用 api-original.json、api-new.json；rev1248程序集哈希、XML/旧字节/输出检查 static-validation.json。只生成本 MOD DLL/PDB/deps.json，无复制游戏、Harmony 或 publicized 引用。

无已有独立单测；postfix 字段流依赖游戏 outcome 类型，取消判断/界面函数依赖游戏生命周期。Tests/1.7/Mod69Checks.cs 单独编译 0 warnings/errors（checks-build.log），待主代理：确认三个具体 patch，实际调用游戏 GetOutcome 验证无选择原结果、选择仅一次、第二次不泄漏；实际 ApplyOutcome 入口的 cancelled/finished prefix 控制用 ended=true 的合法短路夹具避免执行无完整仪式的后续副作用；真实 BeginRitual 构造/Close 触发自有窗口并检查传递的定义/大小/暂停。finally 清字段/窗口，不以此声称完成了真实仪式。

剩余：在完整 Ideology/Royalty 仪式中点击 radio/确认按钮并实际完成所选效果、取消/多仪式边界、可视字体/布局/双语以及保存重载。当前 Map 驱动未执行，不能把逻辑方案或编译结果当作实际选择界面验证。
