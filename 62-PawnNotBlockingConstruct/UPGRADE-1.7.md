> 主代理游戏验证：`mod62-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod62-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod62-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod62-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod62-functional`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod62-functional`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# RimWorld 1.7 beta 升级记录

MOD：Pawn Not Blocking Construction 小人不会阻碍建造。packageId：`RunningBugs.PawnNotBlockingConstruction`；Workshop ID：`3531018451`。

基线：游戏 `1.7.4977 rev926`，固定真实程序集与反编译代码位于 `/home/lisanhu/mine/workspace/rimworld/RW/17`。本次只修改工作区 `work/62-PawnNotBlockingConstruct` 副本，尚未回写原仓库。

状态：**已适配，静态/API/编译检查通过；真实游戏功能验证待主代理执行。** 没有启动游戏，也不以编译成功声称 MOD 已可用。

## 修改范围

- `About/About.xml` 增加 1.7，保留 1.6、packageId、Workshop ID 和依赖。
- 新增独立 `1.7/Source` 与 `1.7/Assemblies/PawnNotBlockingConstruction.dll`；程序集名称和输出相对路径不变。
- 新工程目标 `net10.0`，锁定 `Krafs.Rimworld.Ref 1.7.4977-alpha.1` 与 `Lib.Harmony 3.0.0-rc.2`。没有私有 API 直接访问，因此不添加 Publicizer。
- 1.7 源码唯一逻辑相关变化为 `harmony.PatchAll(typeof(Start).Assembly)`。实际 Harmony 3 的无参 `PatchAll()` 通过调用栈推断程序集，其作者注释说明 JIT 内联可能导致选错程序集；显式选择现有启动类程序集避免此风险。补丁内容、旧版源码/DLL、原 owner `com.RunningBugs.Test` 保留。
- README 增加新版本构建入口，区分原有旧模板的框架要求。
- `Tests/1.7/Mod62Checks.cs` 是给隔离测试驱动的真实游戏断言，测试产物不进入 MOD 的 `Assemblies`。

## 可发现 API 与语义审查

| 使用 / 入口 | 真实 1.7 证据 | 结论 |
| --- | --- | --- |
| `[StaticConstructorOnStartup]`、静态构造函数 | `Source/Verse/StaticConstructorOnStartupUtility.cs:14`，`PlayDataLoader.cs:359` | 属性仍由加载流程枚举并运行，启动入口有效 |
| `new Harmony(string)` / `PatchAll(Assembly)` | 本机 Harmony 1.7 DLL 与 NuGet 3.0.0-rc.2 的 net10 DLL 字节匹配；实际反编译 `HarmonyLib.Harmony` | 接口仍存在；显式程序集重载枚举目标程序集的补丁类 |
| `typeof(Start).Assembly` | .NET 10 `System.Type.Assembly` 属性，最终源码编译 | 获取本 MOD 自有类型所在程序集，不按字符串解析游戏类型或成员 |
| `Log.Message(string)` | `Source/Verse/Log.cs:108` | 存在且调用语义保持；保留现有加载消息 |
| `GenConstruct.BlocksConstruction(Thing constructible, Thing t)` | `Source/RimWorld/GenConstruct.cs:836` | public static bool，唯一同名目标，参数名/类型与 Postfix 绑定一致 |
| Postfix `(Thing constructible, Thing t, ref bool __result)` | 原方法仍返回 bool；补丁使用 Harmony 的 Postfix 名称约定 | 只在 `t is Pawn` 时覆盖返回值；原方法仍执行，非 Pawn 结果不改变 |
| Pawn → Thing 类型关系 | `Source/Verse/Pawn.cs` 与真实 API 编译 | `is Pawn` 判断仍覆盖所有 Pawn；无新私有成员依赖 |
| 实际调用链 | `GenConstruct.cs:743/752/767`；`WorkGiver_ConstructFinishFrames.cs:35` | FirstBlockingThing 仍逐格调用 BlocksConstruction，建造工作仍使用该结果，补丁不是失去调用者的旧入口 |

旧基线 `RW/16/Source/Properties/AssemblyInfo.cs` 可核验程序集版本 `1.6.9676.18020`。旧、新 `RimWorld/GenConstruct.cs` 的全文 diff 为空，相关签名和实现完全相同。此结论不借用 `RW/16` 已指向当前安装的动态 DLL 链接。

原版会在清理建筑面积的分支中把可见 Pawn 视为阻挡；本 MOD 在 Postfix 覆盖 Pawn 结果为 false。植物、物品、建筑、自身和其他 Thing 的原版判断保留。施工结束时原版生成最终建筑可能仍移动 Pawn；本补丁只改变阻挡判断，不改变原版生成/移动流程。

全部自有生产代码只有启动构造函数与一个 Postfix。除显式取得自有类型的 Assembly 外，没有生产代码中的字符串反射查找、动态访问、transpiler、Unity API、游戏字段/属性访问、重写、DefOf、序列化状态或存档字段。Common 的 Def/Patch/语言 XML 均为空模板，没有活跃定义或补丁目标。

## 加载与构建证据

没有有效 `LoadFolders.xml`，旧 `.bak` 文件不参与加载。1.7 按默认规则加载独立 `1.7`、Common 与根目录，只从其 `Assemblies` 子目录枚举 DLL；1.6 仍加载原 `1.6`。没有根/Common Assemblies，也没有显式指向 Tests/Source 的加载规则。

构建显式使用工作区 `NUGET_PACKAGES=.nuget/packages`、`DOTNET_CLI_HOME=.dotnet-home`，以及 HTTP/scratch/plugins 局部缓存；官方源配置为工作区 `.nuget/nuget.config`。在这些环境变量下执行：

```bash
/home/lisanhu/.dotnet/dotnet restore 1.7/Source/mod.csproj --configfile /home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/.nuget/nuget.config
/home/lisanhu/.dotnet/dotnet build 1.7/Source/mod.csproj --configuration Release --no-restore
/home/lisanhu/.dotnet/dotnet restore Tests/1.7/Mod62Checks.csproj --configfile /home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/.nuget/nuget.config
/home/lisanhu/.dotnet/dotnet build Tests/1.7/Mod62Checks.csproj --configuration Release --no-restore
```

工作目录为本 MOD 暂存副本。两份 Release 工程均 `0 warning, 0 error`；日志在工作区 `logs/62-PawnNotBlockingConstruct-{restore,build,checks-restore,checks-build}.log`。MOD 最终 Assemblies 仅含自身 DLL 和 deps.json，不复制 0Harmony、游戏引用或 publicized DLL。未添加 xUnit 等框架，也没有把游戏状态替换成桩来假装运行通过。

## 主代理可运行的核心场景

在隔离游戏中启用 `brrainz.harmony`、本 MOD、测试驱动；等可丢弃玩家地图和 Faction.OfPlayer 就绪后调用：

```csharp
RW17.Validation.Mod62Checks.Run(map);
```

检查源代码只引用真实游戏和 Harmony，不直接编译引用 MOD DLL。它检查实际目标上的 Postfix 所属程序集与类型，然后在空闲可站立格生成木墙 Frame、可见玩家 Pawn 和 Steel，断言：

1. 目标确实注册了本 MOD 的 Postfix。
2. Frame 不阻挡自身。
3. 同格可见 Pawn 的 BlocksConstruction 为 false。
4. 真实地图遍历 FirstBlockingThing 忽略同格 Pawn，返回 null。
5. Steel 对木墙仍是有效阻挡，BlocksConstruction 为 true。
6. 遍历保留物品阻挡，FirstBlockingThing 返回 Steel。

生成的 Thing/Pawn 在 finally 中清理；不删除现有地图物体。成功日志包含 `[RW17.Validation] Mod62 checks passed` 与六个场景标识；任一断言失败抛异常。该测试代码已编译，**尚未执行**，因此这里只列可运行场景，不列通过结果。

生产逻辑直接依赖游戏与补丁调用，没有值得脱离引擎做实现复刻的纯函数；单元测试未执行。除上述断言外，建议在测试存档中完成一次“Pawn 站在已备料墙 Frame 上、另一建设者继续 FinishFrame”的完整工作流程，观察建造完成与原版最终位置调整。完整工作/tick 流程、特殊 Pawn/隐形 Pawn、与其他构造补丁的组合仍待测。

原模板 Harmony owner 在多个模板 MOD 间可能重复，本次保留以控制范围；测试通过实际 PatchMethod 类型和程序集识别本补丁，不仅依赖 owner 字符串。多人组合的诊断/卸载归属风险另行记录，不将单 MOD 功能证据推广到任意组合。
