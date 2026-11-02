"""Run three disposable native-game phases, including two real save reloads."""
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
parser.add_argument("case", help="new single directory name")
parser.add_argument("--game", type=Path, default=Path("/Data/SteamLibrary/steamapps/common/RimWorld"))
parser.add_argument("--language", default="English")
parser.add_argument("--timeout", type=int, default=180)
args = parser.parse_args()
if not args.case or Path(args.case).name != args.case or args.case in {".", ".."}:
    parser.error("case must be a new single directory name")
root = mod / ".build/testing"
case = root / "cases" / args.case
case.mkdir(parents=True, exist_ok=False)
game = root / "game"
game.mkdir(exist_ok=True)
for source in args.game.iterdir():
    if source.name == "Mods":
        continue
    destination = game / source.name
    if destination.exists() or destination.is_symlink():
        continue
    if source.name in {"RimWorld", "RimWorld.dll", "RimWorld.runtimeconfig.json", "RimWorld.deps.json", "steam_appid.txt"}:
        shutil.copy2(source, destination)
    else:
        destination.symlink_to(source, target_is_directory=source.is_dir())
(game / "Mods").mkdir(exist_ok=True)
(root / "harness/About").mkdir(parents=True, exist_ok=True)
shutil.copy2(mod / "Tests/About.xml", root / "harness/About/About.xml")
for name, source in [("Harmony", Path("/Data/SteamLibrary/steamapps/workshop/content/294100/2009463077")),
                     ("CultistKidnapping", mod), ("IntegrationTests", root / "harness")]:
    destination = game / "Mods" / name
    if not destination.is_symlink():
        destination.symlink_to(source, target_is_directory=True)

save_data = case / "SaveData"
config = save_data / "Config"
config.mkdir(parents=True)
prefs = ET.Element("PrefsData")
ET.SubElement(prefs, "devMode").text = "True"
ET.SubElement(prefs, "langFolderName").text = args.language
ET.ElementTree(prefs).write(config / "Prefs.xml", encoding="utf-8", xml_declaration=True)
environment = os.environ.copy()
for name, directory in [("XDG_CONFIG_HOME", "xdg-config"), ("XDG_CACHE_HOME", "xdg-cache"),
                        ("XDG_DATA_HOME", "xdg-data"), ("TMPDIR", "tmp")]:
    (case / directory).mkdir()
    environment[name] = str(case / directory)

results = []
for phase, previous_save in [("start", None), ("resume", "CK-Carrying"), ("repeat", "CK-Repeat")]:
    if previous_save:
        shutil.copy2(save_data / "Saves" / (previous_save + ".rws"), save_data / "Saves/Autostart.rws")
    environment["CK_TEST_PHASE"] = phase
    native_file = save_data / "ck-result.json"
    native_file.unlink(missing_ok=True)
    command = [str(game / "RimWorld"), "-Headless", "-NoCrashDialog",
               "-savedatafolder=" + str(save_data),
               "-mod=brrainz.harmony,RunningBugs.CultistKidnapping,local.cultistkidnapping.tests"]
    if not previous_save:
        command.append("-quicktest")
    started = time.monotonic()
    with (case / (phase + ".log")).open("wb") as output:
        process = subprocess.Popen(command, cwd=game, env=environment, stdout=output,
                                   stderr=subprocess.STDOUT, start_new_session=True)
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
    native = json.loads(native_file.read_text()) if native_file.exists() else None
    console = re.sub(r"\x1b\[[0-9;]*m", "", (case / (phase + ".log")).read_text(errors="replace"))
    errors = [line for line in console.splitlines() if "ERROR" in line
              and "Resolution too small (0x0), resetting UI scale to 1." not in line]
    result = {"phase": phase, "returncode": code, "timed_out": timed_out,
              "seconds": round(time.monotonic() - started, 2), "errors": errors, "native": native}
    results.append(result)
    print(json.dumps(result, ensure_ascii=False, indent=2), flush=True)
    if code or timed_out or errors or not native or not native["passed"]:
        break

summary = {"version": (args.game / "Version.txt").read_text().strip(),
           "assembly_sha256": hashlib.sha256((args.game / "Assembly-CSharp.dll").read_bytes()).hexdigest(),
           "mod_sha256": hashlib.sha256((mod / "1.7/Assemblies/CultistKidnapping.dll").read_bytes()).hexdigest(),
           "results": results}
(case / "result.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n")
raise SystemExit(0 if len(results) == 3 and all(r["native"] and r["native"]["passed"] and not r["errors"] for r in results) else 1)
