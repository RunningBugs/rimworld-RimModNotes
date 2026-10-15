# Common Mod Compatibility Patches

Runtime Harmony compatibility patches for commonly used RimWorld mods. Each patch group is dynamically detected at startup; if the target mod/type/method is absent, the patch is skipped silently.

## Layout

- `1.6/Source/CommonModCompatibilityPatches.cs` — all patch groups, one `internal static class` per target, each with a `TryApply(Harmony)` method registered in `CommonCompatibilityBootstrap`
- `1.6/Assemblies/CommonModCompatibilityPatches.dll` — build output, committed alongside the source
- `About/About.xml` — the mod's only documentation file
- `Tests/whitebox/test_common_mod_compatibility_patches_static.py` — static whitebox checks, run via `Tests/run_whitebox.sh`

## Conventions

- **`About/About.xml` is bilingual: `<name>` and `<description>` carry Chinese first, then English.** When adding, removing, or modifying a patch:
  - Update the `Current patches:` bullet list in `<description>` so it matches the actual patch groups in the source — update both the Chinese block and the English block; they must describe the same set of patches.
  - If the patch targets a third-party mod, add its `packageId` to `<loadAfter>` (target mods are listed under `loadAfter`, not as dependencies). Vanilla/Anomaly targets do not need a `loadAfter` entry.
- Rebuild the corresponding DLL after source changes. This local 1.7 upgrade remains uncommitted until the user explicitly requests commit/push.
- New patch groups follow the existing pattern: `TryApply` returns `false` (skipped) unless the target mod is active and the target method was found.

## Local 1.7 beta port

- Independent `1.7/Source` and `1.7/Assemblies` preserve all legacy 1.6 bytes.
- Fixed net10 Ref1.7.4977-alpha.1 / Harmony3.0.0-rc.2 / targeted Publicizer2.3.2; retain all 30 groups and optional dependency detection.
- Record per-group true targets, runtime callback semantics, registration, functional evidence and missing optional boundaries in UPGRADE-1.7.md; silent skip/compile-only is not functional proof.
- The root agent alone runs isolated game instances and applies stage files to the real repository.
