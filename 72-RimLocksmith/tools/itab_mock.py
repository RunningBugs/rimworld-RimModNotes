"""RimLocksmith ITab 重设计 mockup v2。渲染候选布局到 out/ 供迭代。

用法: /home/lisanhu/mine/workspace/rimworld/rimworld-imgui-sim/.venv/bin/python tools/itab_mock.py
"""
from pathlib import Path
import sys

sys.path.insert(0, "/home/lisanhu/mine/workspace/rimworld/rimworld-imgui-sim")
from rimworld_imgui import IMGUIContext, Rect, Color, GameFont, TextAnchor

OUT = Path(__file__).parent / "out"
OUT.mkdir(exist_ok=True)

BG = Color(0.14, 0.14, 0.14)
PANEL_EDGE = Color(1, 1, 1, 0.25)
TEXT = Color(1, 1, 1)
TEXT_DIM = Color(0.75, 0.75, 0.75)
TEXT_GREEN = Color(0.35, 0.9, 0.35)
TEXT_RED = Color(0.95, 0.4, 0.35)
TEXT_YELLOW = Color(0.95, 0.85, 0.4)
BTN_BG = Color(0.25, 0.25, 0.25)
BTN_EDGE = Color(1, 1, 1, 0.4)
CHK_EDGE = Color(0.9, 0.9, 0.9)

MARGIN = 10
ROW_H = 26
GAP = 6
NOTE = "敌人、囚犯、野生动物始终遵循原版规则,不受此配置影响。"


def draw_button(ctx, rect, label):
    ctx.draw_texture(rect, ctx.solid_tex(BTN_BG))
    x, y, w, h = rect.x, rect.y, rect.width, rect.height
    edge = ctx.solid_tex(BTN_EDGE)
    ctx.draw_texture(Rect(x, y, w, 1), edge)
    ctx.draw_texture(Rect(x, y + h - 1, w, 1), edge)
    ctx.draw_texture(Rect(x, y, 1, h), edge)
    ctx.draw_texture(Rect(x + w - 1, y, 1, h), edge)
    ctx.anchor = TextAnchor.MIDDLE_CENTER
    ctx.font = GameFont.SMALL
    ctx.gui_color = TEXT
    ctx.label(rect, label)


def draw_checkbox(ctx, rect, label, checked):
    box = Rect(rect.x, rect.y + 3, 18, 18)
    ctx.draw_texture(box, ctx.solid_tex(Color(0.05, 0.05, 0.05)))
    edge = ctx.solid_tex(CHK_EDGE)
    x, y, w, h = box.x, box.y, box.width, box.height
    ctx.draw_texture(Rect(x, y, w, 1), edge)
    ctx.draw_texture(Rect(x, y + h - 1, w, 1), edge)
    ctx.draw_texture(Rect(x, y, 1, h), edge)
    ctx.draw_texture(Rect(x + w - 1, y, 1, h), edge)
    if checked:
        ctx.anchor = TextAnchor.MIDDLE_CENTER
        ctx.font = GameFont.SMALL
        ctx.gui_color = TEXT_GREEN
        ctx.label(box, "✓")
    ctx.anchor = TextAnchor.UPPER_LEFT
    ctx.font = GameFont.SMALL
    ctx.gui_color = TEXT
    ctx.label(Rect(rect.x + 26, rect.y, rect.width - 26, rect.height), label)


def draw_hline(ctx, x, y, w):
    ctx.draw_texture(Rect(x, y, w, 1), ctx.solid_tex(PANEL_EDGE))


def state_color(kind):
    return {"on": TEXT_GREEN, "off": TEXT_RED, "partial": TEXT_YELLOW, "mixed": TEXT_YELLOW}[kind]


