#!/usr/bin/env python3
"""Generate a static UsefulStats UI mock screenshot from the current C# layout constants.

This is not a RimWorld runtime renderer. It is a fast design-review artifact that
parses the table column widths from MainTabWindow_UsefulStats.cs and draws a
representative screenshot for visual iteration.
"""
from __future__ import annotations

import argparse
import re
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
CS = ROOT / "1.6/Source/UI/MainTabWindow_UsefulStats.cs"


def constants() -> dict[str, float]:
    text = CS.read_text(encoding="utf-8")
    out: dict[str, float] = {}
    for name, value in re.findall(r"private const float (\w+) = ([0-9.]+)f;", text):
        out[name] = float(value)
    return out


def font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for path in [
        "/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc",
        "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
    ]:
        if Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def fit(draw: ImageDraw.ImageDraw, text: str, fnt, width: int) -> str:
    if draw.textlength(text, font=fnt) <= width:
        return text
    ell = "…"
    while text and draw.textlength(text + ell, font=fnt) > width:
        text = text[:-1]
    return text + ell


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default=str(ROOT / "tmp/usefulstats-ui-mockup.png"))
    args = parser.parse_args()

    c = constants()
    W, H = 1875, 1088
    img = Image.new("RGB", (W, H), (22, 25, 28))
    d = ImageDraw.Draw(img)
    f = font(24)
    small = font(20)
    header_font = font(22)

    # Window frame
    d.rectangle([0, 0, W - 1, H - 1], outline=(92, 105, 112), width=2)
    d.line([28, 34, 1468, 34], fill=(115, 121, 124), width=1)
    d.text((1838, 4), "×", fill=(185, 190, 194), font=font(42))

    # Filters: Kind is now a compact picker button instead of dozens of always-visible tabs.
    y = 52
    d.rectangle([28, y, 248, y + 36], fill=(65, 72, 84), outline=(110, 115, 125), width=2)
    d.text((42, y + 6), "Kind: All", fill=(235, 235, 235), font=small)
    d.text((270, y + 8), "Current", fill=(230, 230, 230), font=f)
    d.text((420, y), "✓", fill=(0, 210, 48), font=font(44))
    d.text((464, y + 8), "Future", fill=(230, 230, 230), font=f)
    d.text((602, y), "✓", fill=(0, 210, 48), font=font(44))
    d.text((660, y + 8), "All-only", fill=(230, 230, 230), font=f)
    d.text((787, y), "✓", fill=(0, 210, 48), font=font(44))
    d.text((842, y + 8), "Search", fill=(230, 230, 230), font=f)
    d.rectangle([922, y, 1182, y + 36], fill=(6, 8, 10), outline=(155, 160, 168), width=1)
    d.text((932, y + 5), "奴隶项圈", fill=(235, 235, 235), font=small)
    d.text((1202, y + 8), "Material", fill=(230, 230, 230), font=f)
    d.rectangle([1292, y, 1452, y + 36], fill=(6, 8, 10), outline=(155, 160, 168), width=1)
    d.text((1302, y + 5), "cloth", fill=(235, 235, 235), font=small)
    d.rectangle([1470, y, 1568, y + 40], fill=(65, 72, 84), outline=(110, 115, 125), width=2)
    d.text((1480, y + 8), "Expand all", fill=(235, 235, 235), font=small)
    d.rectangle([1580, y, 1685, y + 40], fill=(65, 72, 84), outline=(110, 115, 125), width=2)
    d.text((1588, y + 8), "Collapse all", fill=(235, 235, 235), font=small)
    d.rectangle([1700, y, 1783, y + 40], fill=(122, 90, 48), outline=(65, 48, 26), width=3)
    d.text((1717, y + 8), "Refresh", fill=(245, 235, 215), font=small)
    d.text((28, y + 43), "18 / 620 items, 42 visible rows    Choose Kind from the picker; click material rows to expand variants.", fill=(185, 198, 240), font=small)

    # Table
    table_x, table_y = 28, 132
    table_w, table_h = 1820, 928
    d.rectangle([table_x, table_y, table_x + table_w, table_y + table_h], fill=(38, 40, 42), outline=(128, 132, 138), width=2)
    header_y = table_y + 6
    cols = [
        ("Item", "NameWidth", ""),
        ("Work", "WorkWidth", ""),
        ("Material", "MaterialWidth", ""),
        ("Value", "ValueWidth", ""),
        ("Mat/Work", "MaterialWorkWidth", ""),
        ("Val/Work", "ValueWorkWidth", "▼"),
        ("Val/Mat", "ValueMaterialWidth", ""),
    ]
    x = table_x + 8
    col_pos = []
    for label, key, arrow in cols:
        w = int(c[key])
        col_pos.append((x, w))
        d.text((x + 2, header_y + 8), label, fill=(188, 198, 230), font=header_font)
        if arrow:
            d.text((x + w - 28, header_y + 7), arrow, fill=(255, 220, 90), font=header_font)
        x += w

    rows = [
        ["+ 奴隶项圈", "30", "25-250 x stuff (24)", "106 银", "0.01-0.15", "0.05", "4.2"],
        ["    ↳ cloth (default)", "30", "25 x cloth", "106 银", "0.01", "0.05", "4.2"],
        ["    ↳ gold", "27", "250 x gold", "2505 银", "0.15", "1.55", "10.0"],
        ["风雪大衣", "2,500", "80 x cloth", "480 银", "0.03", "0.19", "6.0"],
        ["长剑", "4,500", "50-500 x stuff (12)", "450 银", "0.01-0.11", "0.10", "1.8"],
        ["高级研究台", "12,000", "—", "700 银", "—", "0.06", "—"],
        ["debug-only widget", "100", "—", "0 银", "—", "0", "—"],
    ]
    # Fill viewport with repetitions to show virtual scroll visual density.
    base = rows[:]
    while len(rows) < 24:
        rows.extend(base)

    y = table_y + 42
    row_h = int(c["RowHeight"])
    for i, row in enumerate(rows[:24]):
        bg = (49, 51, 53) if i % 2 == 0 else (38, 40, 42)
        if i == 4:
            bg = (72, 75, 78)
        d.rectangle([table_x + 2, y, table_x + table_w - 30, y + row_h], fill=bg)
        for (cx, cw), cell in zip(col_pos, row):
            d.text((cx + 4, y + 5), fit(d, cell, small, cw - 8), fill=(230, 230, 230), font=small)
        y += row_h

    # Scroll bar
    sx = table_x + table_w - 24
    d.rounded_rectangle([sx, table_y + 48, sx + 16, table_y + table_h - 8], radius=8, fill=(6, 6, 6), outline=(80, 80, 80))
    d.rounded_rectangle([sx + 2, table_y + 65, sx + 14, table_y + 250], radius=6, fill=(86, 90, 96))

    # Open Kind picker menu preview: first row is a text filter, followed by selectable options.
    menu_x, menu_y, menu_w, menu_h = 28, 92, 360, 330
    d.rectangle([menu_x, menu_y, menu_x + menu_w, menu_y + menu_h], fill=(34, 36, 39), outline=(128, 132, 138), width=2)
    d.rectangle([menu_x + 8, menu_y + 8, menu_x + menu_w - 8, menu_y + 38], fill=(6, 8, 10), outline=(155, 160, 168), width=1)
    d.text((menu_x + 18, menu_y + 12), "Filter kind...", fill=(150, 150, 150), font=small)
    kinds = ["All (620)", "Foods (42)", "Manufactured (88)", "Raw resources (36)", "Items (64)", "Weapons (80)", "Apparel (220)", "Buildings (90)"]
    yy = menu_y + 48
    for i, kind in enumerate(kinds):
        if i == 0:
            d.rectangle([menu_x + 8, yy, menu_x + menu_w - 24, yy + 30], fill=(65, 72, 84))
        elif i % 2 == 0:
            d.rectangle([menu_x + 8, yy, menu_x + menu_w - 24, yy + 30], fill=(42, 44, 47))
        d.text((menu_x + 16, yy + 5), kind, fill=(235, 235, 235), font=small)
        yy += 30

    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    img.save(out)
    print(out)


if __name__ == "__main__":
    main()
