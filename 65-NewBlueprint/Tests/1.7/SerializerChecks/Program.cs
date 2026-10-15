using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Blueprint2;
using RimWorld;
using Verse;

// Real native data/DefDatabase, no Unity/game process and no mocked API classes.
var building = new ThingDef { defName = "Unit_Building" };
var stuff = new ThingDef { defName = "Unit_Stuff" };
var floor = new TerrainDef { defName = "Unit_Floor" };
DefDatabase<ThingDef>.Add(building); DefDatabase<ThingDef>.Add(stuff); DefDatabase<TerrainDef>.Add(floor);
var prefab = new PrefabDef { label = "复制 <甲>&乙", size = new IntVec2(4, 3) };
prefab.things.Add(new PrefabThingData { def = building, stuff = stuff, quality = QualityCategory.Excellent, hp = 71,
    relativeRotation = RotationDirection.Counterclockwise, positions = new List<IntVec3> { new(-1, 0, 2), new(3, 0, 1) } });
prefab.terrain.Add(new PrefabTerrainData { def = floor, chance = 0.25f, rects = new List<CellRect> { new(0, 0, 2, 2) } });
string xml = BlueprintXmlSerializer.SerializeBlueprint(prefab);
Require(xml != null && XDocument.Parse(xml).Root?.Name.LocalName == "PrefabDef", "Product serializer produces valid single XML.");
PrefabDef copy = BlueprintXmlSerializer.DeserializeBlueprint(xml);
Require(copy != null && copy.label == prefab.label && copy.size == prefab.size && copy.GetThings().Count() == 2 && copy.GetTerrain().Count() == 4,
    "Actual product roundtrip preserves Unicode/XML special text, dimensions and expanded native positions.");
Require(copy.GetThings().All(t => t.data.def == building && t.data.stuff == stuff && t.data.quality == QualityCategory.Excellent &&
    t.data.hp == 71 && t.data.relativeRotation == RotationDirection.Counterclockwise), "Building metadata survives product conversion.");
Require(copy.GetThings().Select(t => t.cell).SequenceEqual(prefab.GetThings().Select(t => t.cell)) &&
    copy.GetTerrain().Select(t => t.cell).SequenceEqual(prefab.GetTerrain().Select(t => t.cell)) && copy.GetTerrain().All(t => t.data.chance == 0.25f),
    "Native enumerated coordinates and terrain chance survive grouping.");
var empty = new PrefabDef { label = "Empty", size = new IntVec2(0, 0) };
Require(BlueprintXmlSerializer.DeserializeBlueprint(BlueprintXmlSerializer.SerializeBlueprint(empty)).GetThings().Count() == 0, "Empty lists roundtrip.");
string listXml = BlueprintXmlSerializer.SerializeBlueprints(new List<PrefabDef> { prefab, empty });
Require(XDocument.Parse(listXml).Root?.Name.LocalName == "Blueprints" && BlueprintXmlSerializer.DeserializeBlueprints(listXml).Count == 2 &&
    BlueprintXmlSerializer.DeserializeBlueprint(listXml).label == prefab.label, "Actual multi-document and first-item single fallback work.");
Console.WriteLine("PASS real product XML single/list/fallback, text escaping, positions/rects, quality/hp/rotation/stuff/chance, empty boundary");
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
