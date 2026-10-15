---
status: superseded
superseded_by: ../wake-on-exhaustion.md
updated: 2026-10-15
source: changes/rise-and-grind
---
# Rise and Grind 爬起来肝

- 包 ID：`RunningBugs.RiseAndGrind`，依赖 Harmony；1.6/net472 与 1.7/net10.0 共用源码、分别加载 DLL。
- `Need_Rest.NeedInterval` Prefix 保留触发前资格和原任务；Postfix 仅处理新建的 `LayDown` 且 `startInvoluntarySleep=true`，执行真实征召／取消征召。
- 可控且可征召、能主动睡眠的正常殖民者才触发；不补充休息值。正常睡觉、伤病倒地、精神状态、婴儿及已征召者不处理。下一项任务仍由原版作息、需求和优先级决定。
- 原版喂婴儿收尾可能把强制睡眠排队；这类延后睡眠不接管。
- `enableVoice` 默认 true 并由 ModSettings 保存；关闭只影响音效。声音仅在当前可见地图播放，同槽不叠加。
- `RAG_WakeVoices` 使用规范语言名（`LoadedLanguage.LegacyFolderName`）映射 SoundDef；无匹配即静默。默认仅简繁中文有语音，允许 XML Patch 添加其他语言。
- 中文为原创 AI 合成普通话，未使用原梗录音或真人克隆。来源、许可和生成参数见 `tools/audio/`。
- 1.6、1.7 编译通过。当前 1.7 实际游戏三种语言配置通过，结果见 `Tests/results.json`；1.6 未实机运行，主观听感和设置窗口尚未人工验收。
- 安装使用游戏 Mods 下 `83-RiseAndGrind` 软链接；用户存档和启用列表未改动。

使用、扩展和构建说明以 [README](../../README.md) 为准；可运行集成检查位于 [Tests](../../Tests/README.md)。
