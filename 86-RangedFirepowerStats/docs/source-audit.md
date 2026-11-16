# 射击计算源码审计

审计日期：2026-11-12。机制以用户指定的 `../../RW/17/Source` 为起点，并反编译当前实际安装的 DLL 核验关键方法。所有反编译副本保留在本项目忽略的 `.build/audit/`，不把游戏源码分发进新 MOD。

## 基线

- 当前游戏：`/Data/SteamLibrary/steamapps/common/RimWorld`，`1.7.5005 rev1082`。
- 当前游戏 `Assembly-CSharp.dll` SHA-256：`82387f78262461e53bb06a67c3945912b876d1fdb07235122bbb6e59d073f125`。
- 本地源码快照：`RW/17` 的 `1.7.4977 rev1248`。当前 DLL 的 `Verse.Verb`、`Verse.ShotReport`、`RimWorld.StatWorker` 与快照逐文件比较，除反编译器末尾提示外相同。连续射击的更新间隔另外用当前游戏实际进程验证。
- 原 MOD：工坊目录 `2887818980`，packageId `Mlie.RangedDPS`，About 版本 `1.7.0`。实际 `1.7/Assemblies/RangedDPS.dll` SHA-256：`d3c80f89bfa3399923a97dbfc2dd4bb416c2b81fd79e348dcdfb99e3699b1499`。上游：[emipa606/RangedDPS](https://github.com/emipa606/RangedDPS)。公式审计采用本机 DLL，避免上游后续变化混入结论。
- 使用预装 `ilspycmd` 只读反编译；开发/构建/测试产生的文件全部放在本项目。新实现独立编写，不依赖或覆盖原 MOD。

## 原 MOD 的具体问题

`RangedDPS.StatUtilities.RangedWeaponStats.getFullCycleTime` 已经计算 `warmup × AimingDelayFactor + cooldown + (burstShotCount−1) × ticksBetweenBurstShots/60`。**没有漏掉整个连发过程。**但发数、间隔和预热读的是定义，而不是运行中的实际 Verb；冷却不含射手 `RangedCooldownFactor`，也未按引擎 tick 取整与最小间隔处理。

`RangedWeaponStats.FindOptimalRange` 使用 `Enumerable.Range(start, count)`，其中 count 被赋值为 `floor(MaxRange)`，所以 start>1 时会搜到 max 之外。`StatWorker_RangedWeaponDPS` 的武器摘要另取四段精度最大值，未排除枪械实际到不了的距离。

`StatWorker_TurretShooterDPS` 等把最慢/最快 DPS 作算术平均。随机周期的长期速率应为 `每周期伤害 / 平均周期`，不能把倒数先求出再直接平均。炮塔实际还有 15 tick 索敌扫描，新 MOD 对炮塔明确报告持续潜力并注明这一额外等待未计入。

`TurretStats.DamagePerFuel` 使用 `补充倍率 × 单发伤害`，只有每发恰好消耗 1 fuel 时正确；新实现同时读取每发、每梭燃料消耗。

原 MOD 的主伤害已经使用 `ProjectileProperties.GetDamageAmount(weapon)`；武器精度也调用 `VerbProperties.GetHitChanceFactor`。不能笼统说它没有考虑品质或伤害倍率。

## 原版依据

以下路径均相对 `RW/17/Source/`。

| 文件 / 方法 | 与实现的关系 |
| --- | --- |
| `Verse/Verb.cs` — `TicksBetweenBurstShots`、`BurstShotCount` | 武器特性修正连发间隔/数量，分别 Round/Ceil，不能只用 XML |
| `Verse/Verb.cs` — `TryStartCastOn` | `WarmupTime × AimingDelayFactor` 经 SecondsToTicks，生成预热姿态 |
| `Verse/Verb.cs` — `WarmupComplete`、`VerbTick`、`TryCastNextBurstShot` | 预热结束立刻第一发，剩余计数逐 tick 递减；最后一发设置实际冷却 |
| `Verse/Verb_LaunchProjectile.cs` — `WarmupTime`、`Projectile` | 实例武器预热倍率，当前可切换弹丸 |
| `Verse/VerbProperties.cs` — `AdjustedCooldown`、`AdjustedCooldownTicks` | 武器冷却 × 射手远程冷却倍率，再取整 |
| `Verse/GenTicks.cs` — `SecondsToTicks`、`GetCameraUpdateRate` | 每秒 60 tick，Mathf.RoundToInt；画面内缩放级别+1，画面外15 |
| `Verse/Stance_Busy.cs` — `StanceTick` | ticksLeft≤0 仍在下一次姿态更新才结束；有预热而取整为0时仍不能无限快射击 |
| `Verse/Pawn.cs` — `Tick`、`TickInterval` | 姿态先于装备 Verb；工作间隔在 Thing 的间隔更新阶段运行 |
| `Verse/Thing.cs` — `DoTick` | 常规 Tick 后才运行 TickInterval，间隔限制1–15 |
| `Verse/AI/JobDriver_AttackStatic.cs` — `MakeNewToils` | 下一梭在 `tickIntervalAction` 中 TryStartAttack，因此完整周期还要对检查间隔向上对齐 |
| `Verse/ShotReport.cs` — `HitFactorFromShooter` | 每格精度的距离幂、近中远段射手倍率和2.01%下限 |
| `Verse/ShotReport.cs` — `HitReportFor`、`TotalEstimatedHitChance` | 武器、天气、屋顶、烟雾、掩体、姿势、体型和明暗偏移；真实目标行直接调用 |
| `Verse/VerbProperties.cs` — `AdjustedRange` | 当前天气射程上限和特殊武器忽略精度惩罚，再应用武器射程倍率 |
| `RimWorld/WeatherManager.cs` — `CurWeatherAccuracyMultiplier` | 使用过渡中的实际天气倍率，不只读天气 Def 的最终值 |
| `Verse/ProjectileProperties.cs` — `GetDamageAmount` | 实例伤害倍率及取整；输出为护甲前主伤害 |
| `RimWorld/Building_TurretGun.cs` — `Tick`、`TryStartShootSomething`、`BurstCooldownTime` | 独立炮塔预热/冷却与15 tick目标扫描；不套用射手专家倍率 |
| `RimWorld/StatWorker.cs` — `GetBaseValueFor`、`GetValueUnfinalized`、`FinalizeValue` | 属性偏移先于倍率、精度曲线、年龄及最终下限；假想情景必须在正确阶段加减 |

实际 XML 定义来自游戏 `Data`：

- `Ideology/Defs/PreceptDefs/Precepts_Role.xml` 的 `IdeoRole_ShootingSpecialist`：精度原始值 `+7`，瞄准时间 `−0.5`。
- `Core/Defs/TraitDefs/Traits_Spectrum.xml` 的 `ShootingAccuracy`，degree `−1`（乱开枪）：精度原始值 `−5`，瞄准时间 `−0.5`。
- `Core/Defs/Stats/Stats_Pawns_Combat.xml`：`ShootingAccuracyPawn` 的非线性后处理曲线；`AimingDelayFactor.minValue = 0.01`。

这些值只用于说明核对结果；生产代码读取实际已加载的 Def，不把 +7/−5/−0.5 固定在情景计算里。

## 实测边界与口径

发布实现使用 `ProfileStats.PreviewWorker` 的独立实例推演职位、特性和换枪偏移，并使用局部 `ShotReport` 值副本生成预览。没有为 `StatWorker.GetBaseValueFor`、`GetValueUnfinalized` 或 `FinalizeValue` 安装补丁。生产 Harmony 入口只负责新增信息卡、Gizmo 和图表链接，未扫描、隐藏或修改其他模组的属性。测试中对现有模组的属性可见性与查询前后数值逐项核对。

原版正常三发、10 tick 配置间隔：`[61,70,80]`，实际梭内耗时19 tick。配置零间隔、正的极短预热：`[81,81,82]`，头两发可以同 tick，后续最多每 tick一发。因此“所有相邻子弹至少相隔1 tick”并非对首对永远成立。

三次实际 `AttackStatic` 连续射击的首发为 `[156,351,546]`。在无画面/屏幕外15 tick检查的条件下，完整周期195 tick。纯预热+连发+冷却为181 tick，差值14来自等待下一次工作检查。新公式计入这一等待，并在窗口显示。

没有声称模拟护甲、身体部位、误打、额外伤害、多目标爆炸或任意自定义战斗 MOD。逐格表是明确条件下的标准目标期望；真实目标行是原版报告估计，不是逐弹弹道仿真。原 MOD 的炮塔随机端点显示替换为长期平均潜力，不将搬运弹药与索敌等待伪装进精确值。
