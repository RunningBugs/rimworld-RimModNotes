> 主代理游戏验证：`mod54-rev1248-g`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod54-rev1248-g`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# Another Allow Tool：1.7 升级记录

packageId：`RunningBugs.AnotherAllowTool`；Workshop ID：`3508777227`；当前基线：`1.7.4977 rev1248`。只修改暂存副本。状态：**已适配、API/编译与结构检查通过；实际工具/搬运/选择功能待主代理测试。**

## 最小修改

新增独立 1.7 net10.0 工程、同名 AAT.dll，固定 Ref `1.7.4977-alpha.1` / Harmony `3.0.0-rc.2`。生产唯一调用变化是 `PatchAll(typeof(Start).Assembly)`；移除未配套 Publicizer 包、原来实际不起作用的 PublicizeAll 设置。实际 API 均可公开访问，私有 desList 用原有反射，所以没有引入 Publicizer 或扩大虚成员可见性。旧 1.6 文件保留。

About 加 1.7，保留标识/依赖/名称；无有效 LoadFolders.xml，默认精确版本/Common/根目录加载。新 Assemblies 仅 AAT.dll/deps.json，没有游戏/Harmony DLL，Tests 不在实际加载树中。原白盒测试增加 AAT_TEST_VERSION 参数，默认仍测试旧 1.6；原白盒脚本未用基础 Python 执行。

## 可发现 API 与实际语义

已完整阅读五份 C#、XML 和已有测试/架构说明；静态成员/类型原始签名在工作区 `logs/54-AnotherAllowTool-api.json`。没有生产 transpiler、新存档字段或后台读写游戏对象。

| API/功能 | 真实 1.7 核验 |
| --- | --- |
| Startup、Mod/ModSettings、LoadedModManager.GetMod/GetSettings、Log/Colorize | 接口保留；设置类/窗口本来无实现内容，未制造新的设置/序列化格式 |
| ReverseDesignatorDatabase.InitDesignators + desList 反射 | private void 入口与 private List<Designator> 字段仍存在，两个 Postfix 加入 urgent/fully-grown 反向工具；与 GUI 调用流程对应 |
| DesignationManager Add/Remove 两 Postfix | public void 方法、参数 newDes/des、公开 readonly map 与 designationsByDef 保留（Source/Verse/DesignationManager.cs:10/12/167/378）；保留快速按 Def 索引与 dirty/clear cache 机制，不改成全量扫描 |
| Toils_Haul PlaceHauledThingInCell / DepositHauledThingInContainer | 精确四参数/三参数重载在 Toils_Haul.cs:320/409；相关正文与旧版相同，前段变量反编译名变化不影响这两个入口；Postfix 包装实际 initAction，非只注册无调用补丁 |
| Thing.GetGizmos | virtual IEnumerable<Gizmo> 仍存在；Selector.SelectedObjects/NumSelected、Stuff、SelectionDrawer.Notify_Selected 等保留，选择工具仍比较 def/stuff |
| Designator 基类/地图/输入/绘制/消息 | CanDesignateCell/Thing、DesignateThing/SingleCell、ProcessInput/SelectedUpdate、DrawStyleCategory、ContentFinder、Input.GetKey/Event、Map/lister/thingGrid、Fogged、翻译/FloatMenu/WindowStack/声音接口保留；输入/渲染不能由 Headless 证明 |
| Allow/Forbid | CompForbiddable + live/spawned/same-map 过滤、SetForbidden 及 Notify_Forbidden/Unforbidden 保留；没有 ParentHolder==null 假设，不把新 map.spawnedThings 当库存 |
| Haul cache / Job | MapComponentTick/GetComponent、GenTicks、ThingsPotentiallyNeedingHauling、StoreUtility、reservationManager、PawnCanAutomaticallyHaulFast/HaulToStorageJob 保留；候选仍为 urgent designation∩原版 haulables，pawn级检查仍在 JobOnThing |
| Harvest | Plant.HarvestableNow/LifeStage/Growth、HarvestPlant、Home/harvestTag 和 shift/control 条件保留；只指定真正已成熟可收获植物 |
| 可选 Pick Up And Haul | 原动态查找类型名、WorkGiver_HaulGeneral 继承与无参构造验证保留；缺该模块时使用真实原版搬运委托。未启用该外部 MOD，不能声称其兼容路径验证通过 |

所有 XML 解析、类名/DesignationDef/WorkTypeDef/WorkGiverDef/分类 designator 注册、图标路径均检查。空 Common/Patches 与 English/Data 无活跃内容；自有翻译键未在此次升级中重构。Assembly-CSharp 在 rev926→1248 的全部 schema/正文不变且与 alpha1 相等，既有 Unity 调用没有碰到新增 IME/Waylith签名；参考 `shared/revision-api-diff.md` 的范围限制。

原 deposit 包装在原版 initAction **之前**清理 urgent designation，失败/部分投递行为可能也被提前清除；这是保留的既有风险，本次只验证实际成功投递路径，不能把白盒测试的“successful”方法名误当成失败路径已正确。SelectSimilarDefOf 本来未用 DefOf 属性标记，但所测选择逻辑不依赖该字段；UI designation 相关路径待测。

## 检查与真实场景

工作区隔离命令：

```bash
scripts/dotnet.sh restore work/54-AnotherAllowTool/1.7/Source/mod.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/54-AnotherAllowTool/1.7/Source/mod.csproj --configuration Release --no-restore
AAT_TEST_VERSION=1.7 .venv/bin/python -m unittest discover -s work/54-AnotherAllowTool/Tests/whitebox -p 'test_*.py' -v
scripts/dotnet.sh restore work/54-AnotherAllowTool/Tests/1.7/Mod54Checks.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/54-AnotherAllowTool/Tests/1.7/Mod54Checks.csproj --configuration Release --no-restore
```

MOD 与检查源均零警告零错误。现有 13 个白盒用例在默认 1.6 与显式 1.7 均通过，**仅证明代码/XML/资源结构，没有执行真实行为**；没有把它们标成函数单元/游戏功能通过。日志 `logs/54-AnotherAllowTool-{restore,build,checks-restore,checks-build,whitebox-old,whitebox-17}.log`。

主代理在 Playing/noLongEvents 的主线程执行 `RW17.Validation.Mod54Checks.Run(map)`：六处实际补丁/两种 reverse-designator 注册；真实物品 forbid→allow→forbid；urgent designation 安装、实际 cache 交集/删除 dirty、真实 Stockpile/Pawn/JobOnThing 搬运 job、真实 Toils_Haul 投递与清除；真实成熟 Potato 的 HarvestPlant designation；实际 GetGizmos 的 select-similar、def/stuff 判定和真实 Selector 选择变化。

没有编译引用 MOD DLL 或替代 WorkGiver/Toil。finally 恢复原选择/静态筛选状态、删除自建 Stockpile 和实体。检查源已编译、未运行。长期缓存过期/cleanup、AllowAll 与全图 harvest/热键、容器及失败/部分投递、警报 UI、图标/拖框/真实输入、保存重载、PickUpAndHaul 组合仍待验证；不把当前可执行场景推广为全部 1121 行功能通过。


## 2026-10-15 名称和描述版本修订

本轮在现有1.7适配基础上更新About名称/描述版本措辞，packageId/发布ID/支持标签/生产源码与DLL不改。
名称：Another Allow Tool (1.6) → Another Allow Tool (1.6/1.7)。

- 标题原来只有1.6；新1.7及旧版均保留。
- 版本专用标题对应简介需明确1.7，原功能正文完整保留。

语法/身份/版本声明与原产物一致已核验；属于元数据修改，不将其当新功能验证。详细回写审计见工作区expansion/metadata-changes.json。
