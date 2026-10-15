# 1.7 beta 升级记录

状态：已静态适配，**缺少原上游依赖，运行验证未完成**。游戏 `1.7.4977 rev926`；发布 ID `3136274532`；packageId `RunningBugs.SYR.Doormats.zh`。

最小变更仅 About 添加 1.7，保留 1.4/1.5/1.6、名称、发布 ID 和依赖 `syrchalis.doormats`。所有旧内容逐字节保留。没有显式 LoadFolders，实际 1.7 默认回退选取唯一 `1.4` 目录，继续加载翻译；不创建空的 1.7 目录改变选择。1.4/Defs/LetterDef.xml 和 Patches/Patches.xml 均为空容器，无活跃 Def/Patch。

已审查全部 48 个 ThingDef label/description 注入（24 个 Def）与 5 个 Keyed 设置字符串。真实 1.7 ThingDef 仍有 label/description、LoadedLanguage 仍加载 DefInjected/Keyed，Translator.Translate 仍处理 keyed 字符串。没有 C#、DLL、Harmony、反射或自有存档函数。

本机 Workshop 和本地 Mods 未找到 `syrchalis.doormats`，目录 1508778962 亦不存在。已只读查询 [原作者 DoorMats 仓库](https://github.com/Syrchalis/DoorMats)，固定提交 `cb0c31a6575d0891d30265cdba3518569b2d0ea3` 的 [ThingDefs](https://github.com/Syrchalis/DoorMats/blob/cb0c31a6575d0891d30265cdba3518569b2d0ea3/Defs/ThingDefs.xml) 包含全部 24 个目标名，[英文设置键](https://github.com/Syrchalis/DoorMats/blob/cb0c31a6575d0891d30265cdba3518569b2d0ea3/Languages/English/Keyed/settings.xml) 包含全部 5 个键。原作者仓库 metadata 仅声明至 1.3；这些来源只证明原翻译目标，不能证明上游 1.7 可运行。没有擅自替换为别的作者续版、更改依赖 ID 或安装 Workshop 项目。

已执行标准库 XML 解析、全部目标名/键匹配、空 Def/Patch 和旧文件检查，日志 `logs/13-SYR_Doormats_zh/static-validation.json`，查询的原始 XML 保存在同日志目录。纯翻译无需生产构建或函数单元测试。测试专用 `Tests/1.7/Mod13Checks.cs` 检查中文隔离实例中全部 53 个真实翻译字段；已单独以 Ref 1.7.4977-alpha.1 编译，0 warnings/errors，`checks-build.log`。Map 驱动仅是待执行方案，不在发布 DLL 中。

待完成条件：取得与原 packageId/Def/设置键对应、能在 1.7 运行的上游，先建立其加载基线，再启用翻译并用隔离 `langFolderName=ChineseSimplified (简体中文)` 配置运行 Mod13Checks。缺少上游/错误语言会明确抛出失败，不以空检查通过冒充功能验证。还需按需可视检查设置页面。当前静态适配不承诺旧上游兼容 1.7。

1.7官方中文来自 ChineseSimplified (简体中文).tar；PrefsData应设置完整folderName，驱动使用LegacyFolderName识别。中文配置修正不改变翻译内容。
