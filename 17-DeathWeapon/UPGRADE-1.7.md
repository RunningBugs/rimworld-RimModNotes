> 主代理游戏验证：`mod17-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod17-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod17-rev1248-a`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod17-rev1248-a`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# Death Weapon：1.7 beta 升级记录

状态：已适配、生产编译及反射helper独立单元测试通过；真实近战/弹丸死亡断言代码编译通过，游戏执行待主代理。只修改work完整副本。

当前复核基线1.7.4977 rev1248（初次静态审查rev926正文不变），packageId RunningBugs.DeathWeapon，发布ID3142744261。旧1.4/1.5/1.6和Common逐字节保留，保持“直接击杀受到攻击的单位、斩断不死”功能与配方/平衡数值。

## 最小修改

新增独立1.7树，net10.0，精确Krafs.Rimworld.Ref 1.7.4977-alpha.1、Lib.Harmony 3.0.0-rc.2，输出同名DeathWeapon.dll且不复制引用。删除1.7未用HugsLib模板引用（所有源码无HugsLib调用，元数据无该依赖），启动显式PatchAll(typeof(Start).Assembly)。初始版没有HarmonyPatch标注，实际武器入口是XML DamageWorker与弹丸继承；验证发现重入后新增下述精确保护；保留原Harmony运行依赖和启动惯例，不把无patch owner当错误。

仅在1.7 DeathUtility.Kill完整成功路径末尾补pawn.prevMap=null，对齐当前原版Pawn.Kill死亡通知完成后释放临时地图状态（RW/17/Source/Verse/Pawn.cs:2332）。不改变通知期间的prevMap、不改变强制SetDead或整体死亡流程、不调用可被别的MOD改写的Pawn.Kill代替本MOD原有强制流程。其他改动只有版本声明。默认加载1.7+Common+根，无显式LoadFolders，旧DLL不并行加载。所有直接游戏字段均公开并编译通过；三个私有方法由反射调用，不需要Publicizer。

## 入口、全部可发现API与语义

完整静态成员/类型引用含泛型原始签名保存在 logs/17-DeathWeapon/api-references.json。没有transpiler；私有反射目标如下逐项核验：

| 目标 | 当前实际签名/含义 |
| --- | --- |
| Pawn.DoKillSideEffects | private void(DamageInfo?,Hediff,bool spawned)，死亡适应/音效、击杀记录、装备与杀戮需求、Tale/BattleLog；RW/17/Source/Verse/Pawn.cs:2004 |
| Pawn.PreDeathPawnModifications | private void(DamageInfo?,Map)，清手术账单、通知hediffs/衣物/关系/connections/近战verbs；Pawn.cs:2045 |
| Pawn.DropBeforeDying | private void(DamageInfo?,ref Map,ref bool spawned)，处理被搬运Pawn与死亡想法；object[]调用后读取ref更新；Pawn.cs:2058 |

这些名称当前均唯一；保留BindingFlags Public|NonPublic|Instance及完整实参顺序，未凭编译忽略反射。自有未使用的DeathUtility.GetValue泛型辅助没有实际调用；两个CallMethod及HarmonyHelper.GetFieldOrProperty的数据类型/异常行为保持。后者和公开CallMethod通过独立生产代码单元测试。

近战：Common DamageDef DeathWeapon_DeathDamage.workerClass=DeathWeapon.DeathDamageWorker；Maneuver DeathTouch+ToolCapacity链接到该DamageDef，武器tools使用该capacity；DeathDamageWorker.Apply(DamageInfo,Thing) override签名保持，先base.Apply，再仅对Pawn执行强制死亡。非Pawn仍走原base行为。

远程：Gun_DeathRifle的默认弹丸Bullet_DeathBullet.thingClass=DeathWeapon.DeathBullet，弹丸damageDef为原版Bullet。DeathBullet.Impact(Thing,bool) protected override仍由Projectile碰撞/拦截路径调用（RW/17/Source/Verse/Projectile.cs:559；RimWorld/Bullet.cs:12），先调用base Bullet.Impact，再构造DamageInfo并强制杀死实际hitThing Pawn。DamageInfo、ArmorPenetration、ExactRotation.eulerAngles、launcher/equipmentDef/intendedTarget接口保持。未改旧blockedByShield=false意图；拦截路径若hitThing=null则没有Pawn，不能据此声称穿透任意护盾/第三方弹丸行为。

DeathUtility是手工复制的死亡流程，不是单行Pawn.Kill：保持health.isBeingKilled/SetDead、PositionHeld/Map/MapHeld/prevMap、GetLord/Caravan、guilt/ownership/bed、封闭容器、狩猎/屠宰/仪式判断、日志、死亡效应器、caravan/lord/Anomaly、装备物品掉落、尸体创建/保存/腐烂、渲染与动画缓存、ThingComp.Notify_Killed、健康/基因/变异/势力/场景/殖民者/任务/理念/机械师/boss/creepjoiner等通知。所有静态调用及字段记录在完整audit，当前签名可编译；真实普通Pawn和Deathless Pawn流程必须运行检查，不把这些通知都自动称为覆盖通过。

ParentHolder现在可以是Map，当前InContainerEnclosed经ThingOwnerUtility.IsEnclosingContainer明确排除Map/Pawn_CarryTracker/Corpse/Caravan/TradeShip等（Pawn.cs:878；ThingOwnerUtility.cs:192），故地面Pawn不会因新Map owner被误判封闭容器。尸体InnerPawn所有权与Map生成物品所有权需实际验证。

手抄流程还与当前原版有差异：未调用完整RemoveFromHoldingContainer的CompTransporter分支、掉落早于尸体放置、未显式在DeSpawn前通知冥想缓存、尸体只尝试Direct而无Near后备，以及额外RevenantTrailer特殊名。这些差异在现有RW/16参考原版也已可见，不凭目录名称判作新1.7变化；本次不进行整段重构或改变原有容器/特殊生物策略，相关场景仍保留限制。成功后的prevMap释放是明确对齐当前完成状态的小修正。

Common武器/自定义DamageDef/ToolCapacity/Maneuver/LogEntry/分类/配方数据、贴图和翻译全部保留，XML均解析通过。shared/api-changes已确认DefInjected仍按实际字段读取，因此旧verbs.label路径和ThingDefs复数目录不机械修改。空Patch及模板success_letter保持；运行加载/继承Def/资产呈现尚须检查。

## 验证与可执行场景

生产工程及Tests/1.7/checks.csproj均0warnings/0errors。Tests/Unit/Unit.csproj实际执行生产HarmonyHelper：private field、property fallback、private方法/ref参数回写、显式父类型调用、missing member异常五组断言通过；没有复刻实现。未使用的反射辅助和游戏依赖Logger/死亡函数不在普通.NET环境伪装执行。

```bash
bash scripts/dotnet.sh restore work/17-DeathWeapon/1.7/Source/mod.csproj --configfile .nuget/nuget.config
bash scripts/dotnet.sh build work/17-DeathWeapon/1.7/Source/mod.csproj --no-restore -c Release
bash scripts/dotnet.sh run --project work/17-DeathWeapon/Tests/Unit/Unit.csproj --no-restore -c Release
bash scripts/dotnet.sh build work/17-DeathWeapon/Tests/1.7/checks.csproj --no-restore -c Release
```

Tests/1.7/Mod17Checks.cs供主代理合并Run(Map)：确认实际XML worker/type链接；生成一次性敌对Pawn（Biotech启用时真正添加并确认Deathless基因），通过实际Thing.TakeDamage进入自定义DamageWorker，断言死亡/Destroyed、isBeingKilled=false、prevMap=null、尸体已生成且InnerPawn对应；非Pawn用相同DamageDef验证不会被直接强制杀死；再真实创建DeathBullet并Launch，通过实际protected Impact入口触发base Bullet+强制死亡，再断言完整尸体/状态。清理创建对象并恢复随机状态。没有直接调用DeathUtility.Kill作为唯一成功证据，也没有模拟DamageWorker/Impact。

该场景已编译未运行；完整近战命中率、枪械射击/飞行/护盾、caravan/封闭容器/transporters、非人型/Anomaly/第三方不死生物及各通知分支、资产、存档重载仍未测。手抄流程catch会把死亡阶段异常记录并吞掉，所以真实测试必须同时核对日志、死亡状态和尸体，不能只看进程0或“没爆红”。

生产DLL SHA256=070617cd576fa9da0a27e7d70e6f36b47f09f48405c7394ea05e7c621de8bff5；静态成员224、类型149；日志目录logs/17-DeathWeapon/。

## rev1248真实验证发现的既有死亡/Deathless重入缺陷

`testing/cases/mod17-rev1248-a`的真实TakeDamage先在DeathDamageWorker.Apply中执行完整Kill并RemoveComponentsOnKilled，mindState被清除；外层Thing.TakeDamage:1540随后仍调用Pawn.PostApplyDamage。Pawn_HealthTracker.PostApplyDamage:392在已Dead且Deathless时调用ForceDeathrestOrComa/MakeDowned，尝试再次下倒已死Pawn，造成InAggroMentalState/IsGuilty及InMentalState NRE。两条错误栈均位于Downed思绪/下倒阶段，不是原Kill的Died思绪，也不是新生Pawn缺字段。正文与rev926一致，属于真实测试揭示的既有重入缺陷。

仅1.7新增PreventDeathrestAfterDeath Prefix，目标Pawn_HealthTracker.PostApplyDamage(DamageInfo,float)：仅health已Dead且DamageDef.defName为DeathWeapon_DeathDamage才返回false，避免完成死亡后的死亡休眠重入；普通DamageDef或活Pawn仍返回true。完整DeathUtility.Kill的死亡思绪、尸体、通知全部保留，未吞异常或删除死亡分支。没有新增Private访问/Publicizer。

场景增加实际初始health/mindState/guilt有效性、真实launcher作为近战伤害instigator；保留原两条TakeDamage和Launch→Impact真实链、Deathless与尸体所有权/健康状态断言。另反射自有Prefix验证两条非匹配控制（活Pawn+本DamageDef、死Pawn+普通Bullet均必须true）以及匹配完成死亡时false。生产/场景重编译0warnings/0errors，原五组helper单元断言再次通过；等待新case复测，不把此编译当运行通过。

当前修复DLL SHA256=20a9db948941d9e5871ea70fd30da248060726fedd748695697d5b156bdc42b4；当前audit成员226、类型150，之前hash为历史产物。
