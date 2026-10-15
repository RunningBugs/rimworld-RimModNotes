---
status: current
updated: 2026-10-15
source: changes/louder-generated-voice
---
# Rise and Grind 爬起来肝

- 包 ID：`RunningBugs.RiseAndGrind`，依赖 Harmony；1.6/net472 与 1.7/net10.0 共用源码、分别加载 DLL。
- v1.0.2：`Need_Rest.NeedInterval` Prefix 保留触发前资格和原任务；Postfix 仅处理新建的 `LayDown` 且 `startInvoluntarySleep=true`，直接 `EndCurrentJob(InterruptForced)`，由原版选择下一项任务。不调用征召 setter，不额外清队列、清优先工作、重置自由开火或通知跟随动物；不设置防睡眠计时。
- 可控且可征召、能主动睡眠的正常殖民者才触发；不补充休息值。正常睡觉、伤病倒地、精神状态、婴儿及已征召者不处理。下一项任务仍由原版作息、需求和优先级决定。
- 原版喂婴儿收尾可能把强制睡眠排队；这类延后睡眠不接管。
- `enableVoice` 默认 true 并由 ModSettings 保存；关闭只影响音效。声音仅在当前可见地图播放，同槽不叠加。
- 内置语音音量 100%，`context=Any` 跟随界面音量和主音量。自动播放仍传入小人地图作为限制；模组选项试听不限制地图，可在主菜单使用，且不受自动播放开关影响。仅有当前语言已配置声音时显示试听按钮；当前产品只含简繁中文声音，英文默认静默。按钮、消息、简介文案中英翻译与配音范围相互独立。
- 每次成功唤醒显示 `RAG_ReturnToWork` 左上角消息，包含姓名并绑定小人，使用无提示音的 `SilentInput`。中英文分别为「XXX决定爬起来继续工作。」与「XXX has decided to get back up and keep working.」；简繁中文都有翻译，关闭语音不影响消息。原版疲劳倒地通知仍保留。
- `RAG_WakeVoices` 使用规范语言名（`LoadedLanguage.LegacyFolderName`）映射 SoundDef；无匹配即静默。默认仅简繁中文有语音，允许 XML Patch 添加其他语言。
- 中文为原创 AI 合成普通话，未使用原梗录音或真人克隆。用户接受当前语气，继续使用现有急促训斥的生成声音，不再寻找网络原声或重新生成。文件级压缩／限幅使 RMS 从 -18.66635 提高到 -11.78779 dBFS（+6.87856 dB），峰值限制约 -1 dBFS，无硬削波。62583 样本、24kHz、2.607625秒、语速音高台词不变。放大前版本保存在 `tools/audio/previous/WakeUpChinese-commanding-unamplified.wav`。来源、许可和处理参数见 `tools/audio/`。
- 放大后 WAV 已通过实际 Waylith/MiniAudio 解码、完整原生 voice 数据渲染及离线 ASR 原文核对，见 `tools/audio/loudness_mastering.json` 与 `tools/audio/audio_verification.json`。本次只改声音资产和生成脚本，不改程序集、游戏主音量或系统音量。
- About 模组介绍采用一段中文、空行后一段英文，英中混合显示名。简介只写玩家可感知的玩法、消息、音效与选项；实现细节放开发文档。
- 1.6、1.7 编译通过。当前 1.7 三种配置通过，检查实际 Sample、62583 个样本、24kHz、2.607625秒、非零源音量和 isPlaying，并验证原唤醒/消息行为，见 `Tests/results.json`。独立 Waylith/MiniAudio 后端还验证了 WAV 与临时 OGG 解码、原生 voice 数据渲染均正常，见 `Tests/audio-backend-comparison.json`，因此继续使用 WAV。headless/native tap 不能验证用户耳机最终听感，旧版静音原因尚未实证；1.6 未实机运行。
- 安装使用游戏 Mods 下 `83-RiseAndGrind` 软链接；用户存档和启用列表未改动。

使用、扩展和构建说明以 [README](../../README.md) 为准；可运行集成检查位于 [Tests](../../Tests/README.md)。
