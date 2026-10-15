> 主代理游戏验证：`mod45-rev1248-h`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod45-rev1248-h`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 beta 升级记录

状态：已适配，逻辑/GPU 可视验证未完成。基线 `1.7.4977 rev1248`；发布 ID `3440272004`；packageId **保持原值 RunningBugs.modname**，没有借迁移改 Workshop 身份。

新增独立1.7工程/TargetLine.dll，net10.0、精准Ref 1.7.4977-alpha.1/Harmony 3.0.0-rc.2。源码仅显式 PatchAll(typeof(Start).Assembly)；实际使用的 Pawn_StanceTracker.pawn/curStance、Stance_Busy.focusTarg 等字段均公开，新工程移除模板 PublicizeAll/Publicizer 和未用 HugsLib（不改旧工程）。About加1.7，旧1.5/1.6/Common/备份逐字节保持，默认加载单份新同名DLL。

完整API/函数审查：启动Harmony；ModSettingsData.ExposeData三项原键/default；ModSettingsUI ctor/GetSettings/SettingsCategory/Listing_Standard checkbox/Translate；StanceTrackerDraw postfix；DrawTargetLine中的Pawn/Faction/GenHostility、Pawn.HostileTo、Stance_Warmup.focusTarg、CurJob与targetA/B/C、SimpleColor；DrawLineBetweenTargets中LocalTargetInfo.IsValid/Thing/Cell、Thing.DrawPos、ToVector3Shifted与GenDraw.DrawLineBetween(Vector3,Vector3,SimpleColor,float)；未使用的LogLocalTargetInfo仅打印有效目标。没有transpiler、生产反射或自有Def。

当前 Pawn_StanceTracker.StanceTrackerDraw仍由绘制链调用并调用当前Stance.StanceDraw，postfix入口有效。已有warmup仅画Pawn→focus；非warmup画Pawn→A→B→C并各自过滤Invalid。非敌对Green/Blue/Cyan，敌对Red/Orange/Magenta；三个启用标志保留。设置的hostile字段继续用原Scribe键showTargetLineForEnemy，不能按字段名机械改存档键。

已阅读rev1248真实GenDraw实现：SimpleColor overload选择材质，再生成变换并Graphics.DrawMesh；它不是26的RenderTexture合成分支，不因共享CPUalpha问题统一重写。当前源码/签名匹配只能证明逻辑调用路径，UnityStubs/Waylith的实际GPU材质、透明度、绘制高度与新UI仍需窗口验证。

restore/Release build通过0 warnings/errors（logs/45-TargetLine/restore.log/build.log）。命令 scripts/dotnet.sh restore work/45-TargetLine/1.7/Source/mod.csproj --configfile .nuget/nuget.config，随后build --no-restore --configuration Release。原/新完整metadata引用api-original.json/api-new.json，静态/XML/旧字节/单DLL证据static-validation.json。最终仅本MOD DLL/deps.json，无复制游戏/Harmony引用。旧原Ref为1.6.4488-beta，旧实现精确构建provenance未核验。

无已有独立测试。坐标/绘制条件依赖真实Pawn/Job/Stance/ModSettings，未拆生产函数写重复算法单测。Tests/1.7/Mod45Checks.cs已精准依赖单独编译0 warnings/errors（checks-build.log），待主代理：调用实际StanceTrackerDraw/实际postfix，通过仅测试GenDraw观察端拦截记录端点与颜色，覆盖友方/敌方3链、global/friendly/hostile disable、真实warmup只1链、invalid中目标截断及无job；实际Settings Scribe roundtrip保留三原键。finally具体unpatch、恢复设置、清理生成Pawn，合成Job只供绘制输入，不启动AI任务，销毁前清掉该临时引用。

该驱动证明实际绘制调用参数和过滤，**不证明GPU像素**，不会调用拦截后的原Graphics.DrawMesh。实际job运行/瞄准、可见线色/层级/跨图/缩放与设置交互、保存重载仍待测。未用空驱动或仅查owner字符串冒充功能验证。
