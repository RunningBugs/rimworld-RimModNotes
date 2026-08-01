# RimFlix Anime Shows 动漫电视秀

RimFlix (Continued) 的本地内容扩展包：为游戏内电视提供动漫风节目。
无代码，纯 `RimFlix.ShowDef` 定义 + 帧图。自带三个可播放的占位节目（弹跳团子 ×2、星空闪烁），
以及 **6 个 AI 生成的高清节目**（狐耳舰娘、兔耳步枪娘、黑长直刀娘、双马尾无人机娘、紫发魔女、红发恶魔娘，
Animagine XL 本地生成 1216×512 → 620×256 帧，Ken Burns 缓动 22 帧循环）。

A local content pack for RimFlix (Continued): anime-style TV shows with zero code —
just `RimFlix.ShowDef` XML and frame images. Ships three playable placeholder shows plus
six AI-generated HD shows (Animagine XL stills turned into Ken Burns loops).

## 使用 / Usage

1. 订阅并启用 RimFlix (Continued)（工坊 id `3194639480`）。
2. 本目录软链接到游戏 Mods 文件夹并启用，排在 RimFlix 之后。
3. 游戏里建一台平板电视，用 RimFlix 的节目菜单选择节目（AI 节目名以角色名开头，占位节目为 Blob/Star）。

## 素材再生产 / Regenerating Assets

- `assets_src/`：AI 生成的 1216×512 原始大图（不进 Textures，游戏不加载）
- `tools/comfy_gen.py`：驱动本地 ComfyUI（SDXL txt2img）生成新图
- `tools/kenburns.py`：把静态图切成 Ken Burns 推拉循环帧（ping-pong，无跳帧）
- `tools/make_placeholder_shows.py`：占位节目帧生成器

## 添加自己的图片 / Add Your Own Shows

见 [docs/adding-shows.md](docs/adding-shows.md)（含各电视屏幕尺寸表、GIF 拆帧方法、版权提醒）。

## 版权 / Copyright

AI 生成图片仅供本地自用：风格无版权，但碧蓝航线等角色 IP 仍属原厂商，**不要上传创意工坊**。

