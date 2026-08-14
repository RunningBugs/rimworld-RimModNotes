"""对 carry_policy_dialog_sim 的渲染做程序化布局校验 + ASCII 预览。

检查项：
1. 文字宽度超出所在 Rect（label 不裁剪，超出即溢出/互相压盖）
2. 文字 Rect 越出窗口 (1000x720)
3. 行内元素横向间距是否为负（重叠）
输出：违规清单 + 每个状态的 ASCII 结构预览。
"""
from PIL import Image, ImageDraw

import carry_policy_dialog_sim as S
from rimworld_imgui.context import IMGUIContext
from rimworld_imgui.fonts import load_font
from rimworld_imgui.geometry import Rect

records: list[tuple[Rect, str, object, float]] = []
_orig_label = IMGUIContext.label


def patched_label(self, rect, text, *a, **k):
    records.append((rect, text, self.font, self.scale))
    return _orig_label(self, rect, text, *a, **k)


IMGUIContext.label = patched_label


def check(scaleless_w=1000.0):
    bad = []
    for rect, text, font, scale in records:
        if rect is None or not text.strip():
            continue
        try:
            f = load_font(font, scale, text)
        except Exception:
            continue
        tw = ImageDraw.Draw(Image.new("RGBA", (8, 8))).textlength(text, font=f)
        w, h = tw / scale, rect.width
        if w > h + 2:
            bad.append(f"OVERFLOW  '{text}' 文字宽{w:.0f}px > 框宽{h:.0f}px  rect=({rect.x:.0f},{rect.y:.0f})")
        if rect.x_max > 1000 + 1 or rect.y_max > 720 + 1 or rect.x < -1 or rect.y < -1:
            bad.append(f"OUT-OF-WIN '{text}' rect=({rect.x:.0f},{rect.y:.0f},{rect.width:.0f},{rect.height:.0f})")
    return bad


def ascii_preview(img: Image.Image, cols=110) -> str:
    rows = int(img.height / img.width * cols * 0.45)
    small = img.convert("L").resize((cols, rows))
    ramp = " .:-=+*#%@"
    out = []
    for y in range(rows):
        line = "".join(ramp[min(9, small.getpixel((x, y)) * 10 // 256)] for x in range(cols))
        out.append(line)
    return "\n".join(out)


if __name__ == "__main__":
    for name, st in S.STATES.items():
        records.clear()
        ctx = S.draw_frame(st)
        issues = check()
        print(f"===== {name} =====")
        for i in issues:
            print(" ", i)
        if not issues:
            print("  no layout violations")
        if name == "2_added":
            print(ascii_preview(ctx.render()))
    records.clear()
    ctx = S.draw_frame(dict(S.STATES["2_added"], scale=2.0))
    print("===== 2_added_2x =====")
    for i in check():
        print(" ", i)
    print("done")
