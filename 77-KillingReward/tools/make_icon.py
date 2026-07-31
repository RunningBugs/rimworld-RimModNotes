#!/usr/bin/env python3
"""Generate a 64x64 blood-drop icon for the main button / letter."""
from pathlib import Path
from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent.parent / "Textures" / "UI" / "Icons" / "KillingReward.png"
OUT.parent.mkdir(parents=True, exist_ok=True)

img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
# 血滴：圆 + 上方三角，暗红主体 + 高光
d.ellipse((14, 22, 50, 58), fill=(140, 16, 16, 255))
d.polygon([(32, 4), (16, 34), (48, 34)], fill=(140, 16, 16, 255))
d.ellipse((24, 34, 34, 44), fill=(220, 90, 90, 255))
img.save(OUT)
print(f"wrote {OUT}")
