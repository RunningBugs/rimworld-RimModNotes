> 主代理游戏验证：`mod05-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod05-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod05-wave1`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod05-wave1`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# Highlight Enemies：1.7 beta 升级记录

状态：已适配，生产 DLL 和真实异步场景代码编译通过；尚未运行游戏/渲染验证。仅修改 work 完整副本。

游戏 1.7.4977 rev926，packageId RunningBugs.HighlightEnemies，发布 ID 3068397548；名称与原版本文件均保留。

## 最小修改

新增独立 1.7 工程与同名 HighlightEnemies.dll，net10.0，锁定 Krafs.Rimworld.Ref 1.7.4977-alpha.1 / Lib.Harmony 3.0.0-rc.2。去除 1.7 项目未用的 HugsLib 包和 Main.cs 中未用 HugsLib.Utils using；没有实际 HugsLib 类型调用或 About 依赖，不新增该运行 MOD。原 Start 已显式 PatchAll(Assembly.GetExecutingAssembly())，保留。

About 仅新增1.7支持；无显式 LoadFolders.xml，默认1.7+Common+根，DLL相对文件名与旧版一致。没有私有游戏成员访问，Publicizer不需要。原1.4/1.5/1.6以及Common逐字节保留。

## API与语义审查

全部静态引用保存在 logs/05-HighlightEnemies/api-references.json（含泛型/TPL及框架属性）；没有生产反射、transpiler或原版IL假设。

- EnemyHighlighter(Map) 继承 MapComponent，FinalizeInit/MapComponentTick 的签名仍存在；MapComponentUtility仍实际调用这些生命周期（RW/17/Source/Verse/MapComponentUtility.cs:24）。默认开关从 ModSettings读取；关闭时调用DeHighlight。
- TargetsHostileToColony 仍返回玩家阵营敌对目标HashSet（RW/17/Source/Verse/AI/AttackTargetsCache.cs:26），注册/撤销仍来自Thing生成/离图流程；t.Thing、GenHostility.HostileTo、Faction.OfPlayer、map.listerBuildings.allBuildingsNonColonist、ThingDef.building.combatPower仍可访问。继续收集敌对目标及combatPower>0的敌对建筑，没有扩大目标范围。
- AddTo的去重/null判断、InvisibilityUtility.IsPsychologicallyInvisible、GridsUtility.Fogged及两项设置接口保持；不可见/战争迷雾条件的实际分支仍需专门场景，当前编译不证明所有条件。
- Highlight继续Task.Run后台收集，再ContinueWith回到TaskScheduler.FromCurrentSynchronizationContext；实际1.7 Waylith.EngineApplication.RunLoop设置MainThreadSynchronizationContext（RW/17/Waylith/Waylith/EngineApplication.cs:457），其Post/Pump支持回主线程（MainThreadSynchronizationContext.cs:25/:73）。不能在后台Map.FinalizeInit中调用这个功能测试，也不能在主线程Task.Wait造成死锁。
- DesignationManager.AllDesignations返回汇总临时List，DesignationOn/AddDesignation/RemoveDesignation/RemoveAllDesignationsOfDef仍可用（RW/17/Source/Verse/DesignationManager.cs:30/:167/:378/:448）；保留已有先移除过期标记再补缺失标记的顺序。后台读取可变缓存及故障Task未主动报告是既有实现，本次不顺手重构；实际测试必须等待完成并观察Task异常，不能仅搜红字。
- 唯一Harmony目标PlaySettings.DoPlaySettingsGlobalControls(WidgetRow,bool)及其GlobalControlsUtility调用仍存在（RW/17/Source/RimWorld/PlaySettings.cs:108；GlobalControlsUtility.cs:16）。Postfix在worldView时返回，其他场景查Find.CurrentMap.GetComponent后用ToggleableIcon/Texture2D/ContentFinder/SoundDefOf/Translate显示开关。UI/问号绘制不以无窗口场景验证。
- 原Harmony owner误沿用 com.RunningBugs.AlertUtility，与01共享；本次保留既有ID，检查具体PatchMethod而非owner字符串，避免把另一个MOD的按钮补丁当成成功。组合里按owner卸载会相互影响，未测该组合。
- HE_Mod/HE_ModSettings的ModContentPack构造、GetSettings、DoSettingsWindowContents、SettingsCategory、Scribe_Values(bool)、Listing_Standard.CheckboxLabeled、ModSettings.Write及UnityStubs Rect/Texture/Widget接口保持编译；HE.markEnemiesByDefault/Invisible/InFog序列化键与默认true不变。
- Common的HE_Mark DesignationDef仍targetType=Thing，texturePath=HE/UI/Mark-trans-red，现有PNG/DDS与HE/UI/Alarm图标均保留；模板success_letter字段与当前LetterDef对应，空Patch保留。XML全部解析通过，语言键不改，资产实际渲染/新缓存尚未验证。

## 验证与场景

生产构建与 Tests/1.7/checks.csproj 都0 warnings /0 errors。

```bash
bash scripts/dotnet.sh restore work/05-HighlightEnemies/1.7/Source/mod.csproj --configfile .nuget/nuget.config
bash scripts/dotnet.sh build work/05-HighlightEnemies/1.7/Source/mod.csproj --no-restore -c Release
bash scripts/dotnet.sh build work/05-HighlightEnemies/Tests/1.7/checks.csproj --no-restore -c Release
```

没有可脱离游戏的独立业务算法/既有单元测试；真实场景 Tests/1.7/Mod05Checks.cs 需要在完全初始化后的Waylith主线程 Run(Map)，可供主代理合并。它确认实际组件和具体工具栏补丁，生成敌对人类Pawn并确认真实敌对缓存输入；调用真实MapComponentTick、用引擎实际MainThreadSynchronizationContext.Pump推进异步回调（有5秒上限并观察Task异常），验证标记出现、第二次不重复、关闭后清除。finally清理Pawn、还原已有标记/设置/随机状态。没有复制生产筛选算法或模拟UI，已编译但未执行。

未测：上述运行断言、实际图标/问号绘制、迷雾/心理隐身/敌对建筑、地图切换和连续异步竞态、设置保存重载、与AlertUtility或第三方组合。无窗口marker数据通过也不等于视觉功能通过。

DLL SHA-256：1b8ea7b396ae675d530c3da8aaea397bceba73ada86f7b52b179b2328d9df3db；静态成员96、类型75。日志目录 logs/05-HighlightEnemies/。
