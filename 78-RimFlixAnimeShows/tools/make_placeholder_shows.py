#!/usr/bin/env python3
"""Generate placeholder show frames for the RimFlix anime-shows pack.

Two shows, Flatscreen HD size (310x128) + one Tube show (157x128):
- BlobBounce: a cute blob mascot bouncing with a blink
- StarTwinkle: night-sky gradient with twinkling stars
Replace these with your own images; see docs/adding-shows.md.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import math

ROOT = Path(__file__).resolve().parent.parent

FLAT = (310, 128)
TUBE = (157, 128)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def sky(w, h, top, bottom):
    img = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):
        d.line([(0, y), (w, y)], fill=lerp(top, bottom, y / h))
    return img


def blob_frames():
    out = ROOT / "Textures" / "Shows" / "BlobBounce"
    out.mkdir(parents=True, exist_ok=True)
    w, h = FLAT
    n = 8
    for i in range(n):
        t = i / n
        bounce = abs(math.sin(t * math.pi * 2))
        img = sky(w, h, (58, 44, 88), (120, 88, 140))
        d = ImageDraw.Draw(img)
        cx, base = w // 2, h - 26
        cy = base - int(bounce * 34)
        squash = 1.0 + (0.18 if bounce < 0.15 else 0.0)
        rw, rh = int(26 * squash), int(26 / squash)
        d.ellipse((cx - rw, cy - rh, cx + rw, cy + rh), fill=(250, 228, 230), outline=(200, 130, 150), width=3)
        blink = 0.15 if i in (3, 4) else 1.0
        for ex in (-9, 9):
            eh = max(2, int(7 * blink))
            d.ellipse((cx + ex - 3, cy - 6 - eh // 2, cx + ex + 3, cy - 6 + eh), fill=(60, 40, 60))
        d.arc((cx - 8, cy + 2, cx + 8, cy + 12), 10, 170, fill=(200, 110, 130), width=2)
        for sx, sy, sr in ((40, 22, 3), (70, 60, 2), (250, 30, 3), (270, 76, 2)):
            d.ellipse((sx - sr, sy - sr, sx + sr, sy + sr), fill=(255, 255, 220))
        img.save(out / f"Flat_BlobBounce_{i:02d}.png")
    return n


def star_frames():
    out = ROOT / "Textures" / "Shows" / "StarTwinkle"
    out.mkdir(parents=True, exist_ok=True)
    w, h = FLAT
    stars = [(30, 20, 4), (90, 44, 3), (150, 16, 5), (210, 52, 3), (260, 24, 4), (120, 70, 2), (240, 80, 2)]
    n = 6
    for i in range(n):
        img = sky(w, h, (10, 12, 40), (40, 30, 80))
        d = ImageDraw.Draw(img)
        for k, (sx, sy, sr) in enumerate(stars):
            phase = (i + k) % n / n
            r = max(1, int(sr * (0.5 + 0.5 * math.sin(phase * math.pi * 2))))
            bright = int(180 + 75 * (0.5 + 0.5 * math.sin(phase * math.pi * 2)))
            d.ellipse((sx - r, sy - r, sx + r, sy + r), fill=(bright, bright, bright - 30))
            if r >= 3:
                d.line((sx - r - 4, sy, sx + r + 4, sy), fill=(bright, bright, bright - 60), width=1)
                d.line((sx, sy - r - 4, sx, sy + r + 4), fill=(bright, bright, bright - 60), width=1)
        d.ellipse((w - 60, h - 46, w - 20, h - 6), fill=(240, 235, 210))
        d.ellipse((w - 52, h - 44, w - 18, h - 10), fill=(40, 30, 80))
        img.save(out / f"Flat_StarTwinkle_{i:02d}.png")
    return n


def tube_blob_frames():
    out = ROOT / "Textures" / "Shows" / "BlobBounceTube"
    out.mkdir(parents=True, exist_ok=True)
    w, h = TUBE
    n = 6
    for i in range(n):
        t = i / n
        bounce = abs(math.sin(t * math.pi * 2))
        img = sky(w, h, (58, 44, 88), (120, 88, 140))
        d = ImageDraw.Draw(img)
        cx, base = w // 2, h - 22
        cy = base - int(bounce * 26)
        rw = rh = 20
        d.ellipse((cx - rw, cy - rh, cx + rw, cy + rh), fill=(250, 228, 230), outline=(200, 130, 150), width=3)
        for ex in (-7, 7):
            d.ellipse((cx + ex - 2, cy - 8, cx + ex + 2, cy - 2), fill=(60, 40, 60))
        d.arc((cx - 6, cy + 2, cx + 6, cy + 10), 10, 170, fill=(200, 110, 130), width=2)
        img.save(out / f"Tube_BlobBounce_{i:02d}.png")
    return n


if __name__ == "__main__":
    print("blob frames:", blob_frames())
    print("star frames:", star_frames())
    print("tube blob frames:", tube_blob_frames())
