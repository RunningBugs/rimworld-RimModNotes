> 主代理游戏验证：`mod63-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod63-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod63-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod63-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod63-functional-v3`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod63-functional-v3`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod63-functional-v2`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod63-functional-v2`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod63-functional`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod63-functional`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# Hide Genebank Genes From Traders：1.7 beta 升级记录

状态：**已适配并编译；游戏加载、功能断言与可视交易界面验证待主代理执行**。本次仅修改工作区完整暂存副本，没有改写 RimModNotes 或启动游戏。

- packageId：`RunningBugs.HideGenebankGenesFromTraders`。
- Workshop ID：`3531100861`，保留原文件。
- 游戏基线：`1.7.4977 rev926`；真实固定程序集/反编译：`/home/lisanhu/mine/workspace/rimworld/RW/17/`。
- 原项目：`1.6/Source/mod.csproj`，net472，原先浮动 RimRef 1.6 / Harmony 2.3。

## 实际修改

1. `About/About.xml` 保留 1.6 并添加 1.7；名称、描述、packageId、Workshop ID、Harmony 依赖均不变。
2. 新增独立 `1.7/Source`、`1.7/Assemblies`：net10.0，锁定 `Krafs.Rimworld.Ref 1.7.4977-alpha.1` 与 `Lib.Harmony 3.0.0-rc.2`。保留原程序集名和相对 DLL 文件名，关闭引用 DLL 复制。
3. 1.7 启动改为 `harmony.PatchAll(typeof(ApplyPatches).Assembly)`，避免 Harmony 作者指出的无参调用被内联时选错程序集问题；owner ID 不变。两个 Postfix 的筛选实现不变。原文件缺少结尾换行，新副本同时补上该换行。
4. 新增测试专用 `Tests/1.7/Mod63Checks.cs` 与编译检查工程 `checks.csproj`。它们不在任何游戏加载目录，不能进入 MOD 发布 Assemblies。

所有原有 1.6 文件已逐字节与仓库原件比较，未修改。没有安装全局依赖，也没有复制或改写原版程序集。本 MOD 不访问私有成员，无需添加 Publicizer。

## 全部可发现的 API 与补丁入口审查

编译产物元数据审查保存在工作区 `logs/63-HideGenebankGenesFromTraders/api-references.json`，含 22 个 MemberRef 和 33 个 TypeRef（包括框架及编译器属性）。它不是运行跟踪；名称式 Harmony 目标需以下源码证据补充。

| 使用项 | 1.7 核验与语义 | 证据 |
| --- | --- | --- |
| `StaticConstructorOnStartup`、启动类静态构造函数 | 加载器仍枚举带属性类型并运行静态构造函数；构造异常记录到日志，因此需核验注册，不能只看进程退出码。 | `RW/17/Source/Verse/StaticConstructorOnStartupUtility.cs:16` |
| `Harmony(string)`、`PatchAll(Assembly)`、HarmonyPatch/Postfix | 精确 Harmony 3 编译成功；当前没有 transpiler、反射私有成员或旧 IL 假设。显式指向本 MOD 程序集；补丁采用结果 enumerable 的 Postfix。 | `1.7/Source/Main.cs:16`；共享 `api-changes.md` |
| `Pawn_TraderTracker.ColonyThingsWillingToBuy(Pawn)` | 仍为公开实例方法，返回 `IEnumerable<Thing>`；参数 `playerNegotiator`、`__instance` 的类型及 Harmony 注入一致。仍主动枚举可达 GeneBank 的有效基因包，故原补丁入口没有失效。 | `RW/17/Source/RimWorld/Pawn_TraderTracker.cs:96` |
| 地面交易上游 | Pawn 的同名方法仍转调 tracker；TradeDeal 仍向 ITrader 获取销售物品，路径继续有效。 | `RW/17/Source/Verse/Pawn.cs:4669`；`RW/17/Source/RimWorld/TradeDeal.cs:46` |
| `TradeUtility.AllLaunchableThingsForTrade(Map, ITrader = null)` | 仍为公开静态方法，返回 `IEnumerable<Thing>`；Postfix 的 map/trader 参数保持一致。仍从已开电轨道信标覆盖范围枚举 GeneBank 中的有效包。 | `RW/17/Source/RimWorld/TradeUtility.cs:84` |
| 轨道交易上游及既有影响范围 | TradeShip 的销售枚举仍调用该方法；FactionDialogMaker 和地图银币统计也调用此工具方法。这是旧补丁已有影响范围，本次没有扩大。 | `RW/17/Source/RimWorld/TradeShip.cs:89`；`FactionDialogMaker.cs:147`；`TradeUtility.cs:232` |
| `ModsConfig.BiotechActive` | 仍为公开 bool 属性；未启用 Biotech 时不执行基因包过滤，保持旧行为，不新增硬 DLC 依赖。 | `RW/17/Source/Verse/ModsConfig.cs:85` |
| `Thing`、`Genepack`、`CompGenepackContainer`、`Thing.ParentHolder`、`IThingHolder` | ParentHolder 仍取 `holdingOwner?.Owner`；GeneBank 用以自己为 owner 的 ThingOwner 保存基因包，所以容器内包仍直接持有 CompGenepackContainer。只隐藏该容器直接持有的 Genepack，散放/其他容器与非基因物品继续保留。 | `RW/17/Source/Verse/Thing.cs:388`；`RW/17/Source/RimWorld/CompGenepackContainer.cs:41`、`:57` |
| `IEnumerable<Thing>`、`Enumerable.Where`/Func | 继续延迟枚举，不改变排序；容器归属在枚举时读取，取出后应恢复可售。无自定义缓存/序列化字段。 | `1.7/Source/Main.cs:30`、`:52` |
| `UnityEngine.Color.green`、`ColoredText.Colorize(string, Color)`、`Log.Message(string)` | 编译引用 Color 现在来自 UnityStubs；文字着色和日志入口仍存在，未改显示逻辑。 | 元数据审查；`RW/17/Source/Verse/ColoredText.cs:245`、`Log.cs:108` |

当前 `RW/16/Source` 与固定 `RW/17/Source` 的 `Pawn_TraderTracker.cs`、`TradeUtility.cs` 全文件内容逐字节相同。旧目录整体版本尚未独立核验，因此这里把该比较作为已有参考代码的相等证据；1.7 目标存在、签名和行为结论以真实 1.7 快照为准。没有使用 `RW/16` 的动态 DLL 链接构建。

没有实际 `LoadFolders.xml`，只有不加载的 `.bak`。默认加载规则会选择新增 1.7，再加载 Common 和根目录；1.6 目录不会被选中。最终可加载 DLL 仅 `1.7/Assemblies/HideGenebankGenesFromTraders.dll`，没有 Harmony/引擎/参考包 DLL。Common 的两个 XML 文件是空 Defs/Patch 模板，语言文件为空 LanguageData；均解析通过，无实际 Def/Patch/语言注入目标需迁移。

## 构建与静态验证

从本次工作区执行（包装器隔离 NuGet 缓存和 .NET 用户目录）：

```bash
bash scripts/dotnet.sh restore work/63-HideGenebankGenesFromTraders/1.7/Source/mod.csproj --configfile .nuget/nuget.config
bash scripts/dotnet.sh build work/63-HideGenebankGenesFromTraders/1.7/Source/mod.csproj --no-restore -c Release
bash scripts/dotnet.sh tools/api-audit/bin/Release/net10.0/ApiAudit.dll work/63-HideGenebankGenesFromTraders/1.7/Assemblies/HideGenebankGenesFromTraders.dll
bash scripts/dotnet.sh restore work/63-HideGenebankGenesFromTraders/Tests/1.7/checks.csproj --configfile .nuget/nuget.config
bash scripts/dotnet.sh build work/63-HideGenebankGenesFromTraders/Tests/1.7/checks.csproj --no-restore -c Release
```

- 生产 DLL 与测试断言编译均为 **0 warnings / 0 errors**。
- 生产 DLL：5120 字节，SHA-256 `98defc489e681c06a7b8f663fbc9014135f1aae8ddcdd1ffcbcef768773f0f8f`。
- 日志：工作区 `logs/63-HideGenebankGenesFromTraders/{restore.log,build.log,checks-restore.log,checks-build.log,api-references.json,static-checks.json}`。
- 旧版文件完整保留、全部实际 XML 可解析、1.7 Assemblies 无误部署依赖，均已检查。

## 单元测试与游戏功能场景

原 MOD 没有独立单元测试，也没有可脱离真实游戏对象的自有算法；三项行为是启动注册与两个容器过滤 Postfix。没有机械复制筛选实现作“单元测试”。新增 `RW17.Validation.Mod63Checks.Run(Map)` 供主代理合并进独立测试 MOD，**只通过了代码编译，尚未在游戏执行**。

该场景需要一次性玩家地图、已生成殖民者、Biotech 和本 MOD/Harmony 已初始化。它会：

1. 检查两个真实目标上具体 owner + PatchMethod 类型/方法名的 Postfix 已注册。
2. 在殖民者附近空地创建临时 GeneBank、开电 OrbitalTradeBeacon、库内和散放各一个含 Inbred 基因的 Genepack，以及 Silver；恢复测试修改的 Home 格并清理全部生成对象。
3. 仅暂时卸下本 MOD 两个具体 Postfix 的未过滤真实方法确认两条交易枚举原本都包括三件夹具，防止用错误摆放导致“缺失”冒充隐藏成功。
4. **实际调用**地面 `Pawn_TraderTracker.ColonyThingsWillingToBuy` 和轨道 `TradeUtility.AllLaunchableThingsForTrade`，断言库内包不可见，散放包与银币仍可见。
5. 将原库内包取出并放到信标覆盖的 Home 格，再调用两条真实枚举，断言恢复可见。

测试只改变一次性地图；在内层 finally 用原 owner、优先级、before/after 恢复两个具体 Postfix，不使用 UnpatchAll，不修改其他补丁。未使用单独调用 Postfix 或复制生产谓词来代替实际销售链。此控制场景用新 tracker 包装现有殖民者、用轨道工具的可选 trader=null，足以执行所补丁的真实枚举；仍须另补可视贸易窗口/真实商队或 TradeShip 场景，及 Biotech 禁用时的加载验证。

剩余未测：游戏加载与上述场景运行、真实交易 UI、Biotech 禁用、第三方交易/基因容器组合。没有新增存档字段或写入路径；本 MOD 本身不改变存档结构，但未执行保存/重载组合验证。不要把编译成功混入“全部功能验证通过”。

## 首次真实场景失败与测试修正

主代理 `testing/cases/mod63-functional` 的初次场景已执行：加载和具体补丁注册通过，测试在归属链前置断言失败。原断言错误地假设散放包 ParentHolder=null；实际 1.7 的 `Thing.SpawnSetup` 会把所有生成物品加入 `map.spawnedThings`（`RW/17/Source/Verse/Thing.cs:831`），其 owner 是 Map（`Map.cs:512`）。GeneBank 内部 owner 仍为 CompGenepackContainer，生产筛选无需改变。

测试已修正并加强两条实际链断言：库内包必须 ParentHolder=container、holdingOwner=container.innerContainer 且未 Spawned；散放/取出包必须 ParentHolder=map、holdingOwner=map.spawnedThings 且 Spawned。保留未过滤真实枚举控制及所有可售性断言，未删除失败前置条件。修正后的测试重新编译通过，生产 DLL 内容/哈希未改变；等待主代理用新用例重跑，初次失败不能标作功能验证通过。

## 第二次运行的 native 中止及控制方案调整

主代理 testing/cases/mod63-functional-v2 返回 -6（SIGABRT），没有应用层 CrashReport 或地图结果。系统 coredumpctl 的 PID 158510 记录显示后台线程158640经 libcoreclr sigtrap_handler→PROCAbort，原始信息已保存 logs/63-HideGenebankGenesFromTraders/v2-coredump-info.txt。这尚不能证明 MOD 或 reverse patch 是根因；测试驱动当时在后台地图 FinalizeInit 执行，末尾仅有新游戏初始化日志。

为减少控制方法的不确定性，新场景取消所有 reverse patch，占位控制 stub 已移除。控制阶段仅 Unpatch 记录的本 MOD 两个具体 Postfix MethodInfo，调用真实销售方法确认库内/散放/银币输入均存在，然后内层 finally 恢复原 owner、优先级和 before/after；随后执行原有隐藏/取出断言。增加 entered、fixture、control、restore、filter、ejection 阶段控制台标记并刷新输出。生产 DLL 未变，测试再次编译通过；等待主代理串行 v3 重跑，并建议在完整初始化后的主线程调度检查。
