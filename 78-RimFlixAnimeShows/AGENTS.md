# RimFlix Anime Shows — AGENTS.md

RimFlix (Continued)（packageId `zal.rimflix`，工坊 id `3194639480`）的本地内容扩展包：纯
`RimFlix.ShowDef` XML + 帧图，无代码。

## 目录结构

```
78-RimFlixAnimeShows/
├── About/About.xml        # packageId RunningBugs.RimFlix.AnimeShows，硬依赖 zal.rimflix
├── Defs/ShowDefs/         # 每个节目一个 <ID>.xml（由脚本生成）
├── Textures/Shows/<ID>/   # 每个节目一个帧图目录（由脚本生成）
├── assets_src/            # 原图/视频素材（游戏不加载）
└── tools/make_show.sh     # 唯一的生产脚本
```

## 唯一生产流程

给一段视频（mp4），运行：

```bash
tools/make_show.sh <视频文件>
```

脚本会先显示视频**总时长**，然后依次询问并回显确认：
**起止时间（秒 / MM:SS / HH:MM:SS，只在范围内抽帧；结束时间超过总时长自动按最后一帧处理）
→ 节目ID → 显示名 → 电视类型（1 平板 620×256 / 2 巨屏 902×256 / 3 显像管 157×128，**可多选连写如 123 / 13**，
每种尺寸生成独立目录与 defName：<ID>_flat / <ID>_mega / <ID>_tube，统一写入一个 <ID>.xml，
label 自动加 - Flat / - Mega / - Tube 后缀）
→ 抽帧密度（每秒抽几张图，帧数=密度×范围时长）→ 节目总时长（帧间隔=总时长÷帧数，自动换算显示）
→ 是否 ping-pong 循环（默认 y）**。
确认后自动完成：范围内抽帧 →
**等比放缩完整容纳目标尺寸，多余部分填黑（任何情况下都不裁切画面）** → ping-pong 组装 → 写入 `Textures/Shows/<ID>_<类型>/` 并生成 `Defs/ShowDefs/<ID>.xml`。

非交互用法（管道喂答案，顺序与提示一致）：

```bash
printf '0\n01:30:00\nmy_show\n我的节目\n1\n2\n66\ny\ny\n' | tools/make_show.sh video.mp4
```

规则备忘：

- 帧间隔：0.033 ≈ 30fps 动画；0.1~0.3 适合循环片段/幻灯
- defName 必须全 Mod 唯一（ID + 类型后缀：_flat / _mega / _tube）
- 新增/删除节目 = 增删对应的 `Defs/ShowDefs/<ID>.xml` 和 `Textures/Shows/<ID>/`，无其他耦合

## 版权红线

本地自用随意；**AI 生成的碧蓝航线等角色图/视频不要上传创意工坊**（角色 IP 属原厂）。

## 部署

Mod 目录已软链接到 `/Data/SteamLibrary/steamapps/common/RimWorld/Mods/78-RimFlixAnimeShows`，
改动提交即生效；游戏内需启用 RimFlix (Continued) 并排在其后。
