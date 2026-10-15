> 主代理游戏验证：`mod15-rev1248-h`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod15-rev1248-h`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 升级记录

目标 `1.7.4977 rev1248`。**已适配并编译，实际上游/音频功能仍待验证**。packageId `RunningBugs.MusicPatch`、发布 ID 不变；保留原 Source、1.4、1.6、音乐资源及已有用户内容。

## 实际上游与必要最小变更

本机 `mlie.musicalinstruments`（Workshop2274558815）仍声明到 1.6，LoadFolders 回退 1.6+Assets；旧声明不能证明不兼容，但需先跑上游 1.7 基线。实际 1.6 DLL 及 SHA 在 dependencies.json，以实际 DLL 私有固定副本作为新工程参考，Private=false，不进入发布 Assemblies。递归运行依赖 brrainz.harmony、mlie.musicalinstruments；上游另带 VersionFromManifest.dll，由原 MOD 加载。

重要语义实证：当前上游已内置原补丁所有 12 个 SoundDef、对应 CompProp/Comp_PlayingMusic，并在实际 PerformanceManager.StartPlaying(Pawn,Thing,Thing,bool):240/274 和 StopPlaying(Pawn,Thing):279/282 调用 native component。其 Native Notebook 替换重复 musician 注册，不再用 Dictionary.Add。若原样复制旧版 SoundDefs/XML comps，会重复定义和双重音频；这是实际已安装版本变化，必须处理。

新 1.7 **复用唯一上游播放组件**，不复制旧 Defs/Patches、不新增第二音乐 comp。只保留准确四参数 StartPlaying Postfix：调用原 native Comp_PlayingMusic.StartPlaying(Pawn)，维持本补丁原有“演奏就播放”的无条件行为（上游原生开关需要 RoyaltyInstalled/PlayMusic，本补丁旧版本并不限制这些）。StopPlaying 的上游实际入口已经清除 native notebook/currentPlayer，故不用第二个 Stop Prefix。SoundDefs/素材继续来自已安装上游及原根 Sounds；旧版并未改变。内部 native 类型通过真实程序集反射取得，私有可见性不用 Publicizer；MemberInfo 初始化须成功，不用桩代替。旧存档原补丁 currentPlayer 本就没有序列化，此行为仍不自动续播。

完整静态 API 元数据 `logs/15-MusicalInstrumentsPatch/api-references.json`；反射目标与调用时机单独反编译 `PerformanceManager.1.6.cs`、`upstream.Comp_PlayingMusic.cs`。API: StaticConstructorOnStartup、Harmony显式程序集PatchAll、PerformanceManager 实际 overload、ThingWithComps.AllComps、Pawn/Thing、Reflect Invoke/GetType、Verse.Log，均存在。新 1.7 net10.0，精准 NuGet Krafs.Rimworld.Ref1.7.4977-alpha.1、Lib.Harmony3.0.0-rc.2；移除未使用 HugsLib 模板引用。原 Sound API Sustainer.End/Maintain、SoundInfo.InMap 等由实际上游 native comp 负责，其 PerTick 音频生命周期须实测，不能只看旧接口。

## 构建与测试范围

`bash scripts/dotnet.sh build work/15-MusicalInstrumentsPatch/1.7/Source/mod.csproj -c Release --no-restore` 0 warning/error，build.log。Tests/1.7/Mod15Checks.cs 编译 0 warning/error（checks-build.log），只引用 game/Harmony：对真实已加载 PerformanceManager、已加载 FrameDrum、有效生成 Pawn 的实际 StartPlaying/StopPlaying 调用，检查真实 performance 有无 performer 与 native Notebook/currentPlayer 状态、唯一音乐 comp，不直接调用自有 Postfix。finally 清理生成对象/RNG，未运行游戏。不可脱离游戏 Def/Map 的播放逻辑不造伪单元测试。

root 先验证实际上游基线，再跑场景。headless 只能断言开始/停止播放状态，无法证明有声音。待验证：真实有音频窗口内试听、各乐器/多演奏者、Royalty/无Royalty与上游PlayMusic开关、停止淡出、存档重载、表演工作/娱乐 job 流程。上游场景失败须分析现有旧二进制的具体兼容点，不以 label/无红字宣称通过。
