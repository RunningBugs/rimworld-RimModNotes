using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod57Checks
{
    public const string PackageId = "RunningBugs.MRS.Hybrid";
    private static float observedSiegePoints;
    private static void ObserveSiege(IncidentParms parms) => observedSiegePoints = parms.points;

    public static void Run(Map map)
    {
        var siegeHybrid = DefDatabase<RaidStrategyDef>.GetNamed("MRS_SiegeAndBreach");
        var dropHybrid = DefDatabase<RaidStrategyDef>.GetNamed("MRS_CenterDropPodAndBreach");
        var arrival = DefDatabase<PawnsArrivalModeDef>.GetNamed("HybridArrival");
        foreach (var def in new[] { siegeHybrid, dropHybrid })
        {
            Require(def.Worker.GetType().FullName == "MoreRaidStrategies.HybridRaids.RaidStrategyWorker_Hybrid",
                "Both real raid defs resolve to the hybrid worker.");
            var extension = def.modExtensions.Single(e => e.GetType().FullName ==
                "MoreRaidStrategies.HybridRaids.RaidStrategyDefExtension");
            var children = ((IEnumerable)extension.GetType().GetField("subStrategies").GetValue(extension))
                .Cast<object>().ToList();
            Require(children.Count == 2, "Both hybrid defs have two resolved sub-strategies.");
            foreach (object child in children)
            {
                Require(child.GetType().GetField("def").GetValue(child) is RaidStrategyDef,
                    "Sub-strategy cross-reference resolved.");
                var modes = (List<PawnsArrivalModeDef>)child.GetType().GetMethod("GetPawnsArrivalModeDefs").Invoke(child, null);
                Require(modes.Count > 0 && modes.All(m => m?.Worker != null),
                    "Actual arrival-mode fallback/override resolves usable workers.");
            }
        }
        Require(arrival.Worker.GetType().FullName == "MoreRaidStrategies.HybridRaids.PawnsArrivalModeWorker_Hybrid",
            "The hybrid arrival def resolves to this MOD's worker.");
        var worker = siegeHybrid.Worker;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var cached = (MethodInfo)worker.GetType().GetField("MakeLordJobMethod", flags).GetValue(null);
        Require(cached != null && cached.GetParameters().Select(p => p.ParameterType).SequenceEqual(
            new[] { typeof(IncidentParms), typeof(Map), typeof(List<Pawn>), typeof(int) }),
            "The production protected-method reflection cache resolves the exact native signature.");
        IncidentParms Args(Faction f, float points) => new()
        {
            target = map, faction = f, points = points,
            raidStrategy = siegeHybrid, raidArrivalMode = arrival
        };
        var nativeSiege = DefDatabase<RaidStrategyDef>.GetNamed("Siege").Worker;
        var nativeBreach = DefDatabase<RaidStrategyDef>.GetNamed("ImmediateAttackBreaching").Worker;
        Faction faction = null;
        float budget = 0f;
        foreach (float points in new[] { 2000f, 4000f, 8000f })
        {
            foreach (var candidate in Find.FactionManager.AllFactionsListForReading.Where(f => f.def.canSiege &&
                f.def.humanlikeFaction && !f.IsPlayer && !f.defeated && f.HostileTo(Faction.OfPlayer)))
            {
                bool siegeAllowed = nativeSiege.CanUseWith(Args(candidate, points * 0.5f), PawnGroupKindDefOf.Combat);
                bool breachAllowed = nativeBreach.CanUseWith(Args(candidate, points * 0.7f), PawnGroupKindDefOf.Combat);
                Log.Message($"[RW17.Validation] Mod57 native fixture candidate={candidate.def.defName}, " +
                    $"budget={points}, siege={siegeAllowed}, breach={breachAllowed}, " +
                    $"layer={(map.Tile.Valid ? map.Tile.LayerDef.defName : "invalid")}");
                if (siegeAllowed && breachAllowed) { faction = candidate; budget = points; break; }
            }
            if (faction != null) break;
        }
        Require(faction != null, "A real world faction and budget satisfy both complete native sub-strategy eligibility checks.");
        var rejectedFaction = Find.FactionManager.AllFactionsListForReading.First(f => !f.IsPlayer &&
            f.def.humanlikeFaction && !f.def.canSiege && f.def.pawnGroupMakers != null &&
            f.def.pawnGroupMakers.Any(g => g.kindDef == PawnGroupKindDefOf.Combat && g.options?.Count > 0) &&
            f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat) <= budget * 0.5f);
        var accepted = Args(faction, budget);
        var rejected = Args(rejectedFaction, budget);
        var siegeCheck = AccessTools.Method(typeof(RaidStrategyWorker_Siege), nameof(RaidStrategyWorker.CanUseWith));
        var observer = new Harmony("RW17.Validation.Mod57.Observer");
        observer.Patch(siegeCheck, prefix: new HarmonyMethod(typeof(Mod57Checks), nameof(ObserveSiege)));
        try
        {
            observedSiegePoints = float.NaN;
            Require(worker.CanUseWith(accepted, PawnGroupKindDefOf.Combat), "The real siege-capable faction accepts the hybrid.");
            Require(observedSiegePoints == budget * 0.5f, "The actual siege sub-worker receives its half budget on success.");
            Require(accepted.points == budget, "Successful eligibility checks preserve the incident budget.");
            observedSiegePoints = float.NaN;
            Require(!worker.CanUseWith(rejected, PawnGroupKindDefOf.Combat), "The actual non-siege faction rejects the hybrid.");
            Require(observedSiegePoints == budget * 0.5f, "Rejection really reaches the native siege sub-worker.");
            Require(rejected.points == budget, "Rejected sub-strategy checks also restore the incident budget.");
        }
        finally
        {
            observer.Unpatch(siegeCheck, HarmonyPatchType.Prefix, observer.Id);
        }

        var oldThings = map.listerThings.AllThings.ToHashSet();
        var oldLords = map.lordManager.lords.ToHashSet();
        string oldText = arrival.textEnemy;
        var strategyMap = (IDictionary)worker.GetType().GetField("pawnToStrategyMap",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(worker);
        var arrivalMap = (IDictionary)worker.GetType().GetField("pawnToArrivalModeMap",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(worker);
        Require(strategyMap.Count == 0 && arrivalMap.Count == 0, "Use a fresh disposable raid-worker fixture.");
        var raiders = new List<Pawn>();
        Rand.PushState(57017);
        try
        {
            raiders = worker.SpawnThreats(accepted);
            Require(raiders.Count > 0 && strategyMap.Count == raiders.Count && arrivalMap.Count == raiders.Count,
                "Real generation tracks every produced pawn's strategy and arrival mode.");
            Require(strategyMap.Values.Cast<object>().Distinct().Count() == 2,
                "Both actual sub-strategies produced a group.");
            worker.MakeLords(accepted, raiders);
            var lords = map.lordManager.lords.Where(l => !oldLords.Contains(l)).ToList();
            Require(lords.Count == 2 && raiders.All(p => lords.Any(l => l.ownedPawns.Contains(p))),
                "The actual protected native lord jobs form two lords covering all generated raiders.");
            Require(accepted.points == budget, "Spawn/lord dispatch retains the outer incident budget.");
            Log.Message("[RW17.Validation] Mod57 checks passed: two_defs, two_substrategies, arrival_modes, " +
                "reflection_signature, accepted_points_restored, rejected_points_restored, real_pawn_groups, two_native_lords");
        }
        finally
        {
            foreach (var lord in map.lordManager.lords.Where(l => !oldLords.Contains(l)).ToList())
                map.lordManager.RemoveLord(lord);
            foreach (Thing thing in map.listerThings.AllThings.Where(t => !oldThings.Contains(t)).ToList())
                if (!thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
            foreach (Pawn pawn in raiders) if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
            strategyMap.Clear(); arrivalMap.Clear(); arrival.textEnemy = oldText;
            Rand.PopState();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
