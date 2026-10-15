> 主代理游戏验证：`mod55-rev1248-f`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod55-rev1248-f`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 beta 升级记录

状态：已适配，数据/输入及可视验证未完成。基线 `1.7.4977 rev1248`；发布 ID `3512134102`；packageId `RunningBugs.LimitedLetterSlots`。

新建独立 1.7 工程/DLL，Main.cs 与旧 1.6 完全相同，已有显式 Assembly.GetExecutingAssembly 的 PatchAll 不改。net10.0、精准 Ref 1.7.4977-alpha.1/Harmony 3.0.0-rc.2，无 Publicizer（Harmony 字段注入负责 private 读取，生产不直接访问）。About 添加 1.7，旧 1.5/1.6/Common/备份逐字节保持，默认加载新单份同名 DLL。

全部生产函数/API 已审查：启动/Log；Settings.ExposeData 两个字段与原 keys/default；ModSettingsUI 构造/GetSettings、Listing_Standard.Begin/CheckboxLabeled/Label/Slider/End、SettingsCategory/Translate；LetterStack.LettersOnGUI(float) Prefix 的五个 private 注入 letters/tmpBundledLetters/bundleLetterCache/lastTopYInt 及 List/Bundle cache；LetterMaker/LetterDefOf.BundleLetter、Letter.DrawButtonAt、BundleLetter.SetLetters、CheckForMouseOverTextAt、Event.current.type/Repaint；LetterCleaner(Game)/GameComponentUpdate、Input.GetKeyDown(Delete)、Find.LetterStack/RemoveLetter/ToList。无 transpiler、自有 Def 或第三方 API。

rev1248 真实 LetterStack 的目标仍由 UI 绘制循环使用，field 名称与类型不变；原默认算法也使用30高/12间距，Bundle.SetLetters 复制列表后 tmp list 可安全清空。自有 prefix 在 enable=false/槽位不在1..10时返回原方法，启用后超过上限保留最新 N-1 封并把其余放1个 bundle。Root 基线证明 game ABI 全量不变不等于画面兼容，已阅读新版 UnityStubs.Input/Event/Keyboard/ButtonControl 和 GUI 实现：GetKeyDown 经 TextFocus 吞键检查、Delete→Keyboard Key.Delete 的当前帧状态；Event.current 是 ThreadStatic 可读写。新字体/渲染仍必须单独实测。

保留现有行为边界：enableMod 仅控制布局 Prefix，LetterCleaner 未读取该设置，Delete 在禁用布局时仍会清信；没有借迁移改变功能。英文语言文件为空，设置 UI 的缺键属于既有状态，中文 keys 保留。maxVisibleSlots 原序列化键 LLS.MaxVisibleSlots/default3、LLS.enableMod/defaulttrue 保持。

精准 restore/Release build 已通过0 warnings/errors（logs/55-LimitedLetterSlots/restore.log/build.log）。命令 `scripts/dotnet.sh restore work/55-LimitedLetterSlots/1.7/Source/mod.csproj --configfile .nuget/nuget.config` 后 build --no-restore --configuration Release。原/新完整 metadata 索引 api-original.json/api-new.json；static-validation.json 有当前build、XML/旧字节/单 DLL证据。只输出本 MOD DLL/deps.json，无复制 Harmony/游戏 DLL。

没有已有独立测试。槽位计算内嵌在实际 GUI prefix，不为测试拆生产抽象或复刻实现。Tests/1.7/Mod55Checks.cs 已单独编译0 warnings/errors（checks-build.log），尚未执行：调用真实 LetterStack.LettersOnGUI，Letter/Bundle 子类只观察 Draw/hover 消费端避免无窗口渲染，断言3槽最新2封+最旧6封 bundle、1/10有效边界、少信/零信/lazy cache、位置与清临时列表；临时精确 unpatch 得实际原方法作 disable/0/11 fallback 对照，finally恢复具体prefix。真实 Settings.ExposeData 保存/重读7/false，以原 keys留下独立 Mod55SettingsFixture.xml。

驱动还仅在临时 LetterStack 中，通过新版 Keyboard/ButtonControl 控制实际 Delete 当前帧状态，调用真实 Input.GetKeyDown 与实际 LetterCleaner.GameComponentUpdate，验证无键保留/有键清空；finally精确复原按键数值及三类帧flag、原信堆、设置、Event。没有触碰用户信件或物理按键，也不把托管状态注入称作真实设备交付测试。

待主代理执行上述布局/序列化/输入控制；实际可视按钮/展开bundle/tooltip、设置滑块/checkbox、Delete 物理键及文本框吞键需要窗口用例，English/Chinese/屏幕尺寸另记录。无窗口观察不能证明新 GUI/字体像素或真实键鼠路径，因此当前保留验证未完成状态。


## 2026-10-15 名称和描述版本修订

本轮在现有1.7适配基础上更新About名称/描述版本措辞，packageId/发布ID/支持标签/生产源码与DLL不改。

- 补明当前1.7适配；旧1.5未测试免责声明是历史/旧版事实，原句完全保留。

语法/身份/版本声明与原产物一致已核验；属于元数据修改，不将其当新功能验证。详细回写审计见工作区expansion/metadata-changes.json。
