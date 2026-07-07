from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "1.6" / "Source"


def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")


def test_dialog_uses_two_side_by_side_columns_and_new_labels():
    text = read("1.6/Source/Dialog_ChooseOutfit.cs")
    assert "Put on stand" not in text  # UI uses translation keys, not hardcoded English copy
    assert "DoColumn(pawnRect, \"PawnViewTitle\".Translate()" in text
    assert "DoColumn(standRect, \"OutfitStandViewTitle\".Translate()" in text
    assert "pawnMode" not in text
    assert "selectedPawnThings" in text
    assert "selectedStandThings" in text


def test_select_all_default_is_initialized_once_not_every_frame():
    text = read("1.6/Source/Dialog_ChooseOutfit.cs")
    assert "defaultSelectionInitialized" in text
    assert "InitializeDefaultSelectionOnce" in text
    assert text.count("selectedPawnThings.AddRange") == 1
    assert text.count("selectedStandThings.AddRange") == 1


def test_combined_transfer_job_processes_both_directions():
    text = read("1.6/Source/JobDriver_UseOutfitStand_Extension.cs")
    assert "if (!wornApparelToTransferToStand.NullOrEmpty())" in text
    assert "if (!standApparelToTransferToPawn.NullOrEmpty())" in text
    assert "else\n        {\n            PawnTakeMode" not in text
    assert "[.. OutfitStand.StandApparelToTransferToPawn]" in text
    assert "[.. OutfitStand.WornApparelToTransferToStand]" in text
    assert "foreach (Apparel apparel in new List<Apparel>(standApparelToTransferToPawn))" in text
    assert "standApparelToTransferToPawn.Add(apparel)" not in text


def test_targeted_outfit_stand_gizmo_uses_existing_jobs_only():
    text = read("1.6/Source/OutfitStandHaulGizmoUtility.cs")
    assert "new Command_Target" in text
    assert "JobDefOf.PutApparelOnOutfitStand" in text
    assert "JobDefOf.HaulToContainer" in text
    assert "Find.Selector.SingleSelectedThing is Pawn selectedPawn" in text
    assert "MakeJob(DefOfs" not in text
    assert "SetAllow(thingDef, true)" in text


def test_harmony_patch_appends_gizmo_to_spawned_apparel_and_weapons():
    patch_text = read("1.6/Source/HarmonyPatches.cs")
    util_text = read("1.6/Source/OutfitStandHaulGizmoUtility.cs")
    assert "ThingWithComps.GetGizmos" in patch_text
    assert "AppendOutfitStandGizmo" in patch_text
    assert "Notify_ItemAdded" in patch_text
    assert "Notify_ItemRemoved" in patch_text
    assert "DisableDefIfNoLongerHeld" in util_text
    assert "thing is Apparel" in util_text
    assert "thing.def.IsWeapon" in util_text


def test_localization_contains_new_player_facing_strings():
    zh = read("Common/Languages/ChineseSimplified/Keyed/Keys.xml")
    en = read("Common/Languages/English/Keyed/Keys.xml")
    assert "<PawnViewTitle>放到衣架上</PawnViewTitle>" in zh
    assert "<OutfitStandViewTitle>从衣架上穿</OutfitStandViewTitle>" in zh
    assert "<BOS.HaulToTargetOutfitStand>放到目标服装架</BOS.HaulToTargetOutfitStand>" in zh
    assert "<BOS.HaulToTargetOutfitStand>Put on target outfit stand</BOS.HaulToTargetOutfitStand>" in en


def test_harmony_dependency_declared():
    csproj = read("1.6/Source/mod.csproj")
    about = read("About/About.xml")
    assert "Lib.Harmony" in csproj
    assert "brrainz.harmony" in about
