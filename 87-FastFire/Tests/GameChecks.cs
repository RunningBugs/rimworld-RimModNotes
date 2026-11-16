using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace FastFire.Tests;

public sealed class GameChecks(Map map) : MapComponent(map)
{
    private IEnumerator checks;
    private bool finished;
    private bool useNormalSpeedForRenderBench;
    private readonly List<string> passed = new();
    private readonly Dictionary<string, object> samples = new();
    private bool Render => Environment.GetEnvironmentVariable("FF_RENDER") == "1";

    private void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        passed.Add(message);
        Log.Message("FF_ASSERT_OK " + message);
    }

    private void SetMode(FireOptimizationMode mode)
    {
        FastFireMod.Settings.mode = mode;
        FireMapComponent.SyncAllMapsToCurrentSettings();
    }

    private void ClearTestArena(CellRect rect)
    {
        foreach (IntVec3 cell in rect.Cells)
        {
            foreach (Thing t in cell.GetThingList(map).ToArray())
            {
                if (t is Pawn) continue;
                if (t.def.destroyable) t.Destroy();
                else if (t.Spawned) t.DeSpawn();
            }
            map.roofGrid.SetRoof(cell, null);
            if (map.fogGrid.IsFogged(cell)) map.fogGrid.Unfog(cell);
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.WoodPlankFloor);
        }
    }

    private int CountTotalFires() =>
        map.GetComponent<FireMapComponent>() is FireMapComponent comp && FastFireMod.Settings.UseGridEngine
            ? comp.GridFireCount + map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count
            : map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count;

    private IEnumerable Run()
    {
        Require(typeof(FastFireMod).Assembly.Location.Contains("/1.7/Assemblies/"), "matching 1.7 runtime assembly loaded");
        FireMapComponent comp = map.GetComponent<FireMapComponent>();
        Require(comp != null, "FireMapComponent attached to map");

        // Ensure clear calm dry weather for deterministic interaction & TPS testing
        comp.ClearAllFiresForDebug();
        var clearWeather = DefDatabase<WeatherDef>.GetNamed("Clear");
        map.weatherManager.TransitionTo(clearWeather);
        map.weatherManager.curWeatherAge = 4000;

        // 1. Test Dev Mode Debug Actions: Spawn 10000+ varied fires & Clear all fires
        SetMode(FireOptimizationMode.ExtremeGrid);
        MethodInfo spawnMethod = AccessTools.Method(typeof(DebugTools_FastFire), "SpawnMassTestFires");
        MethodInfo clearMethod = AccessTools.Method(typeof(DebugTools_FastFire), "ClearAllFires");
        Require(spawnMethod != null && clearMethod != null, "Dev Mode FastFire debug actions exist");

        spawnMethod.Invoke(null, null);
        int spawnedTotal = comp.TotalStaticFireCount;
        int gridCount = comp.GridFireCount;
        int realThingsAfterSpawn = map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count;
        Require(spawnedTotal >= 10000, $"Dev spawn action created 10000+ fires (got {spawnedTotal})");
        Require(gridCount == spawnedTotal && realThingsAfterSpawn == 0,
            $"ExtremeGrid holds all {gridCount} static fires in grid with 0 Fire Things");

        float minSize = float.MaxValue;
        float maxSize = float.MinValue;
        for (int i = 0; i < map.Area; i++)
        {
            if (comp.HasGridFireAt(i))
            {
                float s = comp.GetStaticFireSizeAt(i);
                if (s < minSize) minSize = s;
                if (s > maxSize) maxSize = s;
            }
        }
        Require(minSize < 0.25f && maxSize > 1.5f, $"spawned fires have varied sizes ({minSize:F2}..{maxSize:F2})");

        clearMethod.Invoke(null, null);
        Require(comp.TotalStaticFireCount == 0 && map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count == 0,
            "Dev ClearAllFires cleared map to 0 fires");

        // 2. Test all Vanilla Interactions in BOTH ExtremeGrid and Compatible modes
        foreach (FireOptimizationMode testMode in new[] { FireOptimizationMode.ExtremeGrid, FireOptimizationMode.Compatible })
        {
            string tag = testMode.ToString();
            SetMode(testMode);
            CellRect arena = CellRect.FromLimits(45, 45, 75, 75);
            ClearTestArena(arena);

            // Prepare an unfogged colonist capable of firefighting
            Pawn pawn = map.mapPawns.FreeColonistsSpawned.First(p => !p.WorkTagIsDisabled(WorkTags.Firefighting));
            pawn.drafter.Drafted = false;
            pawn.jobs.StopAll();
            pawn.workSettings.EnableAndInitialize();
            pawn.workSettings.SetPriority(WorkTypeDefOf.Firefighter, 1);
            pawn.Position = new IntVec3(50, 0, 55);
            pawn.Notify_Teleported();

            // 2a. Automatic firefighting (WorkGiver_FightFires + JobDriver_BeatFire)
            IntVec3 autoFireCell = new IntVec3(54, 0, 55);
            map.areaManager.Home[autoFireCell] = true;
            Require(FireUtility.TryStartFireIn(autoFireCell, map, 0.35f, null), $"{tag}: ignite auto-fight fire");
            Require(autoFireCell.ContainsStaticFire(map), $"{tag}: ContainsStaticFire detects ignited cell");

            // Check Alert_FireInHomeArea while Home fire is active
            var alert = new Alert_FireInHomeArea();
            Require(alert.GetReport().active, $"{tag}: Alert_FireInHomeArea activates for home fire");

            // Check PathGrid cost on fire cell & adjacent cell
            int fireCost = map.pathing.Normal.pathGrid.CalculatedCostAt(autoFireCell, perceivedStatic: true, IntVec3.Invalid);
            int adjCost = map.pathing.Normal.pathGrid.CalculatedCostAt(autoFireCell + IntVec3.East, perceivedStatic: true, IntVec3.Invalid);
            Require(fireCost >= 1000 && adjCost >= 150, $"{tag}: PathGrid applies fire (+1000) and neighbor (+150) cost");

            // Verify WorkGiver_FightFires assigns BeatFire job and colonist puts it out
            var fightGiver = (WorkGiver_Scanner)DefDatabase<WorkGiverDef>.GetNamed("FightFires").Worker;
            Job autoJob = fightGiver.NonScanJob(pawn);
            if (autoJob == null)
            {
                autoJob = GenClosest.ClosestThingReachable(pawn.Position, map, ThingRequest.ForDef(ThingDefOf.Fire),
                    PathEndMode.Touch, TraverseParms.For(pawn), 9999f, t => fightGiver.HasJobOnThing(pawn, t)) is Thing tgt
                    ? fightGiver.JobOnThing(pawn, tgt)
                    : null;
            }
            Require(autoJob != null && autoJob.def == JobDefOf.BeatFire, $"{tag}: WorkGiver_FightFires issues BeatFire job");
            pawn.jobs.StartJob(autoJob, JobCondition.InterruptForced);

            int deadline = Find.TickManager.TicksGame + 600;
            while (autoFireCell.ContainsStaticFire(map) && Find.TickManager.TicksGame < deadline)
            {
                yield return null;
            }
            Require(!autoFireCell.ContainsStaticFire(map), $"{tag}: colonist walked up and extinguished fire");
            Require(!alert.GetReport().active, $"{tag}: Alert_FireInHomeArea clears after fire extinguished");

            // 2b. Right-click FloatMenu prioritize extinguishing (ExtinguishFiresNearby)
            IntVec3 fmCell1 = new IntVec3(58, 0, 55);
            IntVec3 fmCell2 = new IntVec3(59, 0, 55);
            map.areaManager.Home[fmCell1] = false;
            map.areaManager.Home[fmCell2] = false;
            FireUtility.TryStartFireIn(fmCell1, map, 0.3f, null);
            FireUtility.TryStartFireIn(fmCell2, map, 0.3f, null);
            pawn.Position = new IntVec3(56, 0, 55);
            pawn.Notify_Teleported();
            pawn.jobs.StopAll();

            List<FloatMenuOption> opts = FloatMenuMakerMap.GetOptions([pawn], fmCell1.ToVector3Shifted(), out _);
            string extLabel = "ExtinguishFiresNearby".Translate();
            FloatMenuOption extOpt = opts.FirstOrDefault(o => o.Label.Contains(extLabel, StringComparison.OrdinalIgnoreCase));
            Require(extOpt != null && !extOpt.Disabled, $"{tag}: right-click FloatMenu offers ExtinguishFiresNearby on grid fire");
            extOpt.action();
            deadline = Find.TickManager.TicksGame + 800;
            while ((fmCell1.ContainsStaticFire(map) || fmCell2.ContainsStaticFire(map)) && Find.TickManager.TicksGame < deadline)
            {
                yield return null;
            }
            Require(!fmCell1.ContainsStaticFire(map) && !fmCell2.ContainsStaticFire(map),
                $"{tag}: right-click ordered ExtinguishFiresNearby extinguished multiple fires");

            // 2c. JobDriver_Wait standing adjacent auto-beat fire
            pawn.drafter.Drafted = false;
            pawn.jobs.StopAll();
            pawn.Position = new IntVec3(56, 0, 58);
            pawn.Notify_Teleported();
            IntVec3 adjWaitFire = new IntVec3(57, 0, 58);
            map.areaManager.Home[adjWaitFire] = false;
            FireUtility.TryStartFireIn(adjWaitFire, map, 0.25f, null);
            pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait), JobCondition.InterruptForced);
            deadline = Find.TickManager.TicksGame + 600;
            while (adjWaitFire.ContainsStaticFire(map) && Find.TickManager.TicksGame < deadline)
            {
                yield return null;
            }
            Require(!adjWaitFire.ContainsStaticFire(map), $"{tag}: idle pawn standing next to fire auto-beats it out");

            // 2d. DamageDefOf.Extinguish explosion (Firefoam Popper / Waterskip / Foam Turret impact)
            IntVec3 foamCenter = new IntVec3(63, 0, 55);
            FireUtility.TryStartFireIn(foamCenter, map, 1.2f, null);
            FireUtility.TryStartFireIn(foamCenter + IntVec3.East, map, 1.0f, null);
            GenExplosion.DoExplosion(foamCenter, map, 2.9f, DamageDefOf.Extinguish, null,
                postExplosionSpawnThingDef: ThingDefOf.Filth_FireFoam, postExplosionSpawnChance: 1f, postExplosionSpawnThingCount: 1,
                applyDamageToExplosionCellsNeighbors: true);
            for (int t = 0; t < 40 && (foamCenter.ContainsStaticFire(map) || (foamCenter + IntVec3.East).ContainsStaticFire(map)); t++)
            {
                Find.TickManager.DoSingleTick();
            }
            Require(!foamCenter.ContainsStaticFire(map) && !(foamCenter + IntVec3.East).ContainsStaticFire(map),
                $"{tag}: Extinguish explosion (Firefoam) extinguishes fires");
            ClearTestArena(new CellRect(60, 52, 7, 7));

            // 2e. Waterskip ability (if Royalty active)
            if (ModsConfig.RoyaltyActive)
            {
                AbilityDef waterskipDef = DefDatabase<AbilityDef>.GetNamedSilentFail("Waterskip");
                if (waterskipDef != null)
                {
                    IntVec3 wsCenter = new IntVec3(63, 0, 62);
                    FireUtility.TryStartFireIn(wsCenter, map, 1.1f, null);
                    FireUtility.TryStartFireIn(wsCenter + IntVec3.North, map, 0.9f, null);
                    var ab = AbilityUtility.MakeAbility(waterskipDef, pawn);
                    var wsComp = ab.CompOfType<CompAbilityEffect_Waterskip>();
                    wsComp?.Apply(new LocalTargetInfo(wsCenter), LocalTargetInfo.Invalid);
                    Require(!wsCenter.ContainsStaticFire(map) && !(wsCenter + IntVec3.North).ContainsStaticFire(map),
                        $"{tag}: Royalty Waterskip ability extinguishes fires");
                }
            }

            // 2f. Firefoam Popper proximity fuse + Foam Turret targeting + Firefoam Pack detection
            IntVec3 popperCell = new IntVec3(65, 0, 66);
            Thing popper = GenSpawn.Spawn(ThingDef.Named("FirefoamPopper"), popperCell, map);
            IntVec3 popperFireCell = new IntVec3(66, 0, 66);
            FireUtility.TryStartFireIn(popperFireCell, map, 0.5f, null);
            popper.TryGetComp<CompProximityFuse>()?.CompTickRare();
            bool wickStarted = popper.TryGetComp<CompExplosive>()?.wickStarted ?? false;
            Require(wickStarted, $"{tag}: FirefoamPopper proximity fuse detects fire and lights wick");
            popper.Destroy();
            comp.ClearAllFiresForDebug();

            IntVec3 turretCell = new IntVec3(65, 0, 69);
            var turret = (Building_TurretFoam)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDef.Named("Turret_FoamTurret"), ThingDefOf.Steel), turretCell, map);
            turret.SetFaction(Faction.OfPlayer);
            IntVec3 turretFireCell = new IntVec3(69, 0, 69);
            FireUtility.TryStartFireIn(turretFireCell, map, 0.8f, null);
            LocalTargetInfo turretTarget = turret.TryFindNewTarget();
            Require(turretTarget.IsValid && turretTarget.Cell == turretFireCell,
                $"{tag}: FoamTurret TryFindNewTarget locks onto fire");
            turret.Destroy();
            comp.ClearAllFiresForDebug();

            Apparel pack = (Apparel)ThingMaker.MakeThing(ThingDef.Named("Apparel_FirefoampopPack"));
            pawn.apparel.Wear(pack, dropReplacedApparel: false);
            IntVec3 packFireCell = pawn.Position + IntVec3.East;
            FireUtility.TryStartFireIn(packFireCell, map, 0.5f, null);
            float chanceToUse = (float)AccessTools.Method(typeof(CompFirefoamPack), "ChanceToUse")
                .Invoke(pack.GetComp<CompFirefoamPack>(), new object[] { pawn });
            Require(chanceToUse > 0f, $"{tag}: FirefoamPack ChanceToUse detects nearby fire ({chanceToUse:F2})");
            pawn.apparel.Remove(pack);
            pack.Destroy();
            comp.ClearAllFiresForDebug();

            // 2g. Animal Fleeing (FleeUtility.FleeLargeFireJob)
            Pawn deer = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Muffalo"));
            GenSpawn.Spawn(deer, new IntVec3(55, 0, 65), map);
            for (int dx = 1; dx <= 3; dx++)
            {
                for (int dz = 0; dz <= 1; dz++)
                {
                    FireUtility.TryStartFireIn(new IntVec3(55 + dx, 0, 65 + dz), map, 0.7f, null);
                }
            }
            Job fleeJob = FleeUtility.FleeLargeFireJob(deer, minFiresNearbyToFlee: 5, distToFireToFlee: 8, fleeDistance: 12);
            Require(fleeJob != null && fleeJob.def == JobDefOf.Flee, $"{tag}: FleeUtility.FleeLargeFireJob triggers flee");
            deer.Destroy();
            comp.ClearAllFiresForDebug();

            // 2h. Pawn Catching Fire (parent != null remains vanilla) + Teammate Rescuing
            pawn.TryAttachFire(0.25f, null);
            Fire attached = (Fire)pawn.GetAttachment(ThingDefOf.Fire);
            Require(attached != null && attached.parent == pawn, $"{tag}: pawn-attached fire stays vanilla Fire attachment");
            attached.Destroy();

            // 2i. FireWatcher danger observation
            FireUtility.TryStartFireIn(new IntVec3(55, 0, 65), map, 1.5f, null);
            AccessTools.Method(typeof(FireWatcher), "UpdateObservations").Invoke(map.fireWatcher, null);
            Require(map.fireWatcher.FireDanger >= 1.9f && map.fireWatcher.FireDanger < 50f,
                $"{tag}: FireWatcher accurately tracks fire danger ({map.fireWatcher.FireDanger:F2})");
            comp.ClearAllFiresForDebug();
        }

        // 3. Test ExposeData Save/Load roundtrip for ExtremeGrid
        SetMode(FireOptimizationMode.ExtremeGrid);
        FireUtility.TryStartFireIn(new IntVec3(52, 0, 52), map, 1.23f, null);
        FireUtility.TryStartFireIn(new IntVec3(53, 0, 52), map, 0.47f, null);
        string tmpSavePath = Path.Combine(GenFilePaths.SaveDataFolderPath, "ff-roundtrip.xml");
        Scribe.saver.InitSaving(tmpSavePath, "testRoot");
        comp.ExposeData();
        Scribe.saver.FinalizeSaving();
        comp.ClearAllFiresForDebug();
        Require(comp.GridFireCount == 0, "cleared before ExposeData load check");
        Scribe.loader.InitLoading(tmpSavePath);
        Scribe.mode = LoadSaveMode.LoadingVars;
        Scribe.loader.curParent = comp;
        comp.ExposeData();
        Scribe.loader.curParent = null;
        Scribe.loader.crossRefs.RegisterForCrossRefResolve(comp);
        Scribe.loader.initer.RegisterForPostLoadInit(comp);
        Scribe.loader.FinalizeLoading();
        Require(comp.GridFireCount == 2
                && Mathf.Abs(comp.GetStaticFireSizeAt(map.cellIndices.CellToIndex(new IntVec3(52, 0, 52))) - 1.23f) < 0.01f,
            "ExtremeGrid ExposeData save/load roundtrip preserves fires and sizes");
        comp.ClearAllFiresForDebug();

        // 4. Real-Game 12,000+ Fire Benchmark: 0-fire baseline vs ExtremeGrid vs Compatible vs Vanilla (250 ticks = 1 full Rare+Complex cycle)
        double emptyMapMsPerTick = BenchmarkTicks(250);
        SetMode(FireOptimizationMode.ExtremeGrid);
        spawnMethod.Invoke(null, null);
        int benchFireCount = comp.TotalStaticFireCount;
        Require(benchFireCount >= 10000, $"benchmark map has {benchFireCount} fires (>= 10000)");

        // Let's also render screenshots & measure real frame time (MapUpdate + GlowGrid + DrawDynamicThings) if --render is set!
        double? extremeRenderMsPerFrame = null;
        double? compatRenderMsPerFrame = null;
        double? vanillaRenderMsPerFrame = null;
        if (Render)
        {
            IntVec3 center = map.Center;
            Find.CameraDriver.SetRootPosAndSize(center.ToVector3Shifted(), 52f);
            useNormalSpeedForRenderBench = true;
            for (int f = 0; f < 5; f++) yield return null;
            var frameSw = Stopwatch.StartNew();
            for (int f = 0; f < 20; f++) yield return null;
            frameSw.Stop();
            extremeRenderMsPerFrame = frameSw.Elapsed.TotalMilliseconds / 20.0;
            Find.CameraDriver.SetRootPosAndSize(center.ToVector3Shifted(), 28f);
            for (int f = 0; f < 4; f++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "fires-extreme.png"));
            for (int f = 0; f < 3; f++) yield return null;
            Find.CameraDriver.SetRootPosAndSize(center.ToVector3Shifted(), 14f);
            for (int f = 0; f < 6; f++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "fires-extreme-closeup.png"));
            for (int f = 0; f < 3; f++) yield return null;
            useNormalSpeedForRenderBench = false;
            Require(File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "fires-extreme.png")), "ExtremeGrid rendered screenshot captured");
        }

        // Benchmark ExtremeGrid: full 250-tick cycle
        double extremeMsPerTick = BenchmarkTicks(250);
        double extremeMaxTps = 1000.0 / Math.Max(0.0001, extremeMsPerTick);
        int afterExtremeCount = comp.TotalStaticFireCount;

        // Live-switch to Compatible mode -> all Grid fires become real Fire Things
        SetMode(FireOptimizationMode.Compatible);
        int compatRealThings = map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count;
        Require(comp.GridFireCount == 0 && compatRealThings >= 10000,
            $"Live switch ExtremeGrid->Compatible materialized {compatRealThings} Fire Things");

        if (Render)
        {
            Find.CameraDriver.SetRootPosAndSize(map.Center.ToVector3Shifted(), 52f);
            useNormalSpeedForRenderBench = true;
            for (int f = 0; f < 4; f++) yield return null;
            var frameSw = Stopwatch.StartNew();
            for (int f = 0; f < 20; f++) yield return null;
            frameSw.Stop();
            compatRenderMsPerFrame = frameSw.Elapsed.TotalMilliseconds / 20.0;
            Find.CameraDriver.SetRootPosAndSize(map.Center.ToVector3Shifted(), 28f);
            for (int f = 0; f < 4; f++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "fires-compatible.png"));
            for (int f = 0; f < 3; f++) yield return null;
            useNormalSpeedForRenderBench = false;
        }

        // Benchmark Compatible: full 250-tick cycle
        double compatMsPerTick = BenchmarkTicks(250);
        double compatMaxTps = 1000.0 / Math.Max(0.0001, compatMsPerTick);
        int afterCompatCount = comp.TotalStaticFireCount;

        // Live-switch to Vanilla mode -> measure real vanilla baseline on the exact same 10k+ fires
        SetMode(FireOptimizationMode.Vanilla);
        if (Render)
        {
            Find.CameraDriver.SetRootPosAndSize(map.Center.ToVector3Shifted(), 52f);
            useNormalSpeedForRenderBench = true;
            for (int f = 0; f < 3; f++) yield return null;
            var frameSw = Stopwatch.StartNew();
            for (int f = 0; f < 10; f++) yield return null;
            frameSw.Stop();
            vanillaRenderMsPerFrame = frameSw.Elapsed.TotalMilliseconds / 10.0;
            useNormalSpeedForRenderBench = false;
        }
        double vanillaMsPerTick = BenchmarkTicks(250, maxTotalMs: 12000.0);
        double vanillaMaxTps = 1000.0 / Math.Max(0.0001, vanillaMsPerTick);

        // Live-switch back to ExtremeGrid & verify reabsorption!
        SetMode(FireOptimizationMode.ExtremeGrid);
        int leftoverStaticThings = map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count(t => ((Fire)t).parent == null);
        Require(comp.GridFireCount >= 10000 && leftoverStaticThings == 0,
            $"Live switch Vanilla->ExtremeGrid absorbed {comp.GridFireCount} ground fires cleanly (static Fire Things left={leftoverStaticThings})");

        // 5. Mass Rain Extinction Benchmark (10,000+ unroofed flammable wood-floor fires in ExtremeGrid vs Compatible)
        comp.ClearAllFiresForDebug();
        CellRect rainOuter = new CellRect(15, 15, 114, 114);
        ClearTestArena(rainOuter);
        foreach (IntVec3 c in rainOuter.Cells)
        {
            if (c.x < 20 || c.x >= 124 || c.z < 20 || c.z >= 124)
            {
                map.terrainGrid.SetTerrain(c, TerrainDefOf.Concrete);
            }
        }
        CellRect rainArena = new CellRect(20, 20, 104, 104);
        var rain = DefDatabase<WeatherDef>.GetNamed("Rain");
        map.weatherManager.TransitionTo(clearWeather);
        map.weatherManager.curWeatherAge = 4000;

        SetMode(FireOptimizationMode.ExtremeGrid);
        int rainExtremeInitial = 0;
        foreach (IntVec3 c in rainArena.Cells)
        {
            if (FireUtility.TryStartFireIn(c, map, Rand.Range(0.2f, 1.75f), null))
            {
                rainExtremeInitial++;
            }
            if (rainExtremeInitial >= 10000) break;
        }
        Require(rainExtremeInitial >= 10000, $"ExtremeGrid rain test seeded {rainExtremeInitial} fires");
        map.weatherManager.TransitionTo(rain);
        map.weatherManager.curWeatherAge = 4000;
        var rainSw = Stopwatch.StartNew();
        for (int t = 0; t < 4500 && comp.GridFireCount > 0; t++)
        {
            if ((t & 255) == 0 && map.weatherManager.curWeather != rain)
            {
                map.weatherManager.TransitionTo(rain);
                map.weatherManager.curWeatherAge = 4000;
            }
            Find.TickManager.DoSingleTick();
        }
        rainSw.Stop();
        int remainingAfterRain = comp.GridFireCount;
        Require(remainingAfterRain < 500,
            $"Rain mass-extinguished 95%+ of {rainExtremeInitial} ExtremeGrid fires cleanly (remaining={remainingAfterRain} in {rainSw.Elapsed.TotalMilliseconds:F1} ms)");

        // 5b. Rain Mass Extinction in Compatible mode (verify O(N) Batch Despawn on 10,000+ Fire Things)
        comp.ClearAllFiresForDebug();
        map.weatherManager.TransitionTo(clearWeather);
        map.weatherManager.curWeatherAge = 4000;
        SetMode(FireOptimizationMode.Compatible);
        int compatSpawned = 0;
        using (map.pathing.DisableIncrementalScope())
        {
            foreach (IntVec3 c in rainArena.Cells)
            {
                if (FireUtility.TryStartFireIn(c, map, Rand.Range(0.2f, 1.75f), null))
                {
                    compatSpawned++;
                }
                if (compatSpawned >= 10000) break;
            }
        }
        Require(compatSpawned >= 10000, $"Compatible rain test spawned {compatSpawned} Fire Things");
        map.weatherManager.TransitionTo(rain);
        map.weatherManager.curWeatherAge = 4000;
        var compatRainSw = Stopwatch.StartNew();
        for (int t = 0; t < 4500 && map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count(f => ((Fire)f).parent == null) > 0; t++)
        {
            if ((t & 255) == 0 && map.weatherManager.curWeather != rain)
            {
                map.weatherManager.TransitionTo(rain);
                map.weatherManager.curWeatherAge = 4000;
            }
            Find.TickManager.DoSingleTick();
        }
        compatRainSw.Stop();
        int compatRemaining = map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count(t => ((Fire)t).parent == null);
        Require(compatRemaining < 500,
            $"Rain mass-extinguished 95%+ of {compatSpawned} Compatible Fire Things cleanly (remaining={compatRemaining} in {compatRainSw.Elapsed.TotalMilliseconds:F1} ms)");

        SetMode(FireOptimizationMode.ExtremeGrid);
        comp.ClearAllFiresForDebug();
        Require(comp.TotalStaticFireCount == 0 && map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count == 0,
            "final ClearAllFires leaves 0 fires");

        samples["benchmark"] = new
        {
            emptyMapBaselineMsPerTick = Math.Round(emptyMapMsPerTick, 3),
            initialFireCount = benchFireCount,
            extremeGrid = new
            {
                firesAfter = afterExtremeCount,
                msPerTick = Math.Round(extremeMsPerTick, 3),
                maxTheoreticalTps = Math.Round(extremeMaxTps, 1),
                speedupVsVanilla = Math.Round(vanillaMsPerTick / Math.Max(0.0001, extremeMsPerTick), 1),
                renderMsPerFrame = extremeRenderMsPerFrame.HasValue ? Math.Round(extremeRenderMsPerFrame.Value, 2) : (double?)null,
            },
            compatible = new
            {
                firesAfter = afterCompatCount,
                msPerTick = Math.Round(compatMsPerTick, 3),
                maxTheoreticalTps = Math.Round(compatMaxTps, 1),
                speedupVsVanilla = Math.Round(vanillaMsPerTick / Math.Max(0.0001, compatMsPerTick), 1),
                renderMsPerFrame = compatRenderMsPerFrame.HasValue ? Math.Round(compatRenderMsPerFrame.Value, 2) : (double?)null,
            },
            vanillaBaseline = new
            {
                msPerTick = Math.Round(vanillaMsPerTick, 3),
                maxTheoreticalTps = Math.Round(vanillaMaxTps, 1),
                renderMsPerFrame = vanillaRenderMsPerFrame.HasValue ? Math.Round(vanillaRenderMsPerFrame.Value, 2) : (double?)null,
            },
            rainMassExtinctionExtreme = new
            {
                fromCount = rainExtremeInitial,
                toCount = remainingAfterRain,
                elapsedMs = Math.Round(rainSw.Elapsed.TotalMilliseconds, 1),
            },
            rainMassExtinctionCompatible = new
            {
                fromCount = compatSpawned,
                toCount = compatRemaining,
                elapsedMs = Math.Round(compatRainSw.Elapsed.TotalMilliseconds, 1),
            }
        };
        Log.Message($"FF_BENCH extreme={extremeMsPerTick:F3}ms/tick ({extremeMaxTps:F0} TPS), compat={compatMsPerTick:F3}ms/tick ({compatMaxTps:F0} TPS), vanilla={vanillaMsPerTick:F3}ms/tick ({vanillaMaxTps:F0} TPS)");
    }

    private double BenchmarkTicks(int targetTicks, double maxTotalMs = 30000.0)
    {
        // Warm up 5 ticks first
        for (int i = 0; i < 5; i++)
        {
            Find.TickManager.DoSingleTick();
        }
        var sw = Stopwatch.StartNew();
        int ran = 0;
        for (; ran < targetTicks; ran++)
        {
            Find.TickManager.DoSingleTick();
            if (sw.Elapsed.TotalMilliseconds > maxTotalMs)
            {
                ran++;
                break;
            }
        }
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds / Math.Max(1, ran);
    }

    public override void MapComponentUpdate()
    {
        if (finished) { Root.Shutdown(); return; }
        if (Current.ProgramState != ProgramState.Playing || LongEventHandler.AnyEventNowOrWaiting) return;
        Find.TickManager.CurTimeSpeed = useNormalSpeedForRenderBench ? TimeSpeed.Normal : TimeSpeed.Superfast;
        try
        {
            checks ??= Run().GetEnumerator();
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception exception)
        {
            Log.Error("FF_TEST_FAILED " + exception);
            Finish(exception.ToString());
        }
    }

    private void Finish(string error)
    {
        finished = true;
        File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "ff-result.json"),
            JsonSerializer.Serialize(new { passed = error == null, checks = passed, samples, error },
                new JsonSerializerOptions { WriteIndented = true }));
    }
}
