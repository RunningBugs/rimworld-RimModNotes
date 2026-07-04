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
            "Mlie.RimStory",
            "OskarPotocki.VanillaFactionsExpanded.Core",
            "Nals.DynamicPortraits",
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
        for package_id in ["UnlimitedHugs.AllowTool", "Memegoddess.BuildFromInventory", "Memegoddess.ReplaceStuff", "Mlie.RimStory", "OskarPotocki.VanillaFactionsExpanded.Core", "Nals.DynamicPortraits"]:
            self.assertIn(package_id, self.src)
        self.assertIn("if (!ModDetection.IsActive(PackageId))", self.src)
        self.assertIn("if (!ModDetection.AnyActive(BuildFromInventoryPackageId, ReplaceStuffPackageId))", self.src)
        self.assertIn("if (!ModDetection.IsActive(ReplaceStuffPackageId))", self.src)

    def test_missing_target_types_skip_without_error_logging(self) -> None:
        self.assertIn("return false;", self.src)
        self.assertNotIn("was not found. Patch skipped", self.src)
        self.assertNotIn("Failed to patch Allow Tool", self.src)
        self.assertNotIn("Failed to patch Replace Stuff", self.src)

    def test_zero_weight_song_selection_guard_targets_vanilla_choose_next_song(self) -> None:
        self.assertIn("ZeroWeightSongSelectionCompatibility", self.src)
        self.assertIn('AccessTools.Method(typeof(MusicManagerPlay), "ChooseNextSong")', self.src)
        self.assertIn('AccessTools.Field(typeof(MusicManagerPlay), "recentSongs")', self.src)
        self.assertIn('AccessTools.Method(typeof(MusicManagerPlay), "AppropriateNow")', self.src)
        self.assertIn("TotalCommonality(candidates) > 0f", self.src)
        self.assertIn("recentSongs.Clear()", self.src)
        self.assertIn("zero-weight songs after recent-song filtering", self.src)
        self.assertIn("vanilla can choose a normal positive-weight song", self.src)
        self.assertIn("did not select zero-weight special-use music", self.src)
        self.assertNotIn("playableCandidates.RandomElement()", self.src)

    def test_nals_dynamic_portraits_work_items_guard_targets_draw_work_items(self) -> None:
        self.assertIn("NalsDynamicPortraitsWorkItemsCompatibility", self.src)
        self.assertIn('"Nals.DynamicPortraits"', self.src)
        self.assertIn('AccessTools.TypeByName("DynamicPortrait.RenderColonist")', self.src)
        self.assertIn('AccessTools.Method(renderColonistType, "DrawWorkItems")', self.src)
        self.assertIn("IsSafeWorkItemTarget(pawn.CurJob.targetA.Thing)", self.src)
        self.assertIn("def.thingClass", self.src)
        self.assertIn("def.graphicData == null", self.src)
        self.assertIn("Suppressed [NL] Dynamic Portraits DrawWorkItems exception", self.src)

    def test_allow_tool_patch_targets_original_workgiver(self) -> None:
        self.assertIn('AccessTools.TypeByName("AllowTool.WorkGiver_HaulUrgently")', self.src)
        self.assertIn('"PotentialWorkThingsGlobal"', self.src)
        self.assertIn('"GetDesignatedAndHaulableThingsForMap"', self.src)
        self.assertIn("PawnCanAutomaticallyHaulFast", self.src)

    def test_reservation_patch_targets_vanilla_pathfinder_event(self) -> None:
        self.assertIn('AccessTools.Method(typeof(PathFinderMapData), "Notify_Reservation")', self.src)
        for token in ["thing == null", "thing.Destroyed", "!thing.Spawned", "thing.Map == null", "thing.def == null"]:
            self.assertIn(token, self.src)

    def test_build_from_inventory_reservation_stack_count_clamp(self) -> None:
        self.assertIn("BuildFromInventoryReservationCountCompatibility", self.src)
        self.assertIn("nameof(ReservationManager.Reserve)", self.src)
        self.assertIn("ref int stackCount", self.src)
        self.assertIn("job?.def != JobDefOf.HaulToContainer", self.src)
        self.assertIn("stackCount = availableStack", self.src)

    def test_replace_stuff_patch_targets_bridge_helper(self) -> None:
        self.assertIn('AccessTools.TypeByName("Replace_Stuff.PlaceBridges.PlaceBridges")', self.src)
        self.assertIn('"GetNeededBridge"', self.src)
        self.assertIn("public static Exception Finalizer", self.src)
        self.assertIn("Suppressed Replace Stuff bridge helper exception", self.src)

    def test_replace_stuff_patch_targets_over_mineable_helper(self) -> None:
        self.assertIn('AccessTools.TypeByName("Replace_Stuff.OverMineable.InterceptBlueprintOverMinable")', self.src)
        self.assertIn('"Prefix", new[] { typeof(BuildableDef), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(Faction) }', self.src)
        self.assertIn("ReplaceStuffOverMineableCompatibility", self.src)
        self.assertIn("Suppressed Replace Stuff over-mineable blueprint helper exception", self.src)
        for token in ["sourceDef == null", "map == null", "map.thingGrid == null", "map.designationManager == null", "!center.IsValid"]:
            self.assertIn(token, self.src)

    def test_tiny_tweaks_auto_rebuild_skips_null_previous_map(self) -> None:
        self.assertIn("TinyTweaksAutoRebuildCompatibility", self.src)
        self.assertIn('AccessTools.TypeByName("TinyTweaks.CompLaunchableAutoRebuild")', self.src)
        self.assertIn('"ReceiveCompSignal", new[] { typeof(string) }', self.src)
        self.assertIn('AccessTools.Field(compType, "previousMap")', self.src)
        self.assertIn('signal != AutoRebuildSignal', self.src)
        self.assertIn("previousMap == null", self.src)
        self.assertIn("return false", self.src)

    def test_rimstory_adead_guard_targets_try_start_event(self) -> None:
        self.assertIn("RimStoryADeadCompatibility", self.src)
        self.assertIn('AccessTools.TypeByName("RimStory.ADead")', self.src)
        self.assertIn('"TryStartEvent", new[] { typeof(Map) }', self.src)
        self.assertIn('AccessTools.Field(aDeadType, "deadPawn")', self.src)
        self.assertIn('AccessTools.Field(aDeadType, "date")', self.src)
        self.assertIn('AccessTools.TypeByName("RimStory.Resources")', self.src)
        self.assertIn('AccessTools.Field(resourcesType, "eventsToDelete")', self.src)
        self.assertIn("QueueEventForDeletion(__instance)", self.src)
        self.assertIn("public static Exception Finalizer", self.src)
        self.assertIn("Suppressed RimStory ADead anniversary event", self.src)

    def test_goodwill_situation_manager_guard_targets_vef_stack(self) -> None:
        self.assertIn("GoodwillSituationManagerThreadSafetyCompatibility", self.src)
        self.assertIn('"OskarPotocki.VanillaFactionsExpanded.Core"', self.src)
        self.assertIn('AccessTools.Method(typeof(GoodwillSituationManager), nameof(GoodwillSituationManager.GetSituations)', self.src)
        self.assertIn('AccessTools.Method(typeof(GoodwillSituationManager), nameof(GoodwillSituationManager.RecalculateAll)', self.src)
        self.assertIn('AccessTools.Field(typeof(GoodwillSituationManager), "cachedData")', self.src)
        self.assertIn('lock (__instance)', self.src)
        self.assertIn('Reset corrupted GoodwillSituationManager cache', self.src)
        self.assertIn('cache[faction] = BuildSituations(faction)', self.src)


if __name__ == "__main__":
    unittest.main(verbosity=2)
