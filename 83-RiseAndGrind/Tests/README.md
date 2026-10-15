# 本机实际游戏集成验证

最新资产调整：保留同一段中文 AI 语音，仅对文件做压缩／限幅；RMS -18.66635 → -11.78779 dBFS（+6.87856 dB），峰值约 -1 dBFS，无硬削波。62583 样本、24kHz、2.607625 秒保持一致，程序集未修改。放大后文件重新通过实际 Waylith/MiniAudio 原生 voice 渲染和离线 ASR 核词；见 `tools/audio/loudness_mastering.json`、`tools/audio/audio_verification.json`。下文集成用例记录早于本次文件放大，不能据此宣称已复测耳机输出。

此目录只用于测试，不随 Mod 的 LoadFolders 加载。测试直接引用本机固定的 RimWorld 1.7.4977 rev1248 程序集和 Harmony，不使用伪造游戏 API。

在 Mod 根目录执行（先完成生产 1.7 DLL 编译并提供中文 WAV）：

```bash
cd /home/lisanhu/mine/workspace/rimworld/RimModNotes/83-RiseAndGrind
uv venv Tests/.venv
env DOTNET_CLI_HOME="$PWD/.build/dotnet-tests" \
  NUGET_PACKAGES="$PWD/.build/packages-tests" \
  DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 \
  /home/lisanhu/.dotnet/dotnet build Tests/Integration.csproj -c Release
Tests/.venv/bin/python Tests/run_game.py zh-new --language ChineseSimplified
Tests/.venv/bin/python Tests/run_game.py en-new --language English
Tests/.venv/bin/python Tests/run_game.py en-patch-new --language English --extension
```

Python 启动器仅使用标准库。也可以复用专用环境 `/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/.venv/bin/python`；无需安装 Python 依赖。每次使用新的用例名称。

运行前确认用户未在运行游戏。启动器在 `tmp/rise-and-grind-testing/` 中建立独立 apphost、Mods 布局和各用例的 SaveData、XDG、TMPDIR；不会读取或修改玩家存档和启用列表。测试生成新的 quicktest 地图及一次性 Pawn，退出前清理测试 Pawn。测试 DLL 输出在该临时目录的 `harness/Assemblies/` 中，不进入产品 Assemblies。

`Mod83Checks.cs` 触发真实 `Need_Rest.NeedInterval` 疲劳坍睡，观察 `EndCurrentJob(InterruptForced)` 直接中断这次强制躺下，确认不增加精力、不调用征召开关，并保留 `fireAtWill=false`。测试从原版左上角消息列表读取返回工作消息，验证当前中英文文本、姓名、点击目标绑定，以及消息自身没有提示音；关闭语音仍会唤醒并显示消息。声音观察器还会确认没有征召音效或额外消息音效（原版疲劳坍睡自身的 `Message_NegativeEvent` 保留）。

其余检查覆盖中文语言选择、英语默认静默、XML Patch 新增英语语音引用，以及普通睡眠、麻醉倒地、敌人、精神状态、已征召殖民者的过滤，这些过滤情形不会生成返回工作消息。Patch 夹具复用中文 WAV 验证语言扩展能力，其声音内容不是英语配音。

声音检查同时在 `SampleOneShot.TryMakeAndPlay` 返回后采集真正创建的声音源，要求中文和语言扩展声音的返回值非空、`source.clip.samples/frequency/length` 均大于零、`source.volume` 大于零。英语默认、关闭语音及被过滤的 Pawn 均不得创建任何 `RAG_` Sample。采集立即保存数值，避免声音源池回收后改变证据；控制台的 `RAG_SOURCE_STATE` 包含 SoundDef、解码样本数、采样率、时长、实际源音量、声音上下文、是否 on-camera，以及仅供诊断的 `isPlaying`。

用例的 `result.json`、`SaveData/rag-result.json`、`SaveData/rag-boot.json` 和 `console.log` 保存进程结果、断言、实际加载路径及错误。Headless 使用 null audio output；当前检查能验证播放调用最终创建非零音量、带有解码声音数据的真实源，不能证明扬声器输出或玩家实际听感，也不强制 `isPlaying` 为真。系统混音、输出设备、游戏外部音量和图形设置窗口仍须实机确认。1.6 只有独立编译验证，本机没有对应游戏运行入口。

## 2026-10-15 v1.0.2 真实声音源验证

游戏 `1.7.4977 rev1248`；三个隔离进程全部 exit 0、未超时，各有 10 组检查通过，控制台没有新增 ERROR。证据仍在 `/home/lisanhu/mine/workspace/rimworld/tmp/rise-and-grind-testing/cases/`，汇总和源快照见 `Tests/results.json`。

| 用例 | 时间 | `SampleOneShot.TryMakeAndPlay` 返回后的实际结果 |
| --- | --- | --- |
| `zh-source-v1` | 19.00 秒 | `RAG_WakeUpChinese` 源非空，62583 样本、24000 Hz、2.607625 秒，volume=1，context=Any |
| `en-source-v1` | 16.79 秒 | 没有创建任何 `RAG_` 源 |
| `en-patch-source-v1` | 18.04 秒 | `RAG_TestEnglish` 源非空，62583 样本、24000 Hz、2.607625 秒，volume=0.6，context=MapOnly |

两个有语音用例均记录到 `isPlaying=true`；这只作为诊断证据。扩展夹具保留旧 `MapOnly`/60 音量配置，证明它在默认测试音量下也能够创建非零音量的源，不能据此认定旧上下文在所有玩家环境中必然静音。全部用例同时确认语音关闭后没有创建源，消息仍正常；既有起身行为和过滤检查均通过。

