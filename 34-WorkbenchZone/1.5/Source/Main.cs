using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Noise;


// using System.Reflection;
// using HarmonyLib;

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

    public class ZoneSettingsUI : Mod {
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
            min = min < ZoneSettings.maxRadius ? min : ZoneSettings.maxRadius;
            min -= 0.5f; // Making the zone a little bit smaller than the search radius
            return min;
        }

        private void CreateZone()
        {
            /// We need to decide the radius of the new zone
            /// This is by searching all bills on the workbench, and taking the min radius

            if (parent is not Building_WorkTable workTable)
            {
                return;
            }

            if (workTable.billStack.Bills.Empty())
            {
                return;
            }

            var interactCell = workTable.InteractionCell;
            var centerCell = workTable.Position;

            var RadialCells = GenRadial.RadialCellsAround(centerCell, MinBillRadius(parent as Building_WorkTable), useCenter: true);
            if (parent.Map.zoneManager.ZoneAt(interactCell) != null)
            {
                /// If there is a zone at the workbench, check if it is a stockpile
                /// If it is not a stockpile, send a message about existing zone
                /// If it is a stockpile, create a workbench zone

                if (parent.Map.zoneManager.ZoneAt(interactCell) is Zone_Stockpile existing)
                {
                    /// We need to decide storage settings for the new zone
                    /// since it's existing zone, we will use the same settings
                    /// We will select cells within the range that are not already in any zone or are in the existing zone
                    /// And the cell must be zoneable
                    parent.Map.floodFiller.FloodFill(interactCell,
                        (IntVec3 c) => RadialCells.Contains(c)
                            && (parent.Map.zoneManager.ZoneAt(c) == null || parent.Map.zoneManager.ZoneAt(c) == existing)
                            && Designator_ZoneAdd.IsZoneableCell(c, parent.Map),
                        delegate (IntVec3 c)
                        {
                            if (!existing.ContainsCell(c))
                            {
                                existing.AddCell(c);
                            }
                        }
                    );
                }
                else
                {
                    Messages.Message("WorkbenchHasZoneNonStockpile".Translate(), MessageTypeDefOf.NeutralEvent);
                }
                return;
            }

            /// If there is no zone at the workbench, create a new zone
            Zone_Stockpile newZone = new(StorageSettingsPreset.DefaultStockpile, parent.Map.zoneManager);
            newZone.settings.filter.SetDisallowAll();
            var billStack = (parent as Building_WorkTable).billStack;
            billStack.Bills.ForEach(bill =>
            {
                newZone.settings.filter.MergeAll(bill.ingredientFilter);
            });
            parent.Map.zoneManager.RegisterZone(newZone);
            Zone_Stockpile existingStockpile = null;
            parent.Map.floodFiller.FloodFill(interactCell,
                delegate (IntVec3 c)
                {
                    if (parent.Map.zoneManager.ZoneAt(c) is Zone_Stockpile zone_Stockpile)
                    {
                        existingStockpile = zone_Stockpile;
                    }
                    return RadialCells.Contains(c) && parent.Map.zoneManager.ZoneAt(c) == null && Designator_ZoneAdd.IsZoneableCell(c, parent.Map);
                },
                newZone.AddCell
            );

            if (existingStockpile == null)
            {
                return;
            }
            List<IntVec3> list = newZone.Cells.ToList();
            newZone.Delete();
            foreach (IntVec3 item in list)
            {
                existingStockpile.AddCell(item);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent is Building_WorkTable)
            {
                yield return new Command_Action
                {
                    defaultLabel = "WzCreateWorkbenchZone".Translate(),
                    defaultDesc = "WzCreateWorkbenchZoneDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Stockpile"),
                    action = CreateZone
                };
            }
        }
    }
}

