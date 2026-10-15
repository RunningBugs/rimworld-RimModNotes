> 主代理游戏验证：`mod42-rev1248-a`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod42-rev1248-a`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod42-wave2`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod42-wave2`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 beta 升级记录

状态：已适配，游戏验证未完成。基线 `1.7.4977 rev926`；发布 ID `3328179405`；packageId `RunningBugs.SilentDeathPall`。

新增独立 1.7 工程/DLL，net10.0 与精准 Ref 1.7.4977-alpha.1/Harmony 3.0.0-rc.2；只改启动调用为 PatchAll(typeof(Start).Assembly)，旧逻辑和反射方式不变。移除新工程中原/新 metadata 均不引用的 HugsLib，无需 Publicizer；私有成员按既有反射访问。About 增加 1.7，旧 1.5/1.6/Common/备份保留，默认目录选择仅加载同名 1.7 DLL。

完整审查启动函数、GetFieldInfo/GetFieldValue/GetStaticFieldValue/SetFieldValue/GetMethodInfo 五个反射 helper 与 Prefix。静态 API：Harmony；GameCondition_DeathPall.GameConditionTick、AffectedMaps；TickManager.TicksGame；listerThings.ThingsInGroup(Corpse)、Corpse.InnerPawn/Age、MutantUtility.CanResurrectAsShambler；Pawn.Position.Fogged、Translate、Messages.Message/SilentInput、IntRange.RandomInRange。动态 API 均逐项对照真实 `RW/17/Source/RimWorld/GameCondition_DeathPall.cs`：private int nextResurrectTick、private static readonly IntRange ResurrectIntervalRange(600,1800)、private Pawn ResurrectPawn(Corpse) 保留；BindingFlags.Instance/Static|NonPublic 和返回类型一致。

当前 GameConditionTick 仍由 GameConditionManager 的活动条件 tick 路径调用，原版每 60 ticks 检查且尸体至少 15000 ticks，按 private interval 安排下一次。ResurrectPawn 仍调用真实 MutantUtility.ResurrectAsShambler，返回原 InnerPawn。原 Prefix 复制的遍历/过滤/调度与当前实现对应，并且返回 false 跳过原声响消息路径，改发 SilentInput。原版 TryDoMessage 的 60000 消息节流被既有 MOD 绕过，所以每次可见复活都会发静音消息；本轮保持原功能，未改为另一种节流设计。没有自有存档字段；原条件的字段由游戏 Scribe 继续保存。

反射 helper 对实际精确类型读取私有成员，动态派生类重写/私有继承并非静态审查可保证，未声称任意第三方条件兼容。Common Def/Patch/英文语言均为空容器，Anomaly+Harmony 原依赖保留。原 1.6 编译引用是 1.6.4488-beta；RW/16 中对应文本相同但构建出处未核验，只作辅助证据。

已执行 `scripts/dotnet.sh restore work/42-SilentDeathPall/1.7/Source/mod.csproj --configfile .nuget/nuget.config` 和 Release build，0 warnings/errors（restore.log/build.log）。原/新完整 metadata 引用在 logs/42-SilentDeathPall/api-original.json、api-new.json，另有 static-validation.json；单份 SilentDeathPall.dll/deps.json，无复制游戏/Harmony DLL。

没有已有独立单测。五个 helper 的有效契约是实际私有游戏成员，Prefix 依赖条件/地图/尸体/消息，测试真实入口更合适。Tests/1.7/Mod42Checks.cs 已单独编译 0 warnings/errors（checks-build.log），未执行：确认具体 prefix，创建真实成年死者/尸体与临时 DeathPall 条件，覆盖未到 nextTick、非 60 tick、尸龄不足及到期复活；断言实际 Shambler、SilentInput 新消息且 message type 无声音、原 lastResurrectMessageTick 未变、下一次在 600..1800 区间。finally 恢复 tick/旧消息并清理生成对象，条件未注册，因此不留下强制天气。

待主代理实际执行上述场景；另需多尸体/迷雾不可见分支、保存重载和实际音频确认。Headless 可以验证静音类型和真实复活流程，但不能证明听感。未机械为反射 helper 编写重复实现测试或把静态编译冒充功能通过。

主代理首轮实际mod42-wave2因可丢弃新地图已有另一具符合条件尸体而失败，是夹具唯一性控制失败。新版驱动在对齐测试tick后，暂将这些已有 eligible 尸体年龄设0，保留“只有自己尸体eligible”的断言并要求已有尸体始终保持dead/未destroyed；finally先恢复原tick，再按原Age恢复，等价精确恢复原timeOfDeath。不移除/跳过生产过滤或改产品代码；仍等待Root重跑。
