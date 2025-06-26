# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is "Another Allow Tool" - a RimWorld mod that reimplements a subset of the popular Allow Tool mod's functionality without the HugsLib dependency. The mod adds quality-of-life features for item management and selection in RimWorld 1.6.

## Development Commands

### Building the Mod
```bash
dotnet build mod.csproj
```
The output DLL is automatically placed in `../Assemblies/AAT.dll` for use by RimWorld.

### Project Structure
- **Source/**: C# source code (.NET Framework 4.7.2)
- **Common/Defs/**: XML definition files for game content
- **Common/Textures/**: UI textures and icons
- **About/**: Mod metadata and description

## Architecture

### Core Components

**ModSettings.cs** - Entry point and Harmony patching
- `Start` class applies all Harmony patches on static construction
- Uses Harmony ID "com.RunningBugs.AnotherAllowTool"
- Minimal mod settings UI (currently empty)

**HaulUrgently.cs** - Priority hauling system
- `Designator_HaulUrgent`: UI designator for marking items to haul urgently
- `WorkGiver_HaulUrgently`: Work giver that processes urgent haul jobs
- `HaulUrgentlyCache`: Map component that caches designated items for performance
- `Alert_NoUrgentHaulStorage`: Alert when urgent items have no storage
- `PickUpAndHaulCompatHandler`: Compatibility layer for Pick Up And Haul mod

**SelectSimilar.cs** - Multi-selection functionality  
- `Designator_SelectSimilar`: Allows selecting all items of the same type as currently selected
- Patches `Thing.GetGizmos` to add the select similar button to thing inspection panels

### Key Design Patterns

**Harmony Patching**: Uses HarmonyLib for runtime method patching to integrate with RimWorld's systems
- `ReverseDesignatorDatabase_InitDesignators_Patch`: Registers custom designators
- `JobDriver_HaulToCell_MakeNewToils_Patch`: Cleans up urgent haul designations when jobs complete
- `Thing_GetGizmos_Patch`: Adds select similar gizmo to selected things

**MapComponent Caching**: `HaulUrgentlyCache` uses RimWorld's MapComponent system for per-map data storage and automatic cleanup

**DefOf Pattern**: Uses RimWorld's DefOf pattern for referencing XML-defined content (`HaulUrgentlyDefOf`)

### External Dependencies
- **Krafs.Rimworld.Ref** (1.6.4494-beta): RimWorld API references
- **Lib.Harmony** (2.3.*): Runtime method patching library

### Mod Integration
The mod includes compatibility handling for "Pick Up And Haul" mod through reflection-based detection and delegate injection in `PickUpAndHaulCompatHandler`.

## XML Definition Files

**HaulUrgentDefs.xml**: Defines the urgent hauling work type, work giver, and designation
**SelectSimilarDefs.xml**: Defines the select similar designation  
**DesignationCategoryDefs.xml**: Categorizes the new designators in the UI

## Development Notes

- All C# code uses namespace `AAT`
- Target framework is .NET Framework 4.7.2 with C# 11.0 language features
- Debug symbols are disabled for release builds
- Assembly output path is configured to go directly to mod's Assemblies folder