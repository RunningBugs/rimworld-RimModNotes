> 主代理游戏验证：`mod21-rev1248-k`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod21-rev1248-k`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod21-rev1248-j`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod21-rev1248-j`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 升级记录

目标 `1.7.4977 rev1248`。**已适配、编译及真实 IL 单元测试通过；实际上游衣架流程待运行**。保留原 Source/1.4/DLL、packageId 和发布 ID；独立 1.7 默认加载同相对 DLL 文件名 Template.dll，不双载旧产物。

## 真实 API 和必要修复

实际上游 khamenman.armorracks（Workshop1875828205）LoadFolders 回退 /+v1.6，声明只到1.6，不能仅凭旧声明判断不兼容。当前 DLL 固定参考在1.7/Source，Private=false，不输出引用；SHA与路径见 dependencies.json。上游普通 ArmorRacks/JobDriver/Mod/Settings 及完整调用流水线须先跑隔离基线。

旧补丁 TargetMethod 固定 `<MakeNewToils>b__1_0`，而当前真实上游已经是 `<MakeNewToils>b__2_0`（原 IL 第4359行）。旧 transpiler 还固定 ldloc.s 17，且找不到 Wear 时静默无效，不能直接加标签。新1.7通过该具体 driver 的 `<MakeNewToils>b__*` 方法中**实际调用准确 Wear 签名**唯一定位；零/多个候选明确失败。完整实际 IL 及选定类型源码在 logs/21-ArmorRacksForceWearFix。

转移 toil 当前一处 Wear(Apparel,bool,bool) 第4547行。新 transpiler 将此准确 instance 调用替换为静态 WearAndForce(tracker,apparel,dropReplaced,locked)，保留完整栈参数/返回值、标签/异常块与其他指令，不依赖局部编号；输入不就地修改，0/2匹配报错。包装先运行真实 Wear，再从 Apparel.Wearer 取实际穿着者，仅按实际 ArmorRacksModSettings.EquipSetForced 设 forcedHandler，与旧补丁目的相同。上游原生 TransferSetForced 不同开关仍按原流程运作，普通 Wear、武器和 storage 流程不拦截。

API: LoadedModManager.GetMod/GetSettings、public EquipSetForced、Pawn.outfits/forcedHandler.SetForced、Apparel.Wearer、Pawn_ApparelTracker.Wear 参数和语义都存在；无直接 private 编译访问，无需 Publicizer。没有存档字段或有效自有 XML Def/Patch迁移。原未用 Unity.Jobs.LowLevel.Unsafe using 与 HugsLib 模板不带到新工程；新net10.0精准 Krafs.Rimworld.Ref1.7.4977-alpha.1/Lib.Harmony3.0.0-rc.2，显式程序集PatchAll。完整静态引用见 api-references.json。

## 验证与限制

生产 build.log、Map检查 checks-build.log 均0warning/error；命令 `bash scripts/dotnet.sh build work/21-ArmorRacksForceWearFix/1.7/Source/mod.csproj -c Release --no-restore`。

`bash scripts/dotnet.sh run --project work/21-ArmorRacksForceWearFix/Tests/Unit/Unit.csproj -c Release --no-restore` passed：真实生产转换器精确替换、栈兼容、元数据、输入不可变、0/2拒绝；并对**已安装真实上游**发现b__2_0且转换其原始 IL（不是手造 IL 唯一证据）。使用固定真实 snapshot DLL 的局部普通 SDK 引导，不执行游戏函数；此测试不能证明 Waylith 实际补丁效果。

Tests/1.7/Mod21Checks.cs 只引用 game/Harmony，外部类型实际程序集反射。有效Pawn、真实ArmorRack、实际owner+storage设置、两件真实BasicShirt，直接执行实际上游 MakeNewToils 的最终 transfer initAction，验证 TransferSetForced=false 时 EquipSetForced=true 穿到实际Pawn且forced；关闭后第二件实际穿到Pawn但不forced；finally还原上游settings和清理生成对象。不是直接调用 Action/Transpiler替代转移链。尚未由本代理启动游戏；完整job走路/预订/超时、武器、过滤器变化、原有衣物、保存/重载及UI仍待验证。

运行依赖 brrainz.harmony、khamenman.armorracks（实际path `/Data/SteamLibrary/steamapps/workshop/content/294100/1875828205`）。上游根旧Assemblies与v1.6存在重复同相对文件名，原loader去重会选择versioned DLL；测试须确认实际选中sha。旧generated contracts变化已明确适配，今后多个Wear/未知delegate会报错，不静默误补丁。

## 实际测试夹具修正

`testing/cases/mod21-rev1248-j` upstreambaseline成功，但测试在转移之前第36行NRE；选定实际ArmorRack源码证明Settings/InnerContainer是公有字段（不是属性），夹具GetProperty返回null。新场景改用真实GetField并显式检查初始化，**保留全部真实transfer/forced开关断言**。没有产品修改；新checks编译后待root重跑。
