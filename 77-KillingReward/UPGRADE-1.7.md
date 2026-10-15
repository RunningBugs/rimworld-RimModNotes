> 主代理游戏验证：`mod77-rev1248-z`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod77-rev1248-z`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod77-rev1248-y`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod77-rev1248-y`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod77-rev1248-w`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod77-rev1248-w`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# RimWorld 1.7 升级：KillingReward 嗜血恩赐

packageId `RunningBugs.KillingReward`；Workshop `3775204051`。固定游戏 `1.7.4977 rev1248`，SDK10.0.100。本次从原仓库**当前 fe25d06 工作树**复制，完整保留用户原先提交的 KillRewardTracker.cs、Dialog_KillingReward.cs、两种语言与1.6 DLL。`logs/77-KillingReward/user-baseline-preserved.json`逐文件记录 fe25d06/current/copy的SHA256与字节一致证据，未从旧HEAD恢复覆盖。

状态：**已适配，生产与有效既有单元/静态测试通过；真实游戏功能断言待主代理**。

## 最小升级

新增独立1.7/Source、Defs、同名Assemblies/KillingReward.dll，About追加1.7；显式LoadFolders新增v1.7只加载根目录与1.7，保留v1.6原定义，避免旧DLL加载。根语言、纹理复用；1.6源码/产物和Tests/unit/whitebox冻结保留。

新版工程net10.0、`Krafs.Rimworld.Ref 1.7.4977-alpha.1`和`Lib.Harmony 3.0.0-rc.2`；无直接私有访问，**无需Publicizer**。唯一生产C#改动是构造函数PatchAll明确 `typeof(KillingRewardMod).Assembly`；除此所有生产C#与当前1.6逐字相同。原DEV升级、全成员候选、无效目标检查、三类赐福、特质/基因成本、搜索和提示保留，不借升级重构或修改奖励平衡。

## API 与语义审查

完整原/新static metadata调用/字段/类型引用为 `logs/77-KillingReward/api-{original,new}.json`；全部Core、Tracker/Settings/Adapter/Notifier、6个Rewards、Hediff、PawnKill、3个UI文件（Dialog671行）与Def/XML/语言人工审查。以下按功能链记录；动态MOD组合无法从metadata穷尽。

| 功能 | 1.7真实合同/行为 |
| --- | --- |
| 击杀hook/筛选 | Pawn.Kill(DamageInfo?,Hediff)override仍存在并由Pawn_HealthTracker调用；Harmony postfix参数dinfo仍匹配。Faction.HostileTo/IsPlayer和DamageInfo.Instigator真实类型稳定。Spawned Pawn.ParentHolder在1.7是map，不影响本MOD：hook使用Kill后的Pawn.Corpse再定位Map/DrawPos，未假设地上pawn holder=null。MoteMaker.ThrowText原接口保留，像素pending。 |
| Tracker/Core | GameComponent/GetComponent实例仍存在；五个long/int状态、3种消费方法、RequiredForCurrentLevel、Trait/Gene成本和DevLevelUp原行为与key保持。Core7文件无Verse/Unity，纯BCL单测覆盖全部业务函数；极端非法状态/计数溢出未扩展原约束。 |
| ResearchReward | ResearchProjectDef IsFinished/IsHidden/CanStartNow、DefDatabase、LabelCap仍符合当前bench/techprint/Anomaly条件；Complete仍调用真正ResearchManager.FinishProject，补progress/techprints与原生解锁/letter流程。没有重新实现native完成逻辑。 |
| SkillReward | PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction枚举保持；Humanlike/skills、SkillRecord.TotallyDisabled/Level/MaxLevel=20仍有效，原Level setterclamp保留。 |
| ItemReward | ThingDef.PlayerAcquirable/IsCorpse/isUnfinishedThing/stackLimit/thingCategories.Parent链、根类别Defs保留；MadeFromStuff/GenStuff.DefaultStuffFor/ThingMaker/GenSpawn真实接口匹配。Targeter.BeginTargeting(TargetingParameters,Action<LocalTargetInfo>,...)仍存，validator按Map边界/Walkable/fog，callback先消费后投放；真实鼠标取消/路径选择pending。 |
| TraitModReward | TraitDef.degreeDatas/ConflictsWith、TraitSet.allTraits/HasTrait/GainTrait/RemoveTrait仍存在，native刷新联动。对缺degreeDatas的第三方项旧防护保留。 |
| GeneModReward | BiotechActive仅控制可用，无强制DLC依赖；Pawn_GeneTracker.Endogenes/Xenogenes/GenesListForReading/AddGene/RemoveGene仍有效，Addable排除两类已有gene。源代码从未直接访问ThingDef.verbs，无需共享verbs迁移。 |
| Blessing group | PawnsFinder全player成员列表含人/动物/mech、health.AddHediff和HediffComp_Disappears.ResetElapsedTicks依然工作，重复领取刷新180000 tick。所有原Defs/CompClass/DefOf身份保持。 |
| Custom regeneration | **CompPostTickInterval(ref float,int)**仍是实际Pawn.DoTick→Pawn.TickInterval→HealthTickInterval→HediffWithComps.PostTickInterval调用链；HashInterval(15,delta)对批量delta有效。1.7原生350×0.00025治疗量及伤口/缺部位规则相同；自有comp有意没有Flesh/Anomaly gate，故mech可用。原实现未调用HediffSet.Notify_Regenerated的组合资源通知（1.6也已有该API），本轮保留旧策略，不顺手更改RapidRegeneration组合消耗。 |
| UI/letters/button | Window/640×600/close/input flags、MainButtonWorker.Visible/Activate、ChoiceLetter.Choices/DiaOption.Action仍保持。用户DEV按钮/SelectedPawnUnavailable保留，所有claim route仍先消费并校验目标。Listing/Widgets/TextField/scroll/icon/Font/Anchor/GUI颜色及Targeter合同存；新版字体/输入/像素不能由headless代替。 |
| XML/保存 | 3份新1.7 Def原样复制；真实LetterClass/WorkerClass/HediffWithComps/自有CompProperties绑定，图标存在。13设置和5状态key未改，无存档迁移字段；仅新增版本加载定义。 |

没有反射字符串生产入口、transpiler或第三方DLL调用。原文档“只1.6”的历史设计范围由本记录补充新1.7；旧逻辑完整保留。

## 真实已运行的单测与静态检查

新增 `Tests/1.7/unit`复用原4份xUnit案例并链接**新1.7/Source/Core**；只把TFM切net10.0和相对路径修正，原Tests不改。测试依赖精确沿用原版本：Microsoft.NET.Test.Sdk17.9.0、xunit2.7.0、xunit.runner.visualstudio2.5.7，隔离NuGet缓存。实际VSTest18.0.1宿主执行。

- **28通过、0失败/跳过**：增长曲线、progress rollover/pending、击杀8种truth table、skill clamp、stack、中文/英文search/null/trim。`logs/77-KillingReward/unit-tests.log`，24ms。这是链接相同生产纯源码的有效测试，不执行游戏对象。
- **7静态检查通过**：新版metadata/语言key和原风格/实际kill patch声明/Core无游戏依赖/XML worker/source存在/图标。新增Tests/1.7/whitebox只改版本和定位；`whitebox-tests.log`明确为静态检查，不冒充真实函数行为。
- 生产构建0warning/0error；游戏断言工程0warning/0error；产物只有本MOD DLL/deps.json，无Harmony/游戏DLL。`static-validation.json`含旧字节/用户前置改动保全、LoadFolders及所有XML与输出。

```bash
scripts/dotnet.sh restore work/77-KillingReward/1.7/Source/mod.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/77-KillingReward/1.7/Source/mod.csproj -c Release --no-restore
scripts/dotnet.sh restore work/77-KillingReward/Tests/1.7/unit/KillingReward.UnitTests.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh test work/77-KillingReward/Tests/1.7/unit/KillingReward.UnitTests.csproj -c Release --no-restore
.venv/bin/python -m unittest discover -s work/77-KillingReward/Tests/1.7/whitebox -v
scripts/dotnet.sh build logs/77-KillingReward/Checks.csproj -c Release --no-restore
```

## 实际游戏场景断言（尚待运行）

`Tests/1.7/Mod77Checks.cs`只引用游戏/Harmony，不链接MOD DLL：

- 真hostile/player-instigator Pawn.Kill两次精确计数/阈值/letter；真实Corpse-map，null adapter及中立/环境/敌方互杀/无派系/disabled native Kill负控制。
- 真letter Choices.Action打开dialog/撤信、真实mainbutton Activate/disabled Visible；用户alive/dead/null selection guard、DevLevelUp、normal/trait/gene独立成本消费/不足拒绝。
- 实际Skill候选/筛选/+3/cap，真实Trait/Gene增删与候选已持有排除、endo/xeno；真实category Steel、满Stack和stuffed剑投放；真正BeginItemTargeting注册/validator/回调消费投放（物理点击pending）。
- 实际bench+researchmanager eligibility非空，合成独立project经真实FinishProject进度完成（无正式Def修改；真实解锁组合pending）。
- 实际全player group targets包含人/动物/可选mech，真实重复祝福唯一/180000重置；真实HealthTickInterval非hash负控制与350HP/day quantum、实际Pawn.DoTick进入再生；人类合法缺失Hand回生并fresh injury；真实到期health移除。
- 5tracker和13settings实际Scribe往返。finally恢复原component/设置/researchmanager/tick、原有pawn祝福与duration，销毁本case新增Pawn/Corpse/物品/mote/window/letter，关闭仅case新targeter，不碰用户实际存档。

未覆盖：所有GUI视图/真实点击/搜索/地图target取消和所选目标运行中变化、DEV按钮条件可见性像素、无Biotech/Anomaly独立配置、caravan/运输舱/multi-map的真目标、母部位受伤/缺失/假体的再生限制和伤口顺序/高delta/3天全tick持续、组合RapidRegeneration资源、真实研究unlock、跨版本保存新进程重载。没有红字或编译通过不代表这些项已通过。

主代理夹具修正：mod77-rev1248-w的全部功能断言通过，但创建SimpleResearchBench未指定stuff触发原版MakeThing ERROR；夹具显式指定WoodLog，产品代码和功能断言保持原样，重新执行。
