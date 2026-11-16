"""Prepare only the runtime mod and media for the user's Workshop upload."""
from pathlib import Path
import hashlib
import json
import os
import shutil
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
destination = root / "dist/RangedFirepowerStats"
temporary = root / "dist/.RangedFirepowerStats-next"
if temporary.exists(): shutil.rmtree(temporary)
temporary.mkdir(parents=True)

# Publication metadata may be written by the game into a previous staging copy.
# Preserve it in the source before rebuilding the copy.
for relative in ["About/PublishedFileId.txt", "_PublisherPlus.xml"]:
    published = destination / relative
    source = root / relative
    if published.exists() and not source.exists():
        source.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(published, source)

rules = [node.text.replace("\\", "/").casefold()
    for node in ET.parse(root / "_PublisherPlus.xml").findall("Excluded/exclude") if node.text]
rules.append("_publisherplus.xml")  # PublisherPlus always omits its configuration.
assert "dist" in rules and "dist/" in (root / ".gitignore").read_text().splitlines()

def included(path):
    relative = path.relative_to(root).as_posix().casefold()
    return path.name != "_PublisherPlus.xml" and not any(relative.startswith(rule) for rule in rules)

expected = {}
for current, directories, names in os.walk(root, followlinks=False):
    current = Path(current)
    directories[:] = [name for name in sorted(directories) if included(current / name)]
    for name in sorted(names):
        source = current / name
        if not included(source): continue
        assert not source.is_symlink(), str(source)
        relative = source.relative_to(root)
        target = temporary / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        expected[relative.as_posix()] = hashlib.sha256(source.read_bytes()).hexdigest()
metadata = ET.parse(temporary / "About/About.xml").getroot()
assert metadata.findtext("packageId") == "RunningBugs.RangedFirepowerStats"
assert not metadata.findall("incompatibleWith")
assert list((temporary / "1.7/Assemblies").glob("*.dll")) == [temporary / "1.7/Assemblies/RangedFirepowerStats.dll"]
assert {p.relative_to(temporary).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
    for p in temporary.rglob("*") if p.is_file()} == expected
if destination.exists(): shutil.rmtree(destination)
temporary.rename(destination)
files = [{"path": str(p.relative_to(destination)), "bytes": p.stat().st_size,
    "sha256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(destination.rglob("*")) if p.is_file()]
(root / "docs/workshop-package.json").write_text(json.dumps({"packageId": metadata.findtext("packageId"),
    "publisherplus_equivalent": True, "configuration": "_PublisherPlus.xml",
    "files": files, "bytes": sum(p["bytes"] for p in files)}, ensure_ascii=False, indent=2) + "\n")
print(destination)
print("Files:", len(files), "Bytes:", sum(p["bytes"] for p in files))
