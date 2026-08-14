"""CarryPolicy 管理弹窗 UI 原型（Dialog_ManageCarryPolicies mockup）。

模拟原版 Dialog_ManagePolicies 布局：左侧方案列表 + 右侧双栏内容区
（左=携带清单条目表，右=类别/搜索/多选可选项）。
sim 缺少按钮/复选框等控件，均用纯色矩形 + label 手绘近似。

运行：rimworld-imgui-sim/.venv/bin/python carry_policy_dialog_sim.py
输出：out/*.png
"""
from pathlib import Path

from rimworld_imgui import IMGUIContext, Rect, Color, GameFont, TextAnchor

# ---------- 布局常量（写 C# 时直接照搬） ----------
WIN_W, WIN_H = 1000, 720
MARGIN = 10
TITLE = Rect(MARGIN, 8, WIN_W - 2 * MARGIN, 32)
TIP = Rect(MARGIN, 42, WIN_W - 2 * MARGIN, 18)
LEFT_HEADER = Rect(MARGIN, 64, 200, 32)          # “可用方案”
LEFT_LIST = Rect(MARGIN, 100, 200, 502)          # 方案列表框
LEFT_NEW_BTN = Rect(MARGIN, 612, 200, 32)        # “新建”
LEFT_SEARCH = Rect(MARGIN, 654, 200, 24)         # 方案搜索
POLICY_ROW_H = 32

RIGHT_X0 = 220
ICON_BTN = 32                                    # 右上 4 个方形按钮
CONTENT = Rect(RIGHT_X0, 100, WIN_W - MARGIN - RIGHT_X0, 578)

LCOL_W = 460                                     # 携带清单列宽
LCOL_HDR_H = 26
LCOL = Rect(RIGHT_X0, 100, LCOL_W, LCOL_HDR_H)   # 表头
LCOL_LIST = Rect(RIGHT_X0, 100 + LCOL_HDR_H, LCOL_W, 508 - LCOL_HDR_H + 26 - 26)
LCOL_LIST = Rect(RIGHT_X0, 130, LCOL_W, 508)     # 条目列表框（35px 行）
LCOL_CLEAR = Rect(RIGHT_X0, 646, 96, 32)         # “清除全部”
ENTRY_ROW_H = 35

RCOL_X0 = RIGHT_X0 + LCOL_W + 10                 # 可选择列 x
RCOL_W = WIN_W - MARGIN - RCOL_X0                # 300
RCOL_CATS = Rect(RCOL_X0, 100, 132, 26)          # 类别下拉
RCOL_SEARCH = Rect(RCOL_X0 + 140, 100, RCOL_W - 140, 24)  # 搜索框
RCOL_LIST = Rect(RCOL_X0, 130, RCOL_W, 508)      # 可选项列表框（30px 行）
RCOL_SEL_LBL = Rect(RCOL_X0, 646, 140, 32)       # “已选 N 项”
RCOL_ADD_BTN = Rect(WIN_W - MARGIN - 110, 646, 110, 32)    # “添加所选”
OPT_ROW_H = 30

CLOSE = Rect((WIN_W - 120) / 2, 684, 120, 32)

# ---------- 近似配色（对标 RimWorld 贴图色，迭代调） ----------
C_WIN_BG = Color(0.13, 0.13, 0.15)
C_WIN_BORDER = Color(0.55, 0.53, 0.48)
C_SECTION = Color(0.22, 0.22, 0.24)              # DrawMenuSection
C_SECTION_BORDER = Color(0.10, 0.10, 0.10)
C_BTN_BG = Color(0.30, 0.30, 0.33)
C_TEXT = Color(0.92, 0.92, 0.92)
C_TEXT_GREY = Color(0.58, 0.58, 0.58)
C_TEXT_DIM = Color(0.45, 0.45, 0.45)
C_SEL_HIGHLIGHT = Color(0.35, 0.42, 0.50, 0.85)  # DrawHighlightSelected 近似
C_LIGHT_HIGHLIGHT = Color(0.5, 0.5, 0.5, 0.16)   # DrawLightHighlight 近似
C_HOVER = Color(0.6, 0.6, 0.6, 0.25)
C_ICON_BG = Color(0.45, 0.45, 0.52)
C_FIELD_BG = Color(0.13, 0.13, 0.13)

TEXT = lambda: None  # placeholder to keep flake quiet


