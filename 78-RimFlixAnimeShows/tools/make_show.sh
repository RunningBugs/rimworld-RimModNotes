#!/usr/bin/env bash
# make_show.sh — 视频 → RimFlix 电视节目（本 Mod 唯一的生产脚本）
#
# 用法:
#   ./make_show.sh <视频文件>
#   显示视频总时长后,按提示输入: 起止时间 / 电视类型(可单选或多选) / 抽帧密度 / 节目总时长 / ping-pong
#   确认后自动抽帧到 Textures/Shows/<ID>_<类型>/ 并生成 Defs/ShowDefs/<ID>.xml
#
# 非交互(管道):
#   printf '0\n01:30:00\nmy_show\n我的节目\n123\n2\n66\ny\ny\n' | ./make_show.sh video.mp4
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

# 时间解析: 支持 秒 / MM:SS / HH:MM:SS -> 秒(浮点)
parse_time() {
  awk -v t="$1" 'BEGIN{
    n=split(t,a,":");
    if(n==1) printf "%.3f", a[1]+0;
    else if(n==2) printf "%.3f", a[1]*60+a[2];
    else printf "%.3f", a[1]*3600+a[2]*60+a[3];
  }'
}
fmt_time() { awk -v s="$1" 'BEGIN{printf "%02d:%02d:%05.2f", int(s/3600), int(s%3600/60), s%60}'; }

DUR=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$VIDEO")
SW=$(ffprobe -v error -select_streams v:0 -show_entries stream=width -of csv=p=0 "$VIDEO")
SH=$(ffprobe -v error -select_streams v:0 -show_entries stream=height -of csv=p=0 "$VIDEO")

echo "=== RimFlix 节目生成 ==="
echo "视频: $VIDEO"
echo "总时长: $(fmt_time "$DUR") | 尺寸: ${SW}x${SH}"

T_START=$(parse_time "$(ask "起始时间 (秒 或 MM:SS 或 HH:MM:SS)" "0")")
T_END=$(parse_time "$(ask "结束时间 (超出总时长按最后一帧算)" "$(awk "BEGIN{printf \"%.3f\", $DUR}")")")
# 结束时间超过总时长 -> 按视频最后一帧处理
awk "BEGIN{exit !($T_END>$DUR)}" && { T_END=$DUR; echo "(结束时间超出总时长,已按最后一帧 $DUR 秒处理)"; }
RANGE=$(awk "BEGIN{printf \"%.3f\", $T_END-$T_START}")
awk "BEGIN{exit !($RANGE>0)}" || { echo "错误: 结束时间必须大于起始时间" >&2; exit 1; }

SHOW_ID=$(ask "节目ID (英文,用于目录和defName)" "$(basename "$VIDEO" | sed 's/\.[^.]*$//' | tr ' ' '_')")
LABEL=$(ask "显示名 (游戏内节目名)" "$SHOW_ID")

echo "电视类型 (可多选,直接连写): 1) Flatscreen 平板(620x256)  2) Megascreen 巨屏(902x256)  3) Tube 显像管(157x128)"
TV=$(ask "选择 (如 1 或 123 或 13)" "1")
# 校验: 只允许 1/2/3,去重
[[ "$TV" =~ ^[123]+$ ]] || { echo "无效选择: $TV" >&2; exit 1; }
TVS=$(echo "$TV" | fold -w1 | sort -u | tr -d '\n')

tv_params() {  # tv_params <数字> -> 后缀 宽 高 def标签
  case "$1" in
    1) echo "_flat 620 256 FlatscreenTelevision Flat";;
    2) echo "_mega 902 256 MegascreenTelevision Mega";;
    3) echo "_tube 157 128 TubeTelevision Tube";;
  esac
}

FPS=$(ask "抽帧密度 (每秒抽几张图, 越大动画越流畅)" "2")
EXPECT=$(awk "BEGIN{printf \"%d\", int($FPS*$RANGE+0.5)}")
[[ "$EXPECT" -lt 1 ]] && EXPECT=1
DEF_TOTAL=$(awk "BEGIN{printf \"%.2f\", $EXPECT*0.15}")
TOTAL=$(ask "节目总时长(秒,帧间隔=总时长÷帧数)" "$DEF_TOTAL")
INTERVAL=$(awk "BEGIN{printf \"%.4f\", $TOTAL/$EXPECT}")
PINGPONG=$(ask "ping-pong 循环 (正放+倒放,无跳帧) [y/n]" "y")

