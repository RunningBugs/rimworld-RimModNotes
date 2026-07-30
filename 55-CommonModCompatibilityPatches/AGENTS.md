# Common Mod Compatibility Patches

Runtime Harmony compatibility patches for commonly used RimWorld mods. Each patch group is dynamically detected at startup; if the target mod/type/method is absent, the patch is skipped silently.

## Layout

- `1.6/Source/CommonModCompatibilityPatches.cs` — all patch groups, one `internal static class` per target, each with a `TryApply(Harmony)` method registered in `CommonCompatibilityBootstrap`
- `1.6/Assemblies/CommonModCompatibilityPatches.dll` — build output, committed alongside the source
- `About/About.xml` — the mod's only documentation file
- `Tests/whitebox/test_common_mod_compatibility_patches_static.py` — static whitebox checks, run via `Tests/run_whitebox.sh`

## Conventions

- **Always sync `About/About.xml` on every change.** When adding, removing, or modifying a patch:
  - Update the `Current patches:` bullet list in `<description>` so it matches the actual patch groups in the source.
  - If the patch targets a third-party mod, add its `packageId` to `<loadAfter>` (target mods are listed under `loadAfter`, not as dependencies). Vanilla/Anomaly targets do not need a `loadAfter` entry.
- Rebuild the DLL after source changes and commit it together with the source.
- New patch groups follow the existing pattern: `TryApply` returns `false` (skipped) unless the target mod is active and the target method was found.
