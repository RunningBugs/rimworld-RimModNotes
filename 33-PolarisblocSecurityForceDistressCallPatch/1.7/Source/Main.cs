using System.Collections.Generic;
using Verse;
using RimWorld.Planet;
namespace SFDistressCallFix;
[StaticConstructorOnStartup]
public class Start
{
    static Start() { Log.Message("SFDistressCallFix: 1.7 vanilla already guards missing corpses; retaining the current original corpse pipeline"); }
}
public static class DistressCallUtilityPatch
{
    // Preserve the public helper while the 1.7 original now contains the required guard.
    public static void SpawnCorpses(Map map, IEnumerable<Pawn> pawns, IEnumerable<Pawn> killers, IntVec3 root, int radius)
        => DistressCallUtility.SpawnCorpses(map, pawns, killers, root, radius);
}
