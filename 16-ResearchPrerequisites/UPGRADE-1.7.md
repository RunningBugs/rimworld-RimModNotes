> 主代理游戏验证：`mod16-rev1248-i`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod16-rev1248-i`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# RimWorld 1.7 升级记录：Research Prerequisites

- packageId `RunningBugs.ResearchPrerequisites`，Workshop `3142286074`；以当前 1.6 源码（含 2026-08 队首即当前研究的改动）为迁移基线。
- 审查游戏固定快照 `RW/17/Source`，`1.7.4977 rev1248`。rev926→1248 Assembly-CSharp 方法正文和私有/公开元数据合同相同；GUI/Input 另查新版 UnityStubs。引用包是 `Krafs.Rimworld.Ref 1.7.4977-alpha.1`，并非执行程序集。
- 状态：**已适配、生产与断言工程编译通过；实际游戏功能待主代理运行**。

## 最小变更与加载

新增 `1.7/Source` 和同名 `1.7/Assemblies/ResearchPrerequisites.dll`；只将无参 PatchAll 改成 `PatchAll(typeof(Start).Assembly)`，明确 Harmony 3 扫描程序集。工程切换 net10.0 / 精确 RimRef / `Lib.Harmony 3.0.0-rc.2`，删除新版工程未使用的模板 HugsLib 引用。不需要 Publicizer：唯一非公开直接入口是 Harmony 按字段名读取 protected selectedProject，未从 C# 直接访问。

About 增加 1.7。保持全部旧版源码、DLL、Common 内容及元数据身份。无 LoadFolders：真实 ModContentPack 自动采用精确 1.7、Common、根目录；GetAllFilesForModPreserveOrder 按相对路径去重，新 `Assemblies/ResearchPrerequisites.dll` 优先于 Common 同名旧 DLL，实际只有一个进入加载清单。故无需复制语言或改变旧目录规则。Common/Defs 与 Patches 为空，Common 两种 Keyed 翻译继续共享；English/Data.xml 空文件无需改动。历史 docs/superpowers 中“只改1.6”描述对应的已完成旧规格，本记录补充新1.7目标，不改历史行为规格。

## API 与语义审查

完整静态 metadata 调用/字段/类型引用保存于 logs/16-ResearchPrerequisites/api-{original,new}.json；并检视全部十个 C# 文件。不能据此声称枚举了任意第三方反射路径。

| 路径 | 1.7 实现与结论 |
| --- | --- |
| MainTabWindow_Research.DrawStartButton(Rect) Prefix/Postfix | private 原方法仍在 DrawRightRect 调用；protected selectedProject 类型仍 ResearchProjectDef，AccessTools.FieldRefAccess 名称有效。Rect.LeftPart/RightPart、Widgets.ButtonText、TooltipHandler、Text.LineHeight、Label/Listing API 保留。 |
| DrawContentSource(Rect,ResearchProjectDef) float Postfix | 原方法仍在研究内容渲染调用，ref __result 累计布局高度合同不变。队首当前项目只画一次，清空经过原 StopProject。 |
| ResearchManager.SetCurrentProject/StopProject | 单一公开 ResearchProjectDef 参数，正常槽 currentProj，Anomaly 槽 CurrentAnomalyKnowledgeProjects。原版研究按钮、MOD controller、实际研究管理仍调用这些方法；Postfix 参数 proj 与真实参数名一致。Scribe.mode 抑制加载时队首同步保留。 |
| ResearchManager.FinishProject(proj,bool,Pawn,bool) | ref doCompletionDialog 参数和 Postfix proj 仍对应。完成后更新 progress/knowledge、置空匹配槽并发 ResearchCompleted；设置按旧语义抑制 modal、由 Postfix 发 NeutralEvent letter。Anomaly ApplyKnowledge 和一般研究实际使用 FinishProject，没有失联补丁。原有全局 ActualDoComplete 静态值与递归前置语义保留，递归/第三方嵌套完成须追加验证。 |
| ResearchQueue 三类别/递归依赖/去重 | ResearchProjectDef.IsFinished 经实际 ResearchManager.GetProgress；PrerequisitesCompleted 同时检查 prerequisites 和 hiddenPrerequisites；TechprintRequirementMet、IsHidden、CanStartNow（台/机控者/分析/检查/Anomaly隐藏条件）均保留。队首未满足 CanStartNow 时等待，不跳过。 |
| GameComponentTick / Instance | GameComponentUtility 每游戏 tick 调用 GameComponentTick；Current.Game.GetComponent(Type) 组件存在，三队列按实际知识槽推进。静态 LastAttemptedHead 是原有防重复确认状态，本轮不改变生命周期。 |
| ResearchQueueController 理念确认 | UnlockedDefs、BuildableDef.canGenerateDefaultDesignator、Faction.ideos.HasAnyIdeoWithMeme、MemeDef.AllDesignatorBuildables、Dialog_MessageBox.CreateConfirmation 均保留。实际原版 MainTabWindow_Research 也使用同一理念检查和 DoBeginResearch 流程。 |
| Scribe / DLC / UI settings | 三个队列旧 key 和 LookMode.Def 不变；普通类别 null、Basic/Advanced DefOf 映射保留。FinishProjectWithLetter 原 key 不变；DubsMintMenus 检测仍通过 active packageId，不新增强依赖。 |
| Input/字体 | 新 UnityStubs Input.GetKey(KeyCode) 经 Keyboard.current 读取，LeftControl/RightControl 合同保留；Text/GUI 像素和物理 Ctrl 点击未在 headless 断言中验证。 |
| Logger.cs | 自有日志前缀与 Verse.Log 三入口和 Caller 信息不变；没有独立业务算法，不制造复刻日志单测。 |

不存在需新增 DefOf/Def/Patch 或改序列化字段的部分。原 About 描述仍是更早“新队列覆盖旧队列”描述，而当前源码已追加/保留队列；这是原有文档偏差，本轮以既有源码和 2026-08 规格为准，不扩展功能或改描述。

## 构建与验证

在工作区执行：

```bash
scripts/dotnet.sh restore work/16-ResearchPrerequisites/1.7/Source/mod.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/16-ResearchPrerequisites/1.7/Source/mod.csproj -c Release --no-restore
scripts/dotnet.sh build logs/16-ResearchPrerequisites/Checks.csproj -c Release --no-restore
```

生产 DLL 与测试断言编译均 **0 warning / 0 error**。日志 `logs/16-ResearchPrerequisites/{restore,build,checks-build}.log`；静态 XML、旧版字节、自动加载覆盖及产物布局验证 `static-validation.json`。输出仅本 MOD DLL/deps.json，没有 Harmony、游戏或 publicized DLL。

无现成可执行单元测试，现有 docs 的列表属于人工测试计划。队列虽含自有递归/去重逻辑，其 eligibility 和 finished 状态读取真实游戏静态上下文，故通过真实游戏断言覆盖，而不构造一份模拟 ResearchProjectDef 实现作为伪单测。

`Tests/1.7/Mod16Checks.cs` 只引用游戏/Harmony，反射调用生产实现；编译不是执行。场景包括：

- 实际组件和五处 Harmony 注册；null/已完成输入，菱形与隐藏前置依赖，重复入队稳定顺序，插队链长度/尾部保存，已完成/null 队首弹出。
- 真实 AdvanceCategory / GameComponentTick / ResearchManager.SetCurrentProject、StopProject、FinishProject：自动启动、队首自愈、手工启动保留、停止清空、缺设施等待且不跳过、条件解锁重试、插队保留被挤项目、完成 letter 代替 modal。
- Anomaly 启用时 Basic/Advanced/正常三槽并行和停止类别隔离；临时 debug_UnhideAllResearch 使输入项目不受地图初始 monolith 阶段阻塞，finally 精确恢复。
- 原私有 ShouldShowQueueButton 对 null/完成/当前/其他项目边界。
- 真实 Scribe 三队列 Def 引用与旧 normal key、FinishProjectWithLetter 设置往返，文件只在隔离 case 中写入。真实 game manager 暂时换成独立 ResearchManager，finally 恢复 manager、所有队列、LastAttemptedHead、设置、window/letter 和 codex debug 状态；不改用户存档或已加载正式 Def。

仍待实际执行及人工/更广场景：渲染、队列行和 Ctrl 按钮点击；缺科技印花/grav inspection/mechanitor/analysis 的真实解锁；理念确认取消与反复点击；DubsMintMenus 兼容；无 Anomaly 独立配置；真实保存→新进程重载/旧存档迁移；第三方 research UI 和嵌套 FinishProject。

完整保存/新进程重载：combined-save-rev1248 → combined-reload-rev1248，两次exit0、无新增ERROR；真实计时器(消息+tick)、Pawn引用物品策略(工业药7)、研究Def队列、世界诅咒activationID在6个断言中保留。一次性夹具由本轮生成，不使用真实用户存档；不声称跨1.6存档迁移。证据位于工作区testing/cases两用例的rw17-save-result.json。
