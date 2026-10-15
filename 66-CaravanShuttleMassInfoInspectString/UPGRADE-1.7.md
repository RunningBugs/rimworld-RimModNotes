> 主代理游戏验证：`mod66-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod66-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod66-functional`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod66-functional`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# RimWorld 1.7 beta 升级记录

状态：**已适配，游戏功能验证未完成**。基线：本机 `1.7.4977 rev926`；发布 ID `3538618455`；packageId `RunningBugs.CaravanShuttleMassInfoInspectString`。本轮仅在工作区暂存副本操作，实际仓库由主代理复核后回写。

## 最小变更

- 新增 `1.7/Source/Main.cs`，内容与 1.6 原源码逐字节相同，保留原有名称和功能。
- 新增独立工程 `1.7/Source/mod.csproj`，因本机改用 Waylith/.NET 10 将新工程设为 `net10.0`；锁定 `Krafs.Rimworld.Ref 1.7.4977-alpha.1`、`Krafs.Publicizer 2.3.2`、`Lib.Harmony 3.0.0-rc.2`。只 publicize `Assembly-CSharp:RimWorld.Planet.CaravanShuttleUtility.GetCaravanShuttleMass`，运行时策略为 `IgnoresAccessChecksTo`。
- 在 About 添加 1.7，新增显式 LoadFolders，1.6 加载 `/`、Common、1.6，1.7 加载 `/`、Common、1.7，和原本的默认目录选择结果一致，避免误载旧 DLL。
- 原项目只有注释中的 Harmony using，没有实际 Harmony 代码；保留其编译包约定，输出及 IL 不包含运行时 Harmony 调用，无需新增 Harmony MOD 依赖。
- 旧 1.6 文件全部保留且通过逐字节检查。共享 Def/Patch/翻译内容无需改动。

## API 与语义审查

源码全部 28 行已阅读，只有两个自有函数；没有 Harmony 目标、反射、transpiler、动态调用、自有 DefOf 或存档字段。

| API / 入口 | 1.7 真实源码证据与结论 |
| --- | --- |
| `WorldObjectCompProperties` 构造、`compClass` | `RW/17/Source/RimWorld/WorldObjectCompProperties.cs`：仍为 public Type 字段，构造函数设置自有组件类型可正常反射实例化。 |
| `WorldObjectComp` 构造、`parent`、`CompInspectStringExtra()` override | `RimWorld/Planet/WorldObjectComp.cs:9,64`：parent 仍为 public WorldObject，虚方法仍返回 string。 |
| `Caravan.Shuttle` 与 `Building_PassengerShuttle` 类型判断 | `RimWorld/Planet/Caravan.cs:267`：脏缓存时调用 `CaravanInventoryUtility.FindShuttle`，找不到返回 null，原有无穿梭机 guard 含义不变。 |
| `CaravanShuttleUtility.GetCaravanShuttleMass(Caravan)` | `RimWorld/Planet/CaravanShuttleUtility.cs:13`：仍为 private static float，枚举物品和小人算缓存哈希，用 CollectionsMassCalculator 计算含小人质量的负载，再扣除穿梭机自身重量。仍由 `CanLaunchCaravanShuttle:64` 调用，属于当前有效起飞重量逻辑。 |
| `Building_PassengerShuttle.TransporterComp` | `RimWorld/Building_PassengerShuttle.cs:27`：仍返回 CompTransporter，通过 GetComp 缓存；未改生命周期假设。 |
| `CompTransporter.MassCapacity` | `RimWorld/CompTransporter.cs:70`：有效 override 优先，否则返回 Props.massCapacity，返回 float。 |
| `"Mass".Translate()`、TaggedString 插值 | `Verse/Translator.cs:53`：仍返回 TaggedString，Release IL 确认编译为对实际接口的调用；使用当前语言的原版键，未加自有语言键。 |
| 组件挂载与显示调用链 | `WorldObject.InitializeComps:283` 仍从 `def.comps[*].compClass` 创建组件；`WorldObject.GetInspectString:320` 遍历调用；`Caravan.GetInspectString:549` 首先调用 base，因此该入口并未成为不可达旧钩子。 |
| XML Patch | `Common/Patches/Patches.xml` 条件创建 Caravan comps 并添加唯一自有组件属性；本机官方 `Data/Core/Defs/WorldObjectDefs/WorldObjects.xml` 仍有单一 Caravan WorldObjectDef。静态检查确认目标存在；实际 Patch 命中及组件创建待游戏验证。 |
| 存档 | 无自有序列化字段；`WorldObject.ExposeData` 在 LoadingVars 调用 InitializeComps，理论上由 Def 重建。实际保存/重载尚未执行。 |

可信旧签名取自本机缓存 `Krafs.Rimworld.Ref 1.6.4871/ref/net472/Assembly-CSharp.dll`，ILSpy 输出保存在 `logs/66-CaravanShuttleMassInfoInspectString/old-reference-signatures.cs`，确认 private static float 签名一致。`RW/16/Source/RimWorld.Planet/CaravanShuttleUtility.cs` 的相关算法文本亦相同，但其旧构建出处仍待核实，因此仅作为辅助证据，不把它称为已核验的完整旧语义基线。

## 构建与已执行检查

工作区路径：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7`。执行环境按 `shared/dependencies.md`，NUGET_PACKAGES 指向工作区 `.nuget/packages`，DOTNET_CLI_HOME 指向 `.dotnet-home`。

