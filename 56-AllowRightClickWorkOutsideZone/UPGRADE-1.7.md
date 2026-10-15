> 主代理游戏验证：`mod56-rev1248-a`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod56-rev1248-a`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod56-wave1`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod56-wave1`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod56-functional`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod56-functional`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# Priority Order Bypass Restrictions：1.7 升级记录

packageId：`RunningBugs.AllowRightClickWorkOutsideZone`；Workshop ID：`3514313356`。本机基线 `1.7.4977 rev926`，只改工作区副本。

状态：**已适配，静态/API/编译通过；真实菜单逻辑与可视交互待主代理验证。**

## 最小修改与布局

新增独立 1.7 工程和 `1.7/Assemblies/ARCWOZ.dll`，目标 `net10.0`、`Krafs.Rimworld.Ref 1.7.4977-alpha.1`；生产 C# 与旧 1.6 完全一致。此 MOD 是 FloatMenuOptionProvider 子类，不使用 Harmony，亦无直接私有成员依赖，故不引入 Harmony/Publicizer 或新的加载依赖。About 加 1.7，保留旧版本与标识。README 补充新构建入口。

没有有效 LoadFolders.xml，默认采用精确版本目录、Common 与根目录；`.bak` 不启用。Assemblies 仅自身 DLL/deps.json；Tests 不在 Assemblies 加载树内；所有旧 1.6 文件保留。

## 所有可发现 API / 语义

静态成员与类型引用完整保存在工作区 `logs/56-AllowRightClickWorkOutsideZone-api.json`。自有代码只有 GetWorkPrioritiesSettings、GetOptions、GetOptionsFor；using System.Reflection 并没有对应的生产反射调用。

- `FloatMenuOptionProvider_WorkGivers.GetOptions(FloatMenuContext)`、`GetOptionsFor(Thing, FloatMenuContext)` 的返回类型、public virtual 入口和实现保持：旧、新该基类文件全文 diff 为空。真实 `RW/17/Source/RimWorld/FloatMenuMakerMap.cs:21/84/96` 仍发现非抽象子类并调用两个入口，故无须添加 XML Provider Def。
- `FloatMenuContext.FirstSelectedPawn` 仍按 currentProvider 验证所选 Pawn；上下文构造函数仍从 Map/点击位置缓存真实目标。保留单选与基类适用性限制。
- `DefDatabase<WorkTypeDef>.AllDefsListForReading`、`Pawn.WorkTypeIsDisabled`、`DefMap<WorkTypeDef,int>` 构造/索引/枚举保持；新框架下 LINQ Where/ToList 仍把基类迭代器在临时解除限制期间完整执行。
- `Pawn.workSettings`、`Pawn_WorkSettings.GetPriority/SetPriority`（`Source/RimWorld/Pawn_WorkSettings.cs:140/160`）以及 `Pawn.playerSettings.AreaRestrictionInPawnCurrentMap`（`Pawn_PlayerSettings.cs:85`）接口保持。先保存全部可做工作优先级与当前地图区域，临时设优先级 3 / 区域 null，再恢复原值；不能将“临时生成菜单”理解为永久修改工作设置。
- `FloatMenuOption.Label` get/set、Translate 与 TaggedString 转换接口保持；对两个菜单路径追加同一 Keyed 翻译 `BypassRestrictions`。Common 翻译键及全部 XML 解析通过；Def/Patch 是空模板，没有活跃 Def 修改。
- 没有序列化/存档字段、Harmony 目标、transpiler、直接 Unity API 或额外 MOD 接口。基类 `MechanoidCanDo=false` 的原有限制保留；升级不顺便改变支持对象。

原代码未用 finally 保护生产侧的临时设置变更；若基类枚举在未捕获异常下终止，恢复路径可能不执行。这是保留的原有异常路径风险，正常调用恢复必须实际测试，不以接口未变掩盖它。

## 编译与实际测试入口

在工作区用隔离 `scripts/dotnet.sh`：

```bash
scripts/dotnet.sh restore work/56-AllowRightClickWorkOutsideZone/1.7/Source/mod.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/56-AllowRightClickWorkOutsideZone/1.7/Source/mod.csproj --configuration Release --no-restore
scripts/dotnet.sh restore work/56-AllowRightClickWorkOutsideZone/Tests/1.7/Mod56Checks.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/56-AllowRightClickWorkOutsideZone/Tests/1.7/Mod56Checks.csproj --configuration Release --no-restore
```

MOD 与检查工程编译通过，零警告零错误；日志在 `logs/56-AllowRightClickWorkOutsideZone-{restore,build,checks-restore,checks-build}.log`。无独立纯函数单元测试；检查使用真实游戏类型与调用链，没有以替代实现冒充游戏。

主代理在当前可丢弃地图上调用 `RW17.Validation.Mod56Checks.Run(map)`。它从真实 FloatMenuMakerMap.providers 取得 MOD 实例；生成临时玩家 Pawn/Filth/允许区，将 Cleaning 设为 0、目标设在允许区外且 Home 区内，对照真实原版 provider 的禁用选项与本 MOD 的可执行选项，再检查本地化后缀、区域与所有工作优先级恢复。finally 清理新对象/区域并恢复 Home 和 currentProvider；不破坏既有地图内容。

该检查源已经真实运行，但初次功能断言未通过，不能标为已验证。它验证生成选项的功能，不验证菜单绘制、鼠标右键/点击，也没有执行所选清洁工作；实际点击后工作持续、工作/tick 行为、区域 setter 的副作用、异常恢复与不同 Pawn/其他菜单 MOD 的组合另待验证。发生游戏错误须保留上下文与日志，不标为已通过。

## 真实运行后的夹具修正（v2，待重跑）

`testing/cases/mod56-wave1` 正确加载了产品，但“MOD 提供 enabled cleaning”断言失败。真实 `WorkGiver_CleanFilth.HasJobOnThing` 明确拒绝 `TicksSinceThickened < 600` 的新 Filth；首版夹具刚生成 Dirt，本就不是可清洁工作。没有产品异常的额外证据，不通过禁用原版年龄规则让产品通过。

v2 只修改测试：为自生成 Filth 建立真实 growTick=当前 tick−601 的历史年龄，选择未被 fog 覆盖的格子，先断言原版 scanner 接受且目标可达，再保留原版受限/MOD 启用、标签、所有优先级/区域恢复等完整断言，并输出实际菜单标签/Disabled 及年龄。修改的是可丢弃测试输入，不替换 scanner 或模拟 MOD 结果。编译日志 `logs/56-AllowRightClickWorkOutsideZone-checks-build-v2.log`；原失败 JSON/控制台与 results.runtime 完整保留，等待主代理主线程重跑。生产 DLL/源码未修改。
