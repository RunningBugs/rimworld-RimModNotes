#!/usr/bin/env python3
"""Batch: Animagine stills -> Grok Imagine video clips -> show frames.

Submits one image-to-video job per still in assets_src, polls, downloads,
extracts frames with ffmpeg (620x256), ping-pongs them into loop-ready
sequences under Textures/Shows/<stem>_live, and writes the ShowDef XML.
"""
import base64
import json
import subprocess
import sys
import time
import urllib.request
from pathlib import Path

KEY = open("/home/lisanhu/.config/openrouter/api_key").read().strip()
ROOT = Path("/home/lisanhu/mine/workspace/rimworld/RimModNotes/78-RimFlixAnimeShows")
CLIP_DIR = ROOT / "assets_src" / "video"
MODEL = "x-ai/grok-imagine-video"

SHOWS = {
    "fox_staff": "the fox-eared shipgirl stands in space, subtle idle animation: long hair and jacket swaying, tail swaying, thrusters flickering, slow camera push-in, starry space background",
    "bunny_rifle": "the bunny-eared shipgirl with a rifle stands before a blue planet, subtle idle animation: hair swaying, ears twitching, satellite panels drifting slowly, slow camera push-in",
    "katana": "the black-haired swordswoman stands before a full moon, subtle idle animation: long hair flowing, coat hem swaying, engine glow pulsing, slow camera push-in",
    "twintail_drone": "the twintail shipgirl floats among drones in space, subtle idle animation: twintails swaying, drones bobbing and circling slowly, thrusters flickering, slow camera push-in",
    "witch": "the purple-haired witch floats above a planet, subtle idle animation: long hair and dress flowing, staff crystal glowing, clouds drifting, slow camera push-in",
    "demon": "the red-haired demon girl hovers in space, subtle idle animation: bat wings flapping slowly, hair swaying, embers drifting, slow camera push-in",
}

COMMON = ", anime style, high quality, consistent character, no scene change"


def api(path, body=None, raw=False):
    req = urllib.request.Request(
        f"https://openrouter.ai/api/v1/videos{path}",
        data=json.dumps(body).encode() if body else None,
        headers={"Content-Type": "application/json", "Authorization": f"Bearer {KEY}"})
    data = urllib.request.urlopen(req, timeout=180).read()
    return data if raw else json.loads(data)


def submit(stem, prompt):
    img_b64 = base64.b64encode((ROOT / "assets_src" / f"{stem}.png").read_bytes()).decode()
    body = {
        "model": MODEL,
        "prompt": prompt + COMMON,
        "duration": 5,
        "resolution": "480p",
        "aspect_ratio": "16:9",
        "generate_audio": False,
        "frame_images": [{"type": "image_url",
                          "image_url": {"url": f"data:image/png;base64,{img_b64}"},
                          "frame_type": "first_frame"}],
    }
    resp = api("", body)
    print(f"{stem}: job {resp['id']}", flush=True)
    return resp["id"]


def wait_job(job_id):
    for _ in range(45):
        time.sleep(20)
        poll = api(f"/{job_id}")
        status = poll.get("status")
        if status == "completed":
            return poll
        if status in ("failed", "cancelled", "expired"):
            raise RuntimeError(f"job {job_id} {status}: {poll.get('error')}")
    raise TimeoutError(job_id)


def main():
    only = sys.argv[1:] or list(SHOWS)
    jobs = {}
    for stem in only:
        clip = CLIP_DIR / f"{stem}.mp4"
        if clip.exists():
            print(f"{stem}: clip exists, skip submit", flush=True)
            continue
        jobs[stem] = submit(stem, SHOWS[stem])
        time.sleep(2)

    CLIP_DIR.mkdir(parents=True, exist_ok=True)
    for stem, job_id in jobs.items():
        wait_job(job_id)
        data = api(f"/{job_id}/content?index=0", raw=True)
        (CLIP_DIR / f"{stem}.mp4").write_bytes(data)
        print(f"{stem}: downloaded {len(data)} bytes", flush=True)

    for stem in only:
        out = ROOT / "Textures" / "Shows" / f"{stem}_live"
        out.mkdir(parents=True, exist_ok=True)
        tmp = Path(f"/tmp/grok_batch_{stem}")
        tmp.mkdir(exist_ok=True)
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(CLIP_DIR / f"{stem}.mp4"),
                        "-vf", "fps=12/5,crop=848:350:(iw-848)/2:(ih-350)/2,scale=620:256",
                        str(tmp / "f_%02d.png")], check=True)
        frames = sorted(tmp.glob("f_*.png"))
        sequence = frames + frames[-2:0:-1]
        for idx, frame in enumerate(sequence):
            (out / f"{stem}_live_{idx:02d}.png").write_bytes(frame.read_bytes())
        print(f"{stem}: {len(sequence)} frames -> {out}", flush=True)

    blocks = []
    for stem in only:
        labels = {
            "fox_staff": "Fox Shipgirl Live", "bunny_rifle": "Bunny Rifle Live",
            "katana": "Katana Live", "twintail_drone": "Twintail Drones Live",
            "witch": "Space Witch Live", "demon": "Demon Wings Live",
        }
        desc = ("Real AI video animation (Animagine XL still animated by Grok Imagine).")
        frame_lis = "\n".join(
            f'\t\t\t<li><texPath>Shows/{stem}_live/{stem}_live_{i:02d}</texPath><graphicClass>Graphic_Single</graphicClass></li>'
            for i in range(22))
        blocks.append(f'''\t<RimFlix.ShowDef>
\t\t<defName>{stem}_live_Flatscreen</defName>
\t\t<label>{labels[stem]} - Flatscreen</label>
\t\t<description>{desc}</description>
\t\t<televisionDefs>
\t\t\t<li>FlatscreenTelevision</li>
\t\t</televisionDefs>
\t\t<secondsBetweenFrames>0.15</secondsBetweenFrames>
\t\t<sound />
\t\t<frames>
{frame_lis}
\t\t</frames>
\t</RimFlix.ShowDef>''')
    xml = '<?xml version="1.0" encoding="utf-8"?>\n<Defs>\n\n' + "\n\n".join(blocks) + "\n\n</Defs>\n"
    (ROOT / "Defs" / "ShowDefs" / "Shows_GrokVideo.xml").write_text(xml, encoding="utf-8")
    print("XML written", flush=True)


if __name__ == "__main__":
    main()
