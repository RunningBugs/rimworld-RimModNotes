#!/usr/bin/env python3
"""Static whitebox checks for the KillingReward mod (no game required)."""
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

MOD_ROOT = Path(__file__).resolve().parents[2]
SOURCE = MOD_ROOT / "1.6" / "Source"


def keyed_keys(path: Path) -> set:
    return {child.tag for child in ET.parse(path).getroot()}


class StaticTests(unittest.TestCase):
    def test_about_metadata(self):
        root = ET.parse(MOD_ROOT / "About" / "About.xml").getroot()
        self.assertEqual(root.findtext("packageId"), "RunningBugs.KillingReward")
        self.assertIn("KillingReward", root.findtext("name"))
        self.assertIn("嗜血恩赐", root.findtext("name"))
        self.assertEqual([v.text for v in root.find("supportedVersions")], ["1.6"])
        deps = [d.findtext("packageId") for d in root.find("modDependencies")]
        self.assertIn("brrainz.harmony", deps)

    def test_translation_keys_match(self):
        en = keyed_keys(MOD_ROOT / "Languages" / "English" / "Keyed" / "Keys.xml")
        zh = keyed_keys(MOD_ROOT / "Languages" / "ChineseSimplified" / "Keyed" / "Keys.xml")
        self.assertEqual(en, zh)
        self.assertGreater(len(en), 20)

    def test_design_flavor_strings_present(self):
        zh = (MOD_ROOT / "Languages" / "ChineseSimplified" / "Keyed" / "Keys.xml").read_text(encoding="utf-8")
        for snippet in ["嗜血恩赐", "尽量别死", "黑暗超凡智能的恩赐", "血祭", "禁忌知识", "技艺灌注", "虚空馈赠", "杀戮即是祈祷", "祭品+1"]:
            self.assertIn(snippet, zh)

    def test_kill_patch_targets_pawn_kill(self):
        patch = (SOURCE / "Patches" / "PawnKillPatch.cs").read_text(encoding="utf-8")
        self.assertIn("HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))", patch)

    def test_core_logic_has_no_verse_dependency(self):
        for cs in (SOURCE / "Core").glob("*.cs"):
            text = cs.read_text(encoding="utf-8")
            self.assertNotRegex(text, r"using\s+(Verse|RimWorld|UnityEngine)", cs.name)

    def test_defs_reference_existing_worker_classes(self):
        main_button = (MOD_ROOT / "1.6" / "Defs" / "MainButtonDefs" / "KillingRewardMainButton.xml").read_text(encoding="utf-8")
        self.assertIn("KillingReward.MainButtonWorker_KillingReward", main_button)
        self.assertTrue((SOURCE / "UI" / "MainButtonWorker_KillingReward.cs").exists())
        letter = (MOD_ROOT / "1.6" / "Defs" / "LetterDefs" / "KillingRewardLetter.xml").read_text(encoding="utf-8")
        self.assertIn("KillingReward.ChoiceLetter_KillingReward", letter)
        self.assertTrue((SOURCE / "UI" / "ChoiceLetter_KillingReward.cs").exists())

    def test_icon_exists_for_main_button_and_letter(self):
        self.assertTrue((MOD_ROOT / "Textures" / "UI" / "Icons" / "KillingReward.png").exists())


if __name__ == "__main__":
    unittest.main()
