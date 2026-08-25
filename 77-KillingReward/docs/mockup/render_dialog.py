#!/usr/bin/env python3
"""Render the Dialog_KillingReward main view mockup with rimworld-imgui-sim."""
from pathlib import Path
import sys

sys.path.insert(0, str(Path.home() / "mine/workspace/rimworld/rimworld-imgui-sim"))
from rimworld_imgui import IMGUIContext, Rect, Color, GameFont, TextAnchor

OUT = Path(__file__).with_name("dialog.png")

ctx = IMGUIContext(640, 600)
DARK_RED = Color(0.45, 0.08, 0.08)
CARD_BG = Color(0.14, 0.14, 0.16)
BAR_BG = Color(0.06, 0.06, 0.07)
WIN_BG = Color(0.09, 0.09, 0.10)
BTN_BG = Color(0.25, 0.25, 0.28)

# 标题
ctx.font = GameFont.MEDIUM
ctx.anchor = TextAnchor.UPPER_CENTER
ctx.label(Rect(0, 12, 640, 30), "黑暗超凡智能的恩赐")

# 等阶 / 待领取
ctx.font = GameFont.SMALL
ctx.anchor = TextAnchor.UPPER_LEFT
ctx.label(Rect(24, 48, 300, 22), "恩赐等阶: 2")
ctx.anchor = TextAnchor.UPPER_RIGHT
ctx.label(Rect(316, 48, 300, 22), "待领取的恩赐: 1")

# 血祭进度条
ctx.anchor = TextAnchor.MIDDLE_CENTER
ctx.fillable_bar(Rect(24, 74, 592, 22), 0.7, ctx.solid_tex(DARK_RED), ctx.solid_tex(BAR_BG))
ctx.label(Rect(24, 74, 592, 22), "血祭 7 / 10")

# 四张奖励卡片（滚动列表，第 4 张部分可见，底部留给关闭按钮）
cards = [
    ("禁忌知识", "祂将知识直接烙进学者的脑海。立刻完成一项当前可研究的科技。"),
    ("技艺灌注", "祂替你拨动了神经与肌肉。选择一名小人，其一项技能提升 3 级。"),
    ("虚空馈赠", "祂从虚空中掷下物资。选择一种物品与投放地点，领取一整格。"),
    ("黑暗赐福", "祂以血为引，滴血重生，破茧成蝶。选择一种赐福：为全员注入再生之力，或为一名小人改写特质、编辑基因。"),
]
CLIP_BOTTOM = 536  # 列表可视区下沿
y = 112
for title, desc in cards:
    ctx.draw_texture(Rect(24, y, 592, 104), ctx.solid_tex(CARD_BG))
    ctx.font = GameFont.SMALL
    ctx.anchor = TextAnchor.UPPER_LEFT
    ctx.label(Rect(36, y + 10, 420, 22), title)
    ctx.gui_color = Color(0.75, 0.75, 0.75)
    ctx.label(Rect(36, y + 34, 432, 60), desc)
    ctx.gui_color = Color(1, 1, 1)
    ctx.draw_texture(Rect(472, y + 30, 120, 44), ctx.solid_tex(BTN_BG))
    ctx.anchor = TextAnchor.MIDDLE_CENTER
    ctx.label(Rect(472, y + 30, 120, 44), "领取")
    ctx.anchor = TextAnchor.UPPER_LEFT
    y += 116
# 用窗口背景色盖住超出可视区的部分，模拟滚动裁剪
ctx.draw_texture(Rect(0, CLIP_BOTTOM, 640, 600 - CLIP_BOTTOM), ctx.solid_tex(WIN_BG))

# 关闭按钮
ctx.draw_texture(Rect(260, 552, 120, 32), ctx.solid_tex(BTN_BG))
ctx.anchor = TextAnchor.MIDDLE_CENTER
ctx.label(Rect(260, 552, 120, 32), "关闭")

ctx.save(str(OUT))
print(f"wrote {OUT}")
