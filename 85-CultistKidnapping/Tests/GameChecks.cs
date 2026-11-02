using System.Collections;
using System.Text.Json;
using CultistKidnapping;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CultistKidnapping.Tests;

public sealed class GameChecks(Map map) : MapComponent(map)
{
    private IEnumerator checks;
    private bool finished;
    private int spawnSeed = 8500;
    private readonly List<string> passed = new();
    private string Phase => Environment.GetEnvironmentVariable("CK_TEST_PHASE") ?? "start";
    private KidnappingMapComponent Component => map.GetComponent<KidnappingMapComponent>();
    private int Now => Find.TickManager.TicksGame;

    private void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        passed.Add(description);
        Log.Message("CK_ASSERT_OK " + description);
    }

    private IEnumerable Wait(Func<bool> predicate, int timeout, string description)
    {
        var deadline = Now + timeout;
        while (!predicate() && Now < deadline) yield return null;
        Require(predicate(), description);
    }

    private IntVec3 Site()
    {
        // Remove terrain-generation randomness from the disposable pathfinding fixture.
        foreach (var cell in CellRect.FromLimits(0, 28, 56, 52).Cells)
        {
            foreach (var thing in cell.GetThingList(map).ToArray())
                if (thing is Building or Plant)
                {
                    if (thing.def.destroyable) thing.Destroy(DestroyMode.Vanish);
                    else if (thing.Spawned) thing.DeSpawn();
                }
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
        }
        return new IntVec3(40, 0, 40);
    }

    private Pawn Spawn(Faction faction, IntVec3 cell)
    {
        Pawn pawn;
        Rand.PushState(spawnSeed++);
        try
        {
            var kind = faction == Faction.OfHoraxCult ? DefDatabase<PawnKindDef>.GetNamed("Horaxian_Underthrall") : PawnKindDefOf.Colonist;
            pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction,
                forceGenerateNewPawn: true, forceNoIdeo: true));
        }
        finally { Rand.PopState(); }
        GenSpawn.Spawn(pawn, cell, map);
        pawn.needs.food.CurLevel = 1;
        pawn.needs.rest.CurLevel = 1;
        return pawn;
    }

    private Lord Ritual(Pawn[] pawns, IntVec3 cell)
    {
        var def = DefDatabase<PsychicRitualDef>.GetNamed("SkipAbduction");
        var assignments = def.BuildRoleAssignments(new TargetInfo(cell, map));
        assignments.AddForcedRole(pawns[0], PsychicRitualRoleDefOf.Invoker, PsychicRitualRoleDef.Context.NonPlayerFaction);
        foreach (var pawn in pawns.Skip(1))
            assignments.AddForcedRole(pawn, PsychicRitualRoleDefOf.Defender, PsychicRitualRoleDef.Context.NonPlayerFaction);
        return LordMaker.MakeNewLord(Faction.OfHoraxCult,
            new LordJob_PsychicRitualRepeating(def, assignments, 3, cell, 300), map, pawns);
    }

    private Pawn ApplyOutcome(Lord lord, Pawn expectedVictim)
    {
        // Make selection deterministic while executing the real, patched vanilla outcome.
        var excluded = map.mapPawns.FreeColonistsSpawned.Concat(map.mapPawns.SlavesOfColonySpawned)
            .Distinct().Where(p => p != expectedVictim && !p.health.hediffSet.HasHediff(HediffDefOf.DarkPsychicShock)).ToArray();
        foreach (var pawn in excluded) pawn.health.AddHediff(HediffDefOf.DarkPsychicShock);
        var ritual = ((LordToil_PsychicRitual)lord.CurLordToil).RitualData.psychicRitual;
        ritual.succeeded = true; // InvokeHorax.End(success: true) sets this before the real outcome.
        new PsychicRitualToil_SkipAbduction(PsychicRitualRoleDefOf.Invoker).Start(ritual, null);
        foreach (var pawn in excluded)
            pawn.health.RemoveHediff(pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.DarkPsychicShock));
        Require(expectedVictim.health.hediffSet.HasHediff(HediffDefOf.DarkPsychicShock) && expectedVictim.Downed,
            "vanilla outcome teleported the selected victim and applied coma");
        return expectedVictim;
    }

    private IEnumerable Start()
    {
        Require(ModsConfig.AnomalyActive && Faction.OfHoraxCult != null, "Anomaly and cult faction loaded");
        Require(typeof(CultistKidnappingMod).Assembly.Location.Replace('\\', '/').EndsWith("/1.7/Assemblies/CultistKidnapping.dll"),
            "game loaded the 1.7 assembly selected by LoadFolders");
        Require(Harmony.GetPatchInfo(AccessTools.Method(typeof(PsychicRitualToil_SkipAbduction), "ApplyOutcome"))
            ?.Transpilers.Any(p => p.owner == "RunningBugs.CultistKidnapping") == true, "production outcome patch installed");
        Require(CultistKidnappingMod.Settings.repeatRaids && CultistKidnappingMod.Settings.intervalDays == new IntRange(3, 5),
            "fresh settings default to enabled with a 3–5 day interval");
        // An established colony avoids vanilla's initial/new-wanderer grace period.
        Find.TickManager.DebugSetTicksGame(10 * GenDate.TicksPerDay);
        foreach (var pawn in map.mapPawns.FreeColonistsSpawned.ToArray())
        { pawn.drafter.Drafted = true; pawn.drafter.FireAtWill = false; }
        var site = Site();
        var victim = Spawn(Faction.OfPlayer, map.Center);
        var invoker = Spawn(Faction.OfHoraxCult, site + new IntVec3(2, 0, 0));
        var near = Spawn(Faction.OfHoraxCult, site + new IntVec3(1, 0, 0));
        var far = Spawn(Faction.OfHoraxCult, site + new IntVec3(6, 0, 0));
        var lord = Ritual(new[] { invoker, near, far }, site);
        ApplyOutcome(lord, victim);
        foreach (var step in Wait(() => lord.LordJob is LordJob_CultistKidnap, 120, "ritual hands off to pursuit at a tick boundary")) yield return step;
        var job = (LordJob_CultistKidnap)lord.LordJob;
        Require(job.Carrier == invoker, "invoker takes priority over a nearer guard");
        Require(near.mindState.duty.def == DutyDefOf.Escort && near.mindState.duty.focus.Thing == invoker,
            "guards use vanilla Escort focused on the carrier");
        foreach (var step in Wait(() => invoker.carryTracker.CarriedThing == victim, 900, "native Kidnap job picks up the victim")) yield return step;
        Require(Component.NextRaidTick < 0, "teleporting and picking up do not schedule a follow-up raid");

        // Interrupt the real job without incapacitating anyone, then check native resumption.
        invoker.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
        job.LordJobTick();
        foreach (var step in Wait(() => job.Carrier?.carryTracker.CarriedThing == victim, 900,
            "an interrupted Kidnap job is selected and resumed")) yield return step;

        var original = job.Carrier;
        foreach (var guard in lord.ownedPawns.Where(pawn => pawn != original).ToArray())
            guard.health.AddHediff(HediffDefOf.Anesthetic);
        near = Spawn(Faction.OfHoraxCult, original.Position + new IntVec3(1, 0, 0));
        far = Spawn(Faction.OfHoraxCult, original.Position + new IntVec3(7, 0, 0));
        lord.AddPawn(near);
        lord.AddPawn(far);
        original.health.AddHediff(HediffDefOf.Anesthetic);
        foreach (var step in Wait(() => job.Carrier == near && near.carryTracker.CarriedThing == victim, 1200,
            "downed carrier drops the victim and the nearest capable guard takes over")) yield return step;
        Require(far.mindState.duty.def == DutyDefOf.Escort && far.mindState.duty.focus.Thing == near,
            "escort retargets to the replacement carrier");
        Require(!job.AddFleeToil && !lord.Graph.transitions.Any(), "pursuit has no casualty-based flee transition");
        original.Kill(null);
        if (original != invoker && !invoker.Dead) invoker.Kill(null);
        Require(lord.ownedPawns.Contains(near), "a killed invoker does not cancel the pursuit");
        GameDataSaveLoader.SaveGame("CK-Carrying");
        Require(File.Exists(GenFilePaths.FilePathForSavedGame("CK-Carrying")), "saved an active native carrying job");
    }

    private IEnumerable Resume()
    {
        var job = map.lordManager.lords.Select(l => l.LordJob).OfType<LordJob_CultistKidnap>().Single();
        var victim = job.Victim;
        Require(job.Carrier?.carryTracker.CarriedThing == victim && job.Carrier.CurJob.def == JobDefOf.Kidnap,
            "save reload restored the exact victim, carrier and native Kidnap job");
        foreach (var step in Wait(() => Faction.OfHoraxCult.kidnapped.KidnappedPawnsListForReading.Contains(victim), 6000,
            "native walking and ExitMap registered the victim in the cult's kidnapped tracker")) yield return step;
        var remaining = Component.NextRaidTick - Now;
        Require(remaining >= 3 * GenDate.TicksPerDay - 120 && remaining <= 5 * GenDate.TicksPerDay,
            "actual kidnapping schedules the next raid within the configured 3–5 days");
        Require(Faction.OfHoraxCult.kidnapped.KidnappedPawnsListForReading.Count(p => p == victim) == 1,
            "vanilla kidnapping was registered exactly once");
        CultistKidnappingMod.Settings.repeatRaids = false;
        Component.MapComponentTick();
        Require(Component.NextRaidTick == -1, "disabling follow-ups cancels the pending raid");
        CultistKidnappingMod.Settings.repeatRaids = true;
        Component.ScheduleRepeat();
        var earliest = Component.NextRaidTick;
        Component.ScheduleRepeat();
        Require(Component.NextRaidTick <= earliest, "another success cannot postpone the earlier pending raid");
        GameDataSaveLoader.SaveGame("CK-Repeat");
        Require(File.Exists(GenFilePaths.FilePathForSavedGame("CK-Repeat")), "saved a pending follow-up raid");
    }

    private IEnumerable Repeat()
    {
        Require(Component.NextRaidTick > Now, "follow-up countdown survived save reload");
        var existing = map.lordManager.lords.ToHashSet();
        Find.TickManager.DebugSetTicksGame(Component.NextRaidTick - 1);
        Log.Message("CK_INCIDENT_CONTEXT " + JsonSerializer.Serialize(new { now = Now, due = Component.NextRaidTick,
            home = map.IsPlayerHome, population = map.mapPawns.FreeColonistsSpawnedCount,
            bigThreats = Find.Storyteller.difficulty.allowBigThreats, gameEnding = Find.GameEnder.gameEnding,
            wandererTick = Find.GameEnder.newWanderersCreatedTick }));
        foreach (var step in Wait(() => map.lordManager.lords.Any(l => !existing.Contains(l) && l.LordJob is LordJob_PsychicRitualRepeating),
            5000, "due follow-up created a real vanilla psychic ritual siege")) yield return step;
        var lord = map.lordManager.lords.Single(l => !existing.Contains(l) && l.LordJob is LordJob_PsychicRitualRepeating);
        var native = (LordJob_PsychicRitualRepeating)lord.LordJob;
        Require(native.def.defName == "SkipAbduction" && Component.NextRaidTick == -1,
            "follow-up uses skip abduction specifically and consumes its timer");

        // Use that native follow-up with an actual colony slave as the only eligible victim.
        var site = Site();
        native.assignments.SetTarget(new TargetInfo(site, map));
        foreach (var pawn in lord.ownedPawns.ToArray())
        {
            pawn.Position = CellFinder.RandomClosewalkCellNear(site, map, 4);
            pawn.Notify_Teleported();
        }
        var slave = Spawn(Faction.OfHoraxCult, map.Center);
        slave.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
        Require(slave.IsSlaveOfColony, "second victim is an actual colony slave");
        ApplyOutcome(lord, slave);
        foreach (var step in Wait(() => Faction.OfHoraxCult.kidnapped.KidnappedPawnsListForReading.Contains(slave), 7000,
            "the follow-up raid's native Kidnap job carries a slave off-map")) yield return step;
        Require(Component.NextRaidTick > Now, "successful slave kidnapping continues the raid chain");
    }

    public override void MapComponentUpdate()
    {
        if (finished) { Root.Shutdown(); return; }
        if (Current.ProgramState != ProgramState.Playing || LongEventHandler.AnyEventNowOrWaiting) return;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
        try
        {
            checks ??= (Phase switch { "start" => Start(), "resume" => Resume(), _ => Repeat() }).GetEnumerator();
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception exception) { Log.Error("CK_TEST_FAILED " + exception); Finish(exception.ToString()); }
    }

    private void Finish(string error)
    {
        finished = true;
        File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "ck-result.json"),
            JsonSerializer.Serialize(new { phase = Phase, passed = error == null, checks = passed, error },
                new JsonSerializerOptions { WriteIndented = true }));
    }
}
