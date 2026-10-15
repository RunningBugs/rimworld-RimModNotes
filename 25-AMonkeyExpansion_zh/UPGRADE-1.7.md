# 1.7 beta 升级记录

状态：已静态适配，**原上游依赖缺失，运行验证未完成**。基线 `1.7.4977 rev926`，发布 ID `3212151086`，packageId `RunningBugs.AMonkeyExpansionZh`。

最小变更仅 About 添加 1.7。原 1.4/1.5/1.6、依赖 `zal.monketime`、名称/ID 和全部 Common 内容逐字节保留，LoadFolders.xml.bak 不改。该 MOD 无生产 C#、DLL、Harmony 或自有函数；Common/Defs 和 Common/Patches 是空容器。默认加载规则继续选 Common+根目录，不新增空版本目录。

完整审查 132 个 DefInjected 键和 12 个 Keyed 键，涉及 ThingDef、RecipeDef、ResearchProjectDef、ResearchTabDef、ThoughtDef、HediffDef，包括 verbs、tools、stages、CompRefuelable 内的翻译路径。真实 1.7 对应字段保留：RecipeDef.jobString/successfullyRemovedHediffMessage、CompProperties_Refuelable.outOfFuelMessage、Tool.label、ThoughtStage.label/description、VerbProperties.label；ThingDef.verbs 是 private，而公开读取接口是 Verbs。DefInjectionPackage 使用 Public|NonPublic 的字段反射，旧 XML verbs 路径仍可解析，因此内容无需改写。测试代码使用公开 Verbs 属性。

1.7 LoadedLanguage/DefInjectionPackage 仍处理所用语言路径与 TranslationHandle；中文文件夹旧名称 ChineseSimplified (简体中文) 经 LegacyFolderName 对应 ChineseSimplified。官方 1.7 Core 英文 Keyed 数据仍包含这里重写的全部 10 个信件键和所需占位符结构。按 DirectXmlLoaderSimple/DefInjectionPackage 现有规则，文本中的字面 `\n` 转换为换行；本轮未顺手修改旧翻译内容或文案。

本机没有 `zal.monketime`/Workshop 3166737970。续版的 [作者 Workshop 页面](https://steamcommunity.com/sharedfiles/filedetails/?id=3166737970) 提供原作来源，但不足以逐项验证续版当前内容。为核实旧目标，只读取得 [原作者源码](https://github.com/CatLover366/AMonkeyExpansion/tree/55be3d2331213388bd96fba2b3da65817e64068c) 固定提交 `55be3d2331213388bd96fba2b3da65817e64068c` 的 1.3 Def/英文 Keyed。所有目标 Def 名与键均匹配；这只能证明原翻译出处，不能证明缺失的续版及其第三方管网组件可在 1.7 加载。未替换上游或修改其文件。

已执行标准库 XML 解析、原作者全部 Def 名/键匹配及旧内容逐字节保留检查，日志 `logs/25-AMonkeyExpansion_zh/static-validation.json`，同目录 upstream 保存原始 XML 和 SHA-256。纯内容无需生产构建或函数单元测试。`Tests/1.7/Mod25Checks.cs` 按 Map 驱动约定检查中文隔离用例中全部 144 个实际加载翻译字段；已用精准 Ref 1.7.4977-alpha.1 独立编译，最终 0 warnings/errors（checks-build.log）。初次测试编译发现 private verbs，改用公开 Verbs 后成功；初始诊断保存在 checks-build-initial-private-verbs.log。

待主代理：取得与 zal.monketime 对应、可在 1.7 加载的上游及完整依赖，检查实际续版 Def/列表结构是否仍对应，然后建立依赖基线和中文用例，执行 Mod25Checks。缺 Def/错列表/错语言均会明确失败；没有用空检查冒充功能。视图排版、上游香蕉行为和跨 MOD 翻译覆盖尚未验证。该翻译会改变原版信件语言，同其他翻译的加载顺序需要按实际组合检查。
