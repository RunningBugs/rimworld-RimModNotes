"""Stage the same path-prefix selection as PublisherPlus, without uploading."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET

mod = Path(__file__).resolve().parents[1]
destination = mod / ".build/publish/content"
exclusions = [node.text.replace("\\", "/").casefold()
              for node in ET.parse(mod / "_PublisherPlus.xml").findall("Excluded/exclude") if node.text]
exclusions.append("_publisherplus.xml")  # PublisherPlus always omits its own configuration.
if destination.exists():
    shutil.rmtree(destination)
destination.mkdir(parents=True)
manifest = []
for current, directories, files in os.walk(mod, followlinks=False):
    current = Path(current)
    included = lambda path: not any(path.relative_to(mod).as_posix().casefold().startswith(excluded)
                                   for excluded in exclusions)
    directories[:] = [name for name in sorted(directories) if included(current / name)]
    for name in sorted(files):
        source = current / name
        if not included(source):
            continue
        if source.is_symlink():
            raise SystemExit("Refusing to upload a symlink: " + str(source))
        relative = source.relative_to(mod)
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        manifest.append({"path": relative.as_posix(), "bytes": source.stat().st_size,
                         "sha256": hashlib.sha256(source.read_bytes()).hexdigest()})
for version in ("1.5", "1.6", "1.7"):
    assert (destination / version / "Assemblies/CultistKidnapping.dll").is_file()
    assert (destination / version / "Source/CultistKidnapping.csproj").is_file()
assert (destination / "Source/LordJob_CultistKidnap.cs").is_file()
assert (destination / "About/Preview.png").is_file()
(destination.parent / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
print(json.dumps({"content": str(destination), "files": len(manifest),
                  "bytes": sum(item["bytes"] for item in manifest),
                  "paths": [item["path"] for item in manifest]}, ensure_ascii=False, indent=2))
