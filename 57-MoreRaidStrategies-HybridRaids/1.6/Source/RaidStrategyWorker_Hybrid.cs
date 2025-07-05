using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI.Group;

namespace MoreRaidStrategies.HybridRaids
{
    public class RaidStrategyWorker_Hybrid : RaidStrategyWorker
    {
        private Dictionary<Pawn, SubStrategy> pawnToStrategyMap = new Dictionary<Pawn, SubStrategy>();
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

                var subParms = new IncidentParms
                {
                    target = parms.target,
                    faction = parms.faction,
                    points = parms.points * subStrategy.pointsFactor,
                    raidStrategy = subStrategy.def,
                    raidArrivalMode = subStrategy.arrivalMode ?? parms.raidArrivalMode,
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
            List<Pawn> allPawns = new List<Pawn>();

            foreach (var subStrategy in extension.subStrategies)
            {
                var pawnGroupMakerParms = new PawnGroupMakerParms
                {
                    groupKind = PawnGroupKindDefOf.Combat,
                    tile = parms.target.Tile,
                    points = parms.points * subStrategy.pointsFactor,
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
                }
                allPawns.AddRange(subPawns);
            }

            var pawnsByArrivalMode = allPawns.GroupBy(p => pawnToStrategyMap[p].arrivalMode ?? parms.raidArrivalMode);

            foreach (var group in pawnsByArrivalMode)
            {
                PawnsArrivalModeDef arrivalMode = group.Key;
                List<Pawn> groupPawns = group.ToList();

                var arrivalParms = new IncidentParms
                {
                    target = parms.target,
                    faction = parms.faction,
                    raidStrategy = def,
                    raidArrivalMode = arrivalMode,
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
                if (subPoints < subStrategy.def.Worker.MinimumPoints(parms.faction, groupKind))
                {
                    return false;
                }
            }
            return true;
        }
    }
}