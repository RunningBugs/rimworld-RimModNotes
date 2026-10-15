using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RW17.Validation;

public static class Mod34Checks
{
    public const string PackageId = "RunningBugs.WorkbenchZone";

    public static void Run(Map map)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "WorkbenchZone");
        var compType = assembly.GetType("WorkbenchZone.CanCreateZone", true);
        var settingsType = assembly.GetType("WorkbenchZone.ZoneSettings", true);
        var radius = settingsType.GetField("maxRadius");
        var oldRadius = radius.GetValue(null);
        var oldZones = map.zoneManager.AllZones.ToArray();
        var oldWindows = Find.WindowStack.Windows.ToArray();
        Building_WorkTable table = null;
        Pawn pawn = null;
        Thing rice = null;
        try
        {
            var center = map.AllCells.First(c => CellRect.CenteredOn(c, 7).Cells.All(q => q.InBounds(map) && q.Standable(map) && !q.Fogged(map) && q.GetThingList(map).Count == 0 && map.zoneManager.ZoneAt(q) == null && Designator_ZoneAdd.IsZoneableCell(q, map)));
            var tableDef = DefDatabase<ThingDef>.GetNamed("ElectricStove");
            Require(tableDef.comps.Count(p => p.compClass == compType) == 1, "Actual XML must attach one zone comp to stove");
            Require(DefDatabase<ThingDef>.AllDefsListForReading.Where(d => typeof(Building_WorkTable).IsAssignableFrom(d.thingClass)).All(d => d.comps.Count(p => p.compClass == compType) == 1), "Every loaded concrete worktable must receive exactly one comp, including inherited-only Brewery");
            table = (Building_WorkTable)ThingMaker.MakeThing(tableDef);
            table.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(table, center, map, Rot4.North);
            var comp = table.AllComps.Single(c => c.GetType() == compType);
            var command = (Command_Action)table.GetGizmos().Single(g => g.GetType().FullName == "WorkbenchZone.Command_CreateWorkbenchZone");
            radius.SetValue(null, 5f);
            command.ProcessInput(new Event { type = EventType.MouseDown, button = 0 });
            Require(map.zoneManager.AllZones.SequenceEqual(oldZones), "No-bill command must not create any zone");
            var recipe = DefDatabase<RecipeDef>.GetNamed("CookMealSimple");
            var rawRice = DefDatabase<ThingDef>.GetNamed("RawRice"); var rawCorn = DefDatabase<ThingDef>.GetNamed("RawCorn");
            var bill1 = new Bill_Production(recipe) { ingredientSearchRadius = 5f, nextTickToSearchForIngredients = 999999 };
            var bill2 = new Bill_Production(recipe) { ingredientSearchRadius = 7f, nextTickToSearchForIngredients = 888888 };
            bill1.ingredientFilter.SetDisallowAll(); bill1.ingredientFilter.SetAllow(rawRice, true);
            bill2.ingredientFilter.SetDisallowAll(); bill2.ingredientFilter.SetAllow(rawCorn, true);
            table.billStack.AddBill(bill1); table.billStack.AddBill(bill2);
            command.ProcessInput(new Event { type = EventType.MouseDown, button = 0 });
            var zone = (Zone_Stockpile)map.zoneManager.ZoneAt(table.InteractionCell);
            Require(zone != null && !oldZones.Contains(zone), "Actual command did not create stockpile at interaction cell");
            var expected = CellRect.CenteredOn(center, 3).Cells.Where(q => Designator_ZoneAdd.IsZoneableCell(q, map)).ToHashSet();
            Require(expected.SetEquals(zone.Cells), "Actual rectangular flood zone does not match connected blank fixture extent");
            Require(zone.settings.filter.Allows(rawRice) && zone.settings.filter.Allows(rawCorn) && !zone.settings.filter.Allows(ThingDefOf.Steel), "New stockpile must merge exact two ingredient filters");
            Require(zone.Cells.All(q => (q - table.Position).LengthHorizontalSquared < bill1.ingredientSearchRadius * bill1.ingredientSearchRadius), "Generated rectangle contains vanilla-strict-radius-invisible cells");
            Require(bill1.nextTickToSearchForIngredients == 0 && bill2.nextTickToSearchForIngredients == 0, "Actual finalization did not reset every bill search cache");
            int previous = zone.CellCount;
            bill1.ingredientSearchRadius = 7f; bill2.ingredientSearchRadius = 9f; radius.SetValue(null, 7f);
            bill1.nextTickToSearchForIngredients = 12345; bill2.nextTickToSearchForIngredients = 67890;
            command.action();
            Require(ReferenceEquals(zone, map.zoneManager.ZoneAt(table.InteractionCell)) && zone.CellCount > previous && CellRect.CenteredOn(center, 4).Cells.Where(q => Designator_ZoneAdd.IsZoneableCell(q, map)).ToHashSet().SetEquals(zone.Cells), "Existing interaction stockpile expansion failed");
            Require(bill1.nextTickToSearchForIngredients == 0 && bill2.nextTickToSearchForIngredients == 0, "Expanded stockpile did not reset native failed-search cache");
            zone.Delete(false);
            bill1.ingredientSearchRadius = 5f; bill2.ingredientSearchRadius = 7f; radius.SetValue(null, 5f);
            command.ProcessInput(new Event { type = EventType.MouseDown, button = 1 });
            var menu = Find.WindowStack.Windows.OfType<FloatMenu>().Single(w => !oldWindows.Contains(w));
            var options = (List<FloatMenuOption>)typeof(FloatMenu).GetField("options", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
            Require(options.Count == 2, "Actual right-click shape menu must offer rectangle and circle");
            options.Single(o => o.Label == "WzCreateCircleZone".Translate().ToString()).action();
            Find.WindowStack.TryRemove(menu, false);
            zone = (Zone_Stockpile)map.zoneManager.ZoneAt(table.InteractionCell);
            var expectedCircle = CellRect.CenteredOn(center, 4).Cells.Where(q => (q - center).LengthHorizontalSquared < 4.5f * 4.5f && Designator_ZoneAdd.IsZoneableCell(q, map)).ToHashSet();
            Require(zone != null && expectedCircle.SetEquals(zone.Cells) && zone.Cells.Any(q => Math.Abs(q.x - center.x) == 4), "Actual menu circle did not produce circular strict-boundary stockpile");
            rice = ThingMaker.MakeThing(rawRice); rice.stackCount = 50;
            var riceCell = zone.Cells.First(q => q.Standable(map) && q.GetThingList(map).Count == 0 && q != table.InteractionCell);
            GenSpawn.Spawn(rice, riceCell, map);
            pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer); GenSpawn.Spawn(pawn, table.InteractionCell, map);
            var ingredients = new List<ThingCount>();
            var findIngredients = typeof(WorkGiver_DoBill).GetMethod("TryFindBestBillIngredients", BindingFlags.Static | BindingFlags.NonPublic);
            Require((bool)findIngredients.Invoke(null, new object[] { bill1, pawn, table, ingredients, new List<IngredientCount>() }) && ingredients.Any(t => t.Thing == rice && t.Count > 0), "Actual vanilla bill ingredient search cannot see stored generated-zone ingredient");
            zone.Delete(false);
            var growing = new Zone_Growing(map.zoneManager); map.zoneManager.RegisterZone(growing); growing.AddCell(table.InteractionCell);
            int count = map.zoneManager.AllZones.Count; command.action();
            Require(map.zoneManager.AllZones.Count == count && growing.CellCount == 1, "Non-stockpile interaction zone must block creation and remain intact");
            growing.Delete(false);
            radius.SetValue(null, 0.1f); command.action();
            Require(map.zoneManager.AllZones.SequenceEqual(oldZones), "Tiny radius cannot reach interaction cell: zero-cell temporary zone must be deregistered");
            var settings = (ModSettings)Activator.CreateInstance(settingsType);
            var path = Path.Combine(GenFilePaths.SaveDataFolderPath, "Mod34SettingsFixture.xml");
            Require(Scribe.mode == LoadSaveMode.Inactive && !File.Exists(path), "Scribe busy or fixture exists");
            radius.SetValue(null, 6.5f); Scribe.saver.InitSaving(path, "Mod34SettingsFixture"); settings.ExposeData(); Scribe.saver.FinalizeSaving();
            radius.SetValue(null, 1f); Scribe.loader.InitLoading(path); settings.ExposeData(); Scribe.loader.FinalizeLoading();
            Require((float)radius.GetValue(null) == 6.5f, "Actual static radius setting Scribe roundtrip failed");
        }
        finally
        {
            Scribe.ForceStop(); radius.SetValue(null, oldRadius);
            foreach (var zone in map.zoneManager.AllZones.Where(z => !oldZones.Contains(z)).ToArray()) zone.Delete(false);
            if (pawn != null && !pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
            if (rice != null && !rice.Destroyed) rice.Destroy(DestroyMode.Vanish);
            if (table != null && !table.Destroyed) table.Destroy(DestroyMode.Vanish);
            foreach (var window in Find.WindowStack.Windows.Where(w => !oldWindows.Contains(w)).ToArray()) Find.WindowStack.TryRemove(window, false);
        }
        Log.Message("RW17 Mod34: actual XML comp/gizmo, rectangle/circle/existing expansion/filter/cache, native ingredient search, no-bill/nonstockpile/tiny controls and Scribe passed; visible clicks/other-zone merge/save-reload pending");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Mod34: " + message);
    }
}
