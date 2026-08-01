# RimFlix Anime Shows 动漫电视秀

RimFlix (Continued) 的本地内容扩展包：为游戏内电视提供自定义节目。
无代码，纯 `RimFlix.ShowDef` 定义 + 帧图。

A local content pack for RimFlix (Continued): custom TV shows with zero code —
just `RimFlix.ShowDef` XML and frame images.

## 使用 / Usage

1. 订阅并启用 RimFlix (Continued)（工坊 id `3194639480`）。
2. 本目录软链接到游戏 Mods 文件夹并启用，排在 RimFlix 之后。
3. 游戏里建对应类型的电视，用 RimFlix 的节目菜单选择节目。

## 制作节目 / Make a Show

给一段视频，运行唯一脚本 `tools/make_show.sh`：

```bash
tools/make_show.sh <视频文件>
```

按提示输入节目ID、显示名、电视类型（平板/巨屏/显像管）、帧数、帧间隔、是否 ping-pong 循环，
确认后自动抽帧并生成节目定义。详见 [AGENTS.md](AGENTS.md)。

## 版权 / Copyright

素材仅供本地自用：AI 生成的角色内容**不要上传创意工坊**。
