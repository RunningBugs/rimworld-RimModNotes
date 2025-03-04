using Verse;
using HarmonyLib;
using RimWorld.Planet;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using System.Linq;
using System;

namespace WorldMapEditor;
[StaticConstructorOnStartup]
public static class Start
{
    static Start()
    {
        Harmony harmony = new("com.runningbugs.worldmapeditor");
        harmony.PatchAll();
    }
}


[DefOf]
public static class MyDefOfs
{
    public static WorldObjectDef InvisibleWorldObject;
}

[StaticConstructorOnStartup]
public class MyTextures
{
    public static Texture2D biomeIcon = ContentFinder<Texture2D>.Get("biome", true);
    public static Texture2D hillnessIcon = ContentFinder<Texture2D>.Get("hillness", true);
}

public class InvisibleObjectWorldGenStep : WorldGenStep
{
    public override int SeedPart => 123456789;

    /**
      * Generate all InvisibleObjects
      */
    public override void GenerateFresh(string seed)
    {
        var worldGrid = Find.WorldGrid;
        for (int i = 0; i < worldGrid.TilesCount; i++)
        {
            InvisibleWorldObject inv = (InvisibleWorldObject)WorldObjectMaker.MakeWorldObject(MyDefOfs.InvisibleWorldObject);
            inv.Tile = i;
            ToggleIconPatcher.invisibleWorldObjects.Add(inv);
            // Find.WorldObjects.Add(inv);
        }
    }
}

public class WorldMapEditorGameComponent : GameComponent
{
    public WorldMapEditorGameComponent(Game game)
    {
    }

    public override void FinalizeInit()
    {
        var woToRemove = new List<WorldObject>();
        foreach (WorldObject wo in Find.WorldObjects.AllWorldObjects.Where(o => o is InvisibleWorldObject))
        {
            woToRemove.Add(wo);
        }
        foreach (WorldObject wo in woToRemove)
        {
            Find.WorldObjects.Remove(wo);
        }
    }
}

public static class WorldMapEditorUtils
{
    public static void CleanUpWorldObjects() {
        var woToRemove = new List<WorldObject>();
        foreach (WorldObject wo in Find.WorldObjects.AllWorldObjects.Where(o => o is InvisibleWorldObject))
        {
            woToRemove.Add(wo);
        }
        foreach (WorldObject wo in woToRemove)
        {
            Find.WorldObjects.Remove(wo);
        }
        ToggleIconPatcher.InEditMode = false;
    }
}

// public class LoadingWindow : Window
// {
//     public override Vector2 InitialSize => new(300, 100);

//     public override void DoWindowContents(Rect inRect)
//     {
//         Text.Font = GameFont.Medium;
//         Widgets.Label(inRect, "WorldMapEditorLoading".Translate());
//     }
// }

[HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls", MethodType.Normal)]
[StaticConstructorOnStartup]
public class ToggleIconPatcher
{
    public static bool InEditMode = false;

    public static Texture2D editModeIcon = ContentFinder<Texture2D>.Get("design", true);

    public static List<InvisibleWorldObject> invisibleWorldObjects = new();

    public static void Postfix(WidgetRow row, bool worldView)
    {
        if (worldView && Current.ProgramState == ProgramState.Playing)
        {
            var before = InEditMode;
            row.ToggleableIcon(ref InEditMode, editModeIcon, "WorldMapEditorToggleButtonTooltip".Translate(), SoundDefOf.Mouseover_ButtonToggle, null);
            var after = InEditMode;

            if (before && !after)
            {
                /// remove world objects from the world
                WorldMapEditorUtils.CleanUpWorldObjects();
            }
            else if (!before && after)
            {
                /// add world objects to the world in a seperate thread
                var woToAdd = new List<WorldObject>();
                foreach (InvisibleWorldObject inv in invisibleWorldObjects)
                {
                    // Find.WorldObjects.Add(inv);
                    woToAdd.Add(inv);
                }

                // Find.WorldObjects.AllWorldObjects.Add(inv);
                    // Find.WorldObjects.worldObjectsHashSet.Add(inv);
                Find.WorldObjects.AllWorldObjects.AddRange(woToAdd);
                Find.WorldObjects.worldObjectsHashSet.AddRange(woToAdd);
            }
        }
    }
}

// [HarmonyPatch(typeof(GenScene), nameof(GenScene.GoToMainMenu))]
// public static class GenScene_GoToMainMenu_Patch
// {
//     public static void Prefix()
//     {
//         WorldMapEditorUtils.CleanUpWorldObjects();
//     }
// }

// [HarmonyPatch(typeof(Root), nameof(Root.Shutdown))]
// public static class Root_Shutdown_Patch
// {
//     public static void Prefix()
//     {
//         WorldMapEditorUtils.CleanUpWorldObjects();
//     }
// }

[HarmonyPatch(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.ExposeData))]
public static class WorldObjectsHolder_ExposeData_Patch
{
    public static void Prefix()
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            WorldMapEditorUtils.CleanUpWorldObjects();
        }
    }
}


public class WorldMapEditorCompProps : WorldObjectCompProperties
{
    public WorldMapEditorCompProps()
    {
        compClass = typeof(WorldMapEditorComp);
    }
}


