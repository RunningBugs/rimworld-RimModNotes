> 主代理游戏验证：`mod50-rev1248-o`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod50-rev1248-o`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod50-rev1248-l`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod50-rev1248-l`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 升级记录

基线：实际 `1.7.4977 rev1248`，游戏接口与固定 `Krafs.Rimworld.Ref 1.7.4977-alpha.1` 的 171679 元数据合同匹配。原 1.6 目录保持原样。

## API 和最小修改

- 全部静态调用保存在工作区 `logs/50-RACurseStandalone-api-audit.json`（149 个 Assembly-CSharp 成员引用、1 个 UnityStubs 成员；泛型引用另列）。反射入口逐项复核：`MemoryThoughtHandler.TryGainMemory(Thought_Memory,Pawn)`、`ResearchManager.ResearchPerformed(float,Pawn)`、`Pawn.Kill(DamageInfo?,Hediff)`、`Pawn.PostApplyDamage(DamageInfo,float)`、`HistoryEventsManager.RecordEvent(HistoryEvent,bool)` 仍为实际调用入口。
- 实际 MemoryThoughtHandler.pawn、WorldComponent/Scribe、GameCondition.Init/End/Expired、IncidentWorker 条件流程、Pawn 伤害/死亡、Research XP 和 HistoryEvent 流程已对照 RW/17/Source；签名与现有意图匹配。游戏本体正文与前一 rev926 相等，参考包匹配；旧 RW/16 源码仅作辅助，旧链接 DLL 不用作基线。
- 新增独立 1.7 源码/DLL及1.7版本声明；目标改 net10.0，精确引用 `Krafs.Rimworld.Ref 1.7.4977-alpha.1`、`Lib.Harmony 3.0.0-rc.2`；显式 `PatchAll(typeof(Start).Assembly)` 避免内联改变调用方程序集。
- 原工程 publicize-all 将基类 protected override 误公开；恢复 `IncidentWorker_Curse.TryExecuteWorker` 为 protected。实际所需成员无需 Publicizer，移除未使用的 HugsLib 和 publicize-all 构建依赖。游戏/Harmony DLL 不复制到产物。
- 实际加载选择新1.7+Common；独立 EntityCodexEntryDefs 同1.6内容，Common旧版内容未改。随机池、activation IDs、legacy curses 与所有存档键不变。

## 构建和验证

工作区运行：`scripts/dotnet.sh build work/50-RACurseStandalone/1.7/Source/mod.csproj -c Release --configfile .nuget/nuget.config`。产品和 Tests/1.7/Checks.csproj 均0警告0错误。

真实地图用例 `Tests/1.7/Mod50Checks.cs`：检查5个Harmony入口实际注册；三轮随机无重复；重叠/最后一实例/invalidID；永久状态Init/End与信件；限时/永久Incident、重复拒绝、clear自动过期；真实无桌吃饭Memory入口→食物中毒、ResearchPerformed→Intellectual XP下降、TakeDamage→Downed、animal.Kill→Chimera、CutTree HistoryEvent→ColdSnap；实际Scribe组件activation和randompool回读。无独立BCL纯函数可可靠脱离Rand/世界运行，因此采用实际运行集成断言而不是桩。

## 尚未覆盖

真实界面、可听音频、跨1.6存档迁移与完整新进程重载；DLC缺失、多地图/无PlayerHome或非默认storyteller、所有非殖民者分支；动态其他MOD组合不能由单体检查外推。原CutTree First(m.IsPlayerHome)/First(storytellerComp)在无匹配项时可能异常，是原有设计风险；不为本轮迁移大改。运行结果由主代理补充，不能凭编译成功判定可用。

实测后夹具修正：Crashlanded已完成Stonecutting，改选实际未完成研究并核验当前project；finally恢复currentProj字段而不是给SetCurrentProject(null)。同时修正新1.7源码两处历史遗漏RA翻译前缀为现有RACurseSA键，不改Common语言或旧版。

完整保存/新进程重载：combined-save-rev1248 → combined-reload-rev1248，两次exit0、无新增ERROR；真实计时器(消息+tick)、Pawn引用物品策略(工业药7)、研究Def队列、世界诅咒activationID在6个断言中保留。一次性夹具由本轮生成，不使用真实用户存档；不声称跨1.6存档迁移。证据位于工作区testing/cases两用例的rw17-save-result.json。
