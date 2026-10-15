> 主代理游戏验证：`mod02-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod02-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod02-wave1`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod02-wave1`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# ItemPolicy：1.7 beta 升级记录

状态：已适配、生产 DLL 与真实地图断言代码编译通过；尚未游戏执行、验证 UI 或存档。只修改 work 完整副本。

基线 1.7.4977 rev926；packageId RunningBugs.ItemPolicy；发布 ID 3030074966。原 1.4/1.5/1.6 及 Common 文件逐字节保留。

## 最小修改

新增独立 1.7/Source 与同名 1.7/Assemblies/ItemPolicy.dll，net10.0；锁定 Krafs.Rimworld.Ref 1.7.4977-alpha.1、Lib.Harmony 3.0.0-rc.2，不复制引用 DLL。启动显式 PatchAll(typeof(ItemsLoadingScreen).Assembly)。1.7 项目删除未用 HugsLib 模板引用：所有 C# 未引用 HugsLib 类型，原 About 也无该依赖；没有机械引入新 HugsLib。

supportedVersions 添加 1.7；仅 v1.7 声明实际必需运行 MOD brrainz.harmony，避免缺失 0Harmony 时无法加载。保留旧版本原依赖表现。没有 LoadFolders.xml，默认选 1.7+Common+根，旧 DLL 不同时加载。无私有游戏成员直接访问，因此不添加 Publicizer。

## 所有可发现 API/动态入口及语义

元数据完整引用审查见 logs/02-ItemPolicy/api-references.json；除框架/编译器属性之外包含游戏、UnityStubs、Harmony 及泛型签名。没有 transpiler 或生产代码私有反射调用。

- 唯一 Harmony 目标 JobGiver_DropUnusedInventory.ShouldKeepDrugInInventory(Pawn,Thing) 仍为公开静态 bool 方法，DropUnusedInventory.TryGiveJob 仍调用它（RW/17/Source/RimWorld/JobGiver_DropUnusedInventory.cs:38/:46）。Postfix 只在自定义期望库存>0时把结果置 true，未覆盖目标原方法的其他分支，参数 pawn/drug 仍一致。
- 自定义 ThinkNode_JobGiver.TryGiveJob(Pawn) 仍由 TryIssueJobPackage 调用（RW/17/Source/Verse/AI/ThinkNode_JobGiver.cs:21/:23）。AnyThingsRequiredNow、Pawn.inventory.Count/UnloadEverything、MassUtility.GearAndInventoryMass/Capacity、Thing.GetStatValue(Mass)、JobMaker.MakeJob(TakeInventory)、Job.count、Rand.Range、Mathf.Min、GenClosest.ClosestThingReachable、ThingRequest/TraverseParms、ForbidUtility.IsForbidden、ReservationUtility.CanReserve 全部通过实际 1.7 引用编译；寻找可达可保留的缺额物品、按栈/目标数量/承载限制设 count，旧逻辑不改。TakeInventory JobDriver 仍通过 Toils_Haul.TakeToInventory 执行拾取（RW/17/Source/Verse/AI/JobDriver_TakeInventory.cs）。实际搬运、超重/禁区/预约边界仍须场景验证。
- Common/Patches/Patches.xml 的 Humanlike ThinkTree 插入 XPath 在本机 Data/Core/Defs/ThinkTreeDefs/Humanlike.xml 精确命中 1 个节点（logs/02-ItemPolicy/xml-target.json），仍插入原版 JobGiver_TakeForInventoryStock 前。Class=_ItemPolicy.JobGiver_TakeItemForInventoryStock 和 leaveJoinableLordIfIssuesJob 都保留；不是只凭编译认为 AI 入口已接通，运行需确认实际 ThinkTree 包含该节点。
- GameComponent(Game)、FinalizeInit、ExposeData 均保持接口，GameComponentUtility.FinalizeInit 仍实际调用初始化（RW/17/Source/Verse/GameComponentUtility.cs:56）。DefDatabase<ThingDef>.AllDefs、Def.label、ThingDef.thingClass 与继承判断仍可用；按标签维护搜索字典、同名优先首个、Contains 大小写匹配等原行为不改。
- 自有 Get/Set/RemovePawnPolicy、Get/Set/RemoveItemPolicyEntry、MergePolicy、AddDef/Search 与 PawnStateTracker Get/Set 接口不变。计数<=0归零、删除最后条目移除 pawn policy、MergePolicy 清空并复制源字典保留语义。原 nextInventoryStockTick 的 Set 调用仍为注释，因此节流状态没有实际写回；这是已有行为，本次不顺手修复。
- Scribe_Collections 的 Def/Value 及 Pawn Reference/ItemPolicy Deep 字典重载均通过编译，序列化键 _ItemPolicy.ItemPolicy 和 _ItemPolicy.ItemPolicyUtility 不变。字典/def索引静态生命周期和 MergePolicy 自合并等旧边界未改变，保存/重载须实测。
- DefOf 字段 ItemPolicyMainButton/ItemPolicyPawnTable 与 Common 定义对应；DefOfHelper 初始化接口、MainTabWindow_PawnTable/PawnColumnWorker_CopyPaste/PawnColumnWorker/Window 的全部 protected/public override、PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists、Pawn.DevelopmentalStage.Baby 仍可编译。Common/PawnTable 列 LabelShortWithIcon/Ideo/Xenotype/Gap、自定义 columns 和 workerClass 仍按当前类型加载。原 ItemsMainButtonDefOf 的初始化 typeof(MainButtonDefOf) 与其他模板问题保持，不扩大升级范围。
- UI 全部 Widget/QuickSearchWidget.filter.Text/Listing/Text/GenUI/WindowStack/DefLabelWithIcon/TextFieldNumericLabeled、UnityStubs Rect/Vector2/Mathf 引用在 API 日志中；RightBoxPixels/ShrinkPixels、复制粘贴、名字比较、搜索回调、窗口布局/数字输入/滚动/删除等自有路径均保留。普通无窗口启动不验证这些渲染及交互。
- Common Defs/XML/语言注入未改，全部 XML 解析通过；没有新增资产、存档字段或第三方依赖。

## 编译与可执行场景

生产项目和 Tests/1.7/checks.csproj 都 0 warnings / 0 errors；日志在 logs/02-ItemPolicy/{restore.log,build.log,checks-restore.log,checks-build.log,api-references.json,xml-target.json}。

```bash
bash scripts/dotnet.sh restore work/02-ItemPolicy/1.7/Source/ItemPolicy.csproj --configfile .nuget/nuget.config
bash scripts/dotnet.sh build work/02-ItemPolicy/1.7/Source/ItemPolicy.csproj --no-restore -c Release
bash scripts/dotnet.sh build work/02-ItemPolicy/Tests/1.7/checks.csproj --no-restore -c Release
```

原项目没有独立测试；自有策略/搜索的数据类直接依赖 Pawn/ThingDef/IExposable，采用真实已初始化游戏中的断言，未复刻算法或模拟 UI。Tests/1.7/Mod02Checks.cs 按约定 public Run(Map) 可合入共享驱动：检查 GameComponent 和具体药物补丁；调用真实策略计数/负数归零/移除/合并复制/搜索；通过真实 ThinkNode.TryIssueJobPackage 签发有界 TakeInventory；使用未过滤 仅临时卸载本MOD具体Postfix的真实方法 找到原版会丢弃的药物作为控制，再调用真实 ShouldKeepDrugInInventory 确認正政策保留、零政策不强留。finally 清理物品、Job池、随机状态、策略及节流字典。已编译，未执行。

未测：实际 ThinkTree 调度/JobDriver拾取、全部质量/承重/禁用/预约/UnloadEverything条件、策略窗口及复制粘贴、保存重载、第三方组合。不能以场景编译成功当作运行断言通过。

产物 SHA-256：0041e29b8e138d8ac947f7f80ed6783ed9464b08dfc2e9ea59008f6350074b5e；静态成员 179，类型 114。

测试控制现不使用 reversepatch：仅临时 Unpatch 本 MOD 的具体药物 Postfix，调用真实目标找到未受本 MOD 处理的 false 对照，finally 按原 owner/priority/before/after 恢复；不操作其他补丁，生产 DLL 不变。

完整保存/新进程重载：combined-save-rev1248 → combined-reload-rev1248，两次exit0、无新增ERROR；真实计时器(消息+tick)、Pawn引用物品策略(工业药7)、研究Def队列、世界诅咒activationID在6个断言中保留。一次性夹具由本轮生成，不使用真实用户存档；不声称跨1.6存档迁移。证据位于工作区testing/cases两用例的rw17-save-result.json。