def border(ctx: IMGUIContext, rect: Rect, color: Color, thick: float = 1.0):
    t = thick
    tex = ctx.solid_tex(color)
    for r in (Rect(rect.x, rect.y, rect.width, t),
              Rect(rect.x, rect.y_max - t, rect.width, t),
              Rect(rect.x, rect.y + t, t, rect.height - 2 * t),
              Rect(rect.x_max - t, rect.y + t, t, rect.height - 2 * t)):
        ctx.draw_texture(r, tex, color=color)


def panel(ctx: IMGUIContext, rect: Rect, fill: Color, bcol: Color):
    ctx.draw_texture(rect, ctx.solid_tex(fill), color=fill)
    border(ctx, rect, bcol)


def button(ctx: IMGUIContext, rect: Rect, label: str, font=GameFont.SMALL):
    ctx.draw_texture(rect, ctx.solid_tex(C_BTN_BG), color=C_BTN_BG)
    border(ctx, rect, Color(0, 0, 0))
    ctx.font, ctx.anchor, ctx.gui_color = font, TextAnchor.MIDDLE_CENTER, C_TEXT
    ctx.label(rect, label)


def checkbox(ctx: IMGUIContext, rect: Rect, checked: bool, enabled: bool = True):
    bg = C_FIELD_BG if enabled else Color(0.18, 0.18, 0.18)
    ctx.draw_texture(rect, ctx.solid_tex(bg), color=bg)
    border(ctx, rect, Color(0.75, 0.75, 0.75) if enabled else Color(0.4, 0.4, 0.4))
    if checked:
        inner = rect.contracted_by(3)
        ctx.draw_texture(inner, ctx.solid_tex(Color(1, 1, 1)), color=Color(1, 1, 1))


def icon_sq(ctx: IMGUIContext, rect: Rect, glyph: str):
    ctx.draw_texture(rect, ctx.solid_tex(C_ICON_BG), color=C_ICON_BG)
    border(ctx, rect, Color(0.2, 0.2, 0.2))
    ctx.font, ctx.anchor, ctx.gui_color = GameFont.TINY, TextAnchor.MIDDLE_CENTER, Color(0.1, 0.1, 0.1)
    ctx.label(rect, glyph)


def info_btn(ctx: IMGUIContext, cx: float, cy: float):
    r = Rect(cx - 8, cy - 8, 16, 16)
    ctx.draw_texture(r, ctx.solid_tex(Color(0.2, 0.35, 0.2)), color=Color(0.2, 0.35, 0.2))
    border(ctx, r, Color(0.5, 0.7, 0.5))
    ctx.font, ctx.anchor, ctx.gui_color = GameFont.TINY, TextAnchor.MIDDLE_CENTER, C_TEXT
    ctx.label(r, "i")


def close_x(ctx: IMGUIContext, rect: Rect):
    ctx.draw_texture(rect, ctx.solid_tex(Color(0.4, 0.16, 0.16)), color=Color(0.4, 0.16, 0.16))
    border(ctx, rect, Color(0.7, 0.4, 0.4))
    ctx.font, ctx.anchor, ctx.gui_color = GameFont.TINY, TextAnchor.MIDDLE_CENTER, C_TEXT
    ctx.label(rect, "×")


def draw_policy_list(ctx: IMGUIContext, state: dict):
    panel(ctx, LEFT_LIST, C_SECTION, C_SECTION_BORDER)
    policies = state.get("policies", [])
    y = LEFT_LIST.y
    for i, (name, is_default) in enumerate(policies):
        row = Rect(LEFT_LIST.x, y, LEFT_LIST.width, POLICY_ROW_H)
        inner = Rect(row.x + 10, row.y, row.width - 10, row.height)
        if state.get("selected_policy") == i:
            ctx.draw_texture(row, ctx.solid_tex(C_SEL_HIGHLIGHT), color=C_SEL_HIGHLIGHT)
        elif state.get("hover_policy") == i:
            ctx.draw_texture(row, ctx.solid_tex(C_HOVER), color=C_HOVER)
        elif i % 2 == 1:
            ctx.draw_texture(row, ctx.solid_tex(C_LIGHT_HIGHLIGHT), color=C_LIGHT_HIGHLIGHT)
        ctx.font, ctx.anchor = GameFont.SMALL, TextAnchor.MIDDLE_LEFT
        ctx.gui_color = C_TEXT
        label = name + ("*" if is_default else "")
        ctx.label(inner, label)
        y += POLICY_ROW_H
    # 搜索行
    s = LEFT_SEARCH
    ctx.draw_texture(s, ctx.solid_tex(C_FIELD_BG), color=C_FIELD_BG)
    border(ctx, s, Color(0.45, 0.45, 0.45))
    ctx.font, ctx.anchor, ctx.gui_color = GameFont.TINY, TextAnchor.MIDDLE_LEFT, C_TEXT_GREY
    ctx.label(s.contracted_by(2), "搜索…")
    button(ctx, LEFT_NEW_BTN, "新建")


