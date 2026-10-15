# Letter Stack Cleaner 2 — 1.7

状态：适配/编译完成，游戏功能待root串行验证。packageId `RunningBugs.LetterStackCleaner2`，无直属发布ID；按用户2026-10-15追加范围升级真实完整自用实现。名称/描述包含1.7，原1.4/1.5保留，不虚加1.6。

## 正确基线与最小改动

实际有效实现位于根`Source/Main.cs`与`Assemblies/LetterStackCleaner2.dll`，不是只有模板启动日志的1.5/Source，也不是1.4会直接遍历并修改集合的旧文件。以当前根实现的先复制letters再RemoveLetter、Delete KeyUp及Click声为起点。新增独立1.7源码/工程/DLL，唯一生产源码修正为明确`PatchAll(typeof(Start).Assembly)`。旧Root/1.4/1.5/Common和LoadFolders.xml.bak不改。

新net10.0工程固定Krafs.Rimworld.Ref1.7.4977-alpha.1、Lib.Harmony3.0.0-rc.2，无Publicizer。源码没有实际HugsLib调用/元数据引用，独立新工程不引入它。没有Harmony补丁类，保留原启动流程而不为模板日志跳过真实GameComponent。默认加载1.7、Common、根；相同Assemblies/LetterStackCleaner2.dll相对路径由1.7覆盖根旧DLL，不并行加载两套。

## API与实际调用链

1.7.4977 rev1248的GameComponent自动创建仍调用(Game)构造；UIRoot.UIRootOnGUI在Current.Game存在时调用GameComponentUtility.GameComponentOnGUI，后者逐组件调用，所以地图/世界都不是失去入口的旧回调。实际Bootstrap/ImguiDispatch.ControlKeys包含Delete，RawKeys.GetKeyUp实际RunKeyPass(EventType.KeyUp)，UIRoot会先处理高优先窗口，已消费事件仍按Used跳过，原行为保持。

Event.current/type/keyCode、Find.LetterStack.LettersListForReading、RemoveLetter和SoundDefOf.Click.PlayOneShotOnCamera真实存在。RemoveLetter实际从集合移除并调用Letter.Removed；当前实现复制列表后移除合法，保留每封通知路径，不改成直接Clear。没有反射、transpiler、自有序列化字段/设置。Common XML只有合法空模板注释，不提供额外活跃Def。完整原/新DLL静态引用见工作区`expansion/logs/27-LetterStackCleaner2/api-{original,1.7}.json`。

## 构建与验证

工作区命令：`scripts/dotnet.sh restore expansion/work/27-LetterStackCleaner2/1.7/Source/mod.csproj --configfile .nuget/nuget.config`，随后` scripts/dotnet.sh build expansion/work/27-LetterStackCleaner2/1.7/Source/mod.csproj --no-restore -c Release`。build.log为0warnings/0errors；XML解析和唯一自有DLL输出检查通过，没有复制Harmony/游戏DLL/deps。

没有原有单测。测试专用`Tests/1.7/Mod27Checks.cs`（RW17.Validation.Mod27Checks）独立编译0/0，**已由root实际native场景执行通过**。它要求真实组件唯一，实际原生LetterMaker生成三种信件，调用真正GameComponentUtility GUI fanout，再观察真实RemoveLetter（只计数，原方法始终执行），验证三封/单封/空集合及每封移除一次。在实际WorldRenderer地图/世界模式分别验证Delete release与Layout/Repaint/KeyDown/其它键/Used事件负控，finally恢复临时letterStack/Event/world状态并具体unpatch观察器。

单测没有冒充真实物理按键：它控制Event入口以验证实际组件和集合行为；X11真实Delete输入、焦点编辑器和其它MOD组合仍待测。没有自有存档键可伪造roundtrip，未宣称任意历史存档/第三方信件类的组合安全。用户真实存档和配置未访问/改动，子代理未启动游戏。

当前实际native证据：`testing/cases/expansion-gui09-letters27-stats73/SaveData/rw17-map.json`对应检查passed，整case退出0/19.4s，仅Headless Resolution基线；保留真实GPU/物理UI边界。


## 主代理最终覆盖证据（2026-10-15）

前段编译/待运行说明属于代理阶段；以下为最终实测覆盖，未列工作流、物理交互、其它三方组合与跨1.6存档仍保留边界。
- expansion-gui09-letters27-stats73：19.4秒，正常exit0/未超时，相关实际检查通过；仅原版Headless 0x0 resolution基线ERROR。

四个本地fork及整个RimModNotes/工作区完整备份验证已完成，见expansion/backup.json；旧版/ID/原用户改动保留。所有游戏测试完全隔离，未修改用户配置/存档，未提交/推送。主代理将在整合后核验受控回写哈希与实际本地路径。


主代理已回写至实际本地目录：/home/lisanhu/mine/workspace/rimworld/RimModNotes/27-LetterStackCleaner2。回写后 expansion-local-selfuse-final 使用实际本地新1.7路径再次验证，相关检查通过且正常退出、无超时；Headless仅基线0x0 ERROR，渲染组合无ERROR。原始文件保留与显式清单哈希均复验；本轮未commit/push/Workshop发布。完整结果见 /home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/expansion/REPORT-1.7.md。
