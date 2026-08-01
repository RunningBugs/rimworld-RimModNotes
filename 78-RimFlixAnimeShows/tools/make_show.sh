#!/usr/bin/env bash
# make_show.sh — 视频 → RimFlix 电视节目（本 Mod 唯一的生产脚本）
#
# 用法:
#   ./make_show.sh <视频文件>
#   按提示输入: 节目ID / 显示名 / 电视类型 / 帧数 / 帧间隔 / 是否 ping-pong 循环
#   确认后自动抽帧到 Textures/Shows/<ID>/ 并生成 Defs/ShowDefs/<ID>.xml
#
# 也可以用管道非交互运行:
#   printf 'my_show\n我的节目\n1\n12\n0.15\ny\ny\n' | ./make_show.sh video.mp4
#
set -euo pipefail

MOD_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VIDEO="${1:-}"
[[ -z "$VIDEO" ]] && { echo "用法: $0 <视频文件>" >&2; exit 1; }
[[ -f "$VIDEO" ]] || { echo "错误: 找不到视频 $VIDEO" >&2; exit 1; }

ask() {  # ask <提示> <默认值> -> stdout
  local prompt="$1" default="$2" ans
  read -r -p "$prompt [$default]: " ans
  echo "${ans:-$default}"
}

echo "=== RimFlix 节目生成 ==="
echo "视频: $VIDEO"

SHOW_ID=$(ask "节目ID (英文,用于目录和defName)" "$(basename "$VIDEO" | sed 's/\.[^.]*$//' | tr ' ' '_')")
LABEL=$(ask "显示名 (游戏内节目名)" "$SHOW_ID")
echo "电视类型: 1) Flatscreen 平板(620x256)  2) Megascreen 巨屏(902x256)  3) Tube 显像管(157x128)"
TV=$(ask "选择" "1")
case "$TV" in
  1) TW=620; TH=256; TVDEF="FlatscreenTelevision";;
  2) TW=902; TH=256; TVDEF="MegascreenTelevision";;
  3) TW=157; TH=128; TVDEF="TubeTelevision";;
  *) echo "无效选择" >&2; exit 1;;
esac
FRAMES=$(ask "抽帧数量" "12")
INTERVAL=$(ask "帧间隔秒数 (0.033≈30fps动画, 0.15≈幻灯)" "0.15")
PINGPONG=$(ask "ping-pong 循环 (正放+倒放,无跳帧) [y/n]" "y")

echo
echo "--- 确认 ---"
echo "ID: $SHOW_ID | 名称: $LABEL | 电视: $TVDEF (${TW}x${TH}) | 帧数: $FRAMES | 间隔: ${INTERVAL}s | ping-pong: $PINGPONG"
CONFIRM=$(ask "开始生成? [y/n]" "y")
[[ "$CONFIRM" != "y" && "$CONFIRM" != "Y" ]] && { echo "已取消"; exit 0; }

# 源信息
DUR=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$VIDEO")
SW=$(ffprobe -v error -select_streams v:0 -show_entries stream=width -of csv=p=0 "$VIDEO")
SH=$(ffprobe -v error -select_streams v:0 -show_entries stream=height -of csv=p=0 "$VIDEO")
FPS=$(awk "BEGIN{printf \"%.6f\", $FRAMES/$DUR}")
# 目标宽高比居中裁切
read CW CH <<< "$(awk "BEGIN{tw=$TW; th=$TH; sw=$SW; sh=$SH; cw=sw; ch=int(sw*th/tw); if(ch>sh){ch=sh; cw=int(sh*tw/th)} cw=int(cw/2)*2; ch=int(ch/2)*2; print cw, ch}")"

OUTDIR="$MOD_ROOT/Textures/Shows/$SHOW_ID"
mkdir -p "$OUTDIR" /tmp/make_show_frames
rm -f /tmp/make_show_frames/f_*.png
ffmpeg -y -loglevel error -i "$VIDEO" \
  -vf "fps=$FPS,crop=$CW:$CH:(iw-$CW)/2:(ih-$CH)/2,scale=$TW:$TH" \
  /tmp/make_show_frames/f_%02d.png

# ping-pong 组装
mapfile -t FS < <(ls /tmp/make_show_frames/f_*.png)
SEQ=("${FS[@]}")
if [[ "$PINGPONG" == "y" || "$PINGPONG" == "Y" ]]; then
  for (( i=${#FS[@]}-2; i>=1; i-- )); do SEQ+=("${FS[$i]}"); done
fi
for i in "${!SEQ[@]}"; do
  cp "${SEQ[$i]}" "$OUTDIR/$(printf '%s_%02d.png' "$SHOW_ID" "$i")"
done
TOTAL=${#SEQ[@]}

# 生成 ShowDef XML
{
  echo '<?xml version="1.0" encoding="utf-8"?>'
  echo '<Defs>'
  echo
  echo -e '\t<RimFlix.ShowDef>'
  echo -e "\t\t<defName>$SHOW_ID</defName>"
  echo -e "\t\t<label>$LABEL</label>"
  echo -e "\t\t<description>Generated from $(basename "$VIDEO") by make_show.sh.</description>"
  echo -e '\t\t<televisionDefs>'
  echo -e "\t\t\t<li>$TVDEF</li>"
  echo -e '\t\t</televisionDefs>'
  echo -e "\t\t<secondsBetweenFrames>$INTERVAL</secondsBetweenFrames>"
  echo -e '\t\t<sound />'
  echo -e '\t\t<frames>'
  for i in $(seq 0 $((TOTAL-1))); do
    printf '\t\t\t<li><texPath>Shows/%s/%s_%02d</texPath><graphicClass>Graphic_Single</graphicClass></li>\n' "$SHOW_ID" "$SHOW_ID" "$i"
  done
  echo -e '\t\t</frames>'
  echo -e '\t</RimFlix.ShowDef>'
  echo
  echo '</Defs>'
} > "$MOD_ROOT/Defs/ShowDefs/$SHOW_ID.xml"

rm -f /tmp/make_show_frames/f_*.png
echo
echo "完成: $TOTAL 帧 -> $OUTDIR"
echo "定义 -> $MOD_ROOT/Defs/ShowDefs/$SHOW_ID.xml"
