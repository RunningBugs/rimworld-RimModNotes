#!/usr/bin/env bash
# grok_video.sh — 用 OpenRouter 的 Grok Imagine (x-ai/grok-imagine-video) 图生视频
#
# 用法:
#   ./grok_video.sh -i 参考图.png -p "你的剧本(英文效果最好)" [-o 输出名] [选项]
#
# 选项:
#   -i IMAGE      参考图(必填,作为视频首帧)
#   -p PROMPT     剧本/提示词(必填)
#   -o NAME       输出名(默认 grok_video);mp4 保存到当前目录或 -d 指定目录
#   -m MODEL      模型(默认 x-ai/grok-imagine-video)
#   -d SECONDS    时长秒(默认 5)
#   -r RES        分辨率 480p|720p(默认 480p;720p 更贵)
#   -a RATIO      宽高比 16:9|9:16|1:1|4:3|3:4|3:2|2:3(默认 16:9)
#   -d DIR        mp4/帧输出目录(默认当前目录)
#   --frames N    生成后抽 N 帧到 <输出名>_frames/(默认不抽帧)
#   --size WxH    抽帧缩放尺寸(默认 620x256,仅配合 --frames)
#   --dry-run     只打印请求 JSON 不提交(调试用)
#
# 示例:
#   ./grok_video.sh -i ~/Downloads/txt2img_00072_.png -o fox_ep1 \
#     -p "the fox-eared shipgirl sways her hips slowly, hair flowing, slow push-in"
#   ./grok_video.sh -i ref.png -o ep2 -r 720p -d 8 --frames 12 --size 620x256 -p "..."
#
set -euo pipefail

KEY_FILE="$HOME/.config/openrouter/api_key"
API="https://openrouter.ai/api/v1/videos"
MODEL="x-ai/grok-imagine-video"
DURATION=5; RES="480p"; RATIO="16:9"
IMAGE=""; PROMPT=""; OUTNAME="grok_video"; OUTDIR=""
FRAMES=0; SIZE="620x256"; DRYRUN=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    -i) IMAGE="$2"; shift 2;;
    -p) PROMPT="$2"; shift 2;;
    -o) OUTNAME="$2"; shift 2;;
    -m) MODEL="$2"; shift 2;;
    -d) DURATION="$2"; shift 2;;
    -r) RES="$2"; shift 2;;
    -a) RATIO="$2"; shift 2;;
    --outdir) OUTDIR="$2"; shift 2;;
    --frames) FRAMES="$2"; shift 2;;
    --size) SIZE="$2"; shift 2;;
    --dry-run) DRYRUN=1; shift;;
    -h|--help) sed -n '2,24p' "$0"; exit 0;;
    *) echo "未知参数: $1" >&2; exit 1;;
  esac
done

[[ -z "$IMAGE" || -z "$PROMPT" ]] && { echo "错误: -i 和 -p 必填 (-h 看帮助)" >&2; exit 1; }
[[ -f "$IMAGE" ]] || { echo "错误: 找不到图片 $IMAGE" >&2; exit 1; }
[[ -f "$KEY_FILE" ]] || { echo "错误: 找不到 OpenRouter key ($KEY_FILE)" >&2; exit 1; }
KEY=$(cat "$KEY_FILE")

TMPIMG=$(mktemp)
trap 'rm -f "$TMPIMG"' EXIT
echo -n "data:image/png;base64,$(base64 -w0 "$IMAGE")" > "$TMPIMG"
BODY=$(jq -n \
  --arg model "$MODEL" --arg prompt "$PROMPT" \
  --argjson dur "$DURATION" --arg res "$RES" --arg ar "$RATIO" \
  --rawfile img "$TMPIMG" \
  '{model: $model, prompt: $prompt, duration: $dur, resolution: $res, aspect_ratio: $ar,
    generate_audio: false,
    frame_images: [{type: "image_url", image_url: {url: $img}, frame_type: "first_frame"}]}')

if [[ "$DRYRUN" == "1" ]]; then
  echo "$BODY" | jq '{model, prompt, duration, resolution, aspect_ratio, generate_audio, frame_images: [.frame_images[0].frame_type]}'
  exit 0
fi

# 请求体写临时文件,避免 curl 参数过长
TMPBODY=$(mktemp)
trap 'rm -f "$TMPIMG" "$TMPBODY"' EXIT
echo -n "$BODY" > "$TMPBODY"

echo "提交任务 ($MODEL, ${DURATION}s, $RES)..."
RESP=$(curl -s -m 120 -X POST "$API" -H "Content-Type: application/json" \
  -H "Authorization: Bearer $KEY" --data-binary @"$TMPBODY")
JOB=$(echo "$RESP" | jq -r '.id // empty')
[[ -z "$JOB" ]] && { echo "提交失败: $RESP" >&2; exit 1; }
echo "job id: $JOB"

# 轮询(最长 20 分钟)
for i in $(seq 1 60); do
  sleep 20
  POLL=$(curl -s -m 30 "$API/$JOB" -H "Authorization: Bearer $KEY")
  ST=$(echo "$POLL" | jq -r '.status')
  case "$ST" in
    completed) break;;
    failed|cancelled|expired) echo "任务 $ST: $(echo "$POLL" | jq -r '.error // ""')" >&2; exit 1;;
  esac
  echo "[$((i*20))s] $ST"
done
[[ "$ST" != "completed" ]] && { echo "超时" >&2; exit 1; }

COST=$(echo "$POLL" | jq -r '.usage.cost // "?"')
echo "完成,成本 \$$COST"

DEST_DIR="${OUTDIR:-$PWD}"
mkdir -p "$DEST_DIR"
MP4="$DEST_DIR/${OUTNAME}.mp4"
curl -s -m 300 "$API/$JOB/content?index=0" -H "Authorization: Bearer $KEY" -o "$MP4"
echo "$MP4"

# 可选抽帧(居中裁切到目标宽高比后缩放)
if [[ "$FRAMES" -gt 0 ]]; then
  FD="$DEST_DIR/${OUTNAME}_frames"
  mkdir -p "$FD"
  W="${SIZE%x*}"; H="${SIZE#*x}"
  SRC_W=$(ffprobe -v error -select_streams v:0 -show_entries stream=width -of csv=p=0 "$MP4")
  SRC_H=$(ffprobe -v error -select_streams v:0 -show_entries stream=height -of csv=p=0 "$MP4")
  # 目标宽高比裁切
  CROP_W=$SRC_W; CROP_H=$(( SRC_W * H / W ))
  if (( CROP_H > SRC_H )); then CROP_H=$SRC_H; CROP_W=$(( SRC_H * W / H )); fi
  CROP_W=$(( CROP_W / 2 * 2 )); CROP_H=$(( CROP_H / 2 * 2 ))
  ffmpeg -y -loglevel error -i "$MP4" \
    -vf "fps=$FRAMES/$DURATION,crop=$CROP_W:$CROP_H:(iw-$CROP_W)/2:(ih-$CROP_H)/2,scale=$W:$H" \
    "$FD/${OUTNAME}_%02d.png"
  echo "帧图 -> $FD ($FRAMES 帧, ${SIZE})"
fi
