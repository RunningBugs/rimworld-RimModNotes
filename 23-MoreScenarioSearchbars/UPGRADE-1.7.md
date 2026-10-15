> 主代理游戏验证：`mod23-rev1248-j`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod23-rev1248-j`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 升级记录

目标 `1.7.4977 rev1248`。**适配/编译完成；上游实际注册、过滤及渲染交互待验证**。packageId/发布 ID 保留，原 Source/1.4/DLL 不变，独立1.7/net10.0，精准 Krafs.Rimworld.Ref1.7.4977-alpha.1、Lib.Harmony3.0.0-rc.2，去未用HugsLib，无需Publicizer；显式PatchAll程序集。About新增1.7，仅v1.7专属依赖/排序要求 Harmony、Scenario Searchbars先加载，旧版本metadata保持。

## 真实上游与语义变化

已安装 packageId nimrag.scenariosearchbar 实际在 Workshop3579415129（原说明链接3052467818并非本机产物），仍声明到1.6，选择1.6/Assemblies/ScenarioSearchbar.dll。固定实际引用 Private=false；真实来源/SHA见 dependencies.json，不用桩；上游旧标签本身不判不兼容，root先跑baseline。递归runtime IDs brrainz.harmony,nimrag.scenariosearchbar。

当前上游**已经包含与本补丁完全相同的 ScenPart_CreateIncident_Patch**：相同目标、相同Postfix和DrawSearchbar(...,35)。直接重编译会绘制两次文本输入。新1.7只增加HarmonyPrepare条件：已注册上游具体Postfix时跳过本重复补丁，缺失时保留原DrawSearchbar fallback。不删除/全局unpatch上游，不修改上游文件。目标改为真实游戏程序集的完整类型名 RimWorld.ScenPart_CreateIncident，避免跨程序集短名冲突；仍反射内部类型，不扩大可见性。

真实游戏 Source/RimWorld/ScenPart_CreateIncident.DoEditInterface 仍是scenario编辑入口并调用继承DoIncidentEditInterface；ScenPart_IncidentBase.DoIncidentEditInterface被上游Prefix接管，使用Tools.Filter<IncidentDef>后生成真正选择菜单。实际上游Tools.DrawSearchbar(Listing_ScenEdit,ScenPart,int)取Listing.GetRect、ScenPart.RowHeight与Widgets.TextEntryLabeled并存储searchStringDict（Tools.1.6.cs:25）；Filter泛型按Def.label处理忽略大小写Regex，空query返回全项（:37）。这些接口仍存在，上游的invalid regex在try外new Regex有既有错误，不在此范围修改上游。完整上游反编译、类型/成员audit在 logs/23-MoreScenarioSearchbars，无自有存档字段或Def/Patch迁移。

## 验证

生产build.log、scenario checks-build.log均0warning/error。`bash scripts/dotnet.sh build work/23-MoreScenarioSearchbars/1.7/Source/mod.csproj -c Release --no-restore`。

Tests/1.7/Mod23Checks只引用game/Harmony，通过实际程序集反射：要求实际目标上两套已知searchbar Postfix合计恰好一份（验证去重策略，不以类型存在当注册）；真实CreateIncident ScenPart+loaded ScenPartDef、真实Eclipse/SolarFlare/ToxicFallout IncidentDefs在实际上游Filter下，空query保留全部、精确label query缩到对应Incident、不存在label为零。finally清理自己的上游字典条目。该非空场景证明实际注册和过滤链，但没有渲染或注入伪鼠标，不能记成UI功能完全通过。尚未实际运行。

有窗口待测：编辑Create Incident时只出现一个搜索框，输入中文/英文/正则后打开的真实incident菜单正确过滤、offset35不覆盖其他控件、选择后incident/天数/重复选项正常；其他scenario类型、旧存档、IME/字体以及第三方编辑UI组合另行验证。没有独立自有算法，故不机械写镜像单元测试；实际上游Filter由游戏场景检查。 root测试须使用上述已安装路径和v1.7排序，避免Prepare早于上游注册造成重复。
