"""Run a disposable real-game process for FastFire verification & benchmarking."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import time
import xml.etree.ElementTree as ET

mod = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("case")
parser.add_argument("--game", type=Path, default=Path("/Data/SteamLibrary/steamapps/common/RimWorld"))
parser.add_argument("--mod-dir", type=Path, help="runtime mod directory; defaults to this project")
parser.add_argument("--language", default="English")
parser.add_argument("--render", action="store_true")
parser.add_argument("--window-size", default="1600,1000")
parser.add_argument("--timeout", type=int, default=240)
args = parser.parse_args()

default_dist = mod / "dist/FastFire"
runtime_mod = args.mod_dir.resolve() if args.mod_dir else (default_dist if default_dist.exists() else mod)
width, height = map(int, args.window_size.split(","))
assert width >= 1000 and height >= 800

root = mod / ".build/testing"
case = root / "cases" / args.case
if case.exists():
    shutil.rmtree(case)
case.mkdir(parents=True, exist_ok=False)

game = case / "game"
game.mkdir()
for source in args.game.iterdir():
    if source.name == "Mods":
        continue
    destination = game / source.name
    if source.name in {"RimWorld", "RimWorld.dll", "RimWorld.runtimeconfig.json", "RimWorld.deps.json", "steam_appid.txt"}:
        shutil.copy2(source, destination)
    else:
        destination.symlink_to(source, target_is_directory=source.is_dir())

(game / "Mods").mkdir()
(root / "harness/About").mkdir(parents=True, exist_ok=True)
shutil.copy2(mod / "Tests/About.xml", root / "harness/About/About.xml")
links = [
    ("Harmony", Path("/Data/SteamLibrary/steamapps/workshop/content/294100/2009463077")),
    ("FastFire", runtime_mod),
    ("IntegrationTests", root / "harness"),
]
for name, source in links:
    (game / "Mods" / name).symlink_to(source, target_is_directory=True)

save_data = case / "SaveData"
config = save_data / "Config"
config.mkdir(parents=True)
prefs = ET.Element("PrefsData")
for key, value in {
    "devMode": "True",
    "adaptiveTrainingEnabled": "False",
    "langFolderName": args.language,
    "screenWidth": str(width),
    "screenHeight": str(height),
    "fullscreen": "False",
}.items():
    ET.SubElement(prefs, key).text = value
ET.ElementTree(prefs).write(config / "Prefs.xml", encoding="utf-8", xml_declaration=True)

environment = os.environ.copy()
for name, directory in [
    ("XDG_CONFIG_HOME", "xdg-config"),
    ("XDG_CACHE_HOME", "xdg-cache"),
    ("XDG_DATA_HOME", "xdg-data"),
    ("TMPDIR", "tmp"),
]:
    (case / directory).mkdir()
    environment[name] = str(case / directory)
environment["FF_RENDER"] = "1" if args.render else "0"

packages = [
    "ludeon.rimworld",
    "ludeon.rimworld.royalty",
    "ludeon.rimworld.ideology",
    "ludeon.rimworld.biotech",
    "ludeon.rimworld.anomaly",
    "ludeon.rimworld.odyssey",
    "brrainz.harmony",
    "RunningBugs.FastFire",
    "local.fastfire.tests",
]

mods_config = ET.Element("ModsConfigData")
ET.SubElement(mods_config, "version").text = (args.game / "Version.txt").read_text().strip()
active = ET.SubElement(mods_config, "activeMods")
for package in packages:
    ET.SubElement(active, "li").text = package.lower()
known = ET.SubElement(mods_config, "knownExpansions")
for expansion in ["royalty", "ideology", "biotech", "anomaly", "odyssey"]:
    ET.SubElement(known, "li").text = "ludeon.rimworld." + expansion
ET.ElementTree(mods_config).write(config / "ModsConfig.xml", encoding="utf-8", xml_declaration=True)

command = [
    str(game / "RimWorld"),
    "-NoCrashDialog",
    "-savedatafolder=" + str(save_data),
    "-mod=" + ",".join(packages),
    "-quicktest",
]
if args.render:
    environment.pop("WAYLAND_DISPLAY", None)
    environment["SDL_VIDEODRIVER"] = "x11"
    environment["XDG_SESSION_TYPE"] = "x11"
    command.append("-WindowSize=" + args.window_size)
    command = ["xvfb-run", "-a", "-s", f"-screen 0 {width}x{height}x24", *command]
else:
    command.append("-Headless")

started = time.monotonic()
with (case / "console.log").open("wb") as output:
    process = subprocess.Popen(
        command,
        cwd=game,
        env=environment,
        stdout=output,
        stderr=subprocess.STDOUT,
        start_new_session=True,
    )
    timed_out = False
    try:
        code = process.wait(timeout=args.timeout)
    except subprocess.TimeoutExpired:
        timed_out = True
        os.killpg(process.pid, signal.SIGTERM)
        try:
            code = process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGKILL)
            code = process.wait()

native_file = save_data / "ff-result.json"
native = json.loads(native_file.read_text()) if native_file.exists() else None
console = re.sub(r"\x1b\[[0-9;]*m", "", (case / "console.log").read_text(errors="replace"))
engine_notes = [
    line
    for line in console.splitlines()
    if "ERROR" in line
    and (
        "Resolution too small (0x0)" in line
        or (args.render and "GLFW Error (PlatformUnavailable): Wayland: Platform not initialized" in line)
    )
]
errors = [line for line in console.splitlines() if "ERROR" in line and line not in engine_notes]

result = {
    "version": (args.game / "Version.txt").read_text().strip(),
    "assembly_sha256": hashlib.sha256((args.game / "Assembly-CSharp.dll").read_bytes()).hexdigest(),
    "mod_sha256": hashlib.sha256((runtime_mod / "1.7/Assemblies/FastFire.dll").read_bytes()).hexdigest(),
    "runtime_mod": str(runtime_mod),
    "returncode": code,
    "timed_out": timed_out,
    "seconds": round(time.monotonic() - started, 2),
    "language": args.language,
    "render": args.render,
    "window_size": [width, height],
    "errors": errors,
    "engine_notes": engine_notes,
    "native": native,
}
(case / "result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
print(json.dumps(result, ensure_ascii=False, indent=2), flush=True)
raise SystemExit(0 if code == 0 and not timed_out and not errors and native and native["passed"] else 1)
