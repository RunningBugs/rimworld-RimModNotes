"""Generate an original preset-voice reminder; never consumes reference audio."""

import argparse
import hashlib
import importlib.metadata
import json
from pathlib import Path
import wave

import numpy as np
import soundfile as sf
import torch
from huggingface_hub import snapshot_download
from qwen_tts import Qwen3TTSModel


MODEL = "Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice"
REVISION = "0c0e3051f131929182e2c023b9537f8b1c68adfe"
TEXT = "你们这个年纪，是怎么睡得着的！"
INSTRUCTION = (
    "用普通话大声训话，像一位着急催人起床干活的喜剧监工。"
    "带着不可思议和恨铁不成钢的情绪，重读‘这个年纪’和‘怎么睡得着’，"
    "结尾上扬质问，语速稍快，短促有力。只说给定台词，不添加词语，不带笑声。"
)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model", help="Local model snapshot (otherwise use pinned HF revision).")
    parser.add_argument("--speaker", default="Uncle_Fu")
    parser.add_argument("--output", type=Path, default=Path(__file__).with_name("WakeUp_Chinese.wav"))
    args = parser.parse_args()
    torch.manual_seed(20261015)
    torch.set_num_threads(4)
    model_path = args.model or snapshot_download(MODEL, revision=REVISION)
    model = Qwen3TTSModel.from_pretrained(
        model_path, device_map="cuda:0", dtype=torch.bfloat16,
        attn_implementation="sdpa", local_files_only=True,
    )
    with torch.inference_mode():
        wavs, sample_rate = model.generate_custom_voice(
            text=TEXT, language="Chinese", speaker=args.speaker,
            instruct=INSTRUCTION, max_new_tokens=256,
        )
    samples = np.asarray(wavs[0], dtype=np.float32).reshape(-1)
    assert samples.size and np.isfinite(samples).all(), "Invalid generated waveform"
    peak = float(np.max(np.abs(samples)))
    assert peak > 0, "Generated audio is silent"
    active = np.flatnonzero(np.abs(samples) > peak * 0.008)
    padding = int(sample_rate * 0.08)
    samples = samples[max(0, int(active[0]) - padding):int(active[-1]) + padding + 1]
    samples *= (10 ** (-2 / 20)) / np.max(np.abs(samples))
    fade = min(int(sample_rate * 0.005), samples.size // 2)
    samples[:fade] *= np.linspace(0, 1, fade)
    samples[-fade:] *= np.linspace(1, 0, fade)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    sf.write(args.output, samples, sample_rate, subtype="PCM_16")
    with wave.open(str(args.output)) as clip:
        assert clip.getnchannels() == 1 and clip.getsampwidth() == 2
        assert 1 < clip.getnframes() / clip.getframerate() < 15
    metadata = {
        "model": MODEL, "revision": REVISION, "speaker": args.speaker,
        "text": TEXT, "instruction": INSTRUCTION, "seed": 20261015,
        "sample_rate": sample_rate, "channels": 1, "subtype": "PCM_16",
        "duration_seconds": len(samples) / sample_rate,
        "sha256": hashlib.sha256(args.output.read_bytes()).hexdigest(),
        "packages": {name: importlib.metadata.version(name) for name in
                     ("qwen-tts", "torch", "transformers", "soundfile")},
    }
    args.output.with_suffix(".json").write_text(
        json.dumps(metadata, indent=2, ensure_ascii=False) + "\n", encoding="utf-8",
    )
    print(json.dumps(metadata, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
