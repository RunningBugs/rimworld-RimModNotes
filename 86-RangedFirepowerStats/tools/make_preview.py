"""Select a real native screenshot as cover and encode the Workshop gallery.

No values, labels or image content are retouched. The cover is a lossless PNG;
gallery JPEGs retain the original screenshot dimensions.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
from PIL import Image, ImageChops

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--gallery-case", default="media-wide-final")
parser.add_argument("--cover-case", default="media-cover-final")
args = parser.parse_args()
gallery = root / ".build/testing/cases" / args.gallery_case / "SaveData"
cover = root / ".build/testing/cases" / args.cover_case / "SaveData/graph-dps.png"
about = root / "About"
budget = 1_000_000
manifest = []

with Image.open(cover) as source:
    rgb = source.convert("RGB")
    assert source.size == (1024, 1024), "cover must be the native square screenshot"
    destination = about / "Preview.png"
    rgb.save(destination, optimize=True, compress_level=9)
    with Image.open(destination) as encoded:
        assert ImageChops.difference(rgb, encoded.convert("RGB")).getbbox() is None
    assert destination.stat().st_size < budget, "cover exceeds upload budget"
    manifest.append({"file": destination.name, "source": str(cover.relative_to(root)),
        "kind": "native screenshot, lossless PNG", "size": list(rgb.size), "bytes": destination.stat().st_size,
        "sha256": hashlib.sha256(destination.read_bytes()).hexdigest()})

images = [
    ("info-overview.png", "01-Info-Page.jpg", "信息页面与独立数据分类"),
    ("pawn-card.png", "02-Range-Table.jpg", "右侧逐格表及图表超链接"),
    ("accuracy-card.png", "03-Precision-Link.jpg", "原版射击精度说明及曲线链接"),
    ("table.png", "04-DPS-Comparison.jpg", "当前天气下的三情景逐格对比"),
    ("graph-dps.png", "05-DPS-Curves.jpg", "DPS 折线图与悬停读数"),
    ("graph-accuracy.png", "06-Accuracy-Curves.jpg", "命中率折线图"),
]
for filename, name, caption in images:
    with Image.open(gallery / filename) as source:
        rgb = source.convert("RGB")
        for quality in [96, 94, 92, 90, 88]:
            buffer = io.BytesIO()
            rgb.save(buffer, format="JPEG", quality=quality, subsampling=0, optimize=True)
            if buffer.tell() < budget: break
        assert buffer.tell() < budget, name
        destination = about / name
        destination.write_bytes(buffer.getvalue())
        manifest.append({"file": name, "caption": caption, "source": str((gallery / filename).relative_to(root)),
            "kind": "native screenshot, JPEG", "size": list(rgb.size), "quality": quality,
            "bytes": destination.stat().st_size, "sha256": hashlib.sha256(destination.read_bytes()).hexdigest()})

(root / "docs/workshop-media.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
print(json.dumps(manifest, ensure_ascii=False, indent=2))
