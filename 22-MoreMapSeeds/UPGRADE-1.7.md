> 主代理游戏验证：`mod22-rev1248-chinese`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod22-rev1248-chinese`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod22-rev1248-e`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod22-rev1248-e`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod22-rev1248-b`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod22-rev1248-b`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 beta 升级记录

状态：已适配，游戏/UI 验证未完成。基线 `1.7.4977 rev926`，发布 ID `3160354960`，packageId `RunningBugs.MoreMapSeeds`。

新增独立 1.7/Source、MoreMapSeeds.dll，net10.0 与精准 Ref 1.7.4977-alpha.1/Harmony 3.0.0-rc.2。无参 PatchAll 改为 typeof(Start).Assembly，并在扩展词库缺失/为空时 return true 使用原版随机种子；新工程移除未用 HugsLib，不需 Publicizer。旧 1.4/1.5/1.6/Common/词库与去重开发工具逐字节保留，About 添加 1.7。默认目录选 1.7/Common/根目录，不同时加载旧 DLL；不改旧 1.4 词库和覆盖规则。

所有生产函数/API 已审查：启动 Harmony/Log；两个未用于当前行为的私有反射 helper GetRulePack/GetField<T>；GenText.RandomSeedString Prefix、GrammarRequest.Includes.Add、GrammarResolver.Resolve、ToLower；RulePackDefOf/MyLetterDefOf；WorldSeedGameComponent 构造、FinalizeInit/base/Find.World.info.seedString/ReceiveLetter/Pause；StandardLetterCopyOnClick.OpenLetter、Text.Resolve、GUIUtility.systemCopyBuffer、Messages/Translate、base.OpenLetter。没有 transpiler 或第三方目标。

实际 1.7 GenText.RandomSeedString:188 签名/默认 grammar 结构保持；Page_CreateWorldParams:58/109 仍调用它生成初始/重随机种子，因此 prefix 可达。Common MCS_SeedGenerator 包含官方 SeedGenerator，并以 r_seed(p=500) 指向 NamesExtended/SeedGenerator，双语词库加载接口仍有效。RulePackDef.rulePack 仍 private RulePack，既有反射 GetRulePack 可查；任意 GetField<T> 的动态对象/字段不能完整静态枚举，当前 helper 无生产调用者，记录此范围不冒充完整动态 API 解析。

GameComponent 的 FinalizeInit 仍用于新游戏/加载初始化；自有种子信写入当前 World seed 后 Pause 保持原行为。Common NeutralEventCopyLetter 的类型/格式仍存在，StandardLetter 虚方法与 ChoiceLetter.Text/TaggedString 保留。剪贴板和 base.OpenLetter 属于实际 GUI/平台行为，需要可视运行，不能用无窗口通过推断可用。原英文没有 Keyed 的 WorldSeedLtterTitle/WorldSeedCopied，中文有对应格式；本轮保留原文案与缺键诊断，不隐藏英文警告。

restore/Release build 已执行，0 warnings/errors（logs/22-MoreMapSeeds/restore.log/build.log）。命令 `scripts/dotnet.sh restore work/22-MoreMapSeeds/1.7/Source/mod.csproj --configfile .nuget/nuget.config`，随后 build --no-restore --configuration Release。原/新完整 metadata 引用 api-original.json、api-new.json；静态 XML/词库/旧文件/输出检查通过 static-validation.json。只有本 MOD DLL/deps.json，无复制游戏/Harmony 引用。旧 1.6 工程 Ref 为 1.6.4488-beta；旧游戏实现 provenance 不完整，以当前真实反编译语义为准。

没有已有生产函数单元测试。独立的开发去重 Python 工具保持原样，不是游戏运行逻辑。Tests/1.7/Mod22Checks.cs 独立编译最终 0 warnings/errors（checks-build.log），尚未执行。实际 GenText 调用验证 prefix 注册、词库被使用、64 个返回非空小写以及固定 Rand 状态重放相同；检查实际初始化种子信/暂停及 Def class，并执行编译的两个 helper 验证真实 rulePack 与简单私有字段的读值合同。Rand 状态恢复，不复制 grammar 实现为假测试。

待主代理运行上述无窗口用例，另做 Page/CreateWorld 的可视种子刷新、打开信件/剪贴板复制及窗口消息；英文/中文分别记录。加载旧存档会再次暂停/发种子信是原设计，这部分需要一次性保存重载验证。当前不将未执行的驱动、未测剪贴板或动态任意字段路径标记为通过。

额外静态边界修正：原英文扩展词库为0字节，中文1071行。1.7 Rule_File 对空文件 selection weight=0，Generate 返回占位 Filestring；不能强制请求空 newSeeds。新版仅加空/缺词库原版回退，中文原扩展流程不变，不编造英文词库。Mod22Checks 在空词库分支临时精确 unpatch 该 prefix，调用真实原版 GenText 作为64结果重放对照，finally恢复原owner；中文分支仍要求实际扩展词库命中。两个语言用例需要分别运行，文件内容保留。

rev1248 实际mod22-rev1248-b 失败经查为夹具把展示文本与原始seed按相同大小写比较：ChoiceLetter.Text setter自动CapitalizeFirst，而WorldGenerator以原始字符串StableStringHash生成世界。新版保留实际种子信存在/资本化展示/暂停断言，且OpenLetter仅一行改从Find.World.info.seedString复制精确值，避免展示大小写改变复制的世界。测试专用setter spy执行真实OpenLetter捕获精确输入后return false，finally具体unpatch及清窗口，绝不读取/改变用户OS剪贴板；原OS交付/像素依然待可视测试。主代理已授权此最小纠正。当前审核基线更新为1.7.4977 rev1248。
