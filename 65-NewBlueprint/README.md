# Blueprint System Mod for RimWorld

A mod that allows you to save and place blueprints in RimWorld using the game's built-in systems.

## Features

- **Save Blueprints**: Drag-select any area with buildings to save as a blueprint
- **Place Blueprints**: Place saved blueprints as construction orders for your colonists
- **Rotation Support**: Rotate blueprints before placing them
- **Blueprint Management**: Save, load, and delete blueprints with a user-friendly interface
- **Full Compatibility**: Uses RimWorld's built-in prefab and blueprint systems for maximum compatibility

## How to Use

### Saving a Blueprint
1. Open the Architect menu and go to the "Blueprints" category
2. Select the "Save Blueprint" tool
3. Drag to select an area containing buildings you want to save
4. Enter a name and optional description for your blueprint
5. Click "Save" to save the blueprint

### Placing a Blueprint
1. Open the Architect menu and go to the "Blueprints" category  
2. Select the "Place Blueprint" tool
3. Choose a saved blueprint from the list
4. Use Q/E keys to rotate the blueprint if needed
5. Click where you want to place the blueprint
6. Your colonists will construct the buildings using available materials

## Technical Details

This mod uses RimWorld's built-in systems:
- `PrefabUtility.CreatePrefab()` for capturing building layouts
- `PrefabUtility.SpawnPrefab()` with `blueprint: true` for placing construction orders
- Standard XML serialization for saving/loading blueprints
- Built-in designator system for UI integration

## Compatibility

- **RimWorld Version**: 1.6
- **Mod Compatibility**: High - uses only public game APIs
- **Save Game Safe**: Yes - can be added to existing saves

## Installation

1. Extract to your RimWorld Mods folder
2. Enable in the mod list
3. Start or load a game

## Building from Source

1. Open `Source/BlueprintMod/BlueprintMod.csproj` in Visual Studio or compatible IDE
2. Ensure references to `Assembly-CSharp.dll` point to your RimWorld installation
3. Build the project
4. The compiled DLL will be placed in the `Assemblies` folder

## License

This mod is provided as-is for educational and gameplay purposes.