def draw_entries(ctx: IMGUIContext, state: dict):
    # 表头
    hdr_y = LCOL.y
    ctx.font, ctx.anchor, ctx.gui_color = GameFont.SMALL, TextAnchor.MIDDLE_LEFT, C_TEXT_GREY
    ctx.label(Rect(LCOL.x + 5, hdr_y, 200, LCOL_HDR_H), "标签")
    ctx.anchor = TextAnchor.MIDDLE_CENTER
    ctx.label(Rect(LCOL.x + 220, hdr_y, 70, LCOL_HDR_H), "数量 ⓘ")
    ctx.anchor = TextAnchor.MIDDLE_LEFT
    ctx.font = GameFont.TINY
    ctx.gui_color = C_TEXT_DIM
    ctx.label(Rect(LCOL.x + 298, hdr_y, 130, LCOL_HDR_H), "≈件数(按普通小人)")
    # 列表框
    panel(ctx, LCOL_LIST, C_SECTION, C_SECTION_BORDER)
    entries = state.get("entries", [])
    if not entries:
        area = LCOL_LIST.contracted_by(10)
        ctx.font, ctx.anchor, ctx.gui_color = GameFont.MEDIUM, TextAnchor.MIDDLE_CENTER, C_TEXT_GREY
        ctx.label(Rect(area.x, area.y, area.width, area.height / 2), "清单为空")
        ctx.font = GameFont.SMALL
        ctx.gui_color = C_TEXT_DIM
        ctx.label(Rect(area.x, area.y + area.height / 2, area.width, area.height / 2),
                  "在右侧勾选物品后点击「添加所选」")
        return
    y = LCOL_LIST.y
    for i, e in enumerate(entries):
        row = Rect(LCOL_LIST.x, y, LCOL_LIST.width, ENTRY_ROW_H)
        if i % 2 == 1:
            ctx.draw_texture(row, ctx.solid_tex(C_LIGHT_HIGHLIGHT), color=C_LIGHT_HIGHLIGHT)
        x = row.x + 5
        icon_sq(ctx, Rect(x, row.y + 4, 27, 27), e["icon"])
        x += 33
        ctx.font, ctx.anchor, ctx.gui_color = GameFont.SMALL, TextAnchor.MIDDLE_LEFT, C_TEXT
        ctx.label(Rect(x, row.y, 145, row.height), e["name"])
        info_btn(ctx, x + 150 + 8, row.y + row.height / 2)
        # 数量输入框（编辑态 / 静态）
        f = Rect(row.x + 220, row.y + 6, 70, ENTRY_ROW_H - 12)
        if state.get("editing_entry") == i:
            ctx.text_field_numeric(f, state.get("edit_buffer", "1"))
        else:
            ctx.draw_texture(f, ctx.solid_tex(C_FIELD_BG), color=C_FIELD_BG)
            border(ctx, f, Color(0.3, 0.3, 0.3))
            ctx.font, ctx.anchor, ctx.gui_color = GameFont.TINY, TextAnchor.MIDDLE_CENTER, C_TEXT
            ctx.label(f, e["count"])
        # ≈件数灰字 / 非食物无
        ctx.font, ctx.anchor, ctx.gui_color = GameFont.TINY, TextAnchor.MIDDLE_LEFT, C_TEXT_GREY
        ctx.label(Rect(row.x + 298, row.y, 100, row.height), e.get("hint", ""))
        close_x(ctx, Rect(row.x + 408, row.y + 8.5, 18, 18))
        y += ENTRY_ROW_H
    button(ctx, LCOL_CLEAR, "清除全部")


