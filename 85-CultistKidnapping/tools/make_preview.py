"""Generate the black-and-white Workshop preview. Run in this mod's .venv."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
image = Image.new("RGB", (1024, 1024), "black")
draw = ImageDraw.Draw(image)
font_root = Path("/usr/share/fonts")
chinese = font_root / "opentype/noto/NotoSansCJK-Regular.ttc"
bold = font_root / "truetype/noto/NotoSans-Bold.ttf"
regular = font_root / "truetype/noto/NotoSans-Regular.ttf"


def text(value, y, size, font):
    face = ImageFont.truetype(str(font), size)
    draw.text((512, y), value, font=face, fill="white", anchor="mm")


text("邪教徒绑架", 344, 118, chinese)
text("CULTIST", 489, 92, bold)
text("KIDNAPPING", 598, 92, bold)
draw.line((200, 705, 824, 705), fill="white", width=2)
text("RIMWORLD  ·  ANOMALY", 759, 33, regular)
text("RunningBugs", 928, 26, regular)
destination = root / "About/Preview.png"
image.save(destination, optimize=True)
print(destination)
