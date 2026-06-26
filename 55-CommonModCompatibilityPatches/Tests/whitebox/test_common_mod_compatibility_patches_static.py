#!/usr/bin/env python3
from __future__ import annotations

import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding="utf-8")


class MetadataTests(unittest.TestCase):
    def test_xml_files_parse(self) -> None:
        for path in ROOT.rglob("*.xml"):
            with self.subTest(path=path.relative_to(ROOT)):
                ET.parse(path)

    def test_target_mods_are_load_after_not_dependencies(self) -> None:
        about = ET.parse(ROOT / "About" / "About.xml")
        deps = {node.text for node in about.findall(".//modDependencies/li/packageId")}
        load_after = {node.text for node in about.findall(".//loadAfter/li")}
        self.assertIn("brrainz.harmony", deps)
        for package_id in [
            "UnlimitedHugs.AllowTool",
            "Memegoddess.BuildFromInventory",
            "Memegoddess.ReplaceStuff",
            "XeoNovaDan.TinyTweaks",
        ]:
            self.assertNotIn(package_id, deps)
            self.assertIn(package_id, load_after)

    def test_old_single_purpose_bugfixes_marked_incompatible(self) -> None:
        about = ET.parse(ROOT / "About" / "About.xml")
        incompatible = {node.text for node in about.findall(".//incompatibleWith/li")}
        self.assertIn("RunningBugs.AllowToolBugfix", incompatible)
        self.assertIn("RunningBugs.ReservationEventBugfix", incompatible)
        self.assertIn("RunningBugs.ReplaceStuffBugfix", incompatible)


class PatchSourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.src = read("1.6/Source/CommonModCompatibilityPatches.cs")

    def test_all_patch_groups_have_package_dynamic_detection(self) -> None:
        self.assertIn("ModsConfig.ActiveModsInLoadOrder", self.src)
        for package_id in ["UnlimitedHugs.AllowTool", "Memegoddess.BuildFromInventory", "Memegoddess.ReplaceStuff"]:
            self.assertIn(package_id, self.src)
        self.assertIn("if (!ModDetection.IsActive(PackageId))", self.src)
        self.assertIn("if (!ModDetection.AnyActive(BuildFromInventoryPackageId, ReplaceStuffPackageId))", self.src)
        self.assertIn("if (!ModDetection.IsActive(ReplaceStuffPackageId))", self.src)

    def test_missing_target_types_skip_without_error_logging(self) -> None:
        self.assertIn("return false;", self.src)
        self.assertNotIn("was not found. Patch skipped", self.src)
        self.assertNotIn("Failed to patch Allow Tool", self.src)
        self.assertNotIn("Failed to patch Replace Stuff", self.src)

    def test_allow_tool_patch_targets_original_workgiver(self) -> None:
        self.assertIn('AccessTools.TypeByName("AllowTool.WorkGiver_HaulUrgently")', self.src)
        self.assertIn('"PotentialWorkThingsGlobal"', self.src)
        self.assertIn('"GetDesignatedAndHaulableThingsForMap"', self.src)
        self.assertIn("PawnCanAutomaticallyHaulFast", self.src)

    def test_reservation_patch_targets_vanilla_pathfinder_event(self) -> None:
        self.assertIn('AccessTools.Method(typeof(PathFinderMapData), "Notify_Reservation")', self.src)
        for token in ["thing == null", "thing.Destroyed", "!thing.Spawned", "thing.Map == null", "thing.def == null"]:
            self.assertIn(token, self.src)

    def test_replace_stuff_patch_targets_bridge_helper(self) -> None:
        self.assertIn('AccessTools.TypeByName("Replace_Stuff.PlaceBridges.PlaceBridges")', self.src)
        self.assertIn('"GetNeededBridge"', self.src)
        self.assertIn("public static Exception Finalizer", self.src)
        self.assertIn("Suppressed Replace Stuff bridge helper exception", self.src)


if __name__ == "__main__":
    unittest.main(verbosity=2)
