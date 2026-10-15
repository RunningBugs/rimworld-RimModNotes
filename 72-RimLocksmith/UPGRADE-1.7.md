> 主代理游戏验证：`mod72-rev1248-m`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod72-rev1248-m`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod72-rev1248-j`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod72-rev1248-j`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# RimLocksmith：1.7 beta 升级

游戏基线 `1.7.4977 rev1248`，真实 `RW/17/Assemblies`/`Source`；原版 1.6 源码。packageId `RunningBugs.RimLocksmith`、Workshop `3750790724` 保持。状态：暂存适配完成，真实游戏功能待主代理串行执行。

## 修改与 API 审查

- About 增加 1.7；新增 `1.7/Source`、`1.7/Patches` 与 `1.7/Assemblies/RimLocksmith.dll`。旧 1.6 非缓存文件逐个比较保持原字节。无 LoadFolders，默认版本选择为 1.7+Common+根，只加载一份逻辑程序集。
- 新项目 net10.0，固定 `Krafs.Rimworld.Ref 1.7.4977-alpha.1`、`Lib.Harmony 3.0.0-rc.2`；没有私有访问需要，不引入 Publicizer。仅改启动为 `.PatchAll(typeof(RimLocksmithMod).Assembly)`，保留原 Harmony owner `runningbugs.rimlocksmith`。其余产品 C# 正文不变，未恢复已禁用的异象控制/路径代价补丁。
- 静态成员清单 `logs/72-RimLocksmith-api-audit.json`，97 个可识别直接游戏/Unity/Harmony 成员；继承/override、反射目标、XML 和 Scribe 另审查。共享 metadata 对比证明 Assembly-CSharp 三份合同全部相等，所用 Unity Rect/Color/GUI/Vector2 未涉及此次新增 IME API。
- `Building_Door.PawnCanOpen(Pawn p)` 是实际 public virtual 入口，1.6 与当前 1.7 该方法正文逐字相同：CanOpenDoors、特殊任务地图、CanOpenAnyDoor、围栏/牵绳、无派系门、释放囚犯、MachinesLike。原 `CheckFriendlyTouched`、`Notify_PawnApproaching`、`CanPhysicallyPass` 及路径规则继续调用它。Postfix 仅在原版 true 时进一步 deny，没有越权放行；保留商队驮兽单独控制与普通 open-any-door 豁免。
- `MainTabWindow_Inspect.get_ShouldShowPaneContents()`、`get_CurTabs()` 保持原签名/多选为空语义；原选门补丁保持。CompProperties→ThingComp、GetInspectTabs、listerBuildings、Reachability.ClearCache、Selector.SelectedObjects、Pawn 类别/Overseer/Lord/Released 与各 UI API 编译和 metadata 匹配；core 策略不引用任何游戏类型。
- XML 仍给 native `Building_Door`、`Building_MultiTileDoor` 注入实际 comp/ITab。四个可选 MOD 门 XML（Architect Fences、DoorsExpanded、LinkableDoors、SOS2）沿用原过滤；DoorsExpanded 两个动态 PawnCanOpen 名称仅在类型存在时注册。未引用第三方 DLL，不冒称已核验这些第三方 1.7 版本。
- Scribe 保留 `_hasConfig`、v1/v2→v3、OnlyPets→All 兼容及 settings 现有键；存档格式未新增/改变，实际跨版本保存重载仍待测。

## 构建与测试

工作区命令：

```bash
scripts/dotnet.sh build work/72-RimLocksmith/1.7/Source/mod.csproj -c Release --configfile .nuget/nuget.config
scripts/dotnet.sh run --project work/72-RimLocksmith/tests/RimLocksmith.Tests/RimLocksmith.Tests.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh run --project work/72-RimLocksmith/Tests/1.7/CoreChecks.csproj -c Release --configfile .nuget/nuget.config
RIMLOCKSMITH_TEST_VERSION=1.7 .venv/bin/python work/72-RimLocksmith/tests/source_invariant_tests.py
scripts/dotnet.sh build work/72-RimLocksmith/Tests/1.7/Mod72Checks.csproj -c Release --configfile .nuget/nuget.config
```

产品 Release 0 warnings、0 errors，`logs/72-RimLocksmith-build.log`；DLL SHA256 `27e8badc310fdf137c3e5372ee6ed81f0c85b95655f1eb675eae35b13d85a446`。输出只有 MOD DLL/deps.json，没有游戏/Harmony DLL。

既有有效独立 Core console tests 分别真实编译执行 1.6/net8 与 1.7/net10 全部通过，涵盖所有 category、不可配置类别、bool 开关、动物档位、机械体三态/overseer、clone/normalize。日志 `logs/72-RimLocksmith-core-1.6-tests.log`、`...-core-1.7-tests.log`。新项目链接现有测试 Program 与新版 Core，不复刻 policy。

既有 source_invariant_tests 新增环境参数，默认仍查 1.6，两个版本均通过；此结构字符串检查不作为运行功能证据。全部 XML 可解析。

`Tests/1.7/Mod72Checks.cs` 仅以游戏/Harmony 编译，反射实际 MOD 类型，0/0。供主代理在 Playing 主线程运行：真实 patch 注册、Door 原生 Def 的 comp/ITab、真实殖民者允许/拒绝/settings 门控、实际友好访客、原生囚犯拒绝/Released 豁免、真实 Cow 围栏不越权、Husky 全允许/不允许、native LordJob_TradeWithColony 的 Cow 驮兽权限、无派系野生狗拒绝、comp 的 clone/显式编辑。finally 恢复设置、移除测试 Lord 并销毁所有生成物件/Pawn。不存在假 Pawn/假权限方法。

## 未测范围

尚未执行真实 Map 检查；窗口/多门 ITab/批量 UI/拷贝粘贴、路径实际绕行/cache 影响、机械师绑定、奴隶、越狱/叛乱、异常实体及第三方门组合、v1/v2真实存档读取/保存重载待验证。原 docs 的历史 behavior audit 保留为历史事实，不把过时架构描述当作当前实现；现源码 postfix 实现是迁移基线。

## 实测夹具修正 v2

主代理真实 `mod72-rev1248-j` 前面的殖民者/访客/释放囚犯断言均已执行，到旧检查line58未生成的Cow发生原生PawnCanOpen NullReference。原方法 `p.FenceBlocked` 为true后直接访问 `p.roping.IsRopedByPawn`；PawnComponentsUtility.AddComponentsForSpawn创建roping，RemoveComponentsForDespawn置null，所以仅GeneratePawn的Cow不能作为门实际行走前置。Harmony堆栈列出的Postfix元数据不意味着Postfix已执行。本轮仅修检查Make：所有Pawn真实GenSpawn并断言roping存在，保留所有权限/围栏/商队断言及生产DLL不变。旧失败保存在results.runtime；v2检查0/0，`logs/72-RimLocksmith-native-check-build-v2.log`，待主代理重跑。
