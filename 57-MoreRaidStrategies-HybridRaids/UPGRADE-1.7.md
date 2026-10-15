> 主代理游戏验证：`mod57-rev1248-d`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod57-rev1248-d`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod57-wave2`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod57-wave2`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# Hybrid Raids：1.7 beta 升级

packageId：`RunningBugs.MRS.Hybrid`；Workshop ID：`3516953777`；基线 `1.7.4977 rev926`。只改暂存副本。状态：**已适配，API/静态/编译通过；真实袭击功能待主代理执行。**

## 最小修改及既有缺陷

新增独立 1.7 net10.0 工程/同名 `MoreRaidStrategies.HybridRaids.dll`，Ref 固定 `1.7.4977-alpha.1`，原工程中的 Harmony 编译包固定 `3.0.0-rc.2`。生产代码没有 Harmony 调用，实际 DLL 元数据不引用 Harmony，因此不增加玩家 Harmony 依赖；没有需要 Publicizer 的直接私有访问。

**验证发现的既有缺陷**：旧 CanUseWith 临时将共享 `IncidentParms.points` 改为子队点数，子策略拒绝时在恢复前 return false。这会污染同一次原版袭击策略选择中的共享预算。仅新 1.7 加入局部 try/finally，无论成功、拒绝或异常均恢复原值；子策略调用、返回结果、预算倍率、生成/到达/Lord 流程不改。旧 1.6 源码和 DLL 保留。

About 增加 1.7、README 增加新入口，标识/现有 XML 策略/翻译不改。没有有效 LoadFolders.xml，默认加载独立版本/Common/根目录；`.bak` 忽略。最终 Assemblies 仅自身 DLL/deps.json，无游戏或 Harmony DLL；Tests 不在实际程序集扫描树内。

## 可发现 API、反射与 XML

全部静态引用/原始签名：工作区 `logs/57-MoreRaidStrategies-HybridRaids-api.json`。四份生产 C# 已逐项读，包括空继承 arrival worker、DefModExtension、SubStrategy 及其 fallback。

| API 组 | 真实 1.7 证据 |
| --- | --- |
| RaidStrategyWorker 的 MakeLordJob / MakeLords / SpawnThreats / CanUseWith | `RW/17/Source/RimWorld/RaidStrategyWorker.cs` 与旧 1.6 全文相同；protected abstract MakeLordJob(IncidentParms, Map, List<Pawn>, int) 与三个 public virtual 入口保持。原版 IncidentWorker_Raid 仍调用生成及 Lord 流程 |
| 反射缓存 | GetMethod("MakeLordJob", Instance|NonPublic) 仍只有一个对应重载，Invoke 对实际子策略 Worker 虚派发，返回 LordJob；不是空 MakeLordJob override 被错误直接调用 |
| IncidentParms / PawnGroupMakerParms | target/faction/points/raidStrategy/raidArrivalMode/pawnGroupMakerSeed/raidAgeRestriction/inSignalEnd/questTag、IIncidentTarget.Tile 与生成参数均保留；新编译目标已匹配 PlanetTile 等实际类型 |
| PawnGroupMakerUtility.GeneratePawns | `PawnGroupMakerUtility.cs:22`，返回 IEnumerable<Pawn>，原来的 ToList、战略/arrival 字典关联与分组顺序保留 |
| Arrival / LordMaker | PawnsArrivalModeWorker.Arrive、TryResolveRaidSpawnCenter（108/115）、EdgeWalkIn 继承和 LordMaker.MakeNewLord 保留；不同到达方式可能先让 Pawn 处于空投容器中，不能假设所有返回 Pawn 已 Spawned 或 ParentHolder 为 null |
| Def / Curve / Rand / 文本 | Def.GetModExtension、RaidStrategyDef.Worker/arriveModes/pointsFactorCurve、PawnsArrivalModeDef.Worker/pointsFactorCurve/textEnemy、SimpleCurve.Evaluate、RandomElement、Rand.Int、Faction.Name/pawnsPlural/ApplyTag/Translate 及 LINQ 集合接口保留 |
| 自有 XML / DefOf | HybridArrival、MRS_SiegeAndBreach、MRS_CenterDropPodAndBreach 和扩展类均能对应 C# 名称；实际 Core 的 Siege、ImmediateAttackBreaching、ImmediateAttackSmart、CenterDrop 都存在；Arrival DefOf 字段一致 |

两种混合仍使用 0.5+0.7、0.6+0.6 的预算份额，随后分别乘原版战略/arrival 的曲线；这是原设计，不变成总和 1.0。SubStrategy.GetPawnsArrivalModeDefs 保留显式 override 优先、否则使用战略 arriveModes 的逻辑。Common/Patches 和模板 LetterDef 无活跃操作；翻译指向实际 RaidStrategyDef 字段，全部 XML 解析通过。没有新增序列化状态或存档迁移。

## 编译及可运行验证

工作区 `scripts/dotnet.sh` 隔离 SDK 用户目录和 NuGet 缓存：

```bash
scripts/dotnet.sh restore work/57-MoreRaidStrategies-HybridRaids/1.7/Source/mod.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/57-MoreRaidStrategies-HybridRaids/1.7/Source/mod.csproj --configuration Release --no-restore
scripts/dotnet.sh restore work/57-MoreRaidStrategies-HybridRaids/Tests/1.7/Mod57Checks.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/57-MoreRaidStrategies-HybridRaids/Tests/1.7/Mod57Checks.csproj --configuration Release --no-restore
```

两个工程零警告零错误；日志为 `logs/57-MoreRaidStrategies-HybridRaids-{restore,build,checks-restore,checks-build}.log`。旧 1.6 全文件、XML、包解析与输出布局已检查。

主代理在一次性地图调用 `RW17.Validation.Mod57Checks.Run(map)`。检查实际加载的两种策略/扩展/arrival fallback/override，以及产品 MakeLordJobMethod 的精确签名；选实际满足全部 native Siege/Breach 条件的派系/预算与不可围攻对照，验证 CanUseWith 成功/拒绝以及两条路径保留原预算。临时 Harmony 观察前缀只记录 native Siege 子 Worker 实际接收的预算×0.5，不改变参数/返回值，并在 finally 卸载，用来证明拒绝测试真正进入过子策略而非在 base 检查前提前结束。随后实际 SiegeAndBreach.SpawnThreats 生成两类 raider group，并实际 MakeLords 验证两个原版 Lord 覆盖所有产生的 Pawn。它不引用 MOD DLL；不是替代 Worker 或模拟分组。

测试使用局部 Rand.PushState，finally 恢复随机状态、arrival 文本，移除自己新增 Lord/Thing/Pawn 并清理测试 worker 字典，保留原有地图对象。检查要求新的空 worker 缓存与能使用围攻的实际地图/派系；不通过修改原版 faction 定义或伪造 Worker 让场景通过。

首版检查已经真实运行但在有效派系前置判断失败，修正后的 v2 已编译、待重跑；没有实现复刻单元测试。中心空投+破墙的完整生成/打开空投舱、实际长期 Lord 行为、炮击/破墙战斗、袭击通知/任务信号/任务标签、多次事件与组合 MOD 仍待验证。原产品本次仅修复已观察到的预算恢复问题，不以编译证明上述行为全部通过。

## mod57-wave2 输入条件复核与 v2

旧夹具仅取第一个 canSiege 派系，就假设 2000 点混合袭击有效。实际 `RaidStrategyWorker_WithRequiredPawnKinds` 要求 Combat group maker 包含 isGoodBreacher 类型；原版 Siege/Breach 还检查点数下限、selection weight、派系禁用列表、地块 mutator 与 layer。canSiege 单旗标不足以构成成功控制，本轮没有证据把首次拒绝归为产品回归。

v2 只修改夹具，依次在真实世界派系及 2000/4000/8000 点有限候选中，独立调用原版 Siege 与 Breach 的完整 CanUseWith，要求两者真实接受后才调用混合 worker 的成功断言；记录派系 Def、原预算、两份 native 结果与 map layer。不改原版 Def、不替代 worker，不在无有效输入时跳过或当成成功。拒绝派系还要求存在有效 Combat pool 与足够基本点数，仍必须由观察前缀证明实际进入 Siege 子调用，并断言预算恢复。

完整 SpawnThreats / 两类 Pawn / 两个原版 Lord 断言保持，没有削弱成只看 Def 加载。编译日志 `logs/57-MoreRaidStrategies-HybridRaids-checks-build-v2.log`；首次 case / results.runtime/history 保留。当前基线升级至 rev1248 后，Assembly-CSharp 全 schema/正文不变，仍锁定 alpha1，但最终功能状态待当前运行时重跑。
