> 主代理游戏验证：`mod58-rev1248-a`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod58-rev1248-a`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod58-wave1`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod58-wave1`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# Free Go To：1.7 beta 升级

packageId：`RunningBugs.FreeGoTo`；Workshop ID：`3518474264`；基线：真实 `1.7.4977 rev926`。仅修改暂存副本。状态：**已适配，编译/API 检查通过；地图行为与可视拖动/渲染待验证。**

## 最小变更

独立 `1.7/Source` / `1.7/Assemblies/FreeGoTo.dll`，保留 1.6 原件；新工程 net10.0，固定 Ref `1.7.4977-alpha.1` / Harmony `3.0.0-rc.2`。生产源码唯一改动为显式 `PatchAll(typeof(PatchAll).Assembly)`；owner 与全部输入、控制器、渲染和补丁逻辑保持。没有需要直接公开的私有调用，反射与 Harmony 字段注入不要求 Publicizer。

About 增加 1.7；README 指向本记录。没有有效 LoadFolders.xml，默认读取独立版本/Common/根目录；`.bak` 忽略。最终 Assemblies 仅同名 MOD DLL/deps.json，无 Harmony、游戏或 publicized DLL；测试工程/产物不进入加载树。

## 全部 API 与动态入口

完整静态引用及签名：工作区 `logs/58-DraftPawnCanGoToOccupiedCell-api.json`。真实参考为 `RW/17/Source` 和固定 Assemblies。生产代码没有 transpiler、序列化或 Def 修改；Common XML 为无活跃操作的模板。

| API / 入口 | 核验结论 |
| --- | --- |
| Selector.HandleMapClicks() / HandleMultiselectGoto(FloatMenuContext) | `Source/RimWorld/Selector.cs:181/801`，仍是 private void；主输入流程调用 HandleMapClicks，前缀完整替换它；另一个前缀绑定相同 context |
| Selector.SelectorOnGUI_BeforeMainTabs / ClearSelection；MapInterface.MapInterfaceUpdate | `Selector.cs:173/274`、`MapInterface.cs:112`；public void 生命周期保留，分别处理控制器标签、取消和 ghost 绘制 |
| 私有反射四项 | Selector 的 SelectAllMatchingObjectUnderMouseOnScreen、HandleMultiselectGoto、SelectUnderMouse、SelectInsideDragBox 均存在；前三个选择方法中除 HandleMultiselectGoto(1参数)外均无参数；缓存 MethodInfo 的实际解析另有游戏检查 |
| `___tmpDraftedGotoPawns` | Selector.cs:28 仍是 private static readonly List<Pawn>；Harmony 前缀按字段名注入实际列表，清理行为与原流程一致 |
| Selector.dragBox / gotoController / SelectedPawns 与 DragBox 字段/IsValid | 真实 public 成员仍存在；MultiPawnGotoController 的 Active、StartInteraction、AddPawn、ProcessInputEvents、FinalizeInteraction 保留 |
| FloatMenuMakerMap、FloatMenuContext、FloatMenuOption 与 FloatMenuMap | GetOptions、GetAutoTakeOption、ShouldGenerateFloatMenuForPawn；上下文选中 Pawn/Cell/Map；isGoto、Chosen、LabelCap、WindowStack.Add 等全部静态引用保留，菜单路径语义与原版一致 |
| 移动目的地与命令 | CellFinder.StandableCellNear、RCellFinder.BestOrderedGotoDestNear（RCellFinder.cs:75）、PawnGotoAction（FloatMenuOptionProvider_DraftedMove.cs:89）保留；仍验证可达性和 drafted destination reservation，不能理解为绕过所有目的地限制 |
| Unity Input / Event | 实际 UnityStubs.Input.GetMouseButton 读取 Mouse.current；无输入设备时返回 false。EventType 的 MouseDown/mouseDown、MouseUp/mouseUp 分别是值 0/1 的等价别名，原代码无需更名 |
| 绘制与声音 | MaterialPool.MatFrom、ShaderDatabase.Transparent、GenColor/Color、MeshPool、Matrix4x4.TRS、Quaternion.identity、Graphics.DrawMesh、PawnRenderer.RenderPawnAt、Altitude/Fogged/ToUIRect/DrawPawnLabel、PlayOneShotOnCamera 等接口保持；DrawMesh 的四参数重载转发到完整实现。存在接口不能证明视觉结果，必须可视验证 |
| Startup / Harmony / 标准库 | 共享 Startup/Harmony 显式程序集修正；AccessTools.Method 和 MethodInfo.Invoke 实际匹配上述目标；LINQ/集合及日志引用见 API JSON |

与旧 1.6 Selector 的差异主要是 EventType 别名拼写及选择声音分支的反编译表示；本 MOD 所替换的输入分支与私有移动方法没有新语义分支遗漏。所有相关参数、字段与生命周期已逐项查看。没有用旧动态 DLL 当基线。

## 编译与待执行场景

工作区隔离 wrapper 命令：

```bash
scripts/dotnet.sh restore work/58-DraftPawnCanGoToOccupiedCell/1.7/Source/mod.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/58-DraftPawnCanGoToOccupiedCell/1.7/Source/mod.csproj --configuration Release --no-restore
scripts/dotnet.sh restore work/58-DraftPawnCanGoToOccupiedCell/Tests/1.7/Mod58Checks.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/58-DraftPawnCanGoToOccupiedCell/Tests/1.7/Mod58Checks.csproj --configuration Release --no-restore
```

两个工程均零警告零错误；日志为 `logs/58-DraftPawnCanGoToOccupiedCell-{restore,build,checks-restore,checks-build}.log`。旧 1.6、XML、解析依赖及输出布局检查通过。

主代理可调用 `RW17.Validation.Mod58Checks.Run(map)`：验证五处真实补丁注册与四个生产反射缓存；生成两个临时 Pawn，启动实际 SinglePawnGotoController，调用真实 Selector.ClearSelection 确認取消，再接受由未征召 Pawn 占用的拖动目标，Finalize 按原版规则选择邻近目的地并检查实际 Goto job 和控制器停用；移走占用者后检查同一原格的精确 Goto 对照。finally 停用控制器并清理自生成 Pawn；代码不直接引用 MOD DLL。首版检查已真实失败，修正夹具/期望的 v2 已编译、待重跑。

没有脱离引擎的独立纯逻辑单元测试。上述场景不包含 UI 事件输入、完成到达、被征召 Pawn 的预约冲突或多人拖动。完整右键按下/拖动/释放、选区/双击保留、ghost 材质/渲染、标签与声音需要可视运行；Headless 通过也必须保留这些限制，不标成完整交互通过。

## mod58-wave1 失败分析与 v2 对照

首版夹具错误要求 BestOrderedGotoDestNear 在同阵营 Humanlike 占格时仍返回原格。真实 RCellFinder.cs:131 之后明确排除该占格，且该文件与旧 1.6 全文相同；并非 1.7 回归。产品允许的是拖动目标进入占格，Finalize 仍调用原版碰撞/预约/可达规则选择相邻目的地，不能为错误测试绕过原版规则。

v2 先验证目标可达、controller 的真实 end 恰为占格，再要求实际 Goto 目标严格等于原版选出的非原格近邻；操作者放远 6 格，防止原版选择其已经站立的邻格而不产生新 job。随后只移走自生成的占用 Pawn，要求原版与同一控制器都精确选择原格，构成真实输入对照。保留取消、五个补丁/四个反射缓存和最终停用断言。记录实际 occupied target / native destination 坐标，未削减成仅激活状态或空 job 通过。

生产代码/DLL不改。新日志 `logs/58-DraftPawnCanGoToOccupiedCell-checks-build-v2.log`；原 `mod58-wave1` 的失败 case、控制台和 results.runtime/runtime_history 保留。等待主代理在完整 Playing/noLongEvents 的主线程重跑。


## 2026-10-15 名称和描述版本修订

本轮在现有1.7适配基础上更新About名称/描述版本措辞，packageId/发布ID/支持标签/生产源码与DLL不改。

- 正文只提原1.6占格拖动限制；保留原bug版本叙述，补明当前1.7适配，不机械改成两个版本均有相同bug。

语法/身份/版本声明与原产物一致已核验；属于元数据修改，不将其当新功能验证。详细回写审计见工作区expansion/metadata-changes.json。