def render_itab(filename, title, rows, multi):
    W, H = 480, 10 + 32 + 8 + ROW_H + len(rows) * ROW_H + 10 + 40 + 10 + 30 + 10
    ctx = IMGUIContext(W, H)
    ctx.draw_texture(Rect(0, 0, W, H), ctx.solid_tex(BG))
    x, y, w = MARGIN, MARGIN, W - 2 * MARGIN

    ctx.font = GameFont.MEDIUM
    ctx.anchor = TextAnchor.UPPER_LEFT
    ctx.gui_color = TEXT
    ctx.label(Rect(x, y, w, 28), title)
    y += 32
    draw_hline(ctx, x, y, w)
    y += GAP + 2

    ctx.font = GameFont.SMALL
    ctx.gui_color = TEXT_DIM
    ctx.label(Rect(x, y, w, ROW_H), "当前配置" + ("(多门合并)" if multi else ""))
    y += ROW_H
    for name, state, kind in rows:
        ctx.anchor = TextAnchor.UPPER_LEFT
        ctx.gui_color = TEXT
        ctx.label(Rect(x + 8, y, w * 0.5, ROW_H), name)
        ctx.anchor = TextAnchor.UPPER_RIGHT
        ctx.gui_color = state_color(kind)
        ctx.label(Rect(x + 8 + w * 0.5, y, w * 0.5 - 8, ROW_H), state)
        y += ROW_H
    y += 2
    draw_hline(ctx, x, y, w)
    y += GAP + 2

    ctx.anchor = TextAnchor.UPPER_LEFT
    ctx.font = GameFont.TINY
    ctx.gui_color = TEXT_DIM
    ctx.label(Rect(x, y, w, 34), NOTE)
    y += 40

    buttons = ["编辑…", "复制", "粘贴", "重置默认"]
    n = len(buttons)
    bw = (w - (n - 1) * GAP) / n
    for i, b in enumerate(buttons):
        draw_button(ctx, Rect(x + i * (bw + GAP), y, bw, 30), b)

    ctx.save(str(OUT / filename))
    print("saved", OUT / filename)


def render_edit_dialog(filename, title, toggles, modes):
    """编辑弹窗:复选框行 + 档位行。"""
    W, H = 420, 10 + 32 + 8 + (len(toggles) + len(modes)) * (ROW_H + 2) + 10 + 34 + 10
    ctx = IMGUIContext(W, H)
    ctx.draw_texture(Rect(0, 0, W, H), ctx.solid_tex(BG))
    x, y, w = MARGIN, MARGIN, W - 2 * MARGIN

    ctx.font = GameFont.MEDIUM
    ctx.anchor = TextAnchor.UPPER_LEFT
    ctx.gui_color = TEXT
    ctx.label(Rect(x, y, w, 28), title)
    y += 32
    draw_hline(ctx, x, y, w)
    y += GAP + 2

    for label, checked in toggles:
        draw_checkbox(ctx, Rect(x + 4, y, w - 4, ROW_H), label, checked)
        y += ROW_H + 2
    for label, mode_text, kind in modes:
        ctx.anchor = TextAnchor.UPPER_LEFT
        ctx.font = GameFont.SMALL
        ctx.gui_color = TEXT
        ctx.label(Rect(x + 4, y, w * 0.55, ROW_H), label)
        bw2 = w * 0.45 - 8
        draw_button(ctx, Rect(x + 4 + w * 0.55, y, bw2, ROW_H), mode_text)
        y += ROW_H + 2
    y += 4
    draw_hline(ctx, x, y, w)
    y += GAP + 2

    ctx.anchor = TextAnchor.UPPER_LEFT
    ctx.font = GameFont.TINY
    ctx.gui_color = TEXT_DIM
    ctx.label(Rect(x, y, w, 30), NOTE)

    ctx.save(str(OUT / filename))
    print("saved", OUT / filename)


render_itab(
    "v2_single_default.png",
    "门锁",
    [
        ("殖民者", "允许", "on"),
        ("奴隶", "允许", "on"),
        ("访客(含外来动物)", "允许", "on"),
        ("商队", "允许", "on"),
        ("殖民地动物", "全部允许", "on"),
        ("殖民地机械体", "全部允许", "on"),
    ],
    multi=False,
)

render_itab(
    "v2_multi_mixed.png",
    "门锁(3 扇,忽略 1 扇)",
    [
        ("殖民者", "允许", "on"),
        ("奴隶", "禁止", "off"),
        ("访客(含外来动物)", "允许", "on"),
        ("商队", "混合", "mixed"),
        ("殖民地动物", "仅宠物", "partial"),
        ("殖民地机械体", "仅受控", "partial"),
    ],
    multi=True,
)

render_edit_dialog(
    "v2_edit_dialog.png",
    "编辑门锁配置(3 扇)",
    [
        ("殖民者", True),
        ("奴隶", False),
        ("访客(含外来动物)", True),
        ("商队(混合,点击统一)", True),
    ],
    [
        ("殖民地动物", "仅宠物", "partial"),
        ("殖民地机械体", "仅受控", "partial"),
    ],
)
