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

脚本会依次询问并回显确认：**节目ID → 显示名 → 电视类型（1 平板 620×256 / 2 巨屏 902×256 /
3 显像管 157×128）→ 抽帧数量（默认 12）→ 帧间隔（默认 0.15s）→ 是否 ping-pong 循环（默认 y）**。
确认后自动完成：居中裁切到目标宽高比 → 缩放到目标尺寸 → 按 fps=N/时长抽 N 帧 →
（可选）ping-pong 成 2N-2 帧 → 写入 `Textures/Shows/<ID>/` 并生成 `Defs/ShowDefs/<ID>.xml`。

非交互用法（管道喂答案）：

```bash
printf 'my_show\n我的节目\n1\n12\n0.15\ny\ny\n' | tools/make_show.sh video.mp4
```

规则备忘：

- 帧间隔：0.033 ≈ 30fps 动画；0.1~0.3 适合循环片段/幻灯
- defName 必须全 Mod 唯一（就是节目ID本身）
- 新增/删除节目 = 增删对应的 `Defs/ShowDefs/<ID>.xml` 和 `Textures/Shows/<ID>/`，无其他耦合

## 版权红线

本地自用随意；**AI 生成的碧蓝航线等角色图/视频不要上传创意工坊**（角色 IP 属原厂）。

## 部署

Mod 目录已软链接到 `/Data/SteamLibrary/steamapps/common/RimWorld/Mods/78-RimFlixAnimeShows`，
改动提交即生效；游戏内需启用 RimFlix (Continued) 并排在其后。
