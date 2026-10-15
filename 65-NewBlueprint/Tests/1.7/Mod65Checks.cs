using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RW17.Validation;
public static class Mod65Checks
{
    public const string PackageId = "RunningBugs.blueprint2";
    public static void Run(Map map)
    {
        Require(Find.CurrentMap == map && ModsConfig.OdysseyActive, "Complete current disposable map and Odyssey foundation defs exist.");
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Blueprint2");
        var category = DefDatabase<DesignationCategoryDef>.GetNamed("Blueprints2");
        Require(category.AllResolvedDesignators.Count(d => d.GetType().Assembly == assembly) == 6, "All six actual XML designators resolve and instantiate.");
        foreach (string name in new[] { "Blueprint_MirrorX", "Blueprint_MirrorZ", "Blueprint_SwitchMode" })
            Require(DefDatabase<KeyBindingDef>.GetNamed(name).category == DefDatabase<KeyBindingCategoryDef>.GetNamed("Blueprint2KeyCategory"), "Actual XML keybinding resolves " + name);
        Type creatorType = assembly.GetType("Blueprint2.UnifiedBlueprintCreateDesignator", true);
        Type placeType = assembly.GetType("Blueprint2.SwitchableBlueprintPlaceDesignator", true);
        Type basePlace = assembly.GetType("Blueprint2.BlueprintPlaceDesignatorBase", true);
        Type serializer = assembly.GetType("Blueprint2.BlueprintXmlSerializer", true);
        Type storage = assembly.GetType("Blueprint2.BlueprintCreateDesignatorBase", true);
        var rectangles = new List<CellRect>(); var originalTerrain = new Dictionary<IntVec3, TerrainDef>();
        var owned = new HashSet<Thing>(); bool oldGod = DebugSettings.godMode;
        string storageKey = "RW17Fixture_" + Guid.NewGuid().ToString("N");
        var stores = new[] { "savedUnifiedBlueprints", "savedBuildingBlueprints", "savedTerrainBlueprints" }.Select(n => (IDictionary)storage.GetField(n).GetValue(null)).ToList();
        CellRect Allocate()
        {
            foreach (IntVec3 origin in CellRect.CenteredOn(map.Center, 70))
            {
                CellRect rect = new(origin.x, origin.z, 3, 3);
                if (rectangles.Any(r => r.ExpandedBy(1).Overlaps(rect))) continue;
                if (!rect.Cells.All(c => c.InBounds(map) && !c.Fogged(map) && c.GetThingList(map).Count == 0 &&
                    c.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy) && !c.GetTerrain(map).BuildableByPlayer &&
                    map.terrainGrid.UnderTerrainAt(c) == null && map.terrainGrid.FoundationAt(c) == null && map.terrainGrid.TempTerrainAt(c) == null)) continue;
                rectangles.Add(rect); foreach (IntVec3 c in rect.Cells) originalTerrain.Add(c, c.GetTerrain(map)); return rect;
            }
            throw new InvalidOperationException("A clear natural disposable 3x3 capture/place fixture exists.");
        }
        void Track(CellRect rect) { foreach (Thing thing in rect.Cells.SelectMany(c => c.GetThingList(map)).ToList()) owned.Add(thing); }
        Designator Placer(PrefabDef prefab, string mode, Rot4 rotation, bool mirror)
        {
            var result = (Designator)Activator.CreateInstance(placeType, prefab);
            AccessTools.Field(placeType, "currentMode").SetValue(result, Enum.Parse(assembly.GetType("Blueprint2.PlaceMode"), mode));
            AccessTools.Field(basePlace, "currentRotation").SetValue(result, rotation); AccessTools.Field(basePlace, "mirrorX").SetValue(result, mirror);
            return result;
        }
        try
        {
            CellRect source = Allocate(); IntVec3 floorCell = source.Min + IntVec3.North;
            map.terrainGrid.SetTerrain(floorCell, TerrainDefOf.WaterShallow);
            map.terrainGrid.SetFoundation(floorCell, TerrainDefOf.HeavyBridge);
            map.terrainGrid.SetTerrain(floorCell, TerrainDefOf.WoodPlankFloor);
            var chair = ThingMaker.MakeThing(ThingDefOf.DiningChair, ThingDefOf.WoodLog); owned.Add(chair);
            chair.TryGetComp<CompQuality>().SetQuality(QualityCategory.Excellent, ArtGenerationContext.Colony);
            int damagedHp = Math.Max(1, chair.MaxHitPoints / 2);
            Require(damagedHp < chair.MaxHitPoints, "Fixture HP represents real partial damage below the actual stuff-adjusted native maximum.");
            chair.HitPoints = damagedHp; chair.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(chair, source.Min + IntVec3.East + IntVec3.North, map, Rot4.East);
            var creator = (Designator)Activator.CreateInstance(creatorType);
            Require(!creator.CanDesignateCell(IntVec3.Invalid).Accepted, "Actual capture designator rejects invalid coordinates.");
            PrefabDef captured = (PrefabDef)AccessTools.Method(creatorType, "CreateBlueprint").Invoke(creator, new object[] { source });
            Require(captured != null && captured.GetThings().Count() == 1 && captured.GetThings().Single().data.def == chair.def &&
                captured.GetThings().Single().data.hp == damagedHp && captured.GetThings().Single().data.quality == QualityCategory.Excellent,
                "Actual product CreateBlueprint captures only buildable structure and its native HP/quality.");
            Require(captured.GetTerrain().Any(t => t.data.def == TerrainDefOf.WoodPlankFloor) && captured.GetTerrain().Any(t => t.data.def == TerrainDefOf.HeavyBridge),
                "Actual capture accesses internal Prefab lists and retains the real floor's foundation.");
            captured.defName = storageKey; captured.label = "RW17 复制 <舰>&甲";
            string xml = (string)serializer.GetMethod("SerializeBlueprint").Invoke(null, new object[] { captured });
            PrefabDef copy = (PrefabDef)serializer.GetMethod("DeserializeBlueprint").Invoke(null, new object[] { xml });
            Require(copy != null && copy.label == captured.label && copy.GetThings().Count() == 1 && copy.GetTerrain().Count() == captured.GetTerrain().Count(),
                "Actual in-game XML roundtrip reconstructs native building/floor/foundation collections with escaped Unicode label.");
            string allXml = (string)serializer.GetMethod("SerializeBlueprints").Invoke(null, new object[] { new List<PrefabDef> { captured, copy } });
            Require(((List<PrefabDef>)serializer.GetMethod("DeserializeBlueprints").Invoke(null, new object[] { allXml })).Count == 2,
                "Actual in-game multi-document serializer preserves both prefabs.");
            CellRect normal = Allocate(); Designator normalPlacer = Placer(copy, "Buildings", Rot4.East, true);
            DebugSettings.godMode = false; normalPlacer.DesignateSingleCell(normal.CenterCell); Track(normal);
            IntVec3 buildingCell = normal.CenterCell + new IntVec3(-1, 0, -1);
            Blueprint_Build bp = buildingCell.GetThingList(map).OfType<Blueprint_Build>().Single();
            Require(bp.EntityToBuild() == chair.def && bp.stuffToUse == chair.Stuff && bp.Rotation == Rot4.South,
                "Actual rotated/mirrored placement creates native build blueprint with expected position/facing/stuff.");
            int before = normal.Cells.SelectMany(c => c.GetThingList(map)).Count(); normalPlacer.DesignateSingleCell(normal.CenterCell);
            Require(normal.Cells.SelectMany(c => c.GetThingList(map)).Count() == before, "Actual blocked duplicate placement does not add another blueprint.");
            Require(!normalPlacer.CanDesignateCell(IntVec3.Invalid).Accepted, "Actual place designator rejects out-of-map input.");
            CellRect god = Allocate(); DebugSettings.godMode = true;
            Placer(copy, "Buildings", Rot4.North, false).DesignateSingleCell(god.CenterCell); Track(god);
            Thing spawned = (god.CenterCell + IntVec3.East + IntVec3.North).GetThingList(map).Single(t => t.def == chair.def);
            Require(spawned.Stuff == chair.Stuff && spawned.HitPoints == damagedHp && spawned.TryGetComp<CompQuality>().Quality == QualityCategory.Excellent && spawned.Rotation == Rot4.East,
                $"Actual god-mode placement preserves metadata: Stuff={spawned.Stuff?.defName}, HP={spawned.HitPoints}/{spawned.MaxHitPoints}, expectedHP={damagedHp}, Quality={spawned.TryGetComp<CompQuality>().Quality}, Rot={spawned.Rotation}.");
            CellRect floor = Allocate(); DebugSettings.godMode = false;
            Placer(copy, "FloorOnly", Rot4.North, false).DesignateSingleCell(floor.CenterCell); Track(floor);
            var floorBps = floor.Cells.SelectMany(c => c.GetThingList(map)).OfType<Blueprint_Build>().ToList();
            Require(floorBps.Count == 1 && floorBps[0].EntityToBuild() == TerrainDefOf.WoodPlankFloor,
                "FloorOnly actual native blueprint placement excludes foundation and building data.");
            CellRect bridge = Allocate(); foreach (IntVec3 c in bridge.Cells) map.terrainGrid.SetTerrain(c, TerrainDefOf.WaterShallow);
            DebugSettings.godMode = true; Placer(copy, "Bridges", Rot4.North, false).DesignateSingleCell(bridge.CenterCell); Track(bridge);
            Require(map.terrainGrid.FoundationAt(bridge.CenterCell + IntVec3.North) == TerrainDefOf.HeavyBridge &&
                bridge.Cells.All(c => c.GetTerrain(map) != TerrainDefOf.WoodPlankFloor), "Bridges actual native placement restores the captured heavy foundation without placing floors.");
            foreach (IDictionary store in stores) store.Add(storageKey, captured);
            object remover = Activator.CreateInstance(assembly.GetType("Blueprint2.BlueprintRemoveDesignator"));
            AccessTools.Method(remover.GetType(), "RemoveBlueprint").Invoke(remover, new object[] { storageKey, captured });
            Require(stores.All(s => !s.Contains(storageKey)), "Actual removal clears this key from all three library stores.");
            Log.Message("[RW17.Validation] Mod65 checks passed: six_xml_designators, key_defs, actual_capture_filter_hp_quality_foundation, " +
                "real_xml_single_multi_roundtrip, normal_rotated_mirrored_native_blueprint, blocked_duplicate, invalid_cell, god_real_building, floor_only, captured_bridge_mode, actual_library_remove");
        }
        finally
        {
            foreach (CellRect rect in rectangles) Track(rect);
            foreach (Thing thing in owned) if (!thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
            foreach (var entry in originalTerrain)
            {
                while (map.terrainGrid.UnderTerrainAt(entry.Key) != null) map.terrainGrid.RemoveTopLayer(entry.Key, doLeavings: false);
                if (map.terrainGrid.FoundationAt(entry.Key) != null) map.terrainGrid.RemoveFoundation(entry.Key, doLeavings: false);
                map.terrainGrid.SetTerrain(entry.Key, entry.Value);
            }
            foreach (IDictionary store in stores) store.Remove(storageKey);
            DebugSettings.godMode = oldGod;
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
