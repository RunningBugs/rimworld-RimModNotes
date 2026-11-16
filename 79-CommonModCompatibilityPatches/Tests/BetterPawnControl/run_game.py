"""Exercise the installed BPC and production patch in a disposable real game."""
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

parser = argparse.ArgumentParser()
parser.add_argument("--case-dir", required=True, type=Path)
parser.add_argument("--product-dll", required=True, type=Path)
parser.add_argument("--checks-dll", required=True, type=Path)
parser.add_argument("--absent", action="store_true")
parser.add_argument("--timeout", type=int, default=180)
args = parser.parse_args()
case = args.case_dir.resolve()
case.mkdir(parents=True, exist_ok=False)
source_game = Path("/Data/SteamLibrary/steamapps/common/RimWorld")
workshop = Path("/Data/SteamLibrary/steamapps/workshop/content/294100")
mod = Path(__file__).resolve().parents[2]
game = case / "game"
game.mkdir()
for source in source_game.iterdir():
    if source.name == "Mods":
        continue
    target = game / source.name
    if source.name in {"RimWorld", "RimWorld.dll", "RimWorld.runtimeconfig.json", "RimWorld.deps.json", "steam_appid.txt"}:
        shutil.copy2(source, target)
    else:
        target.symlink_to(source, target_is_directory=source.is_dir())
(game / "Mods").mkdir()
product = case / "product"
(product / "About").mkdir(parents=True)
(product / "1.7/Assemblies").mkdir(parents=True)
shutil.copy2(mod / "About/About.xml", product / "About/About.xml")
shutil.copy2(mod / "LoadFolders.xml", product / "LoadFolders.xml")
shutil.copy2(args.product_dll, product / "1.7/Assemblies/CommonModCompatibilityPatches.dll")
harness = case / "harness"
(harness / "About").mkdir(parents=True)
(harness / "Assemblies").mkdir()
shutil.copy2(Path(__file__).with_name("About.xml"), harness / "About/About.xml")
shutil.copy2(args.checks_dll, harness / "Assemblies" / args.checks_dll.name)
links = [("Harmony", workshop / "2009463077"), ("CommonCompatibility", product), ("Tests", harness)]
if not args.absent:
    links.append(("BetterPawnControl", workshop / "1541460369"))
for name, source in links:
    (game / "Mods" / name).symlink_to(source, target_is_directory=True)
save_data = case / "SaveData"
config = save_data / "Config"
config.mkdir(parents=True)
prefs = ET.Element("PrefsData")
for key, value in {"devMode": "True", "adaptiveTrainingEnabled": "False", "langFolderName": "English"}.items():
    ET.SubElement(prefs, key).text = value
ET.ElementTree(prefs).write(config / "Prefs.xml", encoding="utf-8", xml_declaration=True)
packages = ["ludeon.rimworld", "ludeon.rimworld.royalty", "ludeon.rimworld.ideology",
            "ludeon.rimworld.biotech", "ludeon.rimworld.anomaly", "ludeon.rimworld.odyssey", "brrainz.harmony"]
if not args.absent:
    packages.append("voult.betterpawncontrol")
packages += ["runningbugs.commonmodcompatibilitypatches", "local.commoncompatibility.bpc.tests"]
mods = ET.Element("ModsConfigData")
ET.SubElement(mods, "version").text = (source_game / "Version.txt").read_text().strip()
active = ET.SubElement(mods, "activeMods")
for package in packages:
    ET.SubElement(active, "li").text = package
known = ET.SubElement(mods, "knownExpansions")
for expansion in ["royalty", "ideology", "biotech", "anomaly", "odyssey"]:
    ET.SubElement(known, "li").text = "ludeon.rimworld." + expansion
ET.ElementTree(mods).write(config / "ModsConfig.xml", encoding="utf-8", xml_declaration=True)
env = os.environ.copy()
for name in ["XDG_CONFIG_HOME", "XDG_CACHE_HOME", "XDG_DATA_HOME", "TMPDIR"]:
    directory = case / name.lower()
    directory.mkdir()
    env[name] = str(directory)
env["BPC_ABSENT"] = "1" if args.absent else "0"
command = [str(game / "RimWorld"), "-NoCrashDialog", "-Headless", "-quicktest",
           "-savedatafolder=" + str(save_data), "-mod=" + ",".join(packages)]
started = time.monotonic()
with (case / "console.log").open("wb") as output:
    process = subprocess.Popen(command, cwd=game, env=env, stdout=output, stderr=subprocess.STDOUT, start_new_session=True)
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
result_file = save_data / "bpc-result.json"
native = json.loads(result_file.read_text()) if result_file.exists() else None
console = re.sub(r"\x1b\[[0-9;]*m", "", (case / "console.log").read_text(errors="replace"))
baseline = [line for line in console.splitlines() if "ERROR" in line and "Resolution too small (0x0)" in line]
errors = [line for line in console.splitlines() if "ERROR" in line and line not in baseline]
result = {"version": (source_game / "Version.txt").read_text().strip(), "absent": args.absent,
          "product_sha256": hashlib.sha256(args.product_dll.read_bytes()).hexdigest(),
          "returncode": code, "timed_out": timed_out, "seconds": round(time.monotonic() - started, 2),
          "errors": errors, "baseline": baseline, "native": native}
(case / "result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
print(json.dumps(result, ensure_ascii=False, indent=2), flush=True)
raise SystemExit(0 if code == 0 and not timed_out and not errors and native and native["passed"] else 1)
