#!/usr/bin/env python3
"""Ken Burns frame generator: turn one still into a smooth pan/zoom loop.

Usage: python3 kenburns.py <src.png> <out_dir> [--frames 12] [--size 620x256] [--zoom 0.06] [--pan 0.05]

Frames ping-pong (forward then reversed) so the RimFlix loop has no jump cut.
"""
import argparse
from pathlib import Path
from PIL import Image


def main():
    p = argparse.ArgumentParser()
    p.add_argument("src")
    p.add_argument("out_dir")
    p.add_argument("--frames", type=int, default=12, help="one-way frame count (ping-pong doubles it minus endpoints)")
    p.add_argument("--size", default="620x256")
    p.add_argument("--zoom", type=float, default=0.06, help="extra zoom over the pan, fraction")
    p.add_argument("--pan", type=float, default=0.05, help="horizontal travel as fraction of width")
    args = p.parse_args()

    out_w, out_h = (int(v) for v in args.size.split("x"))
    aspect = out_w / out_h

    src = Image.open(args.src).convert("RGB")
    # upscale 2x for smoother crops
    big = src.resize((src.width * 2, src.height * 2), Image.LANCZOS)

    # crop window: full height by aspect-matched width, traveling horizontally with slight zoom-in
    win_w = int(big.height * aspect)
    n = args.frames
    crops = []
    for i in range(n):
        t = i / (n - 1)
        z = 1.0 + args.zoom * t
        w = int(win_w * z)
        h = int(big.height * z)
        x_max = max(1, big.width - w)
        x = int(x_max * (0.5 + args.pan * (t - 0.5) * 2))
        x = max(0, min(x, big.width - w))
        y = (big.height - h) // 2
        crops.append(big.crop((x, y, x + w, y + h)).resize((out_w, out_h), Image.LANCZOS))

    # ping-pong: forward + reverse without duplicating endpoints
    sequence = crops + crops[-2:0:-1]

    out_dir = Path(args.out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    stem = Path(args.src).stem
    for idx, frame in enumerate(sequence):
        frame.save(out_dir / f"{stem}_{idx:02d}.png")
    print(f"{len(sequence)} frames -> {out_dir}")


if __name__ == "__main__":
    main()
