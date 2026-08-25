#!/usr/bin/env python3
from pathlib import Path
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
for needle in ['Patch_BuildingDoor_PawnCanOpen', 'ITab_RimLocksmith', 'IsColonyDoor', 'ShouldDeny',
               'Patch_MainTabWindowInspect_CurTabs', 'Patch_MainTabWindowInspect_ShouldShowPaneContents']:
    if needle not in source:
        errors.append(f'missing source invariant {needle}')
# 旧架构(prefix 接管 + 寻路代价 patch)会因 PassDoors 探测代价 MaxValue 冻结狂暴动物,不得回归
if 'GetDoorCost' in source:
    errors.append('PathUtility.GetDoorCost patch must stay deleted (it froze manhunters probing with PassDoors)')
if 'Patch_PathUtility' in source:
    errors.append('path-cost patches must stay deleted; vanilla pathing calls the postfix-narrowed PawnCanOpen')
harmony_patches = (root/'1.6/Source/HarmonyPatches.cs').read_text()
door_patch = harmony_patches.split('public static class Patch_BuildingDoor_PawnCanOpen', 1)[1]
if 'Postfix' not in door_patch or 'Prefix' in door_patch:
    errors.append('Patch_BuildingDoor_PawnCanOpen must be postfix-only (narrow vanilla true -> false, never widen)')
if 'Scribe_Deep.Look(ref DefaultConfig' in source or 'Scribe_Deep.Look(ref config' in source:
    errors.append('LockConfigData is a pure Core DTO; use LockConfigScribe/Scribe_Values, not Scribe_Deep.Look')
utility = (root/'1.6/Source/RimLocksmithUtility.cs').read_text()
if 'LordJob_TradeWithColony' not in utility:
    errors.append('ShouldDeny must carve out LordJob_TradeWithColony pack animals from the CanOpenAnyDoor exemption '
                  '(caravan animals are controlled by the guest toggle)')
if 'guest.Released' not in utility:
    errors.append('ShouldDeny must exempt released prisoners (guest.Released) so they can always leave, like vanilla')
if 'ManageColonyPawns' not in utility:
    errors.append('ShouldDeny must exempt player-faction pawns when the ManageColonyPawns setting is off')
settings = (root/'1.6/Source/RimLocksmithSettings.cs').read_text()
if 'ManageColonyPawns' not in settings or 'manageColonyPawns' not in settings:
    errors.append('RimLocksmithSettings must expose the ManageColonyPawns toggle (default enabled) and scribe it')
factory = (root/'1.6/Source/PawnAccessFactsFactory.cs').read_text()
if 'TraderKind' in factory:
    errors.append('Trader concept is deleted; caravan members classify as Guest, not via TraderKind')
# ANOMALY-CONTROL(disabled): 异象控制功能暂禁用,相关代码以注释保留,不做强制断言
core = '\n'.join(p.read_text() for p in (root/'1.6/Source/Core').glob('**/*.cs'))
for banned in ['Trader', 'OnlyPets', 'MaxPetBodySize']:
    if banned in core:
        errors.append(f'Core must not reference deleted concept: {banned}')
for lang in ['English', 'ChineseSimplified']:
    keyed = (root/f'Common/Languages/{lang}/Keyed/RimLocksmith_Keys.xml').read_text()
    if 'AllowTraders' in keyed or 'OnlyPets' in keyed:
        errors.append(f'{lang} keyed xml still references deleted Trader/OnlyPets concept')
    if 'AllowGuests' not in keyed:
        errors.append(f'{lang} keyed xml missing AllowGuests')
if errors:
    print('\n'.join(errors))
    raise SystemExit(1)
print('source_invariant_tests PASS')
