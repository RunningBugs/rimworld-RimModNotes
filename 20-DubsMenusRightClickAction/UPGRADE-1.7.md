> 主代理游戏验证：`mod20-rev1248-f`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod20-rev1248-f`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 升级记录

目标游戏：本机固定快照 `1.7.4977 rev1248`；上游 Dubs Mint Menus 1.3.1689 已有独立 1.7 程序集。当前结论：**适配、编译及转换器单元测试通过，真实鼠标交互仍待验证**。

## 依赖与加载

- 原 packageId `RunningBugs.DubsMenusRightClickAction`、发布 ID `3151252341` 不变；新增 About 支持 1.7 和独立 `1.7/Source`、`1.7/Assemblies`，原 Root Source/1.4 内容逐字保留。默认加载选 1.7，根 Source 非加载目录。
- net10.0，精确 NuGet `Krafs.Rimworld.Ref 1.7.4977-alpha.1`、`Lib.Harmony 3.0.0-rc.2`。完整 game 元数据合同与 rev1248 相同，实际 UI 语义研究使用最新 UnityStubs。没有私有成员直接编译访问，故无需 Publicizer。
- 上游实际输入 `/Data/SteamLibrary/steamapps/workshop/content/294100/1446523594/1.7/Assemblies/DubsMintMenus.dll`，SHA-256 `5a4b08042c147e502e933539ccc736b52c74d732f486ce2e992f575107618d6b`；固定参考副本在本 MOD `1.7/Source`，Private=false，输出不包含第三方 DLL。其捆绑 Harmony 与上面 NuGet/本机 Harmony 字节相同。运行依赖 `brrainz.harmony`、`dubwise.dubsmintmenus`；测试时两者都须真实启用。
- 未用 HugsLib 模板编译引用移除，仅在新 1.7 工程；启动使用显式 `PatchAll(typeof(Start).Assembly)`，避免 Harmony 3 调用栈推断风险。

## API、调用链及最小修正

完整静态类型/成员引用：`logs/20-DubsMenusRightClickAction/api-references.json`（61 个成员、60 个类型）；动态 Harmony 目标、IL 和 UI 语义另外审查。

- 实际上游 `DubsMintMenus.Patch_BillStack_DoListing.DoRow(RecipeDef, HashSet<Building>, Precept_Building)` 仍由 Doink 调用；选定反编译 `logs/20-DubsMenusRightClickAction/Patch_BillStack_DoListing.cs` 第 697/705/729/908 行。DoRow 仅有一次 `Widgets.ButtonInvisible(Rect,bool)`，按钮成功后才添加配方。反射 TargetMethod 现在匹配完整参数，避免同名重载。
- 旧 transpiler 匹配按钮后假设最后一个 `brfalse` 是按钮分支，并复用分支指令；它忽略短跳转、标签及以后上游分支变化。新 1.7 仅在这一个确切按钮调用插入 `ldarg.0`，替换为同样先调用真实按钮的 wrapper：左键返回真实结果，右键 Used 事件打开产品 InfoCard 并返回 false，保持不添加 bill 的原意。标签/异常块转移至替换序列第一条，复制其余指令而不修改输入。匹配 0 或多个按钮明确报错，不在错误位置插入。
- `Dialog_FancyDanPlantSetterBob.Clicked(ThingDef)` 仍由植物窗口 ButtonImageFitted 成功分支调用（选定反编译第 173/380 行）；保留右键阻止选择、打开信息卡，仅新增 Event.current 空保护。
- 最新 `/RW/17/UnityStubs/UnityEngine/GUI.cs:840` ButtonImpl 在 MouseDown 保存任意鼠标按钮、MouseUp 匹配后 Use 并返回 true；`Source/Verse/Widgets.cs:1703` ButtonInvisible 仍调用 GUI.Button，因此已消费的右键事件语义有当前源码证据。代码审查不能代替真实渲染输入测试。
- Dialog_InfoCard、Find.WindowStack、RecipeDef.products/Product.thingDef、Logger/ModLister 字段属性及 XML 元数据均存在。无自有存档字段或需要迁移的 Def/Patch；原 1.4 模板 Def 不强行复制到 1.7。

## 验证及命令

工作区 `bash scripts/dotnet.sh build work/20-DubsMenusRightClickAction/1.7/Source/mod.csproj -c Release --no-restore`：0 warning / 0 error；日志 `logs/20-DubsMenusRightClickAction/build.log`。

`bash scripts/dotnet.sh run --project work/20-DubsMenusRightClickAction/Tests/Unit/Unit.csproj -c Release --no-restore`：通过真实生产 transpiler 单元测试（精确替换、输入不可变、标签/异常块、brfalse.s 保留、0/2 匹配拒绝），日志 `unit-tests.log`。普通 SDK 不自动加载 RimRef 所标 Private=false 的游戏引用；测试引导显式加载固定真实 snapshot，未使用剥除方法体的参考包执行。此处只执行纯 IL 转换算法，不启动游戏或 UI。

`Tests/1.7/Mod20Checks.cs` 在游戏内检查实际选中上游的两个具体补丁注册及原 DoRow IL 精确转换，已编译（`checks-build.log`）。这是静态合同/注册断言，明确输出右键 UI 待测，不能记为完整功能通过。

真实交互场景待 root 调度：在 Dubs 工作台配方列表中右键有产物配方，信息卡打开且 bill 数不增；左键同一配方正常新增；多产物分别显示信息卡（与 BetterInfoCard 的组合另测）；植物窗口右键打开植物信息卡、当前种植选择不变，左键正常选择。窗口渲染、鼠标事件消费、布局、其他 UI MOD 的 transpiler 组合尚未验证。不能以无红字或注册成功宣称交互可用。