echo
echo "--- 确认 ---"
echo "范围: $(fmt_time "$T_START") ~ $(fmt_time "$T_END") ($(fmt_time "$RANGE"))"
echo "ID: $SHOW_ID | 名称: $LABEL | 电视类型: $TVS"
for (( k=0; k<${#TVS}; k++ )); do
  read -r SUF TW TH TVDEF SHORT <<< "$(tv_params "${TVS:$k:1}")"
  echo "  - ${SHOW_ID}${SUF} ($TVDEF, ${TW}x${TH})"
done
echo "抽帧: 每秒${FPS}张 × $(fmt_time "$RANGE") ≈ $EXPECT 帧 | 节目: ${TOTAL}s (间隔 ${INTERVAL}s) | ping-pong: $PINGPONG"
CONFIRM=$(ask "开始生成? [y/n]" "y")
[[ "$CONFIRM" != "y" && "$CONFIRM" != "Y" ]] && { echo "已取消"; exit 0; }

# 生成节目帧函数: make_frames <宽> <高> <输出目录> <文件前缀>
make_frames() {
  local TW=$1 TH=$2 OUTDIR=$3 PREFIX=$4
  local TMPF
  TMPF=$(mktemp -d)
  # 等比放缩完整容纳进目标尺寸,多余部分填黑(不裁切)
  ffmpeg -y -loglevel error -ss "$T_START" -to "$T_END" -i "$VIDEO" \
    -vf "fps=$FPS,scale=$TW:$TH:force_original_aspect_ratio=decrease,pad=$TW:$TH:(ow-iw)/2:(oh-ih)/2:color=black" \
    "$TMPF/f_%04d.png"
  mapfile -t FS < <(ls "$TMPF"/f_*.png 2>/dev/null | sort)
  local SEQ=("${FS[@]}")
  if [[ "$PINGPONG" == "y" || "$PINGPONG" == "Y" ]]; then
    for (( i=${#FS[@]}-2; i>=1; i-- )); do SEQ+=("${FS[$i]}"); done
  fi
  mkdir -p "$OUTDIR"
  for i in "${!SEQ[@]}"; do
    cp "${SEQ[$i]}" "$OUTDIR/$(printf '%s_%02d.png' "$PREFIX" "$i")"
  done
  rm -rf "$TMPF"
  FRAME_COUNT=${#SEQ[@]}
}

XML_BLOCKS=()
for (( k=0; k<${#TVS}; k++ )); do
  read -r SUF TW TH TVDEF SHORT <<< "$(tv_params "${TVS:$k:1}")"
  SID="${SHOW_ID}${SUF}"
  make_frames "$TW" "$TH" "$MOD_ROOT/Textures/Shows/$SID" "$SID"
  echo "$SID: $FRAME_COUNT 帧 -> Textures/Shows/$SID"

  FRAMES_XML=""
  for i in $(seq 0 $((FRAME_COUNT-1))); do
    FRAMES_XML+=$(printf '\n			<li><texPath>Shows/%s/%s_%02d</texPath><graphicClass>Graphic_Single</graphicClass></li>' "$SID" "$SID" "$i")
  done
  XML_BLOCKS+=("$(cat <<EOF
	<RimFlix.ShowDef>
		<defName>$SID</defName>
		<label>$LABEL - $SHORT</label>
		<description>Generated from $(basename "$VIDEO") [$(fmt_time "$T_START")~$(fmt_time "$T_END")] by make_show.sh.</description>
		<televisionDefs>
			<li>$TVDEF</li>
		</televisionDefs>
		<secondsBetweenFrames>$INTERVAL</secondsBetweenFrames>
		<sound />
		<frames>$FRAMES_XML
		</frames>
	</RimFlix.ShowDef>
EOF
)")
done

{
  echo '<?xml version="1.0" encoding="utf-8"?>'
  echo '<Defs>'
  echo
  ( IFS=$'\n\n'; echo "${XML_BLOCKS[*]}" )
  echo
  echo '</Defs>'
} > "$MOD_ROOT/Defs/ShowDefs/$SHOW_ID.xml"

echo
echo "定义 -> $MOD_ROOT/Defs/ShowDefs/$SHOW_ID.xml"
