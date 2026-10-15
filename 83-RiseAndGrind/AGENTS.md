# Rise and Grind 爬起来肝

## 项目与目录对齐

- 本文件只适用于本 Mod。当前规格见 [docs/specs/wake-on-exhaustion.md](docs/specs/wake-on-exhaustion.md)，以代码为最终依据；`docs/changes/` 是未完成工作，`docs/specs/archive/` 是历史实现。
- 真实目录：`/home/lisanhu/mine/workspace/rimworld/RimModNotes/83-RiseAndGrind`。
- 游戏安装必须使用软链接：`/Data/SteamLibrary/steamapps/common/RimWorld/Mods/83-RiseAndGrind` → 上述真实目录。交付前确认链接及其可见文件正确；不要复制实体目录到游戏 Mods。
- 显示名为 `Rise and Grind 爬起来肝`，包 ID 为 `RunningBugs.RiseAndGrind`，依赖 Harmony。英文名对应“起来继续肝”，中文保留“干／肝”谐音。
- 支持 1.6、1.7；当前程序集版本为 1.0.2。两版共用 `Source/`，通过 `LoadFolders.xml` 加载各自的 `1.6/Assemblies/`、`1.7/Assemblies/`。
- `About/About.xml` 面向玩家：先一段中文，空行后完整英文介绍，显示名中英混合。只写玩法效果、消息、音效和选项；调用原理、征召状态、任务树等放开发文档。
- 仅改本 Mod 的必要文件；保留仓库其他已有改动，不修改玩家存档、启用列表或系统环境。

## 当前功能与实现边界

- 核心补丁在 `Source/RiseAndGrind.cs` 的 `WakeOnExhaustion`，目标为 `Need_Rest.NeedInterval`。
- Prefix 保存原资格和原 Job；Postfix 只处理刚创建的 `LayDown` 且 `startInvoluntarySleep=true`，调用 `pawn.jobs.EndCurrentJob(JobCondition.InterruptForced)` 起身。
- 不再调用 `drafter.Drafted=true/false`。完整征召流程会清队列、重置自由开火、通知跟随动物等；本 Mod 不额外执行这些副作用。征召按钮音效由 UI 点击流程播放，并非 setter 自带。
- 不补休息值、不改作息与工作优先级。原版重新选任务，工作时段通常继续工作，睡觉或自由时段可能重新睡觉；后续仍可能疲劳睡倒。
- 正常睡眠、伤病／麻醉倒地、婴儿、精神状态、已征召、敌人和囚犯不处理。保留 `CanWake` 和原版任务可中断性检查。
- 已知边界：喂婴儿的原版收尾流程可能先安置婴儿，再执行排队的疲劳睡眠；这种延后睡眠不接管。
- 起身消息用 `RAG_ReturnToWork`，带姓名并绑定小人；使用 `MessageTypeDefOf.SilentInput`，自身没有提示音。语音关闭后消息照常显示，原版疲劳倒地通知及其声音仍保留。

## 容易回归的游戏逻辑

- `startInvoluntarySleep` 会在躺下后第一次真正睡眠的 tick 被清除，不能靠普通周期扫描这个标志判断所有睡倒；现有 Postfix 的时机有意义。
- 原版疲劳倒地可能使精神状态恢复。必须在 Prefix 保存资格，不能只在 Postfix 判断精神状态，否则可能叫醒原本应排除的小人。
- Job 使用对象池：结束任务后，新任务可能复用同一对象。不要仅凭 Job 引用是否变化判断起身成功；当前实现还检查强制睡眠标志及征召状态。
- 不要为“防止马上睡回去”新增 `lastDisturbanceTick` 设置；它会额外禁止约 400 tick 内入睡，改变原有语义。
- `Messages.Message` 的最后一个 bool 是 `historical`，不是静音开关；`NeutralEvent` 有提示音，静音消息应选 `SilentInput`。

## 语言与声音约定

- 文字提供简体中文、繁体中文、英文；**目前只内置一段普通话声音，简繁共用**。英文默认静默、没有试听按钮，不回退中文。双语文字不等于双语配音。
- `RAG_WakeVoices` 自定义 Def 维护语言到 SoundDef 的映射，允许其他 Mod 通过 XML Patch 扩展；例子见 [README](README.md)。测试中的 English Patch 复用中文 WAV，只是扩展夹具，不是产品英文配音。
- 用 `LoadedLanguage.LegacyFolderName` 查声音，例如 `ChineseSimplified`；实际 `folderName` 可能是 `ChineseSimplified (简体中文)`，直接比较会漏匹配。
- `enableVoice` 默认 true，只控制自动语音。试听按钮共用 `WakeVoicesDef.CurrentVoice`，不受开关影响；仅当前语言有配置时显示。
- 自动语音传入小人地图，限定当前可见地图。试听可在主菜单使用。内置 SoundDef 为 `context=Any`、`onCamera=true`、`volumeRange=100`、`slot=RAG_WakeVoice`；受游戏主音量和界面音量控制，同槽播放期间不叠加。
- 替换 DLL、Defs 或已加载的音频后，用户游戏需要重新启动才能可靠加载新内容；不要把磁盘文件已更新等同于运行中的游戏已更新。

