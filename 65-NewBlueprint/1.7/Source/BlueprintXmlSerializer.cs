using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Text;

namespace Blueprint2;

// Wrapper for list of blueprints
[XmlRoot("Blueprints")]
public class BlueprintListXml
{
    [XmlElement("PrefabDef")]
    public List<BlueprintXmlData> Blueprints { get; set; }
    
    public BlueprintListXml()
    {
        Blueprints = new List<BlueprintXmlData>();
    }
}

// XML serializable classes for blueprints
[XmlRoot("PrefabDef")]
public class BlueprintXmlData
{
    [XmlElement("label")]
    public string Label { get; set; }
    
    [XmlElement("size")]
    public IntVec2Xml Size { get; set; }
    
    [XmlArray("things")]
    [XmlArrayItem("li")]
    public List<ThingGroupXml> Things { get; set; }
    
    [XmlArray("terrain")]
    [XmlArrayItem("li")]
    public List<TerrainGroupXml> Terrain { get; set; }
    
    public BlueprintXmlData()
    {
        Things = new List<ThingGroupXml>();
        Terrain = new List<TerrainGroupXml>();
    }
}

public class IntVec2Xml
{
    [XmlAttribute("x")]
    public int X { get; set; }
    
    [XmlAttribute("z")]
    public int Z { get; set; }
    
    public IntVec2Xml() { }
    
    public IntVec2Xml(IntVec2 vec)
    {
        X = vec.x;
        Z = vec.z;
    }
    
    public IntVec2 ToIntVec2() => new IntVec2(X, Z);
}

public class IntVec3Xml
{
    [XmlAttribute("x")]
    public int X { get; set; }
    
    [XmlAttribute("y")]
    public int Y { get; set; }
    
    [XmlAttribute("z")]
    public int Z { get; set; }
    
    public IntVec3Xml() { }
    
    public IntVec3Xml(IntVec3 vec)
    {
        X = vec.x;
        Y = vec.y;
        Z = vec.z;
    }
    
    public IntVec3 ToIntVec3() => new IntVec3(X, Y, Z);
}

public class ThingGroupXml
{
    [XmlElement("def")]
    public string Def { get; set; }
    
    [XmlElement("stuff")]
    public string Stuff { get; set; }
    
    [XmlElement("relativeRotation")]
    public RotationDirection RelativeRotation { get; set; }
    
    [XmlIgnore]
    public QualityCategory? Quality { get; set; }
    
    [XmlElement("quality")]
    public string QualityString
    {
        get { return Quality?.ToString() ?? null; }
        set { Quality = string.IsNullOrEmpty(value) ? null : (QualityCategory?)System.Enum.Parse(typeof(QualityCategory), value); }
    }
    
    [XmlElement("hp")]
    public int Hp { get; set; }
    
    [XmlArray("positions")]
    [XmlArrayItem("li")]
    public List<IntVec3Xml> Positions { get; set; }
    
    public ThingGroupXml()
    {
        Positions = new List<IntVec3Xml>();
        RelativeRotation = RotationDirection.None;
    }
}

public class TerrainGroupXml
{
    [XmlElement("def")]
    public string Def { get; set; }
    
    [XmlElement("chance")]
    public float Chance { get; set; }
    
    [XmlArray("positions")]
    [XmlArrayItem("li")]
    public List<IntVec3Xml> Positions { get; set; }
    
    public TerrainGroupXml()
    {
        Positions = new List<IntVec3Xml>();
        Chance = 1f;
    }
}

// Main XML serializer class
public static class BlueprintXmlSerializer
{
    private static readonly XmlSerializer serializer = new XmlSerializer(typeof(BlueprintXmlData));
    private static readonly XmlSerializer listSerializer = new XmlSerializer(typeof(BlueprintListXml));
    
