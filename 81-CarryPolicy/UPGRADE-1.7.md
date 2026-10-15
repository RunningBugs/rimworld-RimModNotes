> 主代理游戏验证：`mod81-rev1248-ab`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod81-rev1248-ab`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod81-rev1248-z`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod81-rev1248-z`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# CarryPolicy：1.7 beta 升级

本机 `1.7.4977 rev1248`、真实RW/17快照/源码。packageId `RunningBugs.CarryPolicy`、Workshop `3783174168`、命名空间 `_CarryPolicy` 保持。已读本MOD AGENTS；原“仅1.6/不用beta”与用户本轮授权冲突，仅这部分及构建说明按用户要求更新，其余项目约定保留。

## 最小变化/API

- 新独立1.7/Source/CarryPolicy.csproj与1.7/Assemblies，net10.0 / Ref `1.7.4977-alpha.1` / Harmony `3.0.0-rc.2`；启动显式 `.PatchAll(typeof(CarryPolicyMod).Assembly)`，owner不变。无 private 编译引用，无Publicizer。其余产品方法正文不改。
- About增1.7，AGENTS修版本边界/精确依赖，README纠正旧正文中错误“WillEat门控/3补丁/getter postfix/仅长节流”描述，与既有现源码2个Prefix一致。未恢复已删除逻辑或新增重构。
- 旧1.6非缓存源码/产物逐个字节相同；默认加载1.7+Common+根，无LoadFolders/根Assemblies，无重复DLL；输出仅CarryPolicy.dll/deps.json，不带游戏/Harmony。
- 完整静态清单 `logs/81-CarryPolicy-api-audit.json`，全部8份产品C#、XML/override/Scribe另审查。真实 `Pawn_InventoryTracker.FirstUnloadableThing` public getter仍扫描Drugs/InventoryStock/Food保留额，实际unload setter、TrackerTick与穿梭机cargo循环继续使用它；原prefix整体扫描+max quota意义保留，不能退回清空第一个结果的postfix。
- `JobGiver_DropUnusedInventory.Drop(Pawn,Thing)`仍private实例void，真实低偏好食物/无用药物分支调用；Prefix参数匹配且会实际TryDrop超量。原ShouldKeepDrug API保持，但产品不再补丁它。
- 原生 `ThinkNode_JobGiver.TryGiveJob` override、GenClosest.ClosestThingReachable、CanReserve/CanReach、MassUtility容量/库存重量、TakeInventory.count、Pawn.inventory.Count/innerContainer、UnloadEverything、Per-Game组件/Dictionary节流签名匹配。临时范围仍仅地图殖民者，不增加商队补货/食尸鬼新规则。
- NutrientPasteDispenser.PowerComp/CanDispenseNow/DispensableDef/TryDispenseFood/CollectDuration、原生hopper/feedstock与JobDriver reservations/Goto/FailOn/EndJobWith真实接口匹配。TryDispenseFood原生仍消耗feedstock返回未生成meal；自有toil init立即转真实inventory，然后等待CollectDuration，不是完成后出餐。
- Need_Food.FoodFallPerTickAssumingCategory与BaseHungerRate仍lifeStage×race×2.6666667E-05，真实Stat Nutrition/质量/载重及Mathf rounding保持定义；食品份数/普通成人0.9营养基准不因1.7机械改常量。
- Policy.LoadKey/CopyFrom、Dialog_ManagePolicies<T>protected overrides、PawnColumnWorker/Widgets Dropdown/QuickSearch/TextBlock、ThingDef.category枚举/EverHaulable/Corpse筛选可编译；无新IME接口调用。Scribe键carryPolicies/carryAssignments、entries/thing/count及policy ID保持，真实Pawn reference assignments与每局非static nextCheckTick保留。
- XML真实Humanlike原TakeForInventoryStock节点/tag匹配；conditional失配会静默不插入，必须实际断言节点存在。Assign列Carry、CarryPolicyColumn worker、CustomJobDef driver名字都核验，不以XML解析替代节点生效。

## 编译及有效检查

```bash
scripts/dotnet.sh build work/81-CarryPolicy/1.7/Source/CarryPolicy.csproj -c Release --configfile .nuget/nuget.config
scripts/dotnet.sh run --project work/81-CarryPolicy/Tests/1.7/DataChecks/DataChecks.csproj -c Release --configfile .nuget/nuget.config
scripts/dotnet.sh build work/81-CarryPolicy/Tests/1.7/Mod81Checks.csproj -c Release --configfile .nuget/nuget.config
PYTHONPATH=/home/lisanhu/mine/workspace/rimworld/rimworld-imgui-sim /home/lisanhu/mine/workspace/rimworld/rimworld-imgui-sim/.venv/bin/python work/81-CarryPolicy/sim/verify_layout.py
```

产品及最终Map检查0warnings/0errors（检查首轮仅局部var声明/缺DefOf快捷字段已修为实际DefDatabase，不改产品），日志 `logs/81-CarryPolicy-build.log`、`...-native-check-build.log`。

DataChecks直接编译产品CarryPolicyData.cs，真实rev1248Assembly-CSharp/UnityStubs/Waylith/UnityCompat仅在test输出中引用，不创建游戏进程，不用fake API类型。真正运行的copy/Def identity/indexing、深复制不相互修改、UI unsaved buffer重置、null source、nonfood rounding（含2.5边界）、zero/null fallback通过；`logs/81-CarryPolicy-data-tests.log`。食品实际stats/Pawn身体状态另用游戏集成，不伪造食物营养器。

既有sim/verify_layout.py使用原imgui-sim项目专用venv运行，全部状态与2x的布局检查无违规，`logs/81-CarryPolicy-sim-layout.log`。这是**模拟原型**，不证明真实Window/字体/输入，不调用需要外部key的vision_review。

`Tests/1.7/Mod81Checks.cs`已独立编译仅游戏/Harmony引用，反射实际MOD，供主代理真实主线程执行：实际两个Prefix、Humanlike实际注入giver/Assign列、真实Per-Game组件/Pawn assignment+使用中删除拒绝、同inventory原生getter具体Unpatch/repatch对照（nativeStock保护medicine、policy保护steel且继续扫描后货物）、实际UnloadEverything setter、真实milk/meal营养+Drop仅超额、short/long/throttle、真正NativeTakeToInventory deficit转移、原生powered dispenser+hopper+corn并实际driver toil→TryDispenseFood→inventory one meal/feed减少、power-off guard。清理自己生成Thing/Pawn、自己的assignment/nextCheckTick/policies；具体对照patch恢复原owner/order。部分初始化callback按真实JobDriver注册执行，未模拟全部行走/等待ticks。

## 仍待验证

Map checks尚未执行；GUI两栏搜索/指派/复制/rename/InfoCard、真实完整tick等候/搬运、超重/不可达/保留药品多重配额边界、库存拒绝后落地失败分支、营养膏机第三方子类、实际Scribe保存重载/旧存档缺Def、商队/shuttle实际装载组合待测。默认方案仅保持既有空清单语义；没有把headless/console/sim通过当成这些功能完成。

最终DLL SHA256 `f20a60175e68702237fecad4be1d8b4ffb7d35afc99e0622c943b6952cfc5d70`；直接静态外部成员188项。

## 原生库存额度夹具 v2

首次实测`mod81-rev1248-z`到stock前置失败：7超出原InventoryStockGroupDef.max。仅测试改为nativeQuota=min(3,真实max)、需>1，CarryPolicy medicine额度1、真实inventory含nativeQuota，使省略nativeStock或错误额度合并必失败；同inventory原生与MOD对照仍保留。另一个实际补货用例单独把CarryPolicy目标设3，不借nativeStock越界。生产不改，v2检查0/0；`logs/81-CarryPolicy-native-check-build-v2.log`，旧runtime保存，待重跑。

整合审核：产品StaticCtor实际需要Harmony，旧About没有有效依赖。仅v1.7声明brrainz.harmony，避免单启用时未加载依赖；旧版本元数据保持。产品DLL及已通过功能检查不变。
