#!/usr/bin/env python3
from pathlib import Path
import re
root = Path(__file__).resolve().parents[1]
errors = []
for path in (root/'1.6/Source/Core').glob('**/*.cs'):
    text = path.read_text()
    for banned in ['using Verse;', 'using RimWorld;', 'using UnityEngine;', 'using HarmonyLib;']:
        if banned in text:
            errors.append(f'{path}: Core must not reference game/runtime API: {banned}')
csproj = (root/'1.6/Source/mod.csproj').read_text()
for needle in ['<TargetFramework>net472</TargetFramework>', 'Krafs.Rimworld.Ref', 'Lib.Harmony', '<OutputPath>../Assemblies</OutputPath>']:
    if needle not in csproj:
        errors.append(f'mod.csproj missing {needle}')
about = (root/'About/About.xml').read_text()
for needle in ['RunningBugs.RimLocksmith', '<li>1.6</li>', 'brrainz.harmony']:
    if needle not in about:
        errors.append(f'About.xml missing {needle}')
patches = list((root/'1.6/Patches').glob('**/*.xml'))
if not patches:
    errors.append('missing XML patches')
source = '\n'.join(p.read_text() for p in (root/'1.6/Source').glob('**/*.cs'))
for needle in ['Patch_BuildingDoor_PawnCanOpen', 'Patch_PathUtility_GetDoorCost', 'ITab_RimLocksmith', 'IsColonyDoor', 'Patch_MainTabWindowInspect_CurTabs', 'Patch_MainTabWindowInspect_ShouldShowPaneContents']:
    if needle not in source:
        errors.append(f'missing source invariant {needle}')
utility = (root/'1.6/Source/RimLocksmithUtility.cs').read_text()
try_allows = utility.split('public static bool TryAllowsOpen', 1)[1].split('public static List<Building_Door> SelectedDoors', 1)[0]
if '.FreePassage' in try_allows:
    errors.append('TryAllowsOpen must not read Building_Door.FreePassage; it recurses through WillCloseSoon -> PawnCanOpen')
if 'Scribe_Deep.Look(ref DefaultConfig' in source or 'Scribe_Deep.Look(ref config' in source:
    errors.append('LockConfigData is a pure Core DTO; use LockConfigScribe/Scribe_Values, not Scribe_Deep.Look')
pawn_facts_factory = (root/'1.6/Source/PawnAccessFactsFactory.cs').read_text()
for needle in ['IsFenceBlockedRoamer(Pawn pawn)', 'HasObjectSpecificRoamSuppression(pawn)', 'SentienceCatalyst', 'removeRoamMtb']:
    if needle not in pawn_facts_factory:
        errors.append(f'PawnAccessFactsFactory missing object-specific animal door check: {needle}')
if 'pawn.RaceProps?.FenceBlocked ?? false' in pawn_facts_factory:
    errors.append('PawnAccessFactsFactory must not classify fence-blocked animals from race props alone; sentience catalyst is per-pawn')
if errors:
    print('\n'.join(errors))
    raise SystemExit(1)
print('source_invariant_tests PASS')
