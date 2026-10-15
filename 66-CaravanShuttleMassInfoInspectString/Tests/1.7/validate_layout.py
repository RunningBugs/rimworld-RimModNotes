"""Static layout verification; does not run the game or its methods."""
from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET

mod = Path(__file__).resolve().parents[2]
original = Path('/home/lisanhu/mine/workspace/rimworld/RimModNotes') / mod.name
assert (mod / '1.7/Source/Main.cs').read_bytes() == (original / '1.6/Source/Main.cs').read_bytes()
for path in (original / '1.6').rglob('*'):
    if path.is_file():
        assert path.read_bytes() == (mod / path.relative_to(original)).read_bytes(), path
for path in mod.rglob('*.xml'):
    ET.parse(path)
about = ET.parse(mod / 'About/About.xml').getroot()
old_about = ET.parse(original / 'About/About.xml').getroot()
assert about.findtext('packageId') == old_about.findtext('packageId')
assert about.findtext('name') == old_about.findtext('name')
assert (mod / 'About/PublishedFileId.txt').read_bytes() == (original / 'About/PublishedFileId.txt').read_bytes()
assert [e.text for e in about.findall('supportedVersions/li')] == ['1.6', '1.7']
folders = ET.parse(mod / 'LoadFolders.xml').getroot()
for version in ('1.6', '1.7'):
    assert [e.text for e in folders.find('v' + version)] == ['/', 'Common', version]
dlls = list((mod / '1.7/Assemblies').glob('*.dll'))
assert [p.name for p in dlls] == ['ShuttleMassInfo.dll']
game = Path('/Data/SteamLibrary/steamapps/common/RimWorld')
assert (game / 'Version.txt').read_text().strip() == '1.7.4977 rev926'
xml = ET.parse(game / 'Data/Core/Defs/WorldObjectDefs/WorldObjects.xml').getroot()
assert len(xml.findall("WorldObjectDef[defName='Caravan']")) == 1
print(json.dumps({'status': 'passed', 'sha256': hashlib.sha256(dlls[0].read_bytes()).hexdigest(),
                  'checks': ['XML syntax', 'official Caravan patch target', 'identity preserved',
                             'old files preserved', '1.7 source unchanged', 'one final DLL', 'load folders']}, indent=2))
