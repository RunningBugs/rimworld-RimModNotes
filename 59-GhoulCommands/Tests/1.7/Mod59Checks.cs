using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RW17.Validation;

public static class Mod59Checks
{
    public const string PackageId = "RunningBugs.GhoulCommands";

    public static void Run(Map map)
    {
        Require(ModsConfig.AnomalyActive, "Anomaly must be enabled for Ghoul Commands.");
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "GhoulCommands");
        object settings = assembly.GetType("GhoulCommands.GhoulCommandsModUI").GetField("Settings").GetValue(null);
        var jugEnabled = settings.GetType().GetField("enableJuggernautSerumToggle");
        var metalEnabled = settings.GetType().GetField("enableMetalbloodSerumToggle");
        object oldJug = jugEnabled.GetValue(settings), oldMetal = metalEnabled.GetValue(settings);
        var nodes = DefDatabase<ThinkTreeDef>.GetNamed("Ghoul").thinkRoot.ThisAndChildrenRecursive
            .OfType<ThinkNode_JobGiver>().ToList();
        var jugNode = nodes.Single(n => n.GetType().FullName == "GhoulCommands.JobGiver_TakeJuggernautSerum");
        var metalNode = nodes.Single(n => n.GetType().FullName == "GhoulCommands.JobGiver_TakeMetalbloodSerum");
        IntVec3 cell = CellRect.CenteredOn(map.Center, 20).FirstOrDefault(c => c.InBounds(map) &&
            c.Standable(map) && c.GetThingList(map).Count == 0 && (c + IntVec3.East).InBounds(map) &&
            (c + IntVec3.East).Standable(map) && (c + IntVec3.East).GetThingList(map).Count == 0, IntVec3.Invalid);
        Require(cell.IsValid, "A disposable reachable serum fixture cell exists.");
        Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Ghoul,
            Faction.OfPlayer, forceGenerateNewPawn: true));
        Thing jug = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("JuggernautSerum"));
        Thing metal = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MetalbloodSerum"));
        try
        {
            jugEnabled.SetValue(settings, true); metalEnabled.SetValue(settings, true);
            GenSpawn.Spawn(pawn, cell + IntVec3.East, map);
            GenSpawn.Spawn(jug, cell, map); GenSpawn.Spawn(metal, cell, map);
            Require(pawn.IsGhoul && pawn.IsColonySubhumanPlayerControlled, "The fixture is a real player-controlled ghoul.");
            var comp = pawn.AllComps.Single(c => c.GetType().FullName == "GhoulCommands.CompGhoulCommands");
            Require(jugNode.TryIssueJobPackage(pawn, default).Job == null &&
                metalNode.TryIssueJobPackage(pawn, default).Job == null, "Default inactive commands produce no job.");
            var gizmos = comp.CompGetGizmosExtra().OfType<Command_Toggle>().ToList();
            Require(gizmos.Count == 2, "Both actual serum commands are exposed.");
            foreach (var gizmo in gizmos) { Require(!gizmo.isActive(), "Fresh command defaults to off."); gizmo.toggleAction(); }
            Require(gizmos.All(g => g.isActive()), "Actual toggle actions update component state.");

            foreach (var scenario in new[]
            {
                (node: jugNode, serum: jug, jobName: "TakeJuggernautSerum", buff: "JuggernautSerum"),
                (node: metalNode, serum: metal, jobName: "TakeMetalbloodSerum", buff: "Metalblood")
            })
            {
                Job job = scenario.node.TryIssueJobPackage(pawn, default).Job;
                Require(job != null && job.def == DefDatabase<JobDef>.GetNamed(scenario.jobName) &&
                    job.GetTarget(TargetIndex.B).Thing == scenario.serum,
                    "Actual injected job giver selects the reachable serum at target B.");
                Require(job.def.driverClass.Assembly == assembly, "The job resolves to this MOD's actual driver.");
                scenario.serum.SetForbidden(true, false);
                Require(scenario.node.TryIssueJobPackage(pawn, default).Job == null, "Forbidden serum is not ordered.");
                scenario.serum.SetForbidden(false, false);
                Hediff buff = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed(scenario.buff), pawn);
                pawn.health.AddHediff(buff);
                Require(scenario.node.TryIssueJobPackage(pawn, default).Job == null, "An existing serum hediff prevents redosing.");
                pawn.health.RemoveHediff(buff);
            }
            jugEnabled.SetValue(settings, false); metalEnabled.SetValue(settings, false);
            Require(!comp.CompGetGizmosExtra().Any() && jugNode.TryIssueJobPackage(pawn, default).Job == null &&
                metalNode.TryIssueJobPackage(pawn, default).Job == null, "Global settings disable commands and job production.");
            Log.Message("[RW17.Validation] Mod59 checks passed: think_tree_nodes, human_comp, " +
                "default_off, two_commands, actual_toggles, two_serum_jobs, forbidden, no_redose, settings_disabled");
        }
        finally
        {
            jugEnabled.SetValue(settings, oldJug); metalEnabled.SetValue(settings, oldMetal);
            foreach (Thing thing in new Thing[] { jug, metal, pawn })
                if (!thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
