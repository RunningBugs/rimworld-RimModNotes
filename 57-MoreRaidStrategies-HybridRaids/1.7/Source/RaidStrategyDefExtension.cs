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
        public List<PawnsArrivalModeDef> arriveModes = new List<PawnsArrivalModeDef>();

        public List<PawnsArrivalModeDef> GetPawnsArrivalModeDefs()
        {
            if (arriveModes == null || arriveModes.Count == 0)
            {
                return def.arriveModes ?? [];
            }
            return arriveModes;
        }
    }
}