```bash
/home/lisanhu/.dotnet/dotnet restore work/66-CaravanShuttleMassInfoInspectString/1.7/Source/mod.csproj --configfile .nuget/nuget.config
/home/lisanhu/.dotnet/dotnet build work/66-CaravanShuttleMassInfoInspectString/1.7/Source/mod.csproj --no-restore --configuration Release
.venv/bin/python work/66-CaravanShuttleMassInfoInspectString/Tests/1.7/validate_layout.py
DOTNET_ROOT=/home/lisanhu/.dotnet .tools/ilspy/ilspycmd --disable-updatecheck -il -r /home/lisanhu/mine/workspace/rimworld/RW/17/Assemblies work/66-CaravanShuttleMassInfoInspectString/1.7/Assemblies/ShuttleMassInfo.dll
```

- Restore/Release build：成功，0 warnings / 0 errors；日志 `logs/66-CaravanShuttleMassInfoInspectString/restore.log`、`build.log`。
- XML/身份/加载路径/1.6 保留/源码不变/输出检查：通过，日志 `static-validation.json`。只生成一份 ShuttleMassInfo DLL，没有复制 0Harmony、游戏引用或 publicized 引用 DLL；PDB、deps.json 为 SDK 构建附属产物。
- 产物：`1.7/Assemblies/ShuttleMassInfo.dll`，SHA-256 `20b7b4e3db2ad45e840796171bd15a3917635fc88a2e01a39ebef9b1071731d8`；生成 IL 在 `artifact.il`，确认直接调用 private 重量方法和所列接口，并生成访问属性。
- 现有单元测试：无。构造和显示函数依赖真实游戏对象/翻译/组件生命周期；未机械创建复刻实现的单元测试。静态布局检查单列，不当作函数测试。
- 共享静态元数据审计：`scripts/dotnet.sh tools/api-audit/bin/Release/net10.0/ApiAudit.dll <新DLL>`，完整结果在 `logs/66-CaravanShuttleMassInfoInspectString/api-audit.json`；动态/反射验证仍按场景执行。
- 新增测试专用 `Tests/1.7/Mod66Checks.cs`，约定入口 `RW17.Validation.Mod66Checks.Run(Verse.Map)`，只引用游戏类型，不引用本 MOD DLL。独立编译检查 0 warnings/errors，日志 `checks-build.log`；它供主代理合并到共享游戏驱动，尚未执行功能断言。

## 待执行验证及限制

游戏由主代理串行启动，本代理没有运行游戏。`Tests/1.7/Mod66Checks.cs` 断言组件唯一挂载、空/null/无穿梭机、实际私有重量调用、物资变化、GetInspectString 链和移除穿梭机，所有运行断言仍 pending。`Tests/1.7/README.md` 另外记录超重、双语言面板及保存重载场景。尤其需要用真实 DLL 执行私有方法访问，确认显示行进入世界地图远行队信息面板。Headless 的字符串断言不能证明可视 UI 渲染，因此当前只能报告“已适配，验证未完成”。

整合审核：产品StaticCtor实际需要Harmony，旧About没有有效依赖。仅v1.7声明brrainz.harmony，避免单启用时未加载依赖；旧版本元数据保持。产品DLL及已通过功能检查不变。
