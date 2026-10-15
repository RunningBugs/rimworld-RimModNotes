> 主代理游戏验证：`mod24-rev1248-b`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod24-rev1248-b`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 beta 升级记录

状态：已适配，游戏验证未完成。基线 `1.7.4977 rev926`，发布 ID `3193742676`，packageId `RunningBugs.DeathIsComing`。实际旧基线仅 1.4/1.5；没有假设已支持 1.6。

新增独立 1.7 工程/DIC_DeathComing.dll，net10.0、精准 Ref 1.7.4977-alpha.1/Harmony 3.0.0-rc.2。新工程移除未使用的 HugsLib，无私有直接访问，不需 Publicizer。旧 1.4/1.5/Common 和备份保留，About 加 1.7，默认目录选择新同名 DLL。

源码仅两个兼容调整。1.7 已无 Pawn.IsNonMutantAnimal；这里的过滤还要求 RaceProps.Humanlike，Humanlike=>ToolUser、Animal=>!ToolUser，故原非动物条件冗余，删除它仍保留原允许的殖民者/奴隶、人类、非怀孕、玩家阵营/家园条件；未盲换语义不同的 IsAnimal。原 Translate(pawn.Name) 在 1.7 走已废弃 params object[]，改 Name.Named("PAWN") 的当前入口。实际 GrammarResolverSimpleStringExtensions 保留 arg 对象在 argsObjects[0]；GrammarResolverSimple:250 的数字 {0} 仍按索引取该对象，默认无 sub-symbol 时 obj.ToString()，保持 Name 的旧位置替换结果，未改变消息文案。

完整 API/生命周期审查覆盖 DefOf IncidentDef、StaticConstructor Log、GameComponent(Game) 自动创建、ExposeData/Scribe_Values.Look(DeathMercy,"DIC_GameComponent.DeathMercy",true)、IncidentWorker.CanFireNowSub/TryExecuteWorker；Find.Maps/Map.IsPlayerHome/mapPawns.AllPawns；Pawn.Faction/IsColonist/IsSlaveOfColony/RaceProps.Humanlike/health.hediffSet.HasPregnancyHediff/Name/Corpse/Kill；RandomElement、Messages/LookTargets/NeutralEvent、Translate。自有 worker 仍由 IncidentDef.Worker 构建且 public TryExecute 调用 protected worker，现有 DIC_DeathComing Def 的 baseChance=0 为手动事件，未调整触发概率。

特殊审查：源码带 ForceFirePatch 属性但启动函数只有 Log，从未调用 Harmony.PatchAll。CanFireNow 实际继承自 IncidentWorker，贸然激活这个属性可能补到整个基类。保留未注册状态，没有为统一样式添加 PatchAll，也不冒充已有强制事件补丁生效。实际事件自身 override CanFireNowSub 为 true，仍受原生上层检查。无 transpiler/反射生产路径。

既有问题单列保留：Common English/Data.xml 为空，DeathKill/DeathNoKill 没有 Keyed，可能显示 key 并产生缺失翻译警告；LookTargets 在 Kill 前取 pawn.Corpse，不保证消息跳转；DeathMercy 虽保存但 worker 没读取。此处不借迁移改变这三项历史行为。纯 bool 的 ForceFirePatch.Prefix 合同可单独调用，但它依然不属于实际注册功能。

已执行 restore/Release build，最终 0 warnings/errors（logs/24-DeathIsComing/restore.log、build.log）。初次 obsolete API 诊断保留 build-initial-obsolete-api.log。完整原 1.5/new 1.7 metadata 在 api-original.json/api-new.json；XML/旧字节/单 DLL 检查通过 static-validation.json。没有复制游戏/Harmony DLL。所用旧 Ref 1.5.4033-beta 只用于定位原基线，旧游戏完整语义出处未核验。

有效独立单测：系统已装 .NET 10、工作区隔离脚本运行纯 BCL runner，加载**最终 DLL**，实际执行 ForceFirePatch.Prefix 的两种初始 bool 状态，均返回 false 并使 result=true。命令 `scripts/dotnet.sh logs/24-DeathIsComing/bin/Release/net10.0/Unit.dll <最终DLL绝对路径>`，通过日志 unit-tests.log；不执行引擎，不把该纯合同等同于 Harmony 注册或 Waylith 游戏兼容。

Tests/1.7/Mod24Checks.cs 已精准依赖独立编译 0 warnings/errors（checks-build.log），尚未执行。它检查事件与未注册属性、真实 GameComponent 的原键 Scribe 保存/加载 roundtrip，在一次性新游戏测试无家园候选分支、正常手动事件恰杀一名候选且跳过真实怀孕 Pawn，并对 Name 的新格式/旧 string.Format 的 {0} 做实际等价断言。会杀死可丢弃 quicktest 生成的一名殖民者，故只允许主代理隔离用例；恢复家园阵营/组件字段、清理临时孕妇，保留独立 Mod24Fixture.xml 证据，不使用用户存档。

剩余：真实事件/序列化驱动执行、奴隶/动物/多地图候选、手工场景触发与已有保存加载。生产各函数除纯 Prefix 外均依赖游戏对象或序列化；未复制筛选条件作为假单元测试。主代理必须按实际消息缺键诊断与原始状态比较，不能由最终编译通过推断全部功能已验证。
