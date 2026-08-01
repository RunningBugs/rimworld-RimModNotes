#!/usr/bin/env bash
# txt2img.sh — 本地 ComfyUI (SDXL) 文生图自助脚本
#
# 用法:
#   ./txt2img.sh -p "你的提示词" -o 输出名 [选项]
#
# 选项:
#   -p PROMPT     正向提示词(必填,英文效果最好)
#   -o NAME       输出名(必填);最终文件会打印在最后一行
#   -n NEGATIVE   负向提示词(有默认值,可整段覆盖)
#   -s SEED       随机种子(默认 -1 = 每次随机)
#   -W WIDTH      宽(默认 1216)   -H HEIGHT  高(默认 512)
#   --steps N     步数(默认 28)   --cfg X    CFG(默认 6.0)
#   -c CKPT       模型(默认 animagine-xl-4.0.safetensors)
#   -d DIR        生成后把图复制到该目录(默认留在 ComfyUI output/tvshows/)
#
# 示例:
#   ./txt2img.sh -p "masterpiece, best quality, 1girl, fox ears, ..." -o my_fox -s 42
#   ./txt2img.sh -p "..." -o my_fox2 -d ~/mine/workspace/rimworld/RimModNotes/78-RimFlixAnimeShows/assets_src/sexy
#
set -euo pipefail

SERVER="http://127.0.0.1:8188"
COMFY_DIR="$HOME/comfy/ComfyUI"
CKPT="oneObsession_v23.safetensors"
WIDTH=1216; HEIGHT=512; SEED=-1; STEPS=28; CFG="6.0"
# 默认负向提示词:质量词 + 硬性屏蔽露点/全裸(想改尺度就改这一行)
NEG="lowres, bad anatomy, bad hands, text, error, missing fingers, extra digit, fewer digits, cropped, worst quality, low quality, jpeg artifacts, signature, watermark, username, blurry, worst quality, low quality, normal quality, lowres, bad quality, worst aesthetic, score_1, score_2, score_3, score_4, score_5, ugly, deformed, mutation, disfigured, blurry, distorted, bad anatomy, bad proportions, extra limbs, missing limbs, floating limbs, disconnected limbs, mutation, mutated, long neck, cross-eyed, asymmetrical, malformed, child, loli, young, underage, toddler, baby, flat chest, small breasts, petite, fat, obese, chubby, plump, belly fat, pot belly, thick waist, large belly, baby face, child face, round face, chubby face, chubby cheeks, soft face, doll face, cute face, ugly face, deformed face, disfigured face, bad face, asymmetrical face, poorly drawn hands, bad hands, extra fingers, missing fingers, fused fingers, too many fingers, mutated hands, deformed hands, bad anatomy hands, poorly drawn feet, bad feet, extra toes, missing toes, fused toes, deformed feet, mutated feet, poorly drawn face, bad eyes, asymmetrical eyes, deformed eyes, cross eyed, bad nose, deformed nose, bad mouth, deformed mouth, blurry face, incomplete face, poorly drawn teeth, bad teeth, deformed teeth, missing teeth, extra teeth, uneven teeth, crooked teeth, blurry teeth, poorly drawn nails, bad nails, deformed nails, missing nails, extra nails, fused nails, long nails, dirty nails, poorly drawn ears, bad ears, deformed ears, asymmetrical ears, missing ears, extra ears, fused ears, childlike body, immature body, short legs, short torso, stubby limbs, stocky, thick limbs, disproportionate body, short stature, chubby body"
PROMPT=""; OUTNAME=""; OUTDIR=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    -p) PROMPT="$2"; shift 2;;
    -o) OUTNAME="$2"; shift 2;;
    -n) NEG="$2"; shift 2;;
    -s) SEED="$2"; shift 2;;
    -W) WIDTH="$2"; shift 2;;
    -H) HEIGHT="$2"; shift 2;;
    --steps) STEPS="$2"; shift 2;;
    --cfg) CFG="$2"; shift 2;;
    -c) CKPT="$2"; shift 2;;
    -d) OUTDIR="$2"; shift 2;;
    -h|--help) sed -n '2,20p' "$0"; exit 0;;
    *) echo "未知参数: $1" >&2; exit 1;;
  esac
