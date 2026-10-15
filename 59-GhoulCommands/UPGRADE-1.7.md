> 主代理游戏验证：`mod59-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod59-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod59-wave1`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod59-wave1`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# GhoulCommands：1.7 beta 升级

packageId：`RunningBugs.GhoulCommands`；Workshop ID：`3521557696`；基线 `1.7.4977 rev926`。只修改暂存副本，状态：**已适配、API/静态/编译通过；真实 Anomaly 功能与存档/UI 待验证。**

## 最小修改

新增独立 `1.7/Source` 和同名 `1.7/Assemblies/GhoulCommands.dll`，目标 net10.0、精准 `Krafs.Rimworld.Ref 1.7.4977-alpha.1`。生产源码与旧 1.6 字节完全一致；没有 Harmony 或直接私有 API，不引入 Harmony/Publicizer。About 加 1.7；标识、设置/存档 key、默认值、XML 组件和思维树/JobDef 保留；README 说明新工程入口。

没有有效 LoadFolders.xml，默认版本/Common/根目录加载；旧 `.bak` 不参与。Assemblies 仅自有 DLL/deps.json，Tests 不在扫描树内，旧 1.6 全部文件保留。

## API / XML / 数据语义

全部静态引用和原始签名在工作区 `logs/59-GhoulCommands-api.json`。自有设置、组件、两个 JobDriver、两个 JobGiver 及其匿名回调均已读；生产代码没有反射查找、transpiler 或第三方 MOD API。

| API 组 | 真实 1.7 核验 |
| --- | --- |
| Mod/Settings/Scribe/UI | Mod 构造、GetSettings、SettingsCategory、DoSettingsWindowContents(Rect)、ExposeData、Look<bool>、Listing Begin/End/CheckboxLabeled 和 Translate 保留；两项全局启用默认 true/false 与标签 GhoulCommands.enable* 未改 |
| ThingComp / CompProperties / gizmos | ThingComp.CompGetGizmosExtra、PostExposeData（ThingComp.cs:109/27）仍是对应虚入口；parent、compClass、Command_Toggle 的 label/desc/icon/isActive/toggleAction 保留；Pawn 通过 Human XML patch 获得实际组件 |
| Pawn 资格 | Pawn.cs:716 的 IsGhoul 仍要求 Anomaly + Ghoul mutant；IsColonySubhumanPlayerControlled（834）仍要求 spawned player-controlled subhuman，未扩大到普通殖民者/敌对 ghoul |
| DefDatabase / DefOf | JobDef 绑定、JuggernautSerum 与 MetalbloodSerum ThingDef、JuggernautSerum / Metalblood HediffDef 实际存在；HediffSet.HasHediff 保持避免重复服用 |
| JobGiver | ThinkNode_JobGiver.TryIssueJobPackage→TryGiveJob 的旧、新源码完全一致；判断全局设置/组件标记/身份/已服用状态，然后查可达、未禁止、可预留血清，创建 Target A=Pawn、B=血清的 Job |
| 搜索与预留 | GenClosest.cs:40 ClosestThingReachable、ThingRequest.ForDef、TraverseParms.For、IsForbidden、CanReserveAndReach（ReservationUtility.cs:72）/Reserve（128）仍匹配；没有 ParentHolder==null 的旧假设 |
| JobDriver / Toils | MakeNewToils、TryMakePreToilReservations、Job.GetTarget、TargetIndex.B、Toils_Goto.GotoThing、所有 FailOn 条件保持；ChewIngestible / FinalizeIngest（RimWorld/Toils_Ingest.cs:294/395）所在文件旧、新全文相同；吃速 GetStatValue、useEatingSpeedStat 与 carryTracker.CarriedThing 分支保留 |

真实官方 XML：`Data/Anomaly/Defs/ThinkTreeDefs/Ghoul.xml` 仍有 Ghoul_PreWander 插入点；Human comps 目标存在；两份血清 ThingDef 与两份 HediffDef 的名称未变。Common/Defs/JobDefs 的 driverClass 指向本 MOD 类型，两个 JobDef 和 DefOf 字段一致。Common/Patches 的类名及两条 XPath 均有实际目标；Keyed/DefInjected 翻译标签与序列化字段保持，所有 XML 解析通过。

本 MOD 的功能与 Ghoul ThinkTree patch **需要 Anomaly**；本机测试基线具备该 DLC。旧 About 没有硬声明 Anomaly，未在此次兼容修改中重写旧元数据；未启用 Anomaly 的配置没有验证，不能列入兼容保证。

组件保存的 ghoulTakingJuggernautSerum / ghoulTakingMetalbloodSerum 默认均 false；不是新存档格式。JobGiver 仅产生命令，JobDriver 才执行走近、咀嚼、FinalizeIngest；仅检查生成 Job 不代表血清已经消费或效果已经应用。

## 构建与待执行检查

工作区隔离命令：

```bash
scripts/dotnet.sh restore work/59-GhoulCommands/1.7/Source/mod.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/59-GhoulCommands/1.7/Source/mod.csproj --configuration Release --no-restore
scripts/dotnet.sh restore work/59-GhoulCommands/Tests/1.7/Mod59Checks.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/59-GhoulCommands/Tests/1.7/Mod59Checks.csproj --configuration Release --no-restore
```

MOD 与检查代码均零警告零错误；日志在 `logs/59-GhoulCommands-{restore,build,checks-restore,checks-build}.log`。旧版本、XML、解析包版本和输出布局已检查；无脱离引擎的实现复刻单元测试。

主代理启用 Anomaly 后调用 `RW17.Validation.Mod59Checks.Run(map)`。它从真实 Ghoul ThinkTree 取两个插入后的 JobGiver，生成一个真实 player Ghoul 与两份血清，核验 Human 组件、默认关闭、两个实际 Command_Toggle 动作、真实 Job 选择与 driverClass、禁止物品、已有 Hediff 避免重服、全局关闭等分支。测试只引用游戏类型，通过实际组件/程序集识别 MOD；finally 恢复两项全局设置并清理新 Pawn/物品。检查已编译、尚未执行。

完整走路/预留/咀嚼/服用 tick、数量消耗、buff 效果、受伤/吃速条件、组件/全局设置的保存重载及可视按钮/纹理仍待验证。没有把 Job 生成检查推广为整个自动服用行为通过。
