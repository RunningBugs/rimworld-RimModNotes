> 主代理游戏验证：`mod44-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod44-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod44-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod44-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod44-wave1`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod44-wave1`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod44-wave1`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod44-wave1`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 beta 升级记录

状态：已适配，游戏验证未完成。基线 `1.7.4977 rev926`；发布 ID `3420169701`；packageId `RunningBugs.NotSoManyPolicies`。

新增独立 1.7/Source 工程与 NSMP.dll，保留旧 1.5/1.6 和 Common 内容。目标 net10.0，精准引用 Krafs.Rimworld.Ref 1.7.4977-alpha.1、Lib.Harmony 3.0.0-rc.2、Krafs.Publicizer 2.3.2。原 PublicizeAll 改为仅公开 OutfitDatabase.outfits、FoodRestrictionDatabase.foodRestrictions 两个实际使用字段，策略 IgnoresAccessChecksTo；新工程移除未使用的 HugsLib 编译包，原/新静态 metadata 全部引用均无 HugsLib。源码唯一改动是 `PatchAll(typeof(Start).Assembly)` 明确程序集，实际筛选逻辑不改。About 增加 1.7；无活跃 LoadFolders，默认按 1.7+Common+根目录选唯一同名 DLL，旧文件逐字节保留。

完整源码含启动函数和两个 postfix（各一个 RemoveAll 条件）：已审查 Harmony 构造/PatchAll、OutfitDatabase/FoodRestrictionDatabase 的生成方法及私有列表、ApparelPolicy/FoodPolicy.label、Translate/TaggedString 字符串比较。真实 `RW/17/Source/RimWorld/OutfitDatabase.cs:13,69` 构造仍调用 GenerateStartingOutfits；其中 Anything 标签与 1.6 源码一致，后续新增 Odyssey Spacefarer 同样会按原规则删除。`FoodRestrictionDatabase.cs:13,84` 构造仍调用 GenerateStartingFoodRestrictions，最后内部生成 Ideology 食物方案；postfix 后保留 Lavish/Nothing，因此入口仍可达且语义符合原设计。官方英文键 OutfitAnything/FoodRestrictionLavish/FoodRestrictionNothing 均存在，Nothing filter 为不允许全部，Lavish 是原起始宽松 filter。

无第三方目标、反射或 transpiler；Common XML 是空容器，无自有 DefOf/序列化。旧源声明使用 1.6.4488-beta，静态原 DLL 引用与源代码已核对；完整原/新引用索引分别为工作区 logs/44-NotSoManyPolicies/api-original.json、api-new.json。旧 RW/16 方法体的具体构建 provenance 未核验，未当作可靠完整语义基线。实际 1.7 上下游以本机固定反编译为准。

已执行：`scripts/dotnet.sh restore work/44-NotSoManyPolicies/1.7/Source/mod.csproj --configfile .nuget/nuget.config`，然后 `scripts/dotnet.sh build <同工程> --no-restore --configuration Release`，0 warnings/errors；对应 restore.log/build.log。XML/旧文件保留/引用及输出检查通过（static-validation.json）。最终只有 NSMP.dll 和 SDK deps.json，没有复制 Harmony/游戏/Publicized 引用；DLL SHA-256 `76745ee17a2ed99b42f8f84fd348e99a52bad30ea6cea0cb03fb031e837ebecb`。

没有现有单元测试；两个筛选函数使用游戏 Policy/翻译对象，测试真实构造路径比复制 label 条件的单元测试有效。新增 Tests/1.7/Mod44Checks.cs 已独立编译 0 warnings/errors（checks-build.log），待根代理运行。它确认两个具体 Harmony postfix 注册，然后实际构造两个数据库，检查仅保留 1/2 项、有效默认服装、食品 filter 含义，并确认玩家之后新建方案不会继续被删。所有测试数据库均为临时对象，不改真实游戏方案。

运行尚未执行；还需依赖 Harmony 的单 MOD 初始化/关键路径断言、按需设置页面可视与测试存档重载。原版加载旧存档的 BackCompatibilityConverter_Universal 可再添加 Ideology 方案；本 MOD 原设计只处理起始生成，并不清理已有存档/用户自建方案，本轮保持该范围，不能声称已验证存档所有方案都会变少。
