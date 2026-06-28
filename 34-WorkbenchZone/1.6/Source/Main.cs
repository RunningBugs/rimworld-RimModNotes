using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

using Logs = Logger.Log;

namespace WorkbenchZone
{
    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Logs.Message("WorkbenchZone loaded");
            var harmony = new Harmony("com.runningbugs.workbenchzone");
            harmony.PatchAll();
        }
    }

    public class ZoneSettings : ModSettings
    {
        public static float maxRadius = 7;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref maxRadius, "WorkbenchZone.maxRadius", 20);
        }
    }

    public class ZoneSettingsUI : Mod
    {
        public ZoneSettingsUI(ModContentPack content) : base(content)
        {
            GetSettings<ZoneSettings>();
        }

        public override string SettingsCategory() => "WorkbenchZone".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var settings = GetSettings<ZoneSettings>();
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("WorkbenchZoneSettings".Translate());
            listing.Gap();
            listing.Label("WzMaxRadius".Translate(ZoneSettings.maxRadius));
            listing.Gap();
            ZoneSettings.maxRadius = listing.Slider(ZoneSettings.maxRadius, 1, 40);
            listing.Gap();
            listing.End();
            settings.Write();
        }
    }

    public enum WorkbenchZoneShape
    {
        Rectangle,
        Circle
    }

    public static class ThingFilterExtension
    {
        public static void MergeAll(this ThingFilter filter, ThingFilter other)
        {
            foreach (var thingDef in other.AllowedThingDefs)
            {
                filter.SetAllow(thingDef, true);
            }
        }
    }

    public class CanCreateZone : ThingComp
    {
        private static float MinBillRadius(Building_WorkTable workTable)
        {
            var min = workTable.billStack.Bills.Min(bill => bill.ingredientSearchRadius);
            min = Math.Min(min, ZoneSettings.maxRadius);
            min -= 0.5f; // Keep every generated storage cell safely inside vanilla's strict bill search radius.
            return Math.Max(0f, min);
        }

        private static bool VanillaIngredientSearchCanSeeCell(IntVec3 center, IntVec3 c, float radius)
        {
            // WorkGiver_DoBill uses the exact same strict predicate against billGiver.Position:
            // (t.Position - billGiver.Position).LengthHorizontalSquared < searchRadius * searchRadius.
            float radiusSq = radius * radius;
            return (float)(c - center).LengthHorizontalSquared < radiusSq;
        }

        private static int MaxRectangleOffsetInsideSearchRadius(float radius)
        {
            // A rectangle's corners must also pass vanilla's circular bill-search predicate.
            // For a square centered on the worktable, the farthest cells are (offset, offset),
            // so require 2 * offset^2 < radius^2.
            return Math.Max(0, Mathf.CeilToInt(radius / Mathf.Sqrt(2f)) - 1);
        }

        private static int MaxCircleOffsetInsideSearchRadius(float radius)
        {
            // Axis-aligned edge cells with offset == an integer radius fail vanilla's strict '<'.
            return Math.Max(0, Mathf.CeilToInt(radius) - 1);
        }

        private static HashSet<IntVec3> CellsForShape(Building_WorkTable workTable, float radius, WorkbenchZoneShape shape)
        {
            var cells = new HashSet<IntVec3>();
            var center = workTable.Position;
            int maxOffset = shape == WorkbenchZoneShape.Rectangle
                ? MaxRectangleOffsetInsideSearchRadius(radius)
                : MaxCircleOffsetInsideSearchRadius(radius);

            foreach (IntVec3 c in CellRect.CenteredOn(center, maxOffset).Cells)
            {
                if (!c.InBounds(workTable.Map))
                {
                    continue;
                }

                if (shape == WorkbenchZoneShape.Circle && !VanillaIngredientSearchCanSeeCell(center, c, radius))
                {
                    continue;
                }

                cells.Add(c);
            }

            return cells;
        }

        private void CreateZone(WorkbenchZoneShape shape = WorkbenchZoneShape.Rectangle)
        {
            if (parent is not Building_WorkTable workTable)
            {
                return;
            }

            if (workTable.billStack.Bills.Empty())
            {
                Messages.Message("WzNoBills".Translate(), MessageTypeDefOf.NeutralEvent);
                return;
            }

            var interactCell = workTable.InteractionCell;
            var cellsForShape = CellsForShape(workTable, MinBillRadius(workTable), shape);
            var map = parent.Map;

            if (map.zoneManager.ZoneAt(interactCell) != null)
            {
                if (map.zoneManager.ZoneAt(interactCell) is Zone_Stockpile existing)
                {
                    map.floodFiller.FloodFill(interactCell,
                        c => cellsForShape.Contains(c)
                            && (map.zoneManager.ZoneAt(c) == null || map.zoneManager.ZoneAt(c) == existing)
                            && Designator_ZoneAdd.IsZoneableCell(c, map),
                        c =>
                        {
                            if (!existing.ContainsCell(c))
                            {
                                existing.AddCell(c);
                            }
                        }
                    );
                    FinalizeStockpile(existing, workTable);
                }
                else
                {
                    Messages.Message("WorkbenchHasZoneNonStockpile".Translate(), MessageTypeDefOf.NeutralEvent);
                }

                return;
            }

            Zone_Stockpile newZone = new(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            newZone.settings.filter.SetDisallowAll();
            workTable.billStack.Bills.ForEach(bill => newZone.settings.filter.MergeAll(bill.ingredientFilter));

            map.zoneManager.RegisterZone(newZone);
            Zone_Stockpile existingStockpile = null;
            map.floodFiller.FloodFill(interactCell,
                delegate (IntVec3 c)
                {
                    if (map.zoneManager.ZoneAt(c) is Zone_Stockpile zone_Stockpile)
                    {
                        existingStockpile = zone_Stockpile;
                    }

                    return cellsForShape.Contains(c)
                        && map.zoneManager.ZoneAt(c) == null
                        && Designator_ZoneAdd.IsZoneableCell(c, map);
                },
                newZone.AddCell
            );

            if (newZone.CellCount == 0)
            {
                newZone.Delete(playSound: false);
                return;
            }

            if (existingStockpile == null)
            {
                FinalizeStockpile(newZone, workTable);
                return;
            }

            List<IntVec3> list = newZone.Cells.ToList();
            newZone.Delete(playSound: false);
            foreach (IntVec3 item in list)
            {
                if (!existingStockpile.ContainsCell(item))
                {
                    existingStockpile.AddCell(item);
                }
            }
            FinalizeStockpile(existingStockpile, workTable);
        }

        private static void FinalizeStockpile(Zone_Stockpile zone, Building_WorkTable workTable)
        {
            zone.CheckContiguous();
            zone.slotGroup?.RemoveHaulDesignationOnStoredThings();
            zone.Notify_SettingsChanged();

            // If a pawn tried this bill before the zone existed, vanilla may have cached a
            // failed ingredient search for 500-600 ticks. Reset it so the new/expanded zone
            // is considered immediately.
            foreach (Bill bill in workTable.billStack.Bills)
            {
                bill.nextTickToSearchForIngredients = 0;
            }
        }

        private void OpenShapeMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("WzCreateRectangleZone".Translate(), () => CreateZone(WorkbenchZoneShape.Rectangle)),
                new FloatMenuOption("WzCreateCircleZone".Translate(), () => CreateZone(WorkbenchZoneShape.Circle))
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent is Building_WorkTable)
            {
                yield return new Command_CreateWorkbenchZone
                {
                    defaultLabel = "WzCreateWorkbenchZone".Translate(),
                    defaultDesc = "WzCreateWorkbenchZoneDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Stockpile"),
                    action = () => CreateZone(WorkbenchZoneShape.Rectangle),
                    rightClickAction = OpenShapeMenu
                };
            }
        }
    }

    public class Command_CreateWorkbenchZone : Command_Action
    {
        public Action rightClickAction;

        public override void ProcessInput(Event ev)
        {
            if (ev.button == 1 && rightClickAction != null)
            {
                if (CurActivateSound != null)
                {
                    CurActivateSound.PlayOneShotOnCamera();
                }
                rightClickAction();
                return;
            }

            base.ProcessInput(ev);
        }
    }
}
