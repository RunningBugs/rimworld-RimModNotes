using System.Collections.Generic;
using Verse;
using RimWorld;

namespace MoreRaidStrategies.HybridRaids
{
    public class RaidStrategyDefExtension : DefModExtension
    {
        public List<SubStrategy> subStrategies;
    }

    public class SubStrategy
    {
        public RaidStrategyDef def;
        public float pointsFactor = 1f;
        public PawnsArrivalModeDef arrivalMode;
    }
}