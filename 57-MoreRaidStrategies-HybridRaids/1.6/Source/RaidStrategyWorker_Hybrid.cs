using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI.Group;

namespace MoreRaidStrategies.HybridRaids
{
    [DefOf]
    public static class HybridPawnsArrivalModeDefOf
    {
        public static PawnsArrivalModeDef HybridArrival;
    }

    public class RaidStrategyWorker_Hybrid : RaidStrategyWorker
    {
        private Dictionary<Pawn, SubStrategy> pawnToStrategyMap = new Dictionary<Pawn, SubStrategy>();
        private Dictionary<Pawn, PawnsArrivalModeDef> pawnToArrivalModeMap = new Dictionary<Pawn, PawnsArrivalModeDef>();
        private static readonly MethodInfo MakeLordJobMethod = typeof(RaidStrategyWorker).GetMethod("MakeLordJob", BindingFlags.Instance | BindingFlags.NonPublic);

        protected override LordJob MakeLordJob(IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
        {
            // This method is not used directly. The logic is handled in MakeLords.
            return null;
        }

        public override void MakeLords(IncidentParms parms, List<Pawn> pawns)
        {
            Map map = (Map)parms.target;
            var extension = def.GetModExtension<RaidStrategyDefExtension>();
            if (extension == null)
            {
                Log.Error($"[MoreRaidStrategies.HybridRaids] Missing RaidStrategyDefExtension for {def.defName}");
                // Fallback to default behavior
                base.MakeLords(parms, pawns);
                return;
            }

            var pawnsByStrategy = pawns.GroupBy(p => pawnToStrategyMap[p]).ToList();
            int raidSeed = Rand.Int;

            foreach (var group in pawnsByStrategy)
            {
                SubStrategy subStrategy = group.Key;
                List<Pawn> groupPawns = group.ToList();
                if (groupPawns.Count == 0)
                {
                    continue; // Skip empty groups
                }
                var groupArrivalMode = pawnToArrivalModeMap[groupPawns.FirstOrDefault()];

                var subParms = new IncidentParms
                {
                    target = parms.target,
                    faction = parms.faction,
                    points = parms.points * subStrategy.pointsFactor,
                    raidStrategy = subStrategy.def,
                    raidArrivalMode = groupArrivalMode,
                    pawnGroupMakerSeed = Rand.Int,
                    inSignalEnd = parms.inSignalEnd,
                    questTag = parms.questTag
                };

                LordJob lordJob = (LordJob)MakeLordJobMethod.Invoke(subStrategy.def.Worker, new object[] { subParms, map, groupPawns, raidSeed });
                if (lordJob != null)
                {
                    LordMaker.MakeNewLord(parms.faction, lordJob, map, groupPawns);
                }
            }
        }

        public override List<Pawn> SpawnThreats(IncidentParms parms)
        {
            var extension = def.GetModExtension<RaidStrategyDefExtension>();
            if (extension == null)
            {
                Log.Error($"[MoreRaidStrategies.HybridRaids] Missing RaidStrategyDefExtension for {def.defName}");
                return base.SpawnThreats(parms);
            }

            pawnToStrategyMap.Clear();
            pawnToArrivalModeMap.Clear();
            List<Pawn> allPawns = new List<Pawn>();
            List<PawnsArrivalModeDef> selectedArrivalModes = new();

            foreach (var subStrategy in extension.subStrategies)
            {
                var groupArrivalMode = subStrategy.GetPawnsArrivalModeDefs().RandomElement();

                if (!selectedArrivalModes.Contains(groupArrivalMode))
                {
                    selectedArrivalModes.Add(groupArrivalMode);
                }

                var parmPoints = parms.points;
                var subStrategyPoints = parmPoints * subStrategy.pointsFactor;
                var curve = groupArrivalMode.pointsFactorCurve;
                var factor = curve?.Evaluate(subStrategyPoints) ?? 1f;
                var points = factor * subStrategyPoints;
                var pawnGroupMakerParms = new PawnGroupMakerParms
                {
                    groupKind = PawnGroupKindDefOf.Combat,
                    tile = parms.target.Tile,
                    points = points,
                    faction = parms.faction,
                    raidStrategy = subStrategy.def,
                    raidAgeRestriction = parms.raidAgeRestriction,
                    inhabitants = false,
                    seed = Rand.Int
                };

                List<Pawn> subPawns = PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms, true).ToList();
                foreach (Pawn pawn in subPawns)
                {
                    pawnToStrategyMap[pawn] = subStrategy;
                    pawnToArrivalModeMap[pawn] = groupArrivalMode;
                }
                allPawns.AddRange(subPawns);
            }

            var textEnemy = "";
            foreach (var arrivalMode in selectedArrivalModes)
            {
                textEnemy += $"{arrivalMode.textEnemy}\n\n";
            }
            parms.raidArrivalMode.textEnemy = textEnemy.Translate(parms.faction.def.pawnsPlural, parms.faction.Name.ApplyTag(parms.faction));

            var pawnsByArrivalMode = allPawns.GroupBy(p => pawnToArrivalModeMap[p]);

            foreach (var group in pawnsByArrivalMode)
            {
                PawnsArrivalModeDef arrivalMode = group.Key;
                List<Pawn> groupPawns = group.ToList();
                var pawnArrivalMode = groupPawns.Select(p => pawnToArrivalModeMap[p]).FirstOrDefault();

                var arrivalParms = new IncidentParms
                {
                    target = parms.target,
                    faction = parms.faction,
                    raidStrategy = def,
                    raidArrivalMode = pawnArrivalMode,
                };

                arrivalMode.Worker.TryResolveRaidSpawnCenter(arrivalParms);
                arrivalMode.Worker.Arrive(groupPawns, arrivalParms);
            }

            return allPawns;
        }

        public override bool CanUseWith(IncidentParms parms, PawnGroupKindDef groupKind)
        {
            if (!base.CanUseWith(parms, groupKind))
            {
                return false;
            }
            var extension = def.GetModExtension<RaidStrategyDefExtension>();
            if (extension == null)
            {
                return false;
            }
            foreach (var subStrategy in extension.subStrategies)
            {
                float subPoints = parms.points * subStrategy.pointsFactor;
                float points = parms.points;
                parms.points = subPoints;
                if (!subStrategy.def.Worker.CanUseWith(parms, groupKind))
                {
                    return false;
                }
                parms.points = points; // Restore original points after checking
                // if (subPoints < subStrategy.def.Worker.MinimumPoints(parms.faction, groupKind))
                // {
                //     return false;
                // }
            }
            return true;
        }
    }
}