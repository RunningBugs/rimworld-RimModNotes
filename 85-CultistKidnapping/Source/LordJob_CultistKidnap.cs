using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CultistKidnapping;

[DefOf]
public static class KidnappingDefOf
{
    public static DutyDef CK_AbductVictim;
    static KidnappingDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(KidnappingDefOf));
}

public sealed class LordJob_CultistKidnap : LordJob
{
    private Pawn victim;
    private Pawn carrier;
    private bool kidnapped;
    private bool refresh = true;
    private bool updatingDuties;

    public Pawn Victim => victim;
    public Pawn Carrier => carrier;
    public override bool AddFleeToil => false;
    public override bool GuiltyOnDowned => true;

    public LordJob_CultistKidnap() { }
    public LordJob_CultistKidnap(Pawn victim, Pawn invoker) { this.victim = victim; carrier = invoker; }

    public override StateGraph CreateGraph()
    {
        var graph = new StateGraph();
        graph.AddToil(new LordToil_CultistKidnap());
        return graph;
    }

    public bool CanCarry(Pawn pawn)
    {
#if !RW15
        if (!Map.CanEverExit) return false;
#endif
        if (victim == null || pawn == null || !pawn.Spawned || pawn.Map != Map || pawn.GetLord() != lord ||
            pawn.DeadOrDowned || pawn.InMentalState ||
            pawn.IsPrisoner || pawn.Faction != lord.faction || !pawn.HostileTo(victim) ||
            !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) ||
            !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving) || pawn.carryTracker == null) return false;
        if (!Map.reachability.CanReachMapEdge(victim.PositionHeld, TraverseParms.For(pawn, Danger.Deadly))) return false;
        if (pawn.carryTracker.CarriedThing == victim) return true;
        return victim.Spawned && (victim.Downed || !victim.Awake()) &&
            pawn.carryTracker.CarriedThing == null && pawn.carryTracker.AvailableStackSpace(victim.def) > 0 &&
            pawn.CanReserveAndReach(victim, PathEndMode.ClosestTouch, Danger.Deadly);
    }

    public Job MakeKidnapJob(Pawn pawn)
    {
        if (pawn != carrier || !CanCarry(pawn) || !RCellFinder.TryFindBestExitSpot(pawn, out var exit)) return null;
        var job = JobMaker.MakeJob(JobDefOf.Kidnap, victim, exit);
        job.count = 1;
        job.lord = lord;
        job.locomotionUrgency = LocomotionUrgency.Jog;
        return job;
    }

    public void UpdateDuties()
    {
        if (updatingDuties) return;
        updatingDuties = true;
        try
        {
            var leaving = kidnapped || victim == null || victim.Dead || victim.Destroyed || victim.MapHeld != Map;
            if (leaving) carrier = null;
            else if (!CanCarry(carrier))
                carrier = lord.ownedPawns.Where(CanCarry)
                    .OrderBy(pawn => pawn.Position.DistanceToSquared(victim.PositionHeld)).FirstOrDefault();

            foreach (var pawn in lord.ownedPawns.ToArray())
            {
                if (!pawn.Spawned || pawn.DeadOrDowned) continue;
                PawnDuty duty;
                if (leaving) duty = new PawnDuty(DutyDefOf.ExitMapBestAndDefendSelf);
                else if (pawn == carrier) duty = new PawnDuty(KidnappingDefOf.CK_AbductVictim, victim);
                else if (carrier != null) duty = new PawnDuty(DutyDefOf.Escort, carrier, 8f);
                else duty = new PawnDuty(DutyDefOf.Defend, victim.PositionHeld, 12f);
                duty.locomotion = LocomotionUrgency.Jog;
                var old = pawn.mindState.duty;
                if (old?.def == duty.def && old.focus == duty.focus) continue;
                pawn.mindState.duty = duty;
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            refresh = false;
        }
        finally { updatingDuties = false; }
    }

    public override void LordJobTick()
    {
        if (refresh || Find.TickManager.TicksGame % 120 == 0) UpdateDuties();
    }

    public override void Notify_PawnJobDone(Pawn pawn, JobCondition condition)
    {
        if (!updatingDuties && pawn == carrier && pawn.CurJob?.def == JobDefOf.Kidnap)
        {
            carrier = null;
            refresh = true;
        }
    }

    public override void Notify_PawnLost(Pawn pawn, PawnLostCondition condition)
    {
        if (pawn == carrier) carrier = null;
        refresh = true;
    }

    public override bool ShouldRemovePawn(Pawn pawn, PawnLostCondition reason) => reason != PawnLostCondition.Incapped;

    public void Notify_Kidnapped()
    {
        if (kidnapped) return;
        kidnapped = true;
        refresh = true;
        Map.GetComponent<KidnappingMapComponent>().ScheduleRepeat();
    }

    public override void ExposeData()
    {
        Scribe_References.Look(ref victim, "victim");
        Scribe_References.Look(ref carrier, "carrier");
        Scribe_Values.Look(ref kidnapped, "kidnapped");
    }
}

public sealed class LordToil_CultistKidnap : LordToil
{
    public override bool ForceHighStoryDanger => true;
    public override bool AllowSatisfyLongNeeds => false;
    public override bool AllowSelfTend => false;
    public override void UpdateAllDuties() => ((LordJob_CultistKidnap)lord.LordJob).UpdateDuties();
}

public sealed class JobGiver_AbductVictim : ThinkNode_JobGiver
{
    protected override Job TryGiveJob(Pawn pawn) =>
        (pawn.GetLord()?.LordJob as LordJob_CultistKidnap)?.MakeKidnapJob(pawn);
}
