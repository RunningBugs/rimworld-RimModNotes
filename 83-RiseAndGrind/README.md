# Rise and Grind 爬起来肝

「爬起来干活」的「干」换成熬夜打游戏的「肝」；英文 **Rise and Grind** 也有起床继续拼命干活的意思。

支持 **RimWorld 1.6、1.7**，需要 Harmony。源码共用，程序集分版本加载。真实目录为 `83-RiseAndGrind`，通过同名软链接挂入游戏 Mods 目录。

开发和维护约定、当前决策及排查经验见 [AGENTS.md](AGENTS.md)。

## 行为

殖民者因疲劳随机睡倒时，直接中断当次强制睡眠，由原版 AI 按作息、工作优先级和需求选择下一项任务。工作时段通常继续工作；睡觉或自由时段也可能重新选择睡觉。

- 不补充休息值、不消除疲劳惩罚，之后仍可能再次睡倒。
- 只处理当次刚创建的强制睡眠任务，不打断正常睡眠。
- 只处理已生成、玩家可控、未倒地、未征召、有征召控制器且能主动睡觉的殖民者（包含可控奴隶）。婴儿、敌人、囚犯、精神状态、死亡、伤病或麻醉倒地、死亡休眠不处理。正在着火等原版不允许中断任务的情况也不处理。
- 直接调用原版的当前任务中断接口，不切换征召状态、不额外清空队列和优先工作、不重置自由开火、不通知跟随动物，也不播放征召按钮音效。
- 成功唤醒时在左上角显示「XXX决定爬起来继续工作。」；英文显示「XXX has decided to get back up and keep working.」。消息可定位小人，本身无提示音，关闭语音后仍显示。
- 中文语音默认开启，可在「选项 → 模组选项 → Rise and Grind 爬起来肝」试听或关闭。试听不受自动播放开关影响，也可在主菜单使用。
- 仅当前可见地图上的唤醒播放语音；同一语音槽正在播放时不会叠加。
- 简繁中文使用同一段普通话；其他语言无对应配置时静默，不回退到中文。
- 内置语音使用完整播放音量，并跟随游戏的主音量与界面音量；自动播放仍只限当前可见地图。

当前只内置中文声音，英文界面默认没有声音或试听按钮。按钮文案、起身消息与 Mod 简介保留中英翻译；语言扩展接口不代表已经附带其他语言配音。

已知边界：喂婴儿时，原版可能先安排将婴儿送往安全处，再执行排队的疲劳睡眠。本补丁只在疲劳睡倒当次触发，不接管这种延后睡眠。

## 添加其他语言的语音

扩展 Mod 依赖 `RunningBugs.RiseAndGrind`，并在其后加载。将自己的音频放在扩展 Mod 的 `Sounds/MyVoicePack/WakeUp.ogg`（也可用 `.wav`），定义标准 SoundDef：

```xml
<Defs>
  <SoundDef>
    <defName>MyVoicePack_WakeUpEnglish</defName>
    <context>Any</context>
    <slot>RAG_WakeVoice</slot>
    <subSounds>
      <li>
        <onCamera>true</onCamera>
        <grains>
          <li Class="AudioGrain_Clip"><clipPath>MyVoicePack/WakeUp</clipPath></li>
        </grains>
        <volumeRange>100</volumeRange>
      </li>
    </subSounds>
  </SoundDef>
</Defs>
```

在扩展 Mod 的 `Patches/WakeVoice.xml` 添加映射：

```xml
<Patch>
  <Operation Class="PatchOperationAdd">
    <xpath>Defs/RiseAndGrind.WakeVoicesDef[defName="RAG_WakeVoices"]/voices</xpath>
    <value>
      <li>
        <language>English</language>
        <sound>MyVoicePack_WakeUpEnglish</sound>
      </li>
    </value>
  </Operation>
</Patch>
```

`language` 必须是游戏语言文件夹名，例如 `English`、`Japanese`、`ChineseSimplified`、`ChineseTraditional`。每种语言只留一项；替换已有语音使用 `PatchOperationReplace`，目标为 `.../voices/li[language="ChineseSimplified"]/sound`。扩展不需要编写 C#。

## 中文音效来源与重新生成

内置语音为本地 **Qwen3-TTS-12Hz-1.7B-CustomVoice** 的 `Uncle_Fu` 预设声音合成，未使用原梗录音、参考录音或真人声音克隆。台词为「你们这个年纪是怎么睡得着的！」，重新按急促、强硬的命令式训斥语气生成，重读「这个年纪」和「怎么睡得着」，末尾短促向下收住。

