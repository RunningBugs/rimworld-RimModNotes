> 主代理游戏验证：`mod49-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod49-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod49-wave1`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod49-wave1`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 beta 升级记录

状态：已适配，游戏验证未完成。基线 `1.7.4977 rev926`，发布 ID `3461796028`，packageId `RunningBugs.UnlockHarbingerTreeInAllCases`。

新增独立 1.7 工程/DLL，Main.cs 与原 1.6 源逐字节相同，已有 PatchAll 显式使用 Assembly.GetExecutingAssembly，无需改动。net10.0，锁定 Krafs.Rimworld.Ref 1.7.4977-alpha.1、Lib.Harmony 3.0.0-rc.2。源码没有直接访问私有字段/方法，移除新工程里原模板 PublicizeAll/Publicizer 和未使用的 HugsLib 编译包；真实 Harmony 保护方法入口通过 Harmony 注册访问，不依赖 Publicizer。原/新静态引用均无 HugsLib。About 添加 1.7，旧 1.5/1.6/Common/备份全保留；默认目录选择保证只加载同名 1.7 DLL。

完整 API/补丁审查：启动 Harmony；DefOf 的 EntityCodexEntryDef HarbingerTree；IncidentWorker_HarbingerTreeSpawn.TryExecuteWorker Prefix；IncidentWorker_SpecialTreeSpawn.CanFireNowSub Postfix；Find.EntityCodex、EntityCodex.Discovered/SetDiscovered；Messages.Message、Translate、MessageTypeDefOf.NeutralEvent；IncidentParms.target、Map.Biome.isExtremeBiome、派生类类型判断。无 transpiler、自有序列化或第三方目标。

真实 `RW/17/Source/RimWorld/IncidentWorker_HarbingerTreeSpawn.cs:18` 仍 override bool TryExecuteWorker(IncidentParms)，先执行父类树木生成再通知 Anomaly；public IncidentWorker.TryExecute 仍调用该方法。Prefix 在所有生成结果之前执行，因此失败也解锁的意图仍成立。SpecialTreeSpawn.cs:27 的 protected CanFireNowSub 仍在非 bypass 下拒绝 extreme biome，并检查期望数量/生成位置；原 postfix 只在原结果 false、实际 Harbinger 派生实例、Map 目标且 extreme 为真时翻转结果，其他事件和普通生态不变。SetDiscovered 的可选参数仍可省略，实际设置条目/类别，并发送游戏信号；这是有游戏状态副作用的功能，不能仅测 mock。

官方 Anomaly Def 仍定义 HarbingerTree EntityCodexEntry、HarbingerTreeSpawn Incident 和 Plant_TreeHarbinger；英文/中文 HarbingerTreeForcedDiscovered Keyed 保留。该功能需要 Anomaly；既有 About 只有 loadAfter，不以此声称无 DLC 可用，本轮没有改变用户 DLC 约定。Common Def/Patch 为空容器。

构建：`scripts/dotnet.sh restore work/49-UnlockHarbingerTreeInAllCases/1.7/Source/mod.csproj --configfile .nuget/nuget.config`，`scripts/dotnet.sh build <同工程> --no-restore --configuration Release`，0 warnings/errors。原/新完整静态成员/类型引用分别在 logs/49-UnlockHarbingerTreeInAllCases/api-original.json、api-new.json；XML/旧文件/输出检查通过（static-validation.json）。只生成本 MOD DLL/deps.json，没有复制 Harmony/游戏 DLL。

无已有单元测试，三个自有逻辑函数（启动与两个补丁）全部依赖游戏/Harmony/DLC状态，使用真实集成断言。新增 Tests/1.7/Mod49Checks.cs 独立编译 0 warnings/errors（checks-build.log），待主代理运行：确认具体补丁注册，实际调用受补丁保护方法；临时 extreme biome+零期望数量测试事件可触发，普通生态/其他特殊树保留拒绝；实际执行失败时仍发现图鉴，再次执行保持幂等且无新树。以 finally 恢复 codex、生态与期望数量，不使用真实用户存档。

尚未执行真实游戏。待测试 Anomaly+Harmony 的单体验证、相关消息、普通可生成地图及一次性存档保存/重载。没有独立纯计算函数可做有效脱离游戏单测，未用重复实现的布尔测试替代真实入口。旧 RW/16 反编译构建出处未核验，因此旧语义比较仅依靠保存的 MOD 源/静态引用，当前真实调用链以固定 1.7 源码为准。
