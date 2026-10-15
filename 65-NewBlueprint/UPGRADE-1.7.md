> 主代理游戏验证：`mod65-rev1248-u`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod65-rev1248-u`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod65-rev1248-r`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod65-rev1248-r`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# New Blueprint：1.7 beta 升级

基线 `1.7.4977 rev1248`，真实 `RW/17`。packageId `RunningBugs.blueprint2`、Workshop `3534166729`、程序集 `Blueprint2`保持。原1.6非缓存源码/产物全部字节相同。

## 最小变更与接口

- About声明1.7，README修正仅1.6/仅publicAPI的旧说明，新增1.7独立项目/最终DLL；所有18份产品C#正文不变。
- net10.0；固定 Krafs.Rimworld.Ref `1.7.4977-alpha.1` / Krafs.Publicizer `2.3.2` / Lib.Harmony `3.0.0-rc.2`。原来全Assembly-CSharp publicize改为**仅内部 PrefabDef.things / terrain**，`IgnoresAccessChecksTo`；未公开prefabs或任何其他字段。实际只需这两项且保护级别override保留，编译0/0。
- 产品没有Harmony patch/启动类/0Harmony MemberRef；保留原NuGet编译包但不新增About runtime Harmony依赖，不造空PatchAll入口。XML/DefOf注册是真正入口。默认1.7+根；根无Assemblies/LoadFolders，产物仅本DLL/deps.json，无游戏/Harmony DLL。
- 静态清单 `logs/65-NewBlueprint-api-audit.json`，189个可识别直接游戏/Unity成员；全部18份源码、继承/override、reflection publicize、XML及序列化另读。游戏API合同三份相同，但绘图/clipboard语义仍需实际运行。
- PrefabDef内部things/terrain仍是可变List；GetThings展开rects/positions/defaultposition，GetTerrain展开rects；PrefabThingData的def/stuff/hp/quality/rotation/position及TerrainData.def/chance/rects保持签名。真实PrefabUtility.CreatePrefab仍捕获当前地图Thing/顶部terrain，过滤buildable/补Foundation属于自有逻辑；GetAdjustedLocalPosition四方向算法不变。
- GenConstruct.CanPlaceBlueprintAt、PlaceBlueprintForBuild实际生成nativeBlueprint，God模式仍ThingMaker/GenSpawn+Quality/HP以及TerrainGrid.SetTerrain/SetTempTerrain/SetFoundation；Foundation/UnderTerrain两层语义保持，不能机械替换为散放Thing的ParentHolder检查。
- Designator_Cells/Designator 的 Create/Place/SelectedUpdate/ProcessInput/Finalize、DrawStyleCategory、GenAdj/TrueCenter/Graphic.DrawFromDef/DrawMesh、正式KeyBindingDefOf rotate及3个自定义KeyBinding均编译匹配。没有更改可视ghost/热键时机。
- 当前GUIUtility.systemCopyBuffer直接Glfw.Clipboard；Headless不提供正常窗口输入/剪贴板环境，必须保留可视验证。未修改用户真实剪贴板来做测试。
- XML `Blueprints2`六specialDesignatorClasses、Blueprint2KeyCategory和MirrorX/MirrorZ/SwitchMode实际类/Def存在。原三份静态library字典没有Scribe/磁盘存储，仍是会话数据；XML是分享格式，不虚称新增存档持久化。

## 编译与真正独立逻辑测试

```bash
scripts/dotnet.sh build work/65-NewBlueprint/1.7/Source/mod.csproj -c Release --configfile .nuget/nuget.config
scripts/dotnet.sh run --project work/65-NewBlueprint/Tests/1.7/SerializerChecks/SerializerChecks.csproj -c Release --configfile .nuget/nuget.config
scripts/dotnet.sh build work/65-NewBlueprint/Tests/1.7/Mod65Checks.csproj -c Release --configfile .nuget/nuget.config
```

产品/Map检查0warnings、0errors，日志 `logs/65-NewBlueprint-build.log`、`...-native-check-build.log`。DLL SHA256 `4dafe4958333c838d3dc669831cfecb364ee8f02844594fba158c0b0f2fe1c54`。XML全部解析通过。

`SerializerChecks`直接编译产品 BlueprintXmlSerializer.cs，用本机rev1248真实Assembly-CSharp/UnityStubs/Waylith/UnityCompat及定向Publicizer运行；仅在其test bin引用这些，**不放进MOD Assemblies**。真实native Prefab/DefDatabase的隔离数据fixture，无伪游戏类型/伪serializer，无游戏进程。single/list/first-item fallback、Unicode/XML特殊字符、尺寸、原生positions/rects扩展、quality/hp/rotation/stuff/chance、empty边界全部通过；日志 `logs/65-NewBlueprint-serializer-tests.log`。最初引用包+仅复制DLL的console布局不能由普通SDK定位程序集，已改为明确actual snapshot Reference/runtime deps，当前命令通过；不据此推导Waylith图形或访问兼容。

## 真实 Map 检查材料

`Tests/1.7/Mod65Checks.cs`仅游戏/Harmony编译引用，反射实际MOD；已0/0，待主代理主线程执行：6个真实XML设计器/键Def，真实wood chair+有效半损HP/Excellent/East与真实HeavyBridge+WoodPlankFloor，实际产品区域capture（buildable filter+internal列表访问+Foundation），实际单/多XML往返，正常模式真正GenConstruct.Blueprint_Build（旋转/镜像位置、朝向、stuff）、重复blocked/非法cell边界，God模式真实chair+metadata，FloorOnly仅floor蓝图，Bridges真实foundation而不放floor，实际RemoveBlueprint移除三library。finally清理自己生成Thing、恢复选取的自然terrain/foundation/under层及God开关；不模拟窗口点击，不碰系统剪贴板。

## 已知限制/未测

原生capture不设置Prefab.size，现MOD继承这一行为（放置依赖展开cell，并非size）；独立serializer尺寸测试不证明区域捕获尺寸正确。原XML格式还不保存color/paint、stackCountRange、prefab子结构等完整native数据。自有mirror仅改变位置、不另镜像建筑朝向，这是既有行为；Foundation补组时边遍历边修改List、catch静默的多组边界仍存在原风险。本轮不扩展重构/新增持久化。

真实Map检查未执行；GUI ghost/窗口/命名/导入冲突checkbox、clipboard GLFW、键绑定/模式切换事件、自动建造完成、多种foundation/temporary floor/跨MODdefs/保存重载待测。普通console纯逻辑测试与headless实体断言均不能证明这些交互已通过。

## 实际夹具修正 v2

`mod65-rev1248-r`已通过capture/internal fields/XML及正常BP路径，到God metadata失败。原fixture硬写木DiningChair HP71；真实DiningChair基础MaxHP100，WoodLog stuff factor0.65，native最大值约65（以运行时MaxHitPoints为准）。产品仅恢复 `0<hp<newThing.MaxHitPoints` 的合法受损HP，原71是超过实际Max的夹具，不是SpawnSetup重置HP。v2取真正chair.MaxHitPoints/2并断言严格低于Max，保留捕获+God HP/Quality/Stuff/Rotation全部强断言，错误消息记录具体数值，产品DLL不改。新检查0/0，日志 `logs/65-NewBlueprint-native-check-build-v2.log`；旧失败runtime保留，待主代理重跑。


## 2026-10-15 名称和描述版本修订

本轮在现有1.7适配基础上更新About名称/描述版本措辞，packageId/发布ID/支持标签/生产源码与DLL不改。

- Requirements列实际支持的1.6/1.7；介绍中Prefab系统首次在1.6引入的历史事实不改。
- 中英要求一致，避免把>=1.6误读为今后一切版本已支持。

语法/身份/版本声明与原产物一致已核验；属于元数据修改，不将其当新功能验证。详细回写审计见工作区expansion/metadata-changes.json。
