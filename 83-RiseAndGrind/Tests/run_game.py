"""Run one disposable Rise and Grind integration case using an isolated apphost."""
import argparse
import fcntl
import hashlib
import json
import os
import re
from pathlib import Path
import shutil
import signal
import subprocess
import time
import xml.etree.ElementTree as ET

tests = Path(__file__).resolve().parent
mod = tests.parent
workspace = mod.parent.parent
root = workspace / 'tmp/rise-and-grind-testing'
root.mkdir(parents=True, exist_ok=True)
original = Path('/Data/SteamLibrary/steamapps/common/RimWorld')
baseline = json.loads((workspace / 'RW/17/BASELINE.json').read_text())
parser = argparse.ArgumentParser()
parser.add_argument('case')
parser.add_argument('--language', default='English')
parser.add_argument('--extension', action='store_true')
parser.add_argument('--timeout', type=int, default=180)
args = parser.parse_args()
if not args.case or '/' in args.case or args.case in {'.', '..'}:
    parser.error('case must be a new single directory name')
if (original / 'Version.txt').read_text().strip() != baseline['version']:
    raise SystemExit('Game version differs from fixed test references')
for entry in baseline['files']:
    if entry['name'] in {'Assembly-CSharp.dll', 'UnityStubs.dll', 'RimWorld.dll', 'Waylith.dll'}:
        if hashlib.sha256(Path(entry['source']).read_bytes()).hexdigest() != entry['sha256']:
            raise SystemExit('Game assembly changed: ' + entry['name'])
lock = (root / 'game.lock').open('w')
fcntl.flock(lock, fcntl.LOCK_EX)
game = root / 'game'
game.mkdir(exist_ok=True)
for source in original.iterdir():
    if source.name == 'Mods':
        continue
    destination = game / source.name
    if destination.exists() or destination.is_symlink():
        continue
    if source.name in {'RimWorld', 'RimWorld.dll', 'RimWorld.runtimeconfig.json', 'RimWorld.deps.json', 'steam_appid.txt'}:
        shutil.copy2(source, destination)
    else:
        destination.symlink_to(source, target_is_directory=source.is_dir())
(game / 'Mods').mkdir(exist_ok=True)
(root / 'harness/About').mkdir(parents=True, exist_ok=True)
shutil.copy2(tests / 'harness/About/About.xml', root / 'harness/About/About.xml')
for name, source in [('Harmony', Path('/Data/SteamLibrary/steamapps/workshop/content/294100/2009463077')),
                     ('83-RiseAndGrind', mod), ('IntegrationTests', root / 'harness'),
                     ('VoicePatch', tests / 'voice-patch')]:
    destination = game / 'Mods' / name
    if destination.is_symlink() and destination.resolve() != source.resolve():
        destination.unlink()
    if not destination.is_symlink():
        destination.symlink_to(source, target_is_directory=True)
case = root / 'cases' / args.case
case.mkdir(parents=True, exist_ok=False)
environment = os.environ.copy()
for name, directory in [('XDG_CONFIG_HOME', 'xdg-config'), ('XDG_CACHE_HOME', 'xdg-cache'),
                        ('XDG_DATA_HOME', 'xdg-data'), ('TMPDIR', 'tmp')]:
    (case / directory).mkdir()
    environment[name] = str(case / directory)
environment['RAG_TEST_EXTENSION'] = '1' if args.extension else '0'
config = case / 'SaveData' / 'Config'
config.mkdir(parents=True)
prefs = ET.Element('PrefsData')
language = 'ChineseSimplified (简体中文)' if args.language == 'ChineseSimplified' else args.language
ET.SubElement(prefs, 'langFolderName').text = language
ET.ElementTree(prefs).write(config / 'Prefs.xml', encoding='utf-8', xml_declaration=True)
mods = ['brrainz.harmony', 'RunningBugs.RiseAndGrind', 'local.riseandgrind.tests']
if args.extension:
    mods.append('local.riseandgrind.voicepatch')
command = [str(game / 'RimWorld'), '-Headless', '-NoCrashDialog', '-quicktest',
           '-savedatafolder=' + str(case / 'SaveData'), '-mod=' + ','.join(mods)]
started = time.monotonic()
with (case / 'console.log').open('wb') as output:
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
native_result = case / 'SaveData/rag-result.json'
native = json.loads(native_result.read_text()) if native_result.exists() else None
console = re.sub(r'\x1b\[[0-9;]*m', '', (case / 'console.log').read_text(errors='replace'))
errors = [line for line in console.splitlines() if 'ERROR' in line
          and 'Resolution too small (0x0), resetting UI scale to 1.' not in line]
result = {'unexpected_errors': errors, 'case': args.case, 'command': command, 'version': baseline['version'],
          'returncode': code, 'timed_out': timed_out,
          'elapsed_seconds': round(time.monotonic() - started, 2), 'native_checks': native,
          'production_sha256': hashlib.sha256((mod / '1.7/Assemblies/RiseAndGrind.dll').read_bytes()).hexdigest()}
(case / 'result.json').write_text(json.dumps(result, indent=2, ensure_ascii=False) + '\n')
print(json.dumps(result, indent=2, ensure_ascii=False))
raise SystemExit(0 if code == 0 and not timed_out and not errors and native and native['passed'] else 1)