## 当前音效与处理经验

- 正式文件：`Sounds/RiseAndGrind/WakeUpChinese.wav`。用户已接受当前语气，继续使用自有生成音效；没有新要求时，不再重制或替换成网络原声。
- 模型为 Qwen3-TTS-12Hz-1.7B-CustomVoice，官方预设 `Uncle_Fu`；没有使用原梗录音、参考录音或真人声音克隆。模型版本、来源和许可留在 `tools/audio/`。网络素材可下载不代表有随 Mod 分发的许可。
- 语音为 24 kHz、单声道 PCM16、62583 样本、2.607625 秒。声音、语速、音高和台词保持现状，只做文件级音量处理。
- 最终 RMS 为约 -11.79 dBFS，比放大前 -18.67 dBFS 提高约 6.9 dB；峰值约 -1 dBFS，无硬削波。只做峰值归一化最多多出约 2 dB，不能充分解决平均响度过小；当前采用压缩加限幅。
- 滤镜、前后测量和原生后端验证见 [loudness_mastering.json](tools/audio/loudness_mastering.json)，当前文件元数据见 [WakeUpChinese.json](tools/audio/WakeUpChinese.json)，台词核对见 [audio_verification.json](tools/audio/audio_verification.json)。校验值以这些记录为准，避免在多处复制后过期。
- 放大前备份为 `tools/audio/previous/WakeUpChinese-commanding-unamplified.wav`，最初慢版本也保留在 `previous/`。需要重新调音时从未放大的备份开始，不要对当前成品反复叠加增益。
- 生成脚本 `tools/audio/generate_voice.py` 已包含最终压缩／限幅流程，需要 FFmpeg。用户目前只要求调音量时，不要重新调用 TTS。
- 实际 1.7 Waylith/MiniAudio 已验证 WAV 与临时 OGG 都能解码和渲染，不要未经验证把“声音太小／没听到”归因于 WAV 格式。当前继续使用 WAV。

## 构建与验证

使用项目局部环境和缓存，安装依赖不得使用系统 pip、`pip --user` 或全局安装。Python 检查使用 `Tests/.venv/bin/python`；TTS 依赖使用其专用 `.venv`。可只读复用既有专用环境，不修改其他项目环境。

在 Mod 根目录顺序构建，不要并行：两个版本共用 `Source/obj`。

```bash
/home/lisanhu/.dotnet/dotnet build Source/RiseAndGrind.csproj -c Release -p:RimWorldVersion=1.6
/home/lisanhu/.dotnet/dotnet build Source/RiseAndGrind.csproj -c Release -p:RimWorldVersion=1.7
```

- 1.6：`net472`，固定 `Krafs.Rimworld.Ref 1.6.4871`；参考源码 `~/mine/workspace/rimworld/RW/16/Source/`。
- 1.7：`net10.0`，直接引用本机实际游戏程序集；参考源码／固定测试快照在 `~/mine/workspace/rimworld/RW/17/`。本轮基线为 `1.7.4977 rev1248`，游戏更新后须核对快照与测试基线。
- Harmony 引用安装目录中对应版本的 DLL；不要把 Harmony、游戏 DLL 或旧 Unity 引用复制进产品程序集目录。NuGet 依赖在 `.build/packages`。
- C# 修改后分别更新两版 DLL，不要把同一 DLL 复制两份。仅修改 XML、翻译或音频不要求重编译 DLL；按修改内容做相应检查。
- 集成入口为 `Tests/run_game.py`，复跑命令见 [Tests/README.md](Tests/README.md)。使用独立 apphost、SaveData、XDG 和临时目录，不读取或修改玩家存档与启用列表。不要关闭用户正在运行的游戏；安排游戏集成检查前先确认运行状态。
- 声音检查不能只观察 `PlayOneShotOnCamera` 调用：还要检查实际 `SampleOneShot`、非空 clip、样本数／采样率／时长、非零源音量；诊断可记录 `isPlaying`。同时验证禁用语音和无语言映射时没有 `RAG_` 声音源。
- headless 与 null backend 不是耳机听感验证；原生 voice tap 还位于音量／master／混音增益之前。ASR 正确只证明台词，不证明语气或响度满意。
- 1.6 当前只有编译／API 核验，没有本机可运行副本。1.7 的三种完整集成配置通过，但时间早于最后文件放大；放大后只重跑原生解码／voice 渲染、音量测量和离线 ASR。不要把历史测试哈希或结论套到新资产上。
- 当前证据与产物哈希见 [Tests/results.json](Tests/results.json)。1.7 当前日志在用户数据目录的 `Logs/Player.log`，根目录 `Player.log` 可能是旧 Unity 日志，排查前先看时间戳。

后续更新同步代码、产物和必要说明；规格通过归档更新，历史实现放 `docs/specs/archive/`。交付时再次确认游戏 Mods 软链接，说明实际检查范围，不为纯文档修改重跑整套游戏测试。
