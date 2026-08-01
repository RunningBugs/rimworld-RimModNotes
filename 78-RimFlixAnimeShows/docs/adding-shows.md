# 添加自己的节目 / Adding Your Own Shows

本文档说明如何在本 Mod 框架内添加自己的图片节目（以碧蓝航线等动漫图为例）。

## 前提

- 本包是 **RimFlix (Continued)**（packageId `zal.rimflix`）的内容扩展（无代码），必须先在游戏中启用 RimFlix 本体，并保证本包排在其后（About.xml 已声明硬依赖，游戏会自动校验）。

## 图片尺寸

每种电视的屏幕区域不同，图片建议按目标电视准备（高度 64/128/256 三档，128 是观感与体积的平衡点）：

| 电视 | 64px | 128px（推荐） | 256px |
| --- | --- | --- | --- |
| Tube 显像管 | 79×64 | 157×128 | 315×256 |
| Flatscreen 平板 | 155×64 | 310×128 | 620×256 |
| Megascreen 巨屏 | 225×64 | 451×128 | 902×256 |

同一套图不要跨电视复用（宽高比不同会变形）。GIF 不能直接用——需要拆成逐帧 PNG
（可用 `ffmpeg -i in.gif out_%02d.png` 或在线工具），`secondsBetweenFrames` 控制播放速度：
0.033 ≈ 30fps 动画，0.2~0.5 适合幻灯。

## 添加步骤

1. 在 `Textures/Shows/` 下新建一个节目目录（如 `AzurLane`），放入帧图，按顺序命名
   （如 `Flat_AzurLane_00.png`、`_01.png`……，texPath 不含扩展名）。
2. 在 `Defs/ShowDefs/` 新建 XML（可复制 `Shows_Placeholder.xml` 里的 `RimFlix.ShowDef` 块）：
   - `defName`：唯一 ID
   - `label` / `description`：游戏内显示名与描述
   - `televisionDefs`：目标电视（`TubeTelevision` / `FlatscreenTelevision` / `MegascreenTelevision`）
   - `secondsBetweenFrames`：帧间隔秒数
   - `frames`：每帧一个 `<li><texPath>…</texPath><graphicClass>Graphic_Single</graphicClass></li>`
3. 重启游戏，在 RimFlix 的节目菜单里选择新节目。

## 版权提醒

- 本地自用没有问题；**如果要上传创意工坊，请勿打包版权图（碧蓝航线官方图、COSER 照片等）**，
  工坊分发需要原创或已授权的素材，NSFW 内容还受 Steam 政策限制。
- 占位图由 `tools/make_placeholder_shows.py` 生成，可随意替换。
