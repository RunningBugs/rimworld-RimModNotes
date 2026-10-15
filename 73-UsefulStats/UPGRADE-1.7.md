# Useful Stats 实用数据表 — 1.7

状态：适配、编译与独立纯逻辑测试通过，实际Map/native window断言等待root。packageId `RunningBugs.UsefulStats`，无直属发布ID，按用户追加的完整自用项目范围升级。名称增加[1.6-1.7]，双语描述包含1.7 beta。旧1.6及原tests目录未改。

## 最小适配与加载

独立复制1.6四个生产C#、MainButtonDef到1.7，**四份源码与当前1.6逐字相同，无生产逻辑修改**。新工程只改net10.0、固定Krafs.Rimworld.Ref1.7.4977-alpha.1、关闭deps输出；此MOD没有Harmony/私有访问，不机械新增Harmony/Publicizer或运行依赖。保持AssemblyName/输出相对路径UsefulStats.dll。默认精确1.7目录和根加载，只有1.7/Assemblies/UsefulStats.dll，不带游戏/测试DLL及obj/bin/deps。

## 全API/数据语义审查

真实基线1.7.4977 rev1248；完整旧/新静态引用在工作区`expansion/logs/73-UsefulStats/api-{original,1.7}.json`（新282成员/87类型）。无动态反射/Harmony/IL假设。逐项核查当前实际RecipeDef products/ProducedThingDef/IsSurgery/AllRecipeUsers/AvailableNow/AvailableOnNow/WorkAmountForStuff、IngredientCount.CountRequiredOfFor和ThingFilter.AllowedThingDefs/fixedIngredientFilter；现接口与真实调用行为仍可用。Availability实际按research/meme/faction/ideo precept及recipeWorker检查，不只编译签名。

ThingDef/BuildableDef的recipeMaker、MadeFromStuff/CostStuffCount/CostList/BuildableByPlayer/IsResearchFinished/stuffCategories、GenStuff.DefaultStuffFor、StuffProperties.categories、GetStatValueAbstract(WorkToBuild/WorkToMake/MarketValue)均仍存在。三个源分支Recipe/RecipeMaker/Buildable保持；保留生成RecipeMaker去重、256材料上限、默认材料、variable ingredient、category祖先去重/Root排除/Buildings回退，以及research unlock info。没有引用旧Source桩。

MainButtonDef.tabWindowClass/order/closesWorldView有效，自有Window类型仍继承MainTabWindow，PreOpen→dirty cache→DoWindowContents由原生主Tab调用；Widgets/TextField/scroll/Checkbox/headers/highlight/Tooltip、Rect/GUI/Text状态与UnityStubs当前实现存在。保留当前/未来/all-only、全部字段搜索、材料搜索、类别菜单、九种排序、展开折叠及可见区域行绘制。无RenderTexture或旧AssetBundle/Mono引用。没有自有Scribe存档/设置字段，window缓存只是临时UI状态。

## 构建与纯逻辑测试

工作区命令：

```bash
scripts/dotnet.sh restore expansion/work/73-UsefulStats/1.7/Source/mod.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build expansion/work/73-UsefulStats/1.7/Source/mod.csproj --no-restore -c Release
scripts/dotnet.sh run --project expansion/work/73-UsefulStats/Tests/1.7/UsefulStats.UnitTests.csproj -c Release
```

最终build.log为0warnings/0errors，所有XML解析/源码字节相等/单DLL输出检查通过。`Tests/1.7/UsefulStats.UnitTests.csproj`直接链接**新1.7/Core真实源码**，移植原有效Program断言并仅测试层加强实际NaN/Infinity失败判定；不改旧tests/1.6链接。扩展负分母/非finite/负分子、所有format精度分支、MaterialVariant派生值、row空值/类别路径/key/材料范围/默认值/扩展列表边界，日志unit-tests.log实际输出原UsefulStats.Tests:OK和扩展核心OK，退出0。纯BCL测试没有借游戏引用执行桩。

## 真实游戏driver与剩余范围

`Tests/1.7/Mod73Checks.cs`（RW17.Validation.Mod73Checks）只引用游戏，通过反射调用真实MOD，独立编译0/0，**已由root实际native场景执行通过**。实际Map加载全Def表，核对Wall默认stuff成本/价值/variants/count/default/category，真实CookMealSimple work/value/RawRice营养ingredient数量/bench availability，以及实际ResearchManager进度锁定/解锁和RecipeMaker单行分组。生成建筑前检查真实OccupiedRect，保留实际资源与原研究字典，finally恢复。

driver另直接检查自有纯UIlabel/count-range/contains边界，真正MainTabsRoot.SetCurrentTab触发native open/cache/render/close；搜索/无结果/material/category menu/work正反排序/材料展开折叠/all toggles off均调用生产方法。`RW17_STATS73_RENDER=1`可最后留下实际主Tab供root统一GPU截图，默认清理窗口。纯CoreChecks是独立exe源码，**不要把它复制进公共harness**；harness仅集成Mod73Checks.cs。

待root实际Map结果、GPU像素、物理搜索/类别点击/排序/滚动/展开，以及大型第三方Def/recipe数据组合。独立测试/受控入口不等于真实物理交互全通过。原RecipeMaker“current”主要按research判断、材料摘要/market value单位与生成配方代表规则保持；没有借迁移改写经济计算/配方批量设计，不宣称任意MOD的配方含义都已完整覆盖。没有自有存档状态需要虚构roundtrip，历史存档/大数据表性能边界单列。

当前实际native证据：`testing/cases/expansion-gui09-letters27-stats73/SaveData/rw17-map.json`对应检查passed，整case退出0/19.4s，仅Headless Resolution基线；保留真实GPU/物理UI边界。


## 主代理最终覆盖证据（2026-10-15）

前段编译/待运行说明属于代理阶段；以下为最终实测覆盖，未列工作流、物理交互、其它三方组合与跨1.6存档仍保留边界。
- expansion-gui09-letters27-stats73：19.4秒，正常exit0/未超时，相关实际检查通过；仅原版Headless 0x0 resolution基线ERROR。
- expansion-stats73-render：57.73秒，正常exit0/未超时，相关实际检查通过；无ERROR。

四个本地fork及整个RimModNotes/工作区完整备份验证已完成，见expansion/backup.json；旧版/ID/原用户改动保留。所有游戏测试完全隔离，未修改用户配置/存档，未提交/推送。主代理将在整合后核验受控回写哈希与实际本地路径。


主代理已回写至实际本地目录：/home/lisanhu/mine/workspace/rimworld/RimModNotes/73-UsefulStats。回写后 expansion-local-selfuse-final 使用实际本地新1.7路径再次验证，相关检查通过且正常退出、无超时；Headless仅基线0x0 ERROR，渲染组合无ERROR。原始文件保留与显式清单哈希均复验；本轮未commit/push/Workshop发布。完整结果见 /home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/expansion/REPORT-1.7.md。