done

[[ -z "$PROMPT" || -z "$OUTNAME" ]] && { echo "错误: -p 和 -o 必填 (-h 看帮助)" >&2; exit 1; }
[[ "$SEED" == "-1" ]] && SEED=$(( RANDOM * 32768 + RANDOM ))

# 1) 确保 ComfyUI 在跑
if ! curl -s -m 3 "$SERVER/system_stats" >/dev/null 2>&1; then
  echo "ComfyUI 未运行,正在后台启动..." >&2
  (cd "$COMFY_DIR" && nohup .venv/bin/python main.py --port 8188 > /tmp/comfyui.log 2>&1 &)
  for i in $(seq 1 30); do
    sleep 2
    curl -s -m 3 "$SERVER/system_stats" >/dev/null 2>&1 && break
  done
fi

# 2) 组装 SDXL 工作流并提交
BODY=$(jq -n \
  --arg ckpt "$CKPT" --arg pos "$PROMPT" --arg neg "$NEG" \
  --argjson w "$WIDTH" --argjson h "$HEIGHT" --argjson seed "$SEED" \
  --argjson steps "$STEPS" --argjson cfg "$CFG" --arg out "tvshows/$OUTNAME" \
  '{
    "1": {class_type:"CheckpointLoaderSimple", inputs:{ckpt_name:$ckpt}},
    "2": {class_type:"CLIPTextEncode", inputs:{clip:["1",1], text:$pos}},
    "3": {class_type:"CLIPTextEncode", inputs:{clip:["1",1], text:$neg}},
    "4": {class_type:"EmptyLatentImage", inputs:{width:$w, height:$h, batch_size:1}},
    "5": {class_type:"KSampler", inputs:{model:["1",0], positive:["2",0], negative:["3",0], latent_image:["4",0], seed:$seed, steps:$steps, cfg:$cfg, sampler_name:"euler_ancestral", scheduler:"normal", denoise:1.0}},
    "6": {class_type:"VAEDecode", inputs:{samples:["5",0], vae:["1",2]}},
    "7": {class_type:"SaveImage", inputs:{images:["6",0], filename_prefix:$out}}
  }')

PID=$(curl -s -m 30 -X POST "$SERVER/prompt" -H "Content-Type: application/json" -d "{\"prompt\": $BODY}" | jq -r '.prompt_id')
[[ -z "$PID" || "$PID" == "null" ]] && { echo "提交失败" >&2; exit 1; }
echo "任务 $PID (seed=$SEED) 生成中..." >&2

# 3) 轮询结果
for i in $(seq 1 60); do
  sleep 5
  HIST=$(curl -s -m 10 "$SERVER/history/$PID")
  [[ "$HIST" == "{}" ]] && continue
  STATUS=$(echo "$HIST" | jq -r 'to_entries[0].value.status.status_str')
  if [[ "$STATUS" == "success" ]]; then
    FILE=$(echo "$HIST" | jq -r 'to_entries[0].value.outputs["7"].images[0] | (if .subfolder == "" then .filename else .subfolder + "/" + .filename end)')
    SRC="$COMFY_DIR/output/$FILE"
    if [[ -n "$OUTDIR" ]]; then
      mkdir -p "$OUTDIR"
      cp "$SRC" "$OUTDIR/"
      echo "$OUTDIR/$(basename "$SRC")"
    else
      echo "$SRC"
    fi
    exit 0
  elif [[ "$STATUS" == "error" ]]; then
    echo "$HIST" | jq -r 'to_entries[0].value.status.messages[] | select(.[0]=="execution_error") | .[1].exception_message' >&2
    exit 1
  fi
done
echo "超时" >&2; exit 1