def draw_options(ctx: IMGUIContext, state: dict):
    # 类别下拉
    d = RCOL_CATS
    ctx.draw_texture(d, ctx.solid_tex(C_BTN_BG), color=C_BTN_BG)
    border(ctx, d, Color(0, 0, 0))
    ctx.font, ctx.anchor, ctx.gui_color = GameFont.SMALL, TextAnchor.MIDDLE_LEFT, C_TEXT
    ctx.label(d.contracted_by(2), state.get("category", "食物"))
    ctx.anchor = TextAnchor.MIDDLE_RIGHT
    ctx.label(d.contracted_by(2), "▼")
    # 搜索框
    s = RCOL_SEARCH
    ctx.draw_texture(s, ctx.solid_tex(C_FIELD_BG), color=C_FIELD_BG)
    border(ctx, s, Color(0.45, 0.45, 0.45))
    ctx.font, ctx.anchor = GameFont.TINY, TextAnchor.MIDDLE_LEFT
    if state.get("search_text"):
        ctx.gui_color = C_TEXT
        ctx.label(s.contracted_by(2), state["search_text"])
    else:
        ctx.gui_color = C_TEXT_GREY
        ctx.label(s.contracted_by(2), "搜索…")
    # 可选项列表
    panel(ctx, RCOL_LIST, C_SECTION, C_SECTION_BORDER)
    opts = state.get("options", [])
    y = RCOL_LIST.y
    for i, o in enumerate(opts):
        row = Rect(RCOL_LIST.x, y, RCOL_LIST.width, OPT_ROW_H)
        if o.get("added"):
            ctx.font, ctx.anchor = GameFont.SMALL, TextAnchor.MIDDLE_LEFT
            ctx.gui_color = C_TEXT_DIM
            ctx.label(Rect(row.x + 34, row.y, 160, row.height), o["name"])
            ctx.font = GameFont.TINY
            ctx.anchor = TextAnchor.MIDDLE_RIGHT
            ctx.label(Rect(row.x + 32, row.y, RCOL_LIST.width - 40, row.height), "已添加")
        else:
            if o.get("checked"):
                ctx.draw_texture(row, ctx.solid_tex(C_HOVER), color=C_HOVER)
            elif i % 2 == 1:
                ctx.draw_texture(row, ctx.solid_tex(C_LIGHT_HIGHLIGHT), color=C_LIGHT_HIGHLIGHT)
            checkbox(ctx, Rect(row.x + 8, row.y + 6, 18, 18), o.get("checked", False))
            ctx.font, ctx.anchor, ctx.gui_color = GameFont.SMALL, TextAnchor.MIDDLE_LEFT, C_TEXT
            ctx.label(Rect(row.x + 34, row.y, 200, row.height), o["name"])
        y += OPT_ROW_H
    # 底部
    ctx.font, ctx.anchor, ctx.gui_color = GameFont.SMALL, TextAnchor.MIDDLE_LEFT, C_TEXT_GREY
    ctx.label(RCOL_SEL_LBL, state.get("selected_label", "已选 0 项"))
    button(ctx, RCOL_ADD_BTN, state.get("add_btn_label", "添加所选"))


