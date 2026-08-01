#!/usr/bin/env python3
"""Drive the local ComfyUI server for SDXL txt2img generation.

Usage:
  python3 comfy_gen.py --prompt "..." [--negative "..."] [--width 1216]
      [--height 512] [--seed 42] [--steps 28] [--cfg 6.0]
      [--checkpoint animagine-xl-4.0.safetensors] [--out tvshows/name]
Requires ComfyUI running at 127.0.0.1:8188. Prints the output image path.
"""
import argparse
import json
import sys
import time
import urllib.request

SERVER = "http://127.0.0.1:8188"


def build_workflow(args):
    return {
        "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": args.checkpoint}},
        "2": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["1", 1], "text": args.prompt}},
        "3": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["1", 1], "text": args.negative}},
        "4": {"class_type": "EmptyLatentImage", "inputs": {"width": args.width, "height": args.height, "batch_size": 1}},
        "5": {"class_type": "KSampler", "inputs": {
            "model": ["1", 0], "positive": ["2", 0], "negative": ["3", 0], "latent_image": ["4", 0],
            "seed": args.seed, "steps": args.steps, "cfg": args.cfg,
            "sampler_name": "euler_ancestral", "scheduler": "normal", "denoise": 1.0}},
        "6": {"class_type": "VAEDecode", "inputs": {"samples": ["5", 0], "vae": ["1", 2]}},
        "7": {"class_type": "SaveImage", "inputs": {"images": ["6", 0], "filename_prefix": args.out}},
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--prompt", required=True)
    parser.add_argument("--negative", default="lowres, bad anatomy, bad hands, text, error, missing fingers, extra digit, fewer digits, cropped, worst quality, low quality, jpeg artifacts, signature, watermark, username, blurry")
    parser.add_argument("--width", type=int, default=1216)
    parser.add_argument("--height", type=int, default=512)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--steps", type=int, default=28)
    parser.add_argument("--cfg", type=float, default=6.0)
    parser.add_argument("--checkpoint", default="animagine-xl-4.0.safetensors")
    parser.add_argument("--out", default="tvshows/gen")
    args = parser.parse_args()

    req = urllib.request.Request(
        SERVER + "/prompt",
        data=json.dumps({"prompt": build_workflow(args)}).encode(),
        headers={"Content-Type": "application/json"})
    pid = json.load(urllib.request.urlopen(req, timeout=30))["prompt_id"]

    for _ in range(60):
        time.sleep(10)
        history = json.load(urllib.request.urlopen(f"{SERVER}/history/{pid}", timeout=15))
        if not history:
            continue
        entry = list(history.values())[0]
        status = entry["status"]["status_str"]
        if status == "success":
            outputs = entry.get("outputs", {})
            for node_out in outputs.values():
                for img in node_out.get("images", []):
                    print(f"{img.get('subfolder')}/{img.get('filename')}" if img.get("subfolder") else img.get("filename"))
            return 0
        for message in entry["status"]["messages"]:
            if message[0] == "execution_error":
                print(message[1].get("exception_message"), file=sys.stderr)
                return 1
    print("timeout", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