    public static string SerializeBlueprint(PrefabDef prefab)
    {
        try
        {
            var data = ConvertToXmlData(prefab);
            var ns = new XmlSerializerNamespaces();
            ns.Add("", ""); // Remove default namespace
            
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, data, ns);
                return writer.ToString();
            }
        }
        catch (System.Exception ex)
        {
            Log.Error($"Failed to serialize blueprint to XML: {ex.Message}");
            return null;
        }
    }
    
    public static string SerializeBlueprints(List<PrefabDef> prefabs)
    {
        try
        {
            var blueprintList = new BlueprintListXml();
            blueprintList.Blueprints.AddRange(prefabs.Select(ConvertToXmlData));
            var ns = new XmlSerializerNamespaces();
            ns.Add("", ""); // Remove default namespace

            using (var writer = new StringWriter())
            {
                listSerializer.Serialize(writer, blueprintList, ns);
                return writer.ToString();
            }
        }
        catch (System.Exception ex)
        {
            Log.Error($"Failed to serialize blueprints to XML: {ex.Message}");
            return null;
        }
    }
    
    public static PrefabDef DeserializeBlueprint(string xml)
    {
        try
        {
            // First try to deserialize as a single blueprint
            try
            {
                using (var reader = new StringReader(xml))
                {
                    var data = (BlueprintXmlData)serializer.Deserialize(reader);
                    return ConvertFromXmlData(data);
                }
            }
            catch
            {
                // If that fails, try to deserialize as a list and take the first item
                var blueprints = DeserializeBlueprints(xml);
                return blueprints?.FirstOrDefault();
            }
        }
        catch (System.Exception ex)
        {
            Log.Warning($"Failed to deserialize blueprint from XML: {ex.Message}");
            return null;
        }
    }
    
    public static List<PrefabDef> DeserializeBlueprints(string xml)
    {
        try
        {
            using (var reader = new StringReader(xml))
            {
                var blueprintList = (BlueprintListXml)listSerializer.Deserialize(reader);
                return blueprintList.Blueprints.Select(ConvertFromXmlData).ToList();
            }
        }
        catch (System.Exception ex)
        {
            Log.Warning($"Failed to deserialize blueprints from XML: {ex.Message}");
            return null;
        }
    }
    
    private static BlueprintXmlData ConvertToXmlData(PrefabDef prefab)
    {
        var data = new BlueprintXmlData
        {
            Label = prefab.label,
            Size = new IntVec2Xml(prefab.size)
        };
        
        // Convert things
        var thingGroups = prefab.GetThings()
            .GroupBy(t => new {
                Def = t.data.def.defName,
                Stuff = t.data.stuff?.defName ?? "",
                Rotation = t.data.relativeRotation,
                Quality = t.data.quality,
                HP = t.data.hp
            })
            .ToList();
            
        foreach (var group in thingGroups)
        {
            var thingGroup = new ThingGroupXml
            {
                Def = group.Key.Def,
                Stuff = string.IsNullOrEmpty(group.Key.Stuff) ? null : group.Key.Stuff,
                RelativeRotation = group.Key.Rotation,
                Quality = group.Key.Quality,
                Hp = group.Key.HP
            };
            
            thingGroup.Positions.AddRange(group.Select(t => new IntVec3Xml(t.cell)));
            data.Things.Add(thingGroup);
        }
        
        // Convert terrain
        var terrainGroups = prefab.GetTerrain()
            .GroupBy(t => new {
                Def = t.data.def.defName,
                Chance = t.data.chance
            })
            .ToList();
            
        foreach (var group in terrainGroups)
        {
            var terrainGroup = new TerrainGroupXml
            {
                Def = group.Key.Def,
                Chance = group.Key.Chance
            };
            
            terrainGroup.Positions.AddRange(group.Select(t => new IntVec3Xml(t.cell)));
            data.Terrain.Add(terrainGroup);
        }
        
        return data;
    }
    
    private static PrefabDef ConvertFromXmlData(BlueprintXmlData data)
    {
        var prefab = new PrefabDef
        {
            defName = data.Label ?? $"ImportedBlueprint_{System.DateTime.Now:yyyyMMdd_HHmmss}",
            label = data.Label,
            size = data.Size.ToIntVec2()
        };
        
        // Convert things
        foreach (var thingGroup in data.Things)
        {
            var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(thingGroup.Def);
            if (thingDef == null) continue;
            
            var stuffDef = !string.IsNullOrEmpty(thingGroup.Stuff) ? 
                DefDatabase<ThingDef>.GetNamedSilentFail(thingGroup.Stuff) : null;
                
            foreach (var pos in thingGroup.Positions)
            {
                var thingData = new PrefabThingData
                {
                    def = thingDef,
                    position = pos.ToIntVec3(),
                    stuff = stuffDef,
                    relativeRotation = thingGroup.RelativeRotation,
                    quality = thingGroup.Quality,
                    hp = thingGroup.Hp
                };
                
                // Use Publicizer to access internal field directly
                prefab.things.Add(thingData);
            }
        }
        
        // Convert terrain
        foreach (var terrainGroup in data.Terrain)
        {
            var terrainDef = DefDatabase<TerrainDef>.GetNamedSilentFail(terrainGroup.Def);
            if (terrainDef == null) continue;
            
            var terrainData = new PrefabTerrainData
            {
                def = terrainDef,
                chance = terrainGroup.Chance
            };
            
            if (terrainData.rects == null)
                terrainData.rects = new List<CellRect>();
                
            foreach (var pos in terrainGroup.Positions)
            {
                terrainData.rects.Add(new CellRect(pos.X, pos.Z, 1, 1));
            }
            
            // Use Publicizer to access internal field directly
            prefab.terrain.Add(terrainData);
        }
        
        return prefab;
    }
}