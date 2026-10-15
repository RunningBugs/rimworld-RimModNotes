> 主代理游戏验证：`mod04-rev1248-a`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod04-rev1248-a`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod04-wave1`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod04-wave1`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 beta 升级记录

状态：已静态适配，运行验证未完成。游戏基线 `1.7.4977 rev926`；发布 ID `3049590794`；packageId `RunningBugs.WeatherControlDeviceContZh`。

只在 `About/About.xml` 原有 1.4、1.5、1.6 后添加 1.7。该 MOD 无 C#、DLL、Harmony、Def、活跃 Patch 或自有存档字段；根目录仅有一份中文 DefInjected XML。保留原文件及 `Patches-backup/Patches.xml.bak`，不创建空版本目录，不改变旧版本加载。

完整审查 `Languages/ChineseSimplified/DefInjected/ThingDefs/Data.xml` 的 10 个键：BCS_WeatherMachine 的 label、description，以及 comps.CompUsable-0 到 -7 的 useLabel。已安装上游 `/Data/SteamLibrary/steamapps/workshop/content/294100/2584797720`，packageId `Mlie.WeatherControlDevice`，元数据仅声明至 1.6；1.7 将按默认规则回退到上游 1.6 目录。其 `1.6/Defs/ThingDefs/WeatherControlDefs.xml` 仍定义 BCS_WeatherMachine，8 个 CompProperties_Usable 顺序为 Clear/Rain/Rainy Thunderstorm/Dry Thunderstorm/Foggy Rain/Foggy/Blizzard/Snow，对应中文翻译索引一致。

真实 1.7 `RW/17/Source/Verse/LoadedLanguage.cs:276` 支持目录类型名去除最后一个字符的兼容回退，因此既有 `ThingDefs` 可解析为 ThingDef，无需改名。`RimWorld/CompProperties_Usable.cs` 仍有 useLabel 字段并设置 compClass=CompUsable；`Verse/DefInjectionPackage.cs` 仍解析成员/列表路径。未发现翻译接口改变。上游旧 BCS.dll 的 Waylith/.NET 10 运行兼容并未由上述静态审查证明，不能将本翻译标签解释为上游 1.7 升级已经完成。

已执行标准库 XML 解析、10 个上游目标与顺序核验、旧内容/备份逐字节检查；结果和上游 Def/DLL SHA-256 在工作区 `logs/04-WeatherControlDeviceCont_zh/static-validation.json`。纯内容无需编译或虚构函数单元测试。测试专用 `Tests/1.7/Mod04Checks.cs` 按共享 Map 驱动约定检查真实加载后的全部 10 个字段，需测试配置 `langFolderName=ChineseSimplified (简体中文)`；驱动不属于发布程序集。

待主代理：先单独加载上游建立依赖基线，再加载翻译 MOD，验证上游 DLL 与所有 Def 能在 1.7 启动，并在中文隔离用例运行 Mod04Checks。该断言验证实际翻译注入，不验证上游天气控制功能、窗口排版或任意第三方组合。若上游无法加载，应保留上游阻塞状态，不能静默忽略缺失 Def。未修改 Workshop 或实际 RimModNotes。

主代理首轮04实测发现语言前置用完整folderName与旧bare名不一致。实际1.7官方中文来自 ChineseSimplified (简体中文).tar；PrefsData 应设置完整文件夹名，驱动以 LegacyFolderName=ChineseSimplified 判断。这是夹具修正，生产翻译不改；尚需按正确配置重跑。