def draw_frame(state: dict) -> IMGUIContext:
    ctx = IMGUIContext(WIN_W, WIN_H, scale=state.get("scale", 1.0))
    # 窗口底
    ctx.draw_texture(Rect(0, 0, WIN_W, WIN_H), ctx.solid_tex(C_WIN_BG), color=C_WIN_BG)
    border(ctx, Rect(0, 0, WIN_W, WIN_H), C_WIN_BORDER, thick=2)
    # 标题 / 提示
    ctx.font, ctx.anchor, ctx.gui_color = GameFont.MEDIUM, TextAnchor.UPPER_LEFT, C_TEXT
    ctx.label(TITLE, "管理携带方案")
    ctx.font, ctx.gui_color = GameFont.TINY, C_TEXT_GREY
    ctx.label(TIP, "为殖民地配置通用携带清单，在「分配」页签中指派给小人。数量单位「份」≠「个」：1份=普通小人一顿的量")
    # 左侧：可用方案
    ctx.font, ctx.anchor, ctx.gui_color = GameFont.MEDIUM, TextAnchor.MIDDLE_LEFT, C_TEXT
    ctx.label(LEFT_HEADER, "可用方案")
    draw_policy_list(ctx, state)
    # 右侧：方案名 + 4 个操作按钮
    name = state.get("policy_name", "默认携带方案")
    ctx.font = GameFont.MEDIUM
    ctx.gui_color = C_TEXT
    ctx.label(Rect(RIGHT_X0, 64, 560, 32), name)
    ctx.gui_color = C_TEXT_GREY
    ctx.font = GameFont.SMALL
    ctx.label(Rect(RIGHT_X0 + 250, 64, 300, 32), " (默认)")
    bx = WIN_W - MARGIN - ICON_BTN
    for glyph, hint in (("★", "默认"), ("改", "重命名"), ("▣", "复制"), ("×", "删除")):
        r = Rect(bx, 64, ICON_BTN, ICON_BTN)
        ctx.draw_texture(r, ctx.solid_tex(C_BTN_BG), color=C_BTN_BG)
        border(ctx, r, Color(0, 0, 0))
        ctx.font, ctx.anchor, ctx.gui_color = GameFont.SMALL, TextAnchor.MIDDLE_CENTER, C_TEXT
        ctx.label(r, glyph)
        bx -= ICON_BTN + 10
    # 双栏内容
    draw_entries(ctx, state)
    draw_options(ctx, state)
    # 关闭
    button(ctx, CLOSE, "关闭")
    return ctx


# ---------- 各状态 ----------
POLICIES = [("默认携带方案", True), ("远行", False), ("战斗", False)]
OPTS_FOOD = [
    {"name": "简单食物", "checked": False},
    {"name": "营养膏", "checked": False},
    {"name": "牛奶", "checked": False, "added": True},
    {"name": "奢华食物", "checked": False},
    {"name": "精致食物", "checked": False},
    {"name": "生存饲料", "checked": False},
    {"name": "干肉", "checked": False},
    {"name": "腊肉", "checked": False},
    {"name": "玉米(生)", "checked": False},
    {"name": "稻米(生)", "checked": False, "added": True},
    {"name": "草莓(生)", "checked": False},
]
ENTRIES_FULL = [
    {"icon": "饭", "name": "简单食物", "count": "1", "hint": "≈1件"},
    {"icon": "膏", "name": "营养膏", "count": "1", "hint": "≈1件"},
    {"icon": "奶", "name": "牛奶", "count": "1", "hint": "≈18件"},
    {"icon": "米", "name": "稻米(生)", "count": "2", "hint": "≈40件"},
    {"icon": "草", "name": "草药", "count": "5", "hint": "件"},
]

STATES = {
    "1_empty": dict(
        policies=POLICIES, selected_policy=0, policy_name="默认携带方案",
        entries=[], options=OPTS_FOOD, selected_label="已选 0 项"),
    "2_added": dict(
        policies=POLICIES, selected_policy=0, policy_name="默认携带方案",
        entries=ENTRIES_FULL,
        options=[dict(o) for o in OPTS_FOOD],
        selected_label="已选 2 项"),
    "3_editing": dict(
        policies=POLICIES, selected_policy=0, policy_name="默认携带方案",
        entries=[dict(e) for e in ENTRIES_FULL],
        options=[dict(o) for o in OPTS_FOOD],
        editing_entry=3, edit_buffer="2.5",
        selected_label="已选 2 项"),
    "4_search": dict(
        policies=POLICIES, selected_policy=0, policy_name="默认携带方案",
        entries=[dict(e) for e in ENTRIES_FULL],
        search_text="营",
        options=[
            {"name": "营养膏", "checked": True},
            {"name": "营养膏机", "checked": False, "added": False},
        ],
        selected_label="已选 1 项"),
}
# 2_added：右栏勾选简单食物/营养膏
STATES["2_added"]["options"][0]["checked"] = True
STATES["2_added"]["options"][1]["checked"] = True
STATES["3_editing"]["options"][0]["checked"] = True
STATES["3_editing"]["options"][1]["checked"] = True

if __name__ == "__main__":
    out = Path(__file__).parent / "out"
    out.mkdir(exist_ok=True)
    for name, st in STATES.items():
        draw_frame(st).save(out / f"{name}.png")
    # 2x 放大检查细节
    z = dict(STATES["2_added"], scale=2.0)
    draw_frame(z).save(out / "2_added_2x.png")
    print("saved:", *(p.name for p in sorted(out.glob("*.png"))))
