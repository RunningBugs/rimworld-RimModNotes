#!/usr/bin/env python3
"""Render the gene picker view of Dialog_KillingReward with rimworld-imgui-sim."""
from pathlib import Path
import sys

sys.path.insert(0, str(Path.home() / "mine/workspace/rimworld/rimworld-imgui-sim"))
from rimworld_imgui import IMGUIContext, Rect, Color, GameFont, TextAnchor

OUT = Path(__file__).with_name("gene_picker.png")

ctx = IMGUIContext(640, 600)
DARK_RED = Color(0.45, 0.08, 0.08)
CARD_BG = Color(0.14, 0.14, 0.16)
BAR_BG = Color(0.06, 0.06, 0.07)
INPUT_BG = Color(0.10, 0.10, 0.12)
DESC_GREY = Color(0.75, 0.75, 0.75)
BTN_BG = Color(0.25, 0.25, 0.28)

# 标题
ctx.font = GameFont.MEDIUM
ctx.anchor = TextAnchor.UPPER_CENTER
ctx.label(Rect(0, 12, 640, 30), "黑暗超凡智能的恩赐")

# 等阶 / 待领取
ctx.font = GameFont.SMALL
ctx.anchor = TextAnchor.UPPER_LEFT
ctx.label(Rect(24, 48, 300, 22), "恩赐等阶: 3")
ctx.anchor = TextAnchor.UPPER_RIGHT
ctx.label(Rect(316, 48, 300, 22), "待领取的恩赐: 5")

# 血祭进度条
ctx.anchor = TextAnchor.MIDDLE_CENTER
ctx.fillable_bar(Rect(24, 74, 592, 22), 0.4, ctx.solid_tex(DARK_RED), ctx.solid_tex(BAR_BG))
ctx.label(Rect(24, 74, 592, 22), "血祭 7 / 17")

# 视图标题：选择要添加的基因（小明 · 异种基因）
ctx.font = GameFont.SMALL
ctx.anchor = TextAnchor.UPPER_LEFT
ctx.label(Rect(24, 116, 592, 32), "选择要添加的基因 (小明 · 异种基因)")

# 搜索框（含已输入的“血”）
ctx.draw_texture(Rect(24, 148, 592, 30), ctx.solid_tex(INPUT_BG))
ctx.label(Rect(30, 152, 580, 22), "血|")

# 基因行：图标 + 名称 + 右侧代谢/复杂度
genes = [
    ("血原质", "-1", "+2", Color(0.55, 0.20, 0.25)),
    ("汲血者", "+1", "+1", Color(0.60, 0.15, 0.15)),
    ("凝血", "-2", "+1", Color(0.45, 0.25, 0.35)),
    ("血怒", "+2", "+3", Color(0.65, 0.20, 0.10)),
    ("血腥凝视", "0", "+2", Color(0.50, 0.10, 0.30)),
]
y = 184
for name, met, cpx, icon_color in genes:
    ctx.draw_texture(Rect(26, y + 2, 30, 30), ctx.solid_tex(icon_color))
    ctx.draw_texture(Rect(62, y + 1, 546, 32), ctx.solid_tex(BTN_BG))
    ctx.anchor = TextAnchor.MIDDLE_LEFT
    ctx.label(Rect(70, y + 1, 420, 32), name)
    ctx.anchor = TextAnchor.UPPER_RIGHT
    ctx.label(Rect(480, y + 7, 122, 22), f"代谢 {met} · 复杂 {cpx}", color=DESC_GREY)
    ctx.anchor = TextAnchor.UPPER_LEFT
    y += 36

# 返回按钮
ctx.draw_texture(Rect(24, 564, 120, 32), ctx.solid_tex(BTN_BG))
ctx.anchor = TextAnchor.MIDDLE_CENTER
ctx.label(Rect(24, 564, 120, 32), "返回")

ctx.save(str(OUT))
print(f"wrote {OUT}")