SHA-256：

- 生产 `1.6/Assemblies/RiseAndGrind.dll`：`239c1ce459fc22fd0813272110c614c417e693e8793b9638062d8c6bb1ad3de9`
- 生产 `1.7/Assemblies/RiseAndGrind.dll`：`6830fd5db374ab44d8b7a68a69e2a1e236272489a704747f182d7b9950a0a0cf`
- 独立测试 DLL：`2be076d1a379e0fb9e5f70faeec119614362a42412c4dcbfb8c917621ad4e84e`
- 中文 WAV：`f5e9831143ed3bc7575b56fca7ca17633a01c0d2cd5fe1ed957bd4d1c8474c0b`

本轮没有通过图形界面操作设置中的试听按钮，也没有验证玩家输出设备的实际声音。

## 2026-10-15 v1.0.1 直接中断与消息验证（历史记录）

游戏 `1.7.4977 rev1248`；三个隔离进程全部 exit 0、未超时，各有 9 组检查通过，控制台没有新增 ERROR。证据根目录为 `/home/lisanhu/mine/workspace/rimworld/tmp/rise-and-grind-testing/cases/`，汇总见 `Tests/results.json`。

| 用例 | 时间 | 实际语言与语音结果 |
| --- | --- | --- |
| `zh-direct-v1` | 20.15 秒 | `ChineseSimplified`，调用 `RAG_WakeUpChinese` 一次 |
| `en-direct-v1` | 18.80 秒 | `English`，未调用 Mod 语音 |
| `en-patch-direct-v1` | 18.25 秒 | `English`，XML Patch 新增映射后调用 `RAG_TestEnglish` 一次 |

三次都观测到疲劳躺下被 `EndCurrentJob(InterruptForced)` 中断一次，征召 setter 零调用，精力仍为零，`fireAtWill=false` 保留。真实消息列表中分别出现 `Marat决定爬起来继续工作。` 和 `Marat has decided to get back up and keep working.`，使用无提示音的 `SilentInput`，并绑定对应 Pawn；关闭语音后消息照常出现。只有原版坍睡提示自身的 `Message_NegativeEvent` 声音保留，没有征召音或新增消息提示音。其他过滤检查全部通过。

SHA-256：

- 生产 `1.6/Assemblies/RiseAndGrind.dll`：`2414630cba36cca16f70d6db3e912817a20ff39eab1e1d0da882cc9dd4392730`
- 生产 `1.7/Assemblies/RiseAndGrind.dll`：`b82ddc526e9037f996599f7be0dedccdfa651598a89999e5dc3c57cf83095ad3`
- 独立测试 DLL：`fd409d6e1d395e7c32e4e85a8c77f8e457012337f035d23bd6a1d6a607a0a032`

这些历史用例只观察到语言选声和 `PlayOneShotOnCamera` 入口调用，没有采集返回的 `SampleOneShot`；它们不能单独证明声音源创建成功或实际源音量非零。最终听感须另行试听。

最终中文音频替换为 2.607625 秒的命令式重制版后，另跑 `zh-direct-commanding-v1`：18.94 秒、exit 0、9 组检查通过、无新增 ERROR。加载及播放验证对应 WAV SHA-256 `f5e9831143ed3bc7575b56fca7ca17633a01c0d2cd5fe1ed957bd4d1c8474c0b`，生产 DLL 未再改变。

## 2026-10-15 初版运行记录（历史行为）

游戏 `1.7.4977 rev1248`；以下三个独立进程都 exit 0、未超时、8 组实际游戏检查通过，控制台没有新增 ERROR。证据根目录为 `/home/lisanhu/mine/workspace/rimworld/tmp/rise-and-grind-testing/cases/`。

| 用例 | 时间 | 实际语言与语音结果 |
| --- | --- | --- |
| `zh-v3` | 18.85 秒 | `ChineseSimplified`，调用 `RAG_WakeUpChinese` 一次 |
| `en-v1` | 18.90 秒 | `English`，未调用 Mod 语音 |
| `en-patch-v1` | 19.40 秒 | `English`，XML Patch 新增映射解析后调用 `RAG_TestEnglish` 一次 |

这三次历史记录针对初版征召/取消征召实现，不能作为当前直接中断实现的验证证据。三次均验证了真实坍睡后的征召/取消征召，不增加精力；音效关闭后继续唤醒且静默；普通睡眠、麻醉倒地、敌人、精神状态、已征召 Pawn 的过滤通过。实际加载路径为隔离游戏目录中指向本 Mod 的链接，未加载 Workshop 产品旧版。

SHA-256：

- 生产 `1.7/Assemblies/RiseAndGrind.dll`：`4d1b5534c9c5d3e62b16c2d8fec0d9b8580ee8939b61952fe43243d076982a45`
- 中文 WAV：`70ba8bae8d880c442d280175796727a38567db40d373533d2f8b57b920c2dd94`
- 独立测试 DLL：`0a289086562a47ca653a60911bb35770cac4680c6e2fd2b8b8e8001ab206754d`

先前 `zh-v1`、`zh-v2` 是保留的测试夹具失败证据：声音观察器把原版疲劳通知的 `Message_NegativeEvent` 也计入了 Mod 语音。最终观察器只统计 `RAG_` 声音，不改变原版声音或生产代码。
