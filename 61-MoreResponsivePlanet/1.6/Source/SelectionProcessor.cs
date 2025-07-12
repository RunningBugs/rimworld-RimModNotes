using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld.Planet;
using RimWorld;

namespace MoreResponsivePlanet
{
    public class SelectionProcessor
    {
        private static SelectionProcessor _instance;
        public static SelectionProcessor Instance => _instance ??= new SelectionProcessor();

        public void ProcessDragSelection(WorldSelector worldSelector, Rect dragRect)
        {
            // Use the original RimWorld selection logic but only call it once
            // This mirrors the SelectInsideDragBox method from WorldSelector

            bool shiftIsHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (!shiftIsHeld)
            {
                worldSelector.ClearSelection();
            }

            bool foundSomething = false;

            // Check for caravans in colonist bar first (highest priority)
            if (Current.ProgramState == ProgramState.Playing)
            {
                List<Caravan> caravansInRect = Find.ColonistBar.CaravanMembersCaravansInScreenRect(dragRect);
                for (int i = 0; i < caravansInRect.Count; i++)
                {
                    foundSomething = true;
                    worldSelector.Select(caravansInRect[i]);
                }
            }

            // Check for colonists/corpses in colonist bar
            if (!foundSomething && Current.ProgramState == ProgramState.Playing)
            {
                List<Thing> colonistsInRect = Find.ColonistBar.MapColonistsOrCorpsesInScreenRect(dragRect);
                for (int j = 0; j < colonistsInRect.Count; j++)
                {
                    if (!foundSomething)
                    {
                        CameraJumper.TryJumpAndSelect(colonistsInRect[j]);
                        foundSomething = true;
                    }
                    else
                    {
                        Find.Selector.Select(colonistsInRect[j]);
                    }
                }
            }

            // Check for world objects
            if (!foundSomething)
            {
                List<WorldObject> worldObjectsInRect = WorldObjectSelectionUtility
                    .MultiSelectableWorldObjectsInScreenRectDistinct(dragRect).ToList();

                // Prioritize caravans
                if (worldObjectsInRect.Any(x => x is Caravan))
                {
                    worldObjectsInRect.RemoveAll(x => !(x is Caravan));
                    if (worldObjectsInRect.Any(x => x.Faction == Faction.OfPlayer))
                    {
                        worldObjectsInRect.RemoveAll(x => x.Faction != Faction.OfPlayer);
                    }
                }

                for (int k = 0; k < worldObjectsInRect.Count; k++)
                {
                    foundSomething = true;
                    worldSelector.Select(worldObjectsInRect[k]);
                }
            }

            // If nothing found and drag is small enough, try to select tile
            if (!foundSomething)
            {
                float diagonal = Mathf.Sqrt(dragRect.width * dragRect.width + dragRect.height * dragRect.height);
                bool canSelectTile = diagonal < 30f; // Same threshold as original
                
                if (canSelectTile)
                {
                    ProcessSingleClick(worldSelector, canSelectTile);
                }
            }
        }

        public void ProcessSingleClick(WorldSelector worldSelector, bool canSelectTile = true)
        {
            // This mirrors the SelectUnderMouse method from WorldSelector
            
            // Check colonist bar first
            if (Current.ProgramState == ProgramState.Playing)
            {
                Thing thing = Find.ColonistBar.ColonistOrCorpseAt(UI.MousePositionOnUIInverted);
                Pawn pawn = thing as Pawn;
                if (thing != null && (pawn == null || !pawn.IsCaravanMember()))
                {
                    if (thing.Spawned)
                    {
                        CameraJumper.TryJumpAndSelect(thing);
                    }
                    else
                    {
                        CameraJumper.TryJump(thing);
                    }
                    return;
                }
            }

            bool shiftIsHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            
            // Get selectable objects under mouse
            bool clickedDirectlyOnCaravan;
            bool usedColonistBar;
            List<WorldObject> selectableObjects = worldSelector.SelectableObjectsUnderMouse(out clickedDirectlyOnCaravan, out usedColonistBar).ToList();

            if (usedColonistBar || (clickedDirectlyOnCaravan && selectableObjects.Count >= 2))
            {
                canSelectTile = false;
            }

            if (selectableObjects.Count == 0)
            {
                if (shiftIsHeld) return;

                PlanetTile previousTile = worldSelector.SelectedTile;
                worldSelector.ClearSelection();
                
                if (canSelectTile)
                {
                    worldSelector.SelectedTile = GenWorld.MouseTile();
                    if (previousTile != worldSelector.SelectedTile && worldSelector.SelectedTile.Valid)
                    {
                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    }
                }
            }
            else if (selectableObjects.FirstOrDefault(obj => worldSelector.SelectedObjects.Contains(obj)) != null)
            {
                if (shiftIsHeld)
                {
                    foreach (WorldObject obj in selectableObjects)
                    {
                        if (worldSelector.SelectedObjects.Contains(obj))
                        {
                            worldSelector.Deselect(obj);
                        }
                    }
                    return;
                }

                PlanetTile tile = canSelectTile ? GenWorld.MouseTile() : PlanetTile.Invalid;
                SelectFirstOrNextFrom(worldSelector, selectableObjects, tile);
            }
            else
            {
                if (!shiftIsHeld)
                {
                    worldSelector.ClearSelection();
                }
                worldSelector.Select(selectableObjects[0]);
            }
        }

        private void SelectFirstOrNextFrom(WorldSelector worldSelector, List<WorldObject> objects, PlanetTile tile)
        {
            // This mirrors the SelectFirstOrNextFrom method from WorldSelector
            int selectedIndex = objects.FindIndex(x => worldSelector.SelectedObjects.Contains(x));
            PlanetTile tileToSelect = PlanetTile.Invalid;
            int objectIndexToSelect = -1;

            if (selectedIndex != -1)
            {
                if (selectedIndex == objects.Count - 1 || worldSelector.SelectedObjects.Count >= 2)
                {
                    if (worldSelector.SelectedObjects.Count >= 2)
                    {
                        objectIndexToSelect = 0;
                    }
                    else if (tile.Valid)
                    {
                        tileToSelect = tile;
                    }
                    else
                    {
                        objectIndexToSelect = 0;
                    }
                }
                else
                {
                    objectIndexToSelect = selectedIndex + 1;
                }
            }
            else if (objects.Count == 0)
            {
                tileToSelect = tile;
            }
            else
            {
                objectIndexToSelect = 0;
            }

            worldSelector.ClearSelection();
            if (objectIndexToSelect >= 0)
            {
                worldSelector.Select(objects[objectIndexToSelect]);
            }
            worldSelector.SelectedTile = tileToSelect;
        }
    }
}