public class WorldMapEditorComp : WorldObjectComp
{
    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (Gizmo gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }

        yield return new Command_Action
        {
            defaultLabel = "DestroySettlement".Translate(),
            icon = SettlementAbandonUtility.AbandonCommandTex,
            action = delegate
            {
                /// Destroy the settlement on the selected tile
                var sToRemove = new List<Settlement>();
                foreach (WorldObject wo in Find.WorldSelector.SelectedObjects.Where(o => o is InvisibleWorldObject))
                {
                    Settlement s = Find.WorldObjects.SettlementAt(wo.Tile);
                    if (s != null)
                    {
                        if (s.Spawned)
                        {
                            /// This way, it won't trigger destroy event signal & quests signals
                            sToRemove.Add(s);
                        }
                    }
                }
                foreach (Settlement s in sToRemove)
                {
                    Find.WorldObjects.Remove(s);
                }
            }
        };


        yield return new Command_Action
        {
            defaultLabel = "GenerateSettlement".Translate(),
            icon = SettleUtility.SettleCommandTex,
            action = delegate
            {
                /// Generate a settlement
                /// Will create a float menu with a list of all non-player factions
                /// Selecting a faction will create a settlement with that faction
                var menuOptions = new List<FloatMenuOption>();
                foreach (Faction f in Find.FactionManager.AllFactions)
                {
                    if (f.def.settlementTexturePath.NullOrEmpty())
                    {
                        f.def.settlementTexturePath = BaseContent.BadTexPath;
                    }
                    if (!f.def.isPlayer)
                    {
                        FloatMenuOption option = new(f.Name, delegate
                        {
                            foreach (WorldObject wo in Find.WorldSelector.SelectedObjects.Where(o => o is InvisibleWorldObject))
                            {
                                /// First, clear possible settlements on this tile
                                Settlement s = Find.WorldObjects.SettlementAt(wo.Tile);
                                if (s != null)
                                {
                                    if (s.Spawned)
                                    {
                                        /// This way, it won't trigger destroy event signal & quests signals
                                        Find.WorldObjects.Remove(s);
                                    }
                                }

                                Settlement settlement = WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement) as Settlement;
                                settlement.SetFaction(f);
                                settlement.Tile = wo.Tile;
                                settlement.Name = f.Hidden ? f.Name : SettlementNameGenerator.GenerateSettlementName(settlement);
                                settlement.def.texture = BaseContent.BadTexPath;
                                Find.WorldObjects.Add(settlement);
                            }
                        }, f.def.factionIcon, f.Color);
                        menuOptions.Add(option);
                    }
                }
                if (menuOptions.Count > 0)
                {
                    Find.WindowStack.Add(new FloatMenu(menuOptions));
                }
            }
        };

        yield return new Command_Action
        {
            defaultLabel = "SetBiome".Translate(),
            icon = MyTextures.biomeIcon,
            action = delegate
            {
                /// Set the biome of the selected tiles
                var menuOptions = new List<FloatMenuOption>();
                foreach (var biome in DefDatabase<BiomeDef>.AllDefs)
                {

                    FloatMenuOption option = new(biome.LabelCap, delegate
                    {
                        foreach (WorldObject wo in Find.WorldSelector.SelectedObjects.Where(o => o is InvisibleWorldObject))
                        {
                            Find.WorldGrid[wo.Tile].biome = biome;
                            Find.World.renderer.Notify_StaticWorldObjectPosChanged();
                            Find.World.renderer.RegenerateAllLayersNow();
                        }
                    });
                    menuOptions.Add(option);
                }
                if (menuOptions.Count > 0)
                {
                    Find.WindowStack.Add(new FloatMenu(menuOptions));
                }
            }
        };


        yield return new Command_Action
        {
            defaultLabel = "SetHilliness".Translate(),
            icon = MyTextures.hillnessIcon,
            action = delegate
            {
                /// Set the Hillness type of the selected tiles
                var menuOptions = new List<FloatMenuOption>();
                foreach (Hilliness hillness in Enum.GetValues(typeof(Hilliness)))
                {
                    if (hillness == Hilliness.Undefined)
                    {
                        continue;
                    }

                    FloatMenuOption option = new(hillness.GetLabelCap(), delegate
                    {
                        foreach (WorldObject wo in Find.WorldSelector.SelectedObjects.Where(o => o is InvisibleWorldObject))
                        {
                            Find.WorldGrid[wo.Tile].hilliness = hillness;
                            Find.World.renderer.Notify_StaticWorldObjectPosChanged();
                            Find.World.renderer.RegenerateAllLayersNow();
                        }
                    });
                    menuOptions.Add(option);
                }
                if (menuOptions.Count > 0)
                {
                    Find.WindowStack.Add(new FloatMenu(menuOptions));
                }
            }
        };
    }
}


public class InvisibleWorldObject : WorldObject
{
    public override string Label => "SelectedTiles".Translate();

    public override bool SelectableNow => true;

    private static Material invMat = null;

    public override Material Material
    {
        get
        {
            if (invMat == null)
            {
                invMat = MaterialPool.MatFrom("Invisible", ShaderDatabase.WorldOverlayTransparentLit, WorldMaterials.WorldObjectRenderQueue);
            }
            return invMat;
        }
    }

    public override void ExposeData()
    {
    }
}


