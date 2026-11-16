"""Prepare a clean, minimal runtime mod directory for Steam Workshop upload."""
from pathlib import Path
import os
import shutil
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
destination = root / "dist/FastFire"
temporary = root / "dist/.FastFire-next"
if temporary.exists():
    shutil.rmtree(temporary)
temporary.mkdir(parents=True)

# Preserve PublishedFileId.txt if written by RimWorld into previous dist copy.
published_id = destination / "About/PublishedFileId.txt"
source_id = root / "About/PublishedFileId.txt"
if published_id.exists() and not source_id.exists():
    source_id.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(published_id, source_id)

rules = [
    node.text.replace("\\", "/").casefold()
    for node in ET.parse(root / "_PublisherPlus.xml").findall("Excluded/exclude")
    if node.text
]
rules.append("_publisherplus.xml")

def included(path: Path) -> bool:
    relative = path.relative_to(root).as_posix().casefold()
    return path.name != "_PublisherPlus.xml" and not any(
        relative == rule or relative.startswith(rule + "/") for rule in rules
    )

file_count = 0
total_bytes = 0
for current, directories, names in os.walk(root, followlinks=False):
    current_path = Path(current)
    directories[:] = [name for name in sorted(directories) if included(current_path / name)]
    for name in sorted(names):
        source = current_path / name
        if not included(source):
            continue
        if source.is_symlink():
            raise RuntimeError(f"Unexpected symlink in runtime mod tree: {source}")
        relative = source.relative_to(root)
        target = temporary / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        file_count += 1
        total_bytes += source.stat().st_size

if destination.exists():
    shutil.rmtree(destination)
temporary.rename(destination)
print(f"Prepared clean Workshop package at {destination} ({file_count} files, {total_bytes / 1024.0:.1f} KB)")
