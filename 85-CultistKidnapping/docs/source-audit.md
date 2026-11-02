# 原版实现核对

核对日期：2026-11-03。实际安装版本 **1.7.4995 rev309**，游戏程序集 `/Data/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll`，SHA-256：`56399b6144d9f1695f9c53d054dc82972f7ab35e68d1a19a7129f2f5860ccd21`。仓库旁旧的 `RW/17/Source` 是 1.7.4977，不能当作当前版本；本次用已有 ILSpy 重新反编译实际程序集到被 Git 忽略的 `.build/vanilla-current/`。原版代码不随 MOD 分发。

| 原版位置 | 核对结果和使用方式 |
| --- | --- |
| `Verse.AI.Group.PsychicRitualToil_SkipAbduction.ApplyOutcome` | 原版随机选择未受 DarkPsychicShock 影响的殖民者/奴隶，SkipTo 后施加 30000 tick 昏迷、仪式记忆和信件。只替换此方法内唯一的 SkipTo 调用为调用原函数并记录精确结果的包装，不复制随机选择和效果逻辑。 |
| `RimWorld.PsychicRitualDef_SkipAbduction.CreateToils` / `LordJob_PsychicRitualRepeating.CreateGraph` | 原版会重复 3–5 轮仪式、间歇防守，最后或中断时转为攻击殖民地。成功折跃后在下一个 tick 清理当前 toil，以同一 Lord 的 SetJob/GotoToil 切换为搬运指挥，保留 questTags/inSignalLeave。 |
| `RimWorld.KidnapAIUtility` / `JobGiver_Kidnap` | 原版绑架要求搬运能力、倒地目标、可预约/到达和离图路线；普通搜索半径与战斗判断会妨碍指定目标的持续搬运，因此仅新增指定目标的 JobGiver。候选同时检查移动、操作、携带容量、精神状态、预约、目标可达性和目标到地图边缘的路线。 |
| `RimWorld.JobDriver_Kidnap` / `JobDriver_TakeAndExitMap` | 直接使用 JobDefOf.Kidnap。保留预约、接近、抱起、原版寻路和 ExitMap，以及受害者恢复清醒后任务失败的规则。 |
| `DutyDefOf.Escort` / `JobGiver_AIDefendEscortee` / `JobGiver_AIFollowEscortee` | 护卫直接采用原版的防卫和跟随行为，以当前搬运者为 focus；不实现另一套战斗 AI。 |
| `Verse.Pawn.ExitMap` / `RimWorld.KidnappedPawnsTracker.Kidnap` | ExitMap 在登记绑架前先把搬运者从 Lord 移除。因此 Prefix 保存搬运指挥引用，Postfix 检查实际已经离图且受害者已进入原版 kidnapped 列表，再标记成功。登记、通知、记忆、任务信号和世界小人后续均只执行原版一次。 |
| `IncidentWorker_PsychicRitualSiege` / `Storyteller.TryFire` | 连袭使用原版 incident 和 worker，明确传入邪教派系、PsychicRitualSiege 分组、SkipAbduction 仪式及当前威胁点数。CanFireNow 早于 worker 的派系解析执行，必须提前指定派系，避免检查无关派系。保留原版资格条件和到达/生成方式；失败每小时重试。 |
| `Pawn_GuestTracker.SetGuestStatus` | 奴隶归玩家派系，并保留原奴隶派系信息；原版 SkipAbduction 会选择殖民地奴隶。原生测试使用真实 Slave 状态，不以普通殖民者替代。 |

Harmony 只在加载时查找目标方法并转换 IL；运行中的选择、护卫、搬运和连袭没有反射。Krafs.Publicizer 精确公开 ApplyOutcome，避免以字符串引用私有补丁目标；其它成员直接使用公开 API。升级后若 SkipTo 调用形状不再唯一，会报告明确错误，须重新核对源码，不静默猜测局部变量编号。

测试不会修改游戏 DLL 或玩家启用列表。无 GPU 的隔离游戏仍会输出 Waylith 自身的关闭期分配警告；检查要求没有新的 ERROR。可验证原版搬运/离图、事件生成及两次真实读档，不等同于所有地形、大型模组组合或图形界面设置窗口的覆盖。

## 1.5 / 1.6 兼容性

三版共用 `Source/`，1.5/1.6 编译为 net472，1.7 编译为 net10.0；每版只加载自己的 DLL。实际编译分别使用 Krafs.Rimworld.Ref 1.5.4409、1.6.4871、1.7.4995-unstable。

本机保留的 `RW/16/Source` 和 `RW/16/IL/output` 显示，1.6 的 SkipAbduction outcome、原版 Kidnap job、Lord 切换和 ExitMap→KidnappedPawnsTracker 流程与所用接口一致。`Verse_AI_Group_PsychicRitualToil_SkipAbduction.il` 的 ApplyOutcome 仍接受 `(PsychicRitual, Pawn)`，只有一次返回 Thing 的 `SkipUtility.SkipTo(Thing, IntVec3, Map)` 调用，符合补丁锚点。

1.5 通过其 1.5.4409 参考包核对私有目标签名和全部 API，并成功编译。参考包只有 API、没有可执行方法体；本机没有完整 1.5 游戏，未声称验证过其运行时 IL 或原生游戏行为。公开源码检索未取得可核对的 1.5 方法体。运行时补丁仍会明确检查唯一 SkipTo 调用，接口若不符会报错。

唯一条件编译是 1.5 缺少 Map.CanEverExit（该属性在 1.6 出现），因此此版不做该属性检查，实际地图边缘寻路仍适用。三版统一使用 Unity Mathf.Clamp，兼容旧版 .NET Framework，无反射兼容分支。1.5/1.6 只有参考程序集和源码核对，本机没有可用游戏启动器，未执行原生回归。
