# RimFlix Anime Shows — AGENTS.md

RimFlix (Continued)（packageId `zal.rimflix`，工坊 id `3194639480`）的本地内容扩展包：纯
`RimFlix.ShowDef` XML + 帧图，无代码。本文件记录从静态图到游戏内电视节目的完整生产流程，
后续 AI 助手按此操作即可，无需重新探索。

## 目录结构

```
78-RimFlixAnimeShows/
├── About/About.xml              # packageId RunningBugs.RimFlix.AnimeShows，硬依赖 zal.rimflix
├── Defs/ShowDefs/               # 每个 XML 一个或多个 RimFlix.ShowDef
├── Textures/Shows/<show>/       # 每个节目一个目录，620x256 帧图（texPath 不含扩展名）
├── assets_src/                  # 原图/视频素材（游戏不加载）：1216x512 静态图、video/ mp4
├── tools/                       # 生产工具（见下）
└── docs/                        # adding-shows.md 等
```

## 生产管线（三阶段）

### 1. 文生图（静态原图，1216×512）

- 工具：`tools/txt2img.sh`（bash+curl+jq 驱动本地 ComfyUI，自动拉起服务，成品移动到当前目录）
- 网页工作流：`~/comfy/ComfyUI/user/default/workflows/sdxl_txt2img_oneobsession.json`
- 底模：`oneObsession_v23.safetensors`（Illustrious，默认）。备选：`animagine-xl-4.0.safetensors`（赛璐璐）、`lustifyNSFWCheckpoint_zenithV9.safetensors`（写实风，参数 dpmpp_2m_sde/30/3.5）
- 参数（One Obsession 作者推荐）：**euler + karras，20 步，CFG 5.0**，1216×512
- 提示词：booru tag 风格；硬指标（角色/服装/构图）用 tag 放前面，氛围可用自然语言
- 脚本默认负向词含露骨屏蔽行（`nude, nipples, topless, naked`），尺度取舍时改脚本里那一行

### 2. 图生视频（动画）

两条路线，选一即可：

**A. 本地免费：Wan2.2 TI2V-5B + DR34ML4Y LoRA**
- 网页工作流：`~/comfy/ComfyUI/user/default/workflows/ti2v_5b_lora_i2v.json`（UI 原生格式）
- 模型：`models/diffusion_models/Wan2.2-TI2V-5B-Q8_0.gguf`、`models/loras/DR34ML4Y_TI2V_5B_V1.safetensors`、`models/vae/wan2.2_vae.safetensors`、`models/text_encoders/umt5-xxl-enc-bf16.safetensors`
- 提示词：**自然语言句子**（T5 编码器，别用 tag）
- 硬规则（全部踩过坑）：
  - `merge_loras` 必须 false（GGUF 不支持合并）
  - UI 格式 seed 后必须有 `control_after_generate`（"fixed"），否则后续值全错位
  - `WanVideoEmptyEmbeds` 的 width/height 必须 = 输入图尺寸且都能被 16 整除（标准图 1216×512）
  - `num_frames` 满足 (n-1)%4==0（33/49/61…）
  - 5B i2v 走 EncodeLatentBatch → AddExtraLatent（干净潜变量换首帧），不是 14B 的条件通道
- 输出：`ComfyUI/output/tvshows/ti2v_test/`（帧图 + mp4 双路）

**B. API 付费：Grok Imagine（OpenRouter）**
- 工具：`tools/grok_video.sh`（key 在 `~/.config/openrouter/api_key`，不进库）
- 模型 `x-ai/grok-imagine-video`，480p ≈ $0.05/秒；`--frames N` 可直接抽帧
- 请求体走临时文件（`curl --data-binary @file`，base64 会撞 argv 上限）

### 3. 抽帧打包（节目）

- 标准帧：**620×256**（平板电视高清档；显像管 157×128、巨屏 451×128 见 `docs/adding-shows.md`）
- 循环：取 ~12 帧做 **ping-pong**（正序 + 去头尾倒序 = 22 帧），0.15s/帧 ≈ 3.3s 循环
- 静态图方案：`tools/kenburns.py`（推拉缓动伪动画）
- 巨屏（Megascreen）：`tools/letterbox.py` 把节目帧等比缩放到 256 高后居中贴到 902×256 黑底画布（两侧留黑，不拉伸不裁切）；配套 ShowDef 用 `MegascreenTelevision`，示例 `Defs/ShowDefs/Shows_Megascreen.xml`
- ShowDef 模板见 `Defs/ShowDefs/Shows_GrokVideo.xml` / `Shows_Local5B.xml`；
  写完后必须校验每个 texPath 对应文件存在（参考 git 历史里的校验写法）
- 缓动/抽帧 crop 规则：源 848×480 → `crop=848:350` 居中 → `scale=620:256`；1216×512 源直接裁 848:350 亦可

## 版权红线

- 本地自用随意；**AI 生成的碧蓝航线等角色图不要上传创意工坊**（角色 IP 属原厂）
- 占位图由 `tools/make_placeholder_shows.py` 程序化生成，可随意替换

## 常用命令速查

```bash
# 文生图（当前目录出图）
tools/txt2img.sh -p "masterpiece, best quality, 1girl, ..." -o my_fox -s 42
# Grok 图生视频（5s 480p + 抽 12 帧）
tools/grok_video.sh -i ref.png -o ep1 -p "your script" --frames 12
# 静态图切 Ken Burns 帧
python3 tools/kenburns.py assets_src/foo.png Textures/Shows/foo --size 620x256
# 视频抽帧（子采样 12 帧）
ffmpeg -y -loglevel error -i in.mp4 -vf "fps=12/5,crop=848:350:(iw-848)/2:(ih-350)/2,scale=620:256" out/f_%02d.png
```

## 部署

Mod 目录已软链接到 `/Data/SteamLibrary/steamapps/common/RimWorld/Mods/78-RimFlixAnimeShows`，
改动提交即生效；游戏内需启用 RimFlix (Continued) 并排在其后。
