"""Generate short, stern original preset voices without reference audio."""

import argparse
import hashlib
import importlib.metadata
import json
from pathlib import Path
import subprocess
import tempfile

import numpy as np
import soundfile as sf
import torch
from huggingface_hub import snapshot_download
from qwen_tts import Qwen3TTSModel


MODEL = "Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice"
REVISION = "0c0e3051f131929182e2c023b9537f8b1c68adfe"
TEXT = "你们这个年纪是怎么睡得着的！"
INSTRUCTION = (
    "用标准普通话急促、强硬地大声训斥，像严厉的监工突然拍桌喝令所有人立刻起床干活。"
    "带怒气和不容反驳的命令感，声音要凶、硬、亮，字头咬紧，爆发力强。"
    "一口气快速说完，语速很快，总时长约两秒半，句中不要停顿。"
    "重音砸在‘这个年纪’和‘怎么睡得着’，末尾‘的’短促向下收住。"
    "不要温柔劝说，不要疑问式上扬，不要拖长元音，不要笑声或额外词语。"
)
MASTERING_FILTER = (
    "acompressor=threshold=0.125:ratio=3:attack=5:release=60:makeup=4:"
    "knee=2.828:link=average:detection=rms,"
    "alimiter=limit=0.89125094:level=false:latency=true"
)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--speaker", nargs="+", default=["Uncle_Fu"])
    parser.add_argument("--seed", type=int, default=20261015)
    parser.add_argument("--model", help="Local model snapshot (otherwise use pinned HF revision).")
    parser.add_argument("--output", type=Path, help="Output WAV path, for a single speaker only.")
    args = parser.parse_args()
    if args.output and len(args.speaker) != 1:
        parser.error("--output requires exactly one speaker")
    torch.set_num_threads(4)
    model = Qwen3TTSModel.from_pretrained(
        args.model or snapshot_download(MODEL, revision=REVISION), device_map="cuda:0", dtype=torch.bfloat16,
        attn_implementation="sdpa", local_files_only=True,
    )
    for speaker in args.speaker:
        torch.manual_seed(args.seed)
        with torch.inference_mode():
            wavs, sample_rate = model.generate_custom_voice(
                text=TEXT, language="Chinese", speaker=speaker,
                instruct=INSTRUCTION, max_new_tokens=128,
            )
        raw = np.asarray(wavs[0], dtype=np.float32).reshape(-1)
        assert raw.size and np.isfinite(raw).all()
        output = args.output or Path(__file__).with_name(f"WakeUp_Chinese_{speaker}_commanding.wav")
        output.parent.mkdir(parents=True, exist_ok=True)
        sf.write(output.with_name(output.stem + "_raw.wav"), raw, sample_rate, subtype="PCM_16")
        peak = float(np.max(np.abs(raw)))
        assert peak > 0
        active = np.flatnonzero(np.abs(raw) > peak * 0.008)
        padding = int(sample_rate * 0.05)
        samples = raw[max(0, int(active[0]) - padding):int(active[-1]) + padding + 1].copy()
        samples *= (10 ** (-2 / 20)) / np.max(np.abs(samples))
        fade = min(int(sample_rate * 0.005), samples.size // 2)
        samples[:fade] *= np.linspace(0, 1, fade)
        samples[-fade:] *= np.linspace(1, 0, fade)
        with tempfile.TemporaryDirectory(prefix=".voice-mastering-", dir=output.parent) as work:
            premaster = Path(work) / "premaster.wav"
            sf.write(premaster, samples, sample_rate, subtype="PCM_16")
            subprocess.run([
                "ffmpeg", "-hide_banner", "-loglevel", "error", "-nostdin", "-y",
                "-i", str(premaster), "-af", MASTERING_FILTER,
                "-c:a", "pcm_s16le", "-ar", str(sample_rate), "-ac", "1", str(output),
            ], check=True)
        metadata = {
            "model": MODEL, "revision": REVISION, "speaker": speaker,
            "language": "Chinese", "text": TEXT, "instruction": INSTRUCTION,
            "seed": args.seed, "max_new_tokens": 128,
            "generation_parameters": "Pinned model generation_config.json defaults",
            "sample_rate": sample_rate, "channels": 1, "subtype": "PCM_16",
            "duration_seconds": len(samples) / sample_rate,
            "postprocessing": "Trim silence with 50 ms padding; normalize premaster to -2 dBFS; 5 ms fades; speech compression and limiting to -1 dBFS. No speed or pitch modification.",
            "mastering_filter": MASTERING_FILTER,
            "sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
            "packages": {name: importlib.metadata.version(name) for name in
                         ("qwen-tts", "torch", "transformers", "soundfile")},
        }
        output.with_suffix(".json").write_text(
            json.dumps(metadata, indent=2, ensure_ascii=False) + "\n", encoding="utf-8",
        )
        print(json.dumps(metadata, ensure_ascii=False, indent=2), flush=True)


if __name__ == "__main__":
    main()
