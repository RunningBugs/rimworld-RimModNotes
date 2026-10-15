using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

namespace RW17.Validation;

public static class Mod53Checks
{
    public const string PackageId = "Porio.TunnulerFix.fork";

    public static void Run(Map map)
    {
        Require(ModsConfig.BiotechActive, "Biotech must be active for a real Tunneler.");
        Require(!AppDomain.CurrentDomain.GetAssemblies().Any(a =>
            a.GetName().Name is "TunnelersDeepDrill" or "TunnulerFix"),
            "1.7 loads only root XML, excluding both obsolete XP-transpiler assemblies.");
        WorkGiverDef giverDef = DefDatabase<WorkGiverDef>.GetNamed("Drill");
        Require(giverDef.canBeDoneByMechs, "The actual XML patch enables mech drilling.");
        IntVec3 cell = CellRect.CenteredOn(map.Center, 30).FirstOrDefault(c =>
            GenAdj.OccupiedRect(c, Rot4.North, ThingDefOf.DeepDrill.size)
                .ExpandedBy(1).All(k => k.InBounds(map) && k.Standable(map) && k.GetThingList(map).Count == 0), IntVec3.Invalid);
        Require(cell.IsValid, "A clear drill footprint and interaction area exist.");
        var drill = (Building)ThingMaker.MakeThing(ThingDefOf.DeepDrill);
        drill.SetFaction(Faction.OfPlayer);
        Pawn mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            DefDatabase<PawnKindDef>.GetNamed("Mech_Tunneler"), Faction.OfPlayer, forceGenerateNewPawn: true));
        Pawn human = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
            Faction.OfPlayer, forceGenerateNewPawn: true, forceNoBackstory: true));
        ThingDef oldResource = map.deepResourceGrid.ThingDefAt(cell);
        int oldCount = map.deepResourceGrid.CountAt(cell);
        try
        {
            map.deepResourceGrid.SetAt(cell, ThingDefOf.Steel, 1000);
            GenSpawn.Spawn(drill, cell, map, Rot4.North);
            drill.GetComp<CompPowerTrader>().PowerOn = true;
            GenSpawn.Spawn(mech, drill.InteractionCell, map);
            Require(mech.def.defName == "Mech_Tunneler" && mech.skills == null,
                "The fixture is a real Tunneler without a skills tracker.");
            RunActualWorkTick(mech, drill, giverDef);
            mech.jobs.EndCurrentJob(JobCondition.InterruptForced);
            mech.DeSpawn();
            GenSpawn.Spawn(human, drill.InteractionCell, map);
            SkillRecord mining = human.skills.GetSkill(SkillDefOf.Mining);
            Require(!mining.TotallyDisabled, "The human control can learn Mining.");
            float beforeXp = mining.XpTotalEarned + mining.xpSinceLastLevel;
            RunActualWorkTick(human, drill, giverDef);
            float afterXp = mining.XpTotalEarned + mining.xpSinceLastLevel;
            Log.Message("[RW17.Validation] Mod53 human total XP=" + beforeXp + " -> " + afterXp +
                ", completed-level XP=" + mining.XpTotalEarned + ", current-level XP=" + mining.xpSinceLastLevel);
            Require(afterXp > beforeXp, "Native drilling still awards XP to a real human operator.");
            Log.Message("[RW17.Validation] Mod53 checks passed: no_legacy_dll, xml_mech_enabled, " +
                "real_tunneler_null_skills, native_workgiver_job, actual_driver_tick_progress, human_xp_preserved");
        }
        finally
        {
            foreach (Pawn pawn in new[] { mech, human })
                if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
            if (!drill.Destroyed) drill.Destroy(DestroyMode.Vanish);
            map.deepResourceGrid.SetAt(cell, oldResource, oldCount);
        }
    }

    private static void RunActualWorkTick(Pawn pawn, Building drill, WorkGiverDef giverDef)
    {
        var giver = (WorkGiver_DeepDrill)giverDef.Worker;
        Require(giver.HasJobOnThing(pawn, drill, true), "The native WorkGiver accepts this operator and powered drill.");
        Job job = giver.JobOnThing(pawn, drill, true);
        Require(job?.def == JobDefOf.OperateDeepDrill, "The native giver creates the actual drilling job.");
        pawn.jobs.StartJob(job, JobCondition.InterruptForced);
        Require(pawn.CurJobDef == JobDefOf.OperateDeepDrill && pawn.jobs.curDriver is JobDriver_OperateDeepDrill,
            "The actual job tracker starts the native drilling driver.");
        var toils = (List<Toil>)typeof(JobDriver).GetField("toils", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(pawn.jobs.curDriver);
        Toil work = toils.Last();
        Require(work.actor == pawn && work.tickIntervalAction != null, "The real initialized work toil is present.");
        CompDeepDrill comp = drill.GetComp<CompDeepDrill>();
        float before = comp.ProgressToNextPortionPercent;
        work.tickIntervalAction(60);
        Require(comp.ProgressToNextPortionPercent > before, "Invoking the actual driver work tick advances real drill progress.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
