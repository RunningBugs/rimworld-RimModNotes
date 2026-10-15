using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RW17.Validation;
public static class Mod64Checks
{
    public const string PackageId = "RunningBugs.LandingOnAsteroid";
    private static void ObserveStartBefore(Map map)
    {
        Log.Message($"[RW17.Validation] Mod64 start guard parent={map.ParentHolder?.GetType().Name}, faction={map.Parent.Faction?.def.defName}, " +
            $"player={map.Parent.Faction == Faction.OfPlayer}, generator={map.generatorDef?.defName}, space={map.Tile.LayerDef.isSpace}, " +
            $"gravship={map.wasSpawnedViaGravShipLanding}, existingStart={MapGenerator.PlayerStartSpotValid}, regionsEnabled={map.regionAndRoomUpdater.Enabled}");
    }
    private static void ObserveStartAfter(Map map)
    {
        var stand = map.AllCells.Where(c => c.Standable(map)).ToList();
        var surface = stand.Where(c => !c.Roofed(map) && c.GetAffordances(map).Contains(TerrainAffordanceDefOf.Heavy)).ToList();
        var district = surface.Where(c => c.GetDistrict(map) != null).ToList();
        Log.Message($"[RW17.Validation] Mod64 start candidates stand={stand.Count}, heavyUnroofed={surface.Count}, district={district.Count}, " +
            $"district10={district.Count(c => c.GetDistrict(map).CellCount >= 10)}, regionsEnabled={map.regionAndRoomUpdater.Enabled}, " +
            $"chosen={(MapGenerator.PlayerStartSpotValid ? MapGenerator.PlayerStartSpot.ToString() : "invalid")}, " +
            $"terrain={string.Join(",", surface.Select(c => c.GetTerrain(map).defName).Distinct())}");
    }
    public static void Run(Map map)
    {
        Require(ModsConfig.OdysseyActive, "Actual asteroid content requires Odyssey.");
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "LandingOnAsteroid");
        const string owner = "com.RunningBugs.LandingOnAsteroid";
        MethodInfo tileCheck = AccessTools.Method(typeof(TileFinder), "IsValidTileForNewSettlement");
        foreach (var target in new[] { AccessTools.Method(typeof(Page_SelectStartingSite), "ExtraOnGUI"),
            AccessTools.Method(typeof(Page_SelectStartingSite), "DoWindowContents"), tileCheck })
            Require(Harmony.GetPatchInfo(target)?.Postfixes.Any(p => p.owner == owner && p.PatchMethod.DeclaringType.Assembly == assembly) == true,
                "Actual postfix registered on " + target.Name);
        Require(Harmony.GetPatchInfo(AccessTools.Method(typeof(Page_SelectStartingSite), "DoNext"))?.Prefixes.Any(p => p.owner == owner) == true,
            "Actual next-page prefix registered.");
        Require(Harmony.GetPatchInfo(AccessTools.Method(typeof(GenStep_Asteroid), "SpawnOres"))?.Finalizers.Any(p => p.owner == owner) == true,
            "The actual native ore generator has this MOD's recovery finalizer.");
        Require(Harmony.GetPatchInfo(AccessTools.Method(typeof(GenStep_FindPlayerStartSpot), "Generate"))?.Prefixes.Any(p => p.owner == owner) == true,
            "The actual player-start generator has the scoped asteroid-island fix.");
        Type mineral = assembly.GetType("LandingOnAsteroid.AsteroidMineralConfig", true);
        Type safe = assembly.GetType("LandingOnAsteroid.SafeMapParentCast", true);
        object oldMineral = mineral.GetMethod("GetPreciousResource").Invoke(null, null);
        object settings = assembly.GetType("LandingOnAsteroid.AsteroidStartingMod").GetField("settings").GetValue(null);
        FieldInfo generator = settings.GetType().GetField("defaultMapGenerator"); object oldGenerator = generator.GetValue(settings);
        GameInitData oldInit = Current.Game.InitData;
        WorldSelector selector = Find.WorldSelector;
        var oldSelection = selector.SelectedObjects.ToList(); PlanetTile oldTile = selector.SelectedTile; PlanetLayer oldLayer = selector.SelectedLayer;
        Map oldMap = Find.CurrentMap; ProgramState oldProgramState = Current.ProgramState;
        var genDef = DefDatabase<MapGeneratorDef>.GetNamed("AsteroidBasic");
        var oreStep = (GenStep_Asteroid)DefDatabase<GenStepDef>.GetNamed("AsteroidBasic").genStep;
        var asteroid = Find.WorldObjects.AllWorldObjects.OfType<BasicAsteroidMapParent>().FirstOrDefault(a =>
            a.preciousResource != null && oreStep.mineableCounts.Any(c => c.mineable == a.preciousResource) && a.Map == null);
        Require(asteroid != null && asteroid.Tile.Layer.Def == PlanetLayerDefOf.Orbit, "The real generated world supplies a mineable orbital asteroid.");
        Settlement settlement = null; Map generated = null; Pawn starter = null;
        var generatedPawns = new System.Collections.Generic.List<Pawn>();
        var observer = new Harmony("RW17.Validation.Mod64StartObserver");
        MethodInfo startTarget = AccessTools.Method(typeof(GenStep_FindPlayerStartSpot), "Generate");
        MethodInfo beforeStart = AccessTools.Method(typeof(Mod64Checks), "ObserveStartBefore"), afterStart = AccessTools.Method(typeof(Mod64Checks), "ObserveStartAfter");
        bool observingStart = false;
        void SelectAsteroid() { selector.SelectedLayer = asteroid.Tile.Layer; selector.ClearSelection(); selector.Select(asteroid, playSound: false); }
        try
        {
            mineral.GetMethod("ClearPreciousResource").Invoke(null, null);
            Require(safe.GetMethod("GetPreciousResource").Invoke(null, new object[] { map }) == null,
                "Actual surface Settlement fallback safely returns no precious resource.");
            // Native control uses the same real tile with only this exact postfix temporarily removed.
            var patch = Harmony.GetPatchInfo(tileCheck).Postfixes.Single(p => p.owner == owner);
            var control = new Harmony(owner);
            bool nativeAllowed; var nativeReason = new StringBuilder();
            control.Unpatch(tileCheck, patch.PatchMethod);
            try { nativeAllowed = TileFinder.IsValidTileForNewSettlement(asteroid.Tile, nativeReason); }
            finally { control.Patch(tileCheck, postfix: new HarmonyMethod(patch.PatchMethod) { priority = patch.priority, before = patch.before, after = patch.after }); }
            Require(!nativeAllowed, "The actual native tile check rejects this occupied asteroid starting location.");
            var reason = new StringBuilder("fixture reason");
            Require(TileFinder.IsValidTileForNewSettlement(asteroid.Tile, reason) && reason.Length == 0,
                "The actual restored postfix allows a real asteroid tile and clears rejection text.");
            Require(!TileFinder.IsValidTileForNewSettlement(map.Tile), "The actual postfix does not permit an occupied ordinary colony tile.");
            Current.Game.InitData = new GameInitData { startingPawnCount = 1, playerFaction = Faction.OfPlayer, mapSize = 200 };
            starter = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, forceNoGear: true));
            Current.Game.InitData.startingAndOptionalPawns.Add(starter);
            Current.Game.InitData.startingPossessions.Add(starter, new System.Collections.Generic.List<ThingDefCount>());
            generator.SetValue(settings, "Base_Player"); SelectAsteroid();
            new Page_SelectStartingSite().DoWindowContents(new Rect(0, 0, 760, 600));
            Require(Find.GameInitData.startingTile == asteroid.Tile && Find.GameInitData.mapGeneratorDef == null,
                "A fresh Base_Player option keeps the native Settlement generator fallback.");
            generator.SetValue(settings, "AsteroidBasic"); SelectAsteroid();
            new Page_SelectStartingSite().DoWindowContents(new Rect(0, 0, 760, 600));
            Require(Find.GameInitData.startingTile == asteroid.Tile && Find.WorldInterface.SelectedTile == asteroid.Tile && Find.GameInitData.mapGeneratorDef == genDef,
                "Actual page call configures selected asteroid tile and real AsteroidBasic generator.");
            Require(mineral.GetMethod("GetPreciousResource").Invoke(null, null) == asteroid.preciousResource,
                "The actual page preserves the asteroid's real precious mineral.");
            SelectAsteroid();
            AccessTools.Method(assembly.GetType("LandingOnAsteroid.Page_SelectStartingSite_DoNext_Patch"), "Prefix").Invoke(null, null);
            Require((bool)mineral.GetMethod("HasPreciousResource").Invoke(null, null), "Actual next-page preparation retains precious mineral config.");
            settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
            settlement.SetFaction(Faction.OfPlayer); settlement.Tile = asteroid.Tile; Find.WorldObjects.Add(settlement);
            foreach (MapGeneratorDef actualGenerator in new[] { genDef, DefDatabase<MapGeneratorDef>.GetNamed("Space") })
                Log.Message("[RW17.Validation] Mod64 actual generator " + actualGenerator.defName + " steps=" +
                    string.Join(",", actualGenerator.genSteps.Select(s => s.defName + ":" + s.genStep.GetType().FullName)));
            Log.Message("[RW17.Validation] Mod64 generating real disposable AsteroidBasic map");
            observer.Patch(startTarget, prefix: new HarmonyMethod(beforeStart) { priority = Priority.First }, postfix: new HarmonyMethod(afterStart) { priority = Priority.Last });
            observingStart = true;
            Stopwatch generationClock = Stopwatch.StartNew();
            generated = MapGenerator.GenerateMap(new IntVec3(200, 1, 200), settlement, genDef);
            generationClock.Stop();
            generatedPawns.AddRange(generated.mapPawns.AllPawns);
            Require(generated != null && generated.ParentHolder == settlement && generated.Tile == asteroid.Tile && generated.OrbitalDebris == OrbitalDebrisDefOf.Asteroid,
                "The actual complete native generator creates an orbital colony map with Settlement parent.");
            Require(starter.Spawned && starter.Map == generated && starter.Position.Standable(generated) && !starter.Position.Roofed(generated)
                && starter.Position.GetAffordances(generated).Contains(TerrainAffordanceDefOf.Heavy),
                $"The real starting Pawn must occupy a valid asteroid surface: spawned={starter.Spawned}, correctMap={starter.Map == generated}, " +
                $"pos={starter.Position}, standable={starter.Spawned && starter.Position.Standable(generated)}, roof={starter.Spawned && starter.Position.Roofed(generated)}, " +
                $"terrain={(starter.Spawned ? starter.Position.GetTerrain(generated).defName : "unspawned")}, " +
                $"heavy={starter.Spawned && starter.Position.GetAffordances(generated).Contains(TerrainAffordanceDefOf.Heavy)}.");
            IntVec3 walkTarget = CellRect.CenteredOn(starter.Position, 8).FirstOrDefault(c => c != starter.Position && c.InBounds(generated)
                && c.Standable(generated) && !c.Roofed(generated) && c.GetAffordances(generated).Contains(TerrainAffordanceDefOf.Heavy)
                && c.GetDistrict(generated) == starter.Position.GetDistrict(generated), IntVec3.Invalid);
            Require(walkTarget.IsValid && starter.CanReach(walkTarget, Verse.AI.PathEndMode.OnCell, Danger.Deadly),
                "The real landed Pawn can use actual native path reachability to another cell in the asteroid district after map finalization.");
            int mineralCount = generated.listerThings.ThingsOfDef(asteroid.preciousResource).Count;
            Require(mineralCount > 0, "The native SpawnOres cast recovery actually spawns the asteroid's precious mineables.");
            var component = Current.Game.GetComponent(assembly.GetType("LandingOnAsteroid.LandingOnAsteroidGameComponent"));
            Require(component != null, "Actual game component is registered."); component.FinalizeInit();
            Require(Find.WorldObjects.MapParentAt(asteroid.Tile) == settlement && !Find.WorldObjects.AllWorldObjects.Contains(asteroid),
                "Actual component removes the obsolete unmapped asteroid so tile lookup finds the generated colony.");
            Log.Message("[RW17.Validation] Mod64 checks passed: actual_targets, native_tile_reject_vs_allow, " +
                "real_page_tile_generator, actual_mineral_config, real_200x200_native_asteroid_generation, spawned_precious_ores=" + mineralCount + ", actual_parent_dedup");
            Log.Message("[RW17.Validation] Mod64 actual asteroid generation milliseconds=" + generationClock.ElapsedMilliseconds);
        }
        finally
        {
            if (observingStart) { observer.Unpatch(startTarget, beforeStart); observer.Unpatch(startTarget, afterStart); }
            if (generated != null && Current.Game.Maps.Contains(generated)) Current.Game.DeinitAndRemoveMap(generated, notifyPlayer: false);
            else if (settlement?.Map != null && Current.Game.Maps.Contains(settlement.Map)) Current.Game.DeinitAndRemoveMap(settlement.Map, notifyPlayer: false);
            foreach (Pawn pawn in generatedPawns) if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
            if (starter != null && !starter.Destroyed) starter.Destroy(DestroyMode.Vanish);
            if (settlement != null && Find.WorldObjects.AllWorldObjects.Contains(settlement)) Find.WorldObjects.Remove(settlement);
            if (!Find.WorldObjects.AllWorldObjects.Contains(asteroid)) Find.WorldObjects.Add(asteroid);
            mineral.GetMethod("SetPreciousResource").Invoke(null, new[] { oldMineral }); generator.SetValue(settings, oldGenerator);
            Current.Game.InitData = oldInit; Current.ProgramState = oldProgramState; Current.Game.CurrentMap = oldMap;
            selector.SelectedLayer = oldLayer; selector.ClearSelection(); foreach (var value in oldSelection) selector.Select(value, playSound: false); selector.SelectedTile = oldTile;
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
