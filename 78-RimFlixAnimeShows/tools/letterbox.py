#!/usr/bin/env python3
"""Letterbox show frames to Megascreen TV size with black side bars.

Scales each frame to the target height preserving aspect ratio, then pastes
it centered on a black canvas (default 902x256 Megascreen HD).

Usage: python3 letterbox.py <in_dir> <out_dir> [--size 902x256]
"""
import argparse
from pathlib import Path
from PIL import Image


def main():
    p = argparse.ArgumentParser()
    p.add_argument("in_dir")
    p.add_argument("out_dir")
    p.add_argument("--size", default="902x256", help="target canvas WxH (megascreen: 451x128 or 902x256)")
    args = p.parse_args()

    out_w, out_h = (int(v) for v in args.size.split("x"))
    in_dir, out_dir = Path(args.in_dir), Path(args.out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    frames = sorted(f for f in in_dir.iterdir() if f.suffix.lower() in (".png", ".jpg", ".jpeg"))
    for f in frames:
        img = Image.open(f).convert("RGB")
        scale = out_h / img.height
        new_w = round(img.width * scale)
        if new_w > out_w:  # 过宽则按宽度缩放(上下留黑)
            scale = out_w / img.width
            new_w = out_w
        resized = img.resize((new_w, round(img.height * scale)), Image.LANCZOS)
        canvas = Image.new("RGB", (out_w, out_h), (0, 0, 0))
        canvas.paste(resized, ((out_w - resized.width) // 2, (out_h - resized.height) // 2))
        canvas.save(out_dir / f.name)
    print(f"{len(frames)} frames -> {out_dir} ({out_w}x{out_h})")


if __name__ == "__main__":
    main()
