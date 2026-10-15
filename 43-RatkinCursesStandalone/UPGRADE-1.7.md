# 鼠鼠诅咒独立版1.7适配

2026-10-15，实际RimWorld1.7.4977 rev1248，包ID RunningBugs.RatkinCursesStandalone。完整自用项目，无发布ID；新增独立1.7源/RASL.dll，旧1.5/1.6与Common内容/存档键不动。**生产和原生检查编译通过；真实游戏功能尚待root执行**。

## 最小变更

- 固定net10、Krafs.Rimworld.Ref1.7.4977-alpha.1、Lib.Harmony3.0.0-rc.2，PatchAll明确typeof(Start).Assembly。未使用HugsLib，删掉1.7模板引用；无私有访问，不添加Publicizer。默认加载版本目录优先，DLL仍叫RASL.dll，与旧产物相对名一致；根Common内容保留。
- 名称改为鼠鼠诅咒 Ratkin Curses [1.5/1.6/1.7]，描述如实说明具体Wounded功能和验证边界。真实GrayPall WeatherDef及Fleshbeast死亡音效只在Anomaly数据包中定义，1.7增加versioned Anomaly依赖（连同Harmony），未给旧版新增必要依赖，也未用桩/替代天气假装不需DLC。
- 旧GameCondition_Curse.End对base.SingleMap直接访问；世界/场景级条件所属GameConditionManager.ownerMap=null，正常world End会空引用。1.7先取得AffectedMaps快照，正常执行base.End，再对实际各受影响地图StartNextWeather。地图级同样更新；保留世界curse清除/通知/正常base生命周期，没吞例外或删天气效果。
- 43和已发布50是不同项目。本项目只拥有RA_CurseCondition_Wounded具体Def和RASL.WorldComponent/类，完整保留自己的殖民者/殖民地奴隶、真人、正数健康伤害、spawned且未dead/downed过滤。未引入50的五诅咒架构、settings或activation IDs，不把项目合并。

## 全API/内容和语义

两个实际C#文件、Project、全部Common XML/语言已读。完整静态成员/类型引用见expansion/logs/43-RatkinCursesStandalone/api-references.json，语义调用链api-review.json；所有XML解析成功，原版GameCondition基类字段、weatherDef、canBePermanent/allowUnderground及DefModExtension真实字段吻合。

Pawn.TakeDamage真实injury链调用PostApplyDamage(DamageInfo,float)，Prefix参数仍合法，触发是实际损伤而非直接调用Prefix；血迹为原种族BloodDef，随机附近Standable/非water格，调用FilthMaker真实placement规则。Sound来自Anomaly。HealthUtility.DamageUntilDowned在实际1.7先设health.forceDowned、正常TakeDamage/健康状态，再进入PostApplyDamage；50已经有此同类原生证据，但43仍要独立真实验证，不能借50的pass当本modpass。

WorldComponent GetComponent、Scribe_Values "curse"、GameConditionMaker/Manager.Init/End、base ForceWeather.ExposeData、LetterMaker及translation.Named保留。实际GameCondition.SingleMap只对应ownerMap；AffectedMaps覆盖world和map，是End最小修复的依据（RW/17/Source/RimWorld/GameCondition.cs:40,169,239；GameConditionManager.cs:167；GameCondition_ForceWeather.cs:13）。

数据证据：实际安装Data/Anomaly/Defs/WeatherDefs/Weathers.xml:193 GrayPall；SoundDefs/Pawn_Fleshbeast_Vox.xml:59 death sound。不是仅按标题猜DLC。只有一个具体Wounded Def；旧Description复数措辞不构成已实现五种诅咒。

## 构建/记录

```bash
bash scripts/dotnet.sh build expansion/work/43-RatkinCursesStandalone/1.7/Source/mod.csproj -c Release
bash scripts/dotnet.sh build expansion/work/43-RatkinCursesStandalone/Tests/1.7/checks.csproj -c Release
bash scripts/dotnet.sh tools/api-audit/bin/Release/net10.0/ApiAudit.dll expansion/work/43-RatkinCursesStandalone/1.7/Assemblies/RASL.dll
```

prod/checks都0warning/0error。旧文件逐SHA证据legacy-preservation.json，DLL SHA/严格8文件应用清单见expansion/results/43-RatkinCursesStandalone.json。没有发布deps.json/引用包/Harmony/测试DLL/obj/bin。本代理未启动游戏。

Tests/1.7/Mod43Checks.cs → RW17.Validation.Mod43Checks.Run(Map)，真实PackageId；只编译引用游戏/Harmony并反射本mod。实际注册world permanent条件→letter/state/weather；真实colonist和colony slave受伤downed且未dead并留下血迹；inactive/zero/unspawned/noncolonyhuman/animal/End后受伤阴性控制；实际world End清state并解除forcedWeather，随后map Init/End。finally恢复原世界状态、战斗日志、天气/letters，清理仅自己新增condition/pawn/filth；原地图Pawn保留。

仍未测：实际运行上述native；真正天气图形/音效；worldcomponent/gamecondition完整rws save+新进程回读；多world/map同类condition叠加（原单string设计会受End清状态影响，未顺手改成50架构）；随机损伤/长期游戏、其他MOD补丁组合。功能验证只按root返回的具体真实场景标记，不能以编译/加载无错声称通过。


## 主代理最终覆盖证据（2026-10-15）

前段编译/待运行说明属于代理阶段；以下为最终实测覆盖，未列工作流、物理交互、其它三方组合与跨1.6存档仍保留边界。
- expansion-polaris41-ratkin43：18.0秒，正常exit0/未超时，相关实际检查通过；仅原版Headless 0x0 resolution基线ERROR。

四个本地fork及整个RimModNotes/工作区完整备份验证已完成，见expansion/backup.json；旧版/ID/原用户改动保留。所有游戏测试完全隔离，未修改用户配置/存档，未提交/推送。主代理将在整合后核验受控回写哈希与实际本地路径。


主代理已回写至实际本地目录：/home/lisanhu/mine/workspace/rimworld/RimModNotes/43-RatkinCursesStandalone。回写后 expansion-local-selfuse-final 使用实际本地新1.7路径再次验证，相关检查通过且正常退出、无超时；Headless仅基线0x0 ERROR，渲染组合无ERROR。原始文件保留与显式清单哈希均复验；本轮未commit/push/Workshop发布。完整结果见 /home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/expansion/REPORT-1.7.md。
