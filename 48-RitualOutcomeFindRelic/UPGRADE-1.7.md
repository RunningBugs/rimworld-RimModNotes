> 主代理游戏验证：`mod48-rev1248-s`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod48-rev1248-s`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# RimWorld 1.7 升级：Ritual Outcome Find Relic

packageId `RunningBugs.RitualOutcomeFindRelic`，Workshop `3444646324`。基于当前1.6圣物线索逻辑，审查 `1.7.4977 rev1248` 固定游戏源码。**已适配、编译通过；真实任务场景待主代理执行**。

## 最小改动

新增1.7源码/同名FindRelic.dll与About1.7；旧版字节和所有Common Def/语言保持，默认1.7/Common/root，无活跃LoadFolders（.bak忽略）。仅新版工程net10.0、精确 `Krafs.Rimworld.Ref 1.7.4977-alpha.1` / `Krafs.Publicizer 2.3.2`，IgnoresAccessChecksTo；去掉PublicizeAll，仅公开2个实际调用的protected方法：QuestPart_SubquestGenerator.TryGenerateSubquest与QuestPart_SubquestGenerator_RelicHunt.InitSlate。没有Harmony补丁或调用，删除新版未用Harmony包，About仍仅依赖Ideology。

SettingsUI构造函数移除多余 `this.modSettings =`赋值，保留GetSettings<Settings>()；真实Verse.Mod.GetSettings已经给私有modSettings赋值并绑定Mod。无需为了重复赋值额外publicize私有字段。

唯一业务修正：将无可生成新圣物的guard移入existing parent==null分支。原方法先检查CanGenerateRelic，native父任务生成圣物后该属性false，即使现有parent未完成且需要继续线索，也可能被阻止。新分支只在创建新parent时要求合法可生成圣物；已有parent仍检查生成器relic存在、pending/成功上限。主代理确认该既有缺陷的最小修正，仅新1.7；不绕过新圣物生成是否合法，不改变chance、任务威胁点数或原上限策略。

## API 与语义逐项

完整原/新metadata为 `logs/48-RitualOutcomeFindRelic/api-{original,new}.json`。所有自有函数/闭包、DefOf、XML及序列化审查，不能当作穷尽任意动态第三方路径。

- RitualAttachableOutcomeEffectDef FindRelic.workerClass仍绑定自有Worker；Apply(Dictionary<Pawn,int>,LordJob_Ritual,RitualOutcomePossibility,out string,ref LookTargets) override与真实原生签名相同。RitualOutcomeEffectWorker_FromQuality的AttachableOutcome调用它；无需Harmony。Chance失败或实际任务未生成时extraOutcomeDesc=null，成功时原letterInfoText，既有真实成功语义保留。
- RelicHunt QuestScriptDef及native QuestNode_Root_RelicHunt仍存在；原生root以PrimaryIdeo可生成Precept_Relic筛选、生成Thing后relicGenerated=true；StorytellerUtility.DefaultThreatPointsNow(Find.World) slate传入native CanRun和QuestUtility.GenerateQuestAndMakeAvailable，有实际敌人安全点数来源，不恢复空slate旧缺陷。
- QuestManager/QuestsListForReading、q.root/parent/State、QuestState顺序NotYetAccepted=0/Ongoing=1与结束状态>1保持。existing parent匹配原root.defName和进行中范围；GetSubquests/PartsListForReading及公开relic字段合同一致。
- 原生QuestPart_SubquestGenerator的CanGenerateSubquest仍采用pending<maxActive及successful+pending<maxSuccessful；自有前置检查逐项相同。TryGenerateSubquest protected virtual实际查询下一script/InitSlate/GenerateQuest/parent/link/letter。RelicHunt.GetNextSubquestDef只检查随机队首的CanRun并可能false，自有fallback仍逐一InRandomOrder测试其他真实script，不假设“随机第一个可运行”。
- native RelicHunt.InitSlate protected override传points/relic/relicSlateName，target为useMapParentThreatPoints.HasMap的Map或AnyPlayerHomeMap/World fallback；自有fallback target与它一致，两处Publicizer仍调用真实virtual方法，扩大可见性不是语义证明。
- FR_GameComp.FinalizeInit/SettingsUI.DoSettingsWindowContents仍更新Def.effectDesc；RefreshEffectDesc用BCL首百分数/小数/占位符识别后实际Translate+ToStringPercent。原keyed和DefInjected模板{0}保留，不新增语言文件；Percent特殊输入与反复设置需真实语言检查。
- Settings.FindRelicChance的原Scribe key/default1不变。LudeonTK DebugAction注解和静态GenerateAndSendQuest入口保留；UI Listing/Slider与SettingsCategory旧key一致。
- Common空Patch无目标；PerceptDefs文件夹仅加载扫描目录而非类型名绑定，RitualAttachableOutcomeEffectDef的真实类别按XML节点加载，不机械纠正文件夹拼写。

## 验证

```bash
scripts/dotnet.sh restore work/48-RitualOutcomeFindRelic/1.7/Source/mod.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/48-RitualOutcomeFindRelic/1.7/Source/mod.csproj -c Release --no-restore
scripts/dotnet.sh build logs/48-RitualOutcomeFindRelic/Checks.csproj -c Release --no-restore
```

生产/断言工程均0warning/0error，产物仅FindRelic.dll/deps.json，没有游戏/Harmony/publicized DLL。静态XML/旧版本/布局检查 `static-validation.json`。没有原可执行tests；百分数识别与当前Translate/静态chance耦合，不复制其算法当独立fake单测，而在真实游戏中调用生产函数验证。

`Tests/1.7/Mod48Checks.cs`已编译、尚未实际执行：

- 实际loaded XML.Worker/Apply，零chance不建quest/不报成功；临时QuestManager隔离原任务，给当前PrimaryIdeo按真实PreceptMaker/AddPrecept.Init生成一份合法临时relic，原relic可生成标志有精确快照。没有parent且全relic已生成时拒绝新任务，不绕过合法性。
- chance1真实Apply生成native RelicHunt+真实child并返回text，观察实际parent CanRun slate的current threat points、实际relicGenerated变化；全部relic CanGenerate=false时继续已有parent，实际第二child不建重复parent；active-cap/成功+pending-cap拒绝且失败Apply不报成功。
- 缺生成器/nullrelic返回false；测试仅一次阻止native TryGenerateSubquest作为真实首选择失败的输入，生产fallback仍运行真正CanRun和QuestUtility，必须实际建第三child并link同parent，不能凭返回true判通过。
- 实际RefreshEffectDesc整数字/小数/模板/仅首百分数/无数字边界，实际组件FinalizeInit更新说明，实际Scribe chance0.23往返。
- finally具体unpatch两spy、移除隔离任务/newworldobjects/生成relicThing/letters、恢复原questmanager/primary precepts与每个relic标志和thing引用/chance/说明；只写隔离Mod48SettingsFixture.xml。测试会消耗本case游戏的UniqueID/Rand，正式数据不涉及。

仍待：完成真实仪式整条流程/仪式设置UI、子任务实际进行和主圣物Site最终发现、世界/无地图target、全未运行script列表错误边界/未来script类型、中文description/UI、任务/设置真实保存新进程加载、多MOD组合。原root缺少条件时仍Log.Error并返回false的行为保持，不能把捕获异常视为功能通过。
