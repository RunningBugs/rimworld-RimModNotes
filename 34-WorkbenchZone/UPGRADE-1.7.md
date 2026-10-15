> 主代理游戏验证：`mod34-rev1248-o`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod34-rev1248-o`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod34-rev1248-k`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod34-rev1248-k`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# RimWorld 1.7 升级：Workbench Zone 工作台存储区

packageId `RunningBugs.WorkbenchZone`，Workshop `3440272267`。以当前 1.6 的矩形/圆形、严格取料半径和缓存重置版本迁移；固定 `1.7.4977 rev1248`。**已适配、编译与有效几何单测通过，实际地图检查待执行**。

## 最小改动

新增独立1.7源码/DLL；About1.7声明。生产代码改无参PatchAll为 `harmony.PatchAll(typeof(Start).Assembly)`，新工程net10.0、`Krafs.Rimworld.Ref 1.7.4977-alpha.1`、`Lib.Harmony 3.0.0-rc.2`，去除未用HugsLib，不需Publicizer。没有活跃LoadFolders，.bak不载入；真实自动1.7/Common/root，1.7同名XML覆盖旧Common补丁以修复继承重复，Common翻译复用，旧源码和产物保持。

## API 与语义

完整原/新metadata调用与字段类型引用在 `logs/34-WorkbenchZone/api-{original,new}.json`；全部Main/Logger及Common内容审查。程序集游戏contract与rev1248真实一致，GUI仍需实际视觉证据。

- 启动只有日志/Harmony初始化，源码没有HarmonyPatch类；真正入口是XML附加CanCreateZone ThingComp，经ThingWithComps.GetGizmos→CompGetGizmosExtra给Building_WorkTable提供命令。CompProperties.compClass类型、InitializeComps、parent、Command_Action.ProcessInput和rightClickAction闭包合同保留。
- 真实1.7 WorkGiver_DoBill.TryFindBestIngredientsHelper仍严格采用 `(t.Position-billGiver.Position).LengthHorizontalSquared < searchRadius*searchRadius`（真实Source/RimWorld/WorkGiver_DoBill.cs:462），中心没有改为InteractionCell。本MOD radius=min(bill ingredientSearchRadius,设置)-0.5，矩形角与圆形轴均严格内侧，保持已有几何。
- Building_WorkTable.billStack/Bills、Bill.ingredientSearchRadius/ingredientFilter/nextTickToSearchForIngredients公开合同保留；Finalization将每份Bill缓存归零，让新增区域立即进入后续原生取料。旧设计min半径与maxRadius静态默认值7/序列化默认20的差异保留。
- ThingFilter.AllowedThingDefs/SetAllow/SetDisallowAll、Zone_Stockpile(settings/preset/slotGroup)、ZoneManager.ZoneAt/RegisterZone、Map.floodFiller.FloodFill、Designator_ZoneAdd.IsZoneableCell和Zone.AddCell/CheckContiguous/Delete(false)均保留。IsZoneableCell按InBounds/fog/地图边缘/CanOverlapZones实际判定；工作台自身占地不允重叠，区域从交互格向外连通。
- 现有交互格Stockpile扩充和碰到其他Stockpile的合并继续保留对原区域filter的策略，不额外覆盖既有filter；零cell临时区域删除；非stockpile/无Bill用原Messages通知。
- FloatMenu/FloatMenuOption实际Action、Event.button、CurActivateSound、UI图标ContentFinder、Listing/Slider、Scribe_Values原WorkbenchZone.maxRadius key不变。Logger无独立业务状态，三Verse.Log入口无变化。
- Common LetterDef只有注释；语言两个Keyed完整，Patches唯一Add仍指向Defs/ThingDef/comps。

**实际确认的既有XML缺陷**：旧Patch同时添加抽象BenchBase与具体comps子Def，1.7 XmlInheritance追加列表导致stove重复组件，已在真实case失败并以新1.7 XML修正（见下）。实际stove与全部原生工作台组件唯一断言保留，没有放宽。另新建区域碰到多个/形状外既有Stockpile的选择逻辑是原有行为，列为额外场景。

## 验证证据与命令

```bash
scripts/dotnet.sh restore work/34-WorkbenchZone/1.7/Source/mod.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/34-WorkbenchZone/1.7/Source/mod.csproj -c Release --no-restore
scripts/dotnet.sh logs/34-WorkbenchZone/bin/Release/net10.0/Unit.dll work/34-WorkbenchZone/1.7/Assemblies/WorkbenchZone.dll /home/lisanhu/mine/workspace/rimworld/RW/17/Assemblies
scripts/dotnet.sh build logs/34-WorkbenchZone/Checks.csproj -c Release --no-restore
```

生产和断言0warning/0error，输出只本MOD DLL/deps.json。`logs/34-WorkbenchZone/static-validation.json`记录XML解析/旧文件字节/加载路径。

**有效单测已运行**：BCL .NET10 runner反射调用最终生产DLL的三个几何函数，加载固定实际程序集只解析游戏值类型/执行纯数值函数；160个quarter-step radius（0.25..40）共1280断言及zero-radius边界。验证矩形角/圆轴的最大严格可见offset、正负/中心/内侧/外侧cell的独立欧氏距离合同，不复制Ceil公式当oracle。`unit-tests.log`实际通过。该证据只覆盖几何，不代表地图/GUI。

`Tests/1.7/Mod34Checks.cs`编译通过、实际执行待主代理：

- 实际XML添加stove唯一Comp，实际GetGizmos命令的无Bill控制和左键ProcessInput生成矩形，原生IsZoneableCell独立范围核对、两个真实食材Bill过滤器合并、半径内cell和全部Bill缓存归零。
- 交互格原Stockpile扩充，右键ProcessInput真实FloatMenu与circle选项Action，圆边界；生成真实Rice堆+Pawn，调用实际WorkGiver_DoBill.TryFindBestBillIngredients证明生成区食材可见。
- 非Stockpile交互格拒绝创建、微小半径零cell注册后撤销、实际Scribe静态radius往返；finally仅删除新增zone/pawn/rice/worktable/window并恢复设置。

未覆盖：可见命令/选单/点击和图标、真实工作job执行/电力/可达性长期控制、多个既有stockpile合并/不可连通地形/地图边界与fog、全地图类型组件影响、真实保存新进程重载。本代理不启动游戏，不声称无红字或完整运行验证。

## 实际加载失败后的一处XML修正

主代理 `mod34-rev1248-k` 实际唯一Comp断言失败，证实旧Broad Patch给抽象BenchBase与child各加一份继承重复。新增 `1.7/Patches/Patches.xml` 以相同相对文件名覆盖Common旧Patch：只在具体Def既有comps追加一次；具体原生WorkTable类别若没有本地comps（如Brewery），附加一个可继承原comps的本地comps节点。全部旧Common不改、不重写base comps、不改变其他comp；无C#重构。标准classes含WorkTable/HeatPush/Autonomous/MechGestator。static继承检查确保所有实际官方WorkTable派生类均恰一份（含Brewery与Gestator），driver强化同一实际全表断言并保留stove唯一控制。未知第三方自定义worktable类且完全缺本地comps/thingClass的组合需要额外验证，不能外推任意第三方兼容。

此前“未改变旧Patch”的描述是第一阶段适配；本节为最终新增1.7覆盖，XML加载确认只选择新Patch，旧版仍选Common原Patch。生产DLL hash不变，补丁改动只影响1.7 XML。
