#!/usr/bin/env python3
"""White-box regression tests for AAT RimWorld mod source.

These tests intentionally inspect source and XML structure because most RimWorld
runtime objects must be created inside the game main thread and are not safe to
instantiate in an external unit-test runner.
"""

from __future__ import annotations

import re
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "1.6" / "Source"
COMMON = ROOT / "Common"


def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding="utf-8")


class XmlTests(unittest.TestCase):
    def test_all_xml_files_parse(self) -> None:
        for path in ROOT.rglob("*.xml"):
            with self.subTest(path=path.relative_to(ROOT)):
                ET.parse(path)

    def test_designation_category_contains_known_designators(self) -> None:
        tree = ET.parse(COMMON / "Defs" / "AllowFunctionDefs" / "DesignationCategoryDefs.xml")
        values = {node.text for node in tree.findall(".//specialDesignatorClasses/li")}
        self.assertIn("AAT.Designator_Allow", values)
        self.assertIn("AAT.Designator_Forbid", values)
        self.assertIn("AAT.Designator_AllowAll", values)
        self.assertIn("AAT.Designator_HaulUrgent", values)
        self.assertIn("AAT.Designator_SelectSimilar", values)
        self.assertIn("AAT.Designator_HarvestFullyGrown", values)


class HaulUrgentlyWhiteBoxTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.src = read("1.6/Source/HaulUrgently.cs")

    def _method_body(self, method_name: str) -> str:
        marker = f"public override IEnumerable<Thing> {method_name}"
        start = self.src.index(marker)
        next_method = self.src.index("public override bool ShouldSkip", start)
        return self.src[start:next_method]

    def test_potential_work_things_does_not_run_pawn_level_haul_checks(self) -> None:
        body = self._method_body("PotentialWorkThingsGlobal")
        self.assertNotIn("PawnCanAutomaticallyHaulFast", body)
        self.assertNotIn("HaulToStorageJob", body)

    def test_job_on_thing_contains_pawn_level_haul_check(self) -> None:
        job_start = self.src.index("public override Job JobOnThing")
        job_end = self.src.index("private static IReadOnlyList<Thing>", job_start)
        body = self.src[job_start:job_end]
        self.assertIn("JobOnThingDelegate", body)
        self.assertIn("PawnCanAutomaticallyHaulFast", self.src)

    def test_cache_intersects_urgent_designations_with_vanilla_lister_haulables(self) -> None:
        self.assertIn("designationsByDef[HaulUrgentlyDefOf.HaulUrgentlyDesignation]", self.src)
        self.assertIn("map.listerHaulables.ThingsPotentiallyNeedingHauling()", self.src)
        self.assertIn("designatedSet.Contains(thing)", self.src)

    def test_cache_reuses_collections_and_does_not_linq_rebuild_hot_lists(self) -> None:
        cache_start = self.src.index("public class HaulUrgentlyCache")
        cache = self.src[cache_start:]
        self.assertIn("private readonly List<Thing> designatedThings", cache)
        self.assertIn("private readonly HashSet<Thing> designatedSet", cache)
        self.assertNotIn("map.designationManager.AllDesignations", cache)
        self.assertNotRegex(cache, r"designatedThings\s*=\s*map\.designationManager\.AllDesignations.*\.ToList\(")

    def test_designation_add_remove_dirty_cache_patches_exist(self) -> None:
        self.assertIn("DesignationManager_RemoveDesignation_Patch", self.src)
        self.assertIn("DesignationManager_AddDesignation_Patch", self.src)
        self.assertIn("nameof(DesignationManager.AddDesignation)", self.src)

    def test_successful_cell_and_container_deposit_remove_urgent_designation(self) -> None:
        self.assertIn("ToilsHaul_PlaceHauledThingInCell_Patch", self.src)
        self.assertIn("ToilsHaul_DepositHauledThingInContainer_Patch", self.src)
        self.assertGreaterEqual(self.src.count("RemoveUrgentDesignationOn(carriedThing)"), 2)


class AllowForbidWhiteBoxTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.src = read("1.6/Source/AllowForbidDesignators.cs")

    def test_allow_forbid_use_forbiddable_filter_before_set_forbidden(self) -> None:
        self.assertIn("IsForbiddable", self.src)
        self.assertIn("GetComp<CompForbiddable>()", self.src)
        self.assertIn("SetForbidden(TargetForbiddenState, false)", self.src)

    def test_allow_forbid_notify_lister_haulables_on_state_change(self) -> None:
        self.assertIn("Notify_Forbidden", self.src)
        self.assertIn("Notify_Unforbidden", self.src)


class DesignatorHotPathWhiteBoxTests(unittest.TestCase):
    def test_select_similar_gizmo_hot_path_avoids_linq(self) -> None:
        src = read("1.6/Source/SelectSimilar.cs")
        self.assertNotIn("using System.Linq", src)
        self.assertNotIn(".OfType<", src)
        self.assertNotIn(".All(", src)
        self.assertIn("AllSelectedSameDefAndStuff", src)

    def test_cell_designators_copy_thinggrid_internal_lists_before_operating(self) -> None:
        for rel in ["1.6/Source/SelectSimilar.cs", "1.6/Source/HarvestFullyGrown.cs", "1.6/Source/HaulUrgently.cs"]:
            src = read(rel)
            with self.subTest(rel=rel):
                self.assertIn("tmpDesignateThings", src)
                self.assertIn("tmpDesignateThings.Clear()", src)


class AssetTests(unittest.TestCase):
    def test_referenced_icons_exist(self) -> None:
        source_text = "\n".join(p.read_text(encoding="utf-8") for p in SOURCE.glob("*.cs"))
        icon_paths = set(re.findall(r'ContentFinder<Texture2D>\.Get\("([^"]+)"', source_text))
        for icon in icon_paths:
            with self.subTest(icon=icon):
                self.assertTrue((COMMON / "Textures" / f"{icon}.png").exists(), icon)


if __name__ == "__main__":
    unittest.main(verbosity=2)
