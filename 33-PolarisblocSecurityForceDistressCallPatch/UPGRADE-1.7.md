> 主代理游戏验证：`mod33-rev1248-o`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod33-rev1248-o`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod33-rev1248-m`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod33-rev1248-m`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod33-rev1248-k`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod33-rev1248-k`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 升级记录

目标 `1.7.4977 rev1248`。**已适配/编译，实际Polaris/求救事件回归待运行**。保留原1.5源码/DLL、Common、packageId和发布ID；新独立1.7净化为最小原版流程兼容，About支持1.7。

## API/语义与最小修正

原唯一功能是在 DistressCallUtility.SpawnCorpses 内 SimulateKilledByPawn 后加入 corpse==null continue，防止 Polaris Cartridge 当场复活/销毁尸体后 NRE。当前实际1.7 `Source/RimWorld/Planet/DistressCallUtility.cs:29-33` **已经有同样保护**。真实 Anomaly SitePartWorker_DistressCall_Fleshbeasts:59/60/78仍调用该入口，不是废弃方法。

旧手抄全替换还与当前原版产生无关差异：没有 Fingerspike 特例、没有新 corpse.SetForbidden(true)、找不到位置break而当前是continue。新1.7最小解除自己的整段Harmony替换，让当前原版已有保护和新增行为生效；StaticConstructorOnStartup记录已由原版覆盖。保留公开 SpawnCorpses helper 作为同签名委托原版，避免旧外部调用静默失去入口；不再注册Prefix。没有为了打包而注册无意义补丁。

net10.0/精准 Krafs.Rimworld.Ref1.7.4977-alpha.1，未用Harmony/HugsLib编译模板移除，无直接private访问/无需Publicizer。完整实际类型/成员引用见 api-references.json；helper参数 Map/IEnumerable<Pawn>/IntVec3/int 和原版签名精确相同。无自有Scribe字段/有效Def XML需要迁移，Common原内容保留。

实际已安装上游CoreLAB tmp和SecurityForce tmp各有1.6产物，旧声明不证明不兼容，由root先核基线。路径/sha见dependencies.json。实际SecurityForce PawnKind继承apparelRequired PolarisCartridge；实际Core `Polarisbloc.HediffComp_Cartridge.CompPostPostRemoved`在Dead+有关联Apparel时TryResurrect并DestroyApparel，证据 HediffComp_Cartridge.1.6.cs。选定Armor/BodyFix类也留证，未修改上游或使用桩。

## 验证范围

`bash scripts/dotnet.sh build work/33-PolarisblocSecurityForceDistressCallPatch/1.7/Source/mod.csproj -c Release --no-restore` production0warning/error（build.log），Tests/1.7/Mod33Checks同样0warning/error，旧版字节保留。

Map场景要求真实目标没有旧owner整段Prefix，生成实际上游SFSoldier并确认其实际Cartridge存在，保留此Apparel但去掉吸伤护甲以使模拟死亡触发真实上游复活，真实普通Pawn控制与有效killer共同调用真正DistressCallUtility.SpawnCorpses。要求Cartridge实际消耗，普通尸体真实生成/InnerPawn状态/Map/death time/forbidden保留，SF若最终Dead则尸体正常，若Alive则无残留尸体；输出实际sfDead，清理对象/RNG。不假造缺失Corpse Pawn、不跳过真实SimulateKilledByPawn。因其循环可能复活后再次击杀，本场景的Cartridge路径通过不保证最终触发nullCorpse分支，不能夸称每条分支已覆盖。没有独立自有算法，委托原版不写实现镜像单元测试。尚未实际游戏执行。

运行递归IDs brrainz.harmony（上游依赖运行）、vanya.polarisbloc.corelab.tmp、vanya.polarisbloc.securityforce.tmp；使用实际游戏Mods/rimworld-PolarisblocCore与rimworld-SecurityForce的只读副本，避免同时启用同packageId Workshop变体。保存/重载、真正Anomaly distress site整体生成、多corpse/找不到位置/Fingerspike与特殊护甲、复活后通知/阵营状态待测。原版已覆盖修复目的，不能把本补丁静态启动无红字算作整个任务的成功。

## 实际隔离依赖门禁修正

mod33-rev1248-k 的boot JSON没有两Polaris IDs，console明确no installed mod matches，实际两个game Mods依赖为外部本机目录symlink，隔离Mods未stage；该upstreambaseline exit0不能证明依赖已加载。SFSoldier defName在实际上游XML:102存在。新场景先要求真实运行IDs与已加载kind，避免missing def后继续生成null PawnKind引出NRE，全部Cartridge/尸体断言保留；root须在testing/game/Mods只读stage两个实际依赖目录，再重跑baseline及场景。

## 实际null-corpse时序回归与夹具修正

mod33-rev1248-m已真加载上游，Cartridge消耗与普通尸体控制通过，但SF断言错误要求每个Dead SF都必须有尸体，这与本补丁原本要跳过缺尸体对象的目的相冲突。实际Pawn.Destroy会PassToWorld；ResurrectionUtility.TryResurrect:57仅当原尸体flag=spawned且IsWorldPawn时RemovePawn，unspawned尸体复活留在世界；HealthUtility.SimulateKilledByPawn:387继续while(!Dead)再次杀死；Pawn.Kill明确else-if IsWorldPawn分支调用Corpse.PostCorpseDestroy而不创建corpse。于是Dead+WorldPawn+nullCorpse是合法的真实输入，原版guard应continue并处理下一普通对象。

新检查在实际TryResurrect、Pawn.Kill和SimulateKilledByPawn加只观察不跳过的具体Postfix，要求真实Cartridge成功复活一次、至少两次完整Kill、模拟返回时已是Dead worldpawn无corpse，并保留后续普通corpse/spawn/map/time/forbidden全部控制。finally仅卸载具体观察函数。此修改**增强真实null分支和时序的断言**，不修改生产/vanilla/上游行为，不制造fake null输入。新checks编译后待root实跑；两事件的时序日志会输出到console，现阶段源证据与待实测观察分开记录。