官方[模型页](https://huggingface.co/Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice)和[合成软件](https://github.com/QwenLM/Qwen3-TTS)采用 Apache 2.0 许可；Mod 不分发模型权重。模型版本、提示词、随机种子、校验值和许可原文见 [tools/audio](tools/audio/)。当前成品为 24 kHz 单声道 PCM16 WAV，时长约 2.6 秒（比初版缩短约 26%），峰值 -1 dBFS；没有进行机械加速或变调。音频经过压缩和限幅，平均 RMS 电平由 -18.67 提升至 -11.79 dBFS（约 +6.9 dB），未出现硬削波。旧版及放大前版本保存在 [tools/audio/previous](tools/audio/previous/)，测量与处理记录见 [loudness_mastering.json](tools/audio/loudness_mastering.json)。

需要 NVIDIA CUDA 设备和 FFmpeg；重新生成请使用独立环境（会下载模型和运行依赖）：

```bash
cd tools/audio
uv venv .venv --python 3.12
uv pip install --python .venv/bin/python -r requirements.txt --extra-index-url https://download.pytorch.org/whl/cu128
.venv/bin/python generate_voice.py --output ../../Sounds/RiseAndGrind/WakeUpChinese.wav
```

## 编译

使用 .NET 10 SDK。所有恢复的构建依赖均在本 Mod 的 `.build/packages`，中间文件在项目局部 `Source/obj`。1.6 使用固定版本 `Krafs.Rimworld.Ref 1.6.4871`；1.7 直接引用本机实际游戏程序集，避免将旧 Unity 引用带入新运行时。两版均从已安装的 Harmony 引用对应版本 DLL，不打包 Harmony。

```bash
dotnet build Source/RiseAndGrind.csproj -c Release -p:RimWorldVersion=1.6
dotnet build Source/RiseAndGrind.csproj -c Release -p:RimWorldVersion=1.7
```

非本机路径可覆盖 `-p:RimWorld17Dir=/path/to/RimWorld` 和 `-p:HarmonyDir=/path/to/Harmony`。输出为 `1.6/Assemblies/RiseAndGrind.dll`、`1.7/Assemblies/RiseAndGrind.dll`。

## 验证

1.6 和 1.7 分别编译通过，零警告、零错误。集成检查覆盖真实疲劳触发、直接中断睡眠、无征召调用、自由开火保持、休息值、双语无声消息、音效开关及排除条件。中文、英语默认静默、XML Patch 增加语音映射三种配置均通过；还检查实际创建的声音源、剪辑样本数和非零源音量，无新增 ERROR。扩展夹具复用中文音频，产品没有内置英文配音。结果见下方摘要。

当前 1.7 的实际 Waylith/MiniAudio 后端分别验证过放大前的 WAV 与临时转换的 OGG，两者均能完整解码和产生原生 voice 数据，所以保留 WAV。见 [格式对照结果](Tests/audio-backend-comparison.json)。最后的文件放大没有重跑整套游戏集成，而是重新验证了最终 WAV 的原生渲染、响度和 ASR 台词，见 [音量处理记录](tools/audio/loudness_mastering.json)。这些检查不等同于验证用户耳机的最终输出；试听按钮用于直接核对当前设备的效果。

1.6 本机没有可运行副本，仅做编译和 API 核验；1.7 使用 Headless 音频输出，声音资源及实际声音源已验证，自动化检查不覆盖设置窗口外观或用户设备的最终听感。详见 [测试说明](Tests/README.md) 和 [结果摘要](Tests/results.json)。

## English

When a controllable colonist collapses from exhaustion, this mod directly interrupts that sleep job. Vanilla AI chooses the next job using the existing schedule and work priorities. Drafting state, fire-at-will, and prioritized work are not reset. It does not refill rest or prevent future collapses. Normal sleep, medical incapacity, babies, and mental states are excluded.

A silent message in the top-left reads “XXX has decided to get back up and keep working.” Chinese translations are included. The message still appears when the voice option is disabled.

The optional voice is on by default. Simplified and Traditional Chinese share a Mandarin clip. Languages without an explicit mapping are silent. Translation mods can add standard SoundDefs and patch `RAG_WakeVoices/voices` as shown above, using exact game language folder names. No C# is required.
