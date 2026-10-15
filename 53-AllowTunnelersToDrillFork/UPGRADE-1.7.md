> 主代理游戏验证：`mod53-rev1248-a`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod53-rev1248-a`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod53-wave2`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod53-wave2`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# Allow Tunnelers To Drill fork：1.7 beta 升级

packageId：`Porio.TunnulerFix.fork`；Workshop ID：`3503872878`；基线 `1.7.4977 rev926`。仅改暂存副本。状态：**1.7 内容已适配，加载/XML/检查代码编译通过；真实 Tunneler 工作 tick 待主代理运行。**

## 为什么 1.7 只需 XML

原可见功能来自根 `Patches/AllowMechToDrill.xml`：将原版 WorkGiverDef Drill 的 canBeDoneByMechs 从 false 改为 true。旧 DLL 唯一补丁是 XP 空技能防护：对 JobDriver_OperateDeepDrill 的编译器生成 closure 做 IL 插入，避免没有 skills 的机械体获得经验时报错。

真实 1.7 `RW/17/Source/RimWorld/JobDriver_OperateDeepDrill.cs` 已明确执行 `actor.skills?.Learn(SkillDefOf.Mining, 0.065f * delta)`。旧 1.6 可信源码与当前文件全文 diff 为空，同样已有保护。原版仍先执行 CompDeepDrill.DrillWorkDone，然后只给有 skills 的 Pawn 学习经验。因此 1.7 不需要这个冗余 transpiler，也不需要新建一个没有工作内容的空 DLL/工程。

旧补丁硬编码 closure 名、局部变量 0 和相对 `ldc.r4 0.065` 的指令位置，不能仅因为旧 C# 编译成功就继续套用。此次不是删除允许钻机功能，而是排除旧代码并保留实际 XML 权限变化；使用真实机械体 tick 和真人 XP 对照验证原版保护。

## 最小修改与旧布局保留

- About 仅增加 1.7 标签；原名称、packageId、发布 ID、作者及旧 Harmony 依赖保留。
- 新增 LoadFolders.xml：v1.7 **只列根目录 `/`**，因此根 Patches 正常加载，1.4/1.5 DLL 均不加载；根与 Common 本来没有 Assemblies。
- v1.6 明确使用 `1.5` + 根目录，保持原默认最近旧版本 fallback；v1.4 / v1.5 也各自保持对应目录 + 根布局。所有旧源码、DLL、PDB 和 1.4 自带 Harmony 文件不改。
- 不创建空 1.7 目录、空生产 DLL、空工程或新 publicized 引用；测试工程位于 Tests/1.7，未列入加载目录，不能作为发布程序集。

## API 与 XML 语义核验

| 入口 | 真实 1.7 证据 |
| --- | --- |
| Drill.canBeDoneByMechs | `Data/Core/Defs/WorkGiverDefs/WorkGivers.xml:964/974`，实际目标存在且原值 false，PatchOperationReplace 保留同一字段 |
| WorkGiver_DeepDrill | `Source/RimWorld/WorkGiver_DeepDrill.cs`，HasJobOnThing 检查派系、预留、供电/资源、卸载与着火；JobOnThing 生成 JobDefOf.OperateDeepDrill，未被改写 |
| JobDriver_OperateDeepDrill | 实际 MakeNewToils 生成 Goto 和持续工作两个 Toil；tickIntervalAction 使用 delta、真实 CompDeepDrill 与原版 skills 空值保护；保留人类 Mining XP |
| CompDeepDrill | `Source/RimWorld/CompDeepDrill.cs`，DrillWorkDone 依实际 DeepDrillingSpeed / MiningYield 更新 portionProgress / yield 并记录 tick，后续生产矿物仍是原版逻辑 |
| Biotech Tunneler | 官方 Biotech 重型机械体 XML 中真实 Mech_Tunneler ThingDef / PawnKindDef 存在；测试不能用仅手设 skills=null 的假 Pawn 代替 |
| 旧 Harmony API/反射/transpiler | 1.7 不加载旧程序集，因此这些旧闭包查找、OpCode 偏移、标签插入与 AssemblyInfo 不进入本版本执行路径。不是简单把旧引用改成新 NuGet 后声称 IL 兼容 |

没有新存档数据、组件、第三方接口或翻译。原版进度/资源/预留/经验含义未改。全部 XML 解析通过，显式路径确实排除旧 DLL，并按版本记录原加载目录。

## 编译与必须执行的真实场景

生产部分是 XML，不需要编译。只编译有实际用途的检查代码，采用专用环境及固定 Ref `1.7.4977-alpha.1`：

```bash
scripts/dotnet.sh restore work/53-AllowTunnelersToDrillFork/Tests/1.7/Mod53Checks.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/53-AllowTunnelersToDrillFork/Tests/1.7/Mod53Checks.csproj --configuration Release --no-restore
```

检查工程 net10.0，零警告零错误；日志在工作区 `logs/53-AllowTunnelersToDrillFork-checks-{restore,build}.log`。没有运行游戏，没有把编译当成可用证明。

主代理启用 Biotech，在完整 Playing / 无长事件的主线程调用 `RW17.Validation.Mod53Checks.Run(map)`。场景包括：确认两份旧 XP DLL 没被加载；确认真实 XML mech 权限；生成真实 Tunneler、实际 DeepDrill 与可用钢矿/供电条件；原版 WorkGiver 接受机械体并创建真实 drilling Job；真实 JobTracker 启动 JobDriver_OperateDeepDrill，从其已初始化的 work Toil 调用原版 tickIntervalAction(60)，断言钻机进度增加且 skills==null 没有异常。随后真人操作者重复真实工作 tick，断言 Mining XP 增加。

这不是复刻 DrillWorkDone 或只观察设置值的测试：调用实际 JobDriver 委托和实际进度组件。fixture 使用新对象和临时 DeepResourceGrid 内容，finally 清理自己生成的 Pawn/钻机并恢复原资源格；不修改用户存档和现有地图对象。

首版已真实执行并在真人 XP 对照断言失败；不能标成已通过。覆盖单个真实工作 callback，不代表完整长期自动排班、路径行走、电网模拟、完成矿物批次与保存重载均通过；这些保持待测。无脱离引擎的新增单元测试。特别禁止在旧 XP DLL 未正确排除时仅看无红字就标成成功。

## mod53-wave2 的 XP 观测修正（v2，待重跑）

该 case 的 Tunneler 无 skills、真实工作 job/driver/tick 与进度断言已经经过，最后真人 XP 对照失败。实际 `SkillRecord.cs:72` 的 XpTotalEarned **只返回完成等级门槛的累加值，不含当前 xpSinceLastLevel**；在没有升技能等级的单个工作 tick 中，它不改变，首版选错了观测指标。

v2 保留全部真实工作断言，改用 `XpTotalEarned + xpSinceLastLevel` 的真实累计经验比较，并记录两个原始分量/前后值；不提高技能、不直接喂 XP、不替换 Learn 或 work delegate。生产 XML/加载规则未变。零警告零错误编译日志为 `logs/53-AllowTunnelersToDrillFork-checks-build-v2.log`，首次失败的 case 与 results.runtime/history 保留，待主代理重新执行。


## 2026-10-15 名称和描述版本修订

本轮在现有1.7适配基础上更新About名称/描述版本措辞，packageId/发布ID/支持标签/生产源码与DLL不改。
名称：TEMPORARY 1.6 Allow Tunnelers To Drill → Allow Tunnelers To Drill (1.6/1.7)。

- 旧TEMPORARY 1.6标题与新1.7内容不符；保留功能名称、所有旧supportedVersions及上游致谢。
- 只替换当前支持声明与临时移除承诺；保留1.5作者/历史版本链接、功能和存档说明。

语法/身份/版本声明与原产物一致已核验；属于元数据修改，不将其当新功能验证。详细回写审计见工作区expansion/metadata-changes.json。
