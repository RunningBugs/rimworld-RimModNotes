using System;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;
namespace RW17.Validation;
public static class Mod11Checks
{
    public const string PackageId = "RunningBugs.RoadOnIce";
    public static void Run(Map map)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "RoadsOfTheRim");
        var extensionType = assembly.GetType("RoadsOfTheRim.DefModExtension_RotR_RoadDef", true);
        var method = extensionType.GetMethod("BiomeAllowed", BindingFlags.Public | BindingFlags.Static);
        var road = DefDatabase<RoadDef>.AllDefs.First(d => d.modExtensions?.Any(e => e.GetType() == extensionType) == true);
        var biome = Find.WorldGrid.Surface[map.Tile.tileId].PrimaryBiome;
        if (biome == null || biome.defName == "Ocean" || biome.defName == "Lake") throw new Exception("Mod11: needs real land tile with biome");
        var original = DefDatabase<BiomeDef>.AllDefs.ToDictionary(b => b, b => b.allowRoads);
        try
        {
            biome.allowRoads = false;
            object[] args = { map.Tile.tileId, road, null };
            if ((bool)method.Invoke(null, args) || args[2] != biome) throw new Exception("Mod11: real upstream false-control/biome lookup failed");
            var component = map.components.Single(c => c.GetType().FullName == "RoadOnIce.MyMapComponent");
            component.FinalizeInit();
            args = new object[] { map.Tile.tileId, road, null };
            if (!(bool)method.Invoke(null, args) || args[2] != biome) throw new Exception("Mod11: map lifecycle did not make upstream road gate pass");
            if (!BiomeDefOf.IceSheet.allowRoads || !BiomeDefOf.SeaIce.allowRoads) throw new Exception("Mod11: ice biome flags remain disabled");
        }
        finally { foreach (var entry in original) entry.Key.allowRoads = entry.Value; }
        Console.WriteLine("RW17_MOD11_UPSTREAM_BIOME_GATE_OK; road placement/construction UI remains pending");
    }
}
