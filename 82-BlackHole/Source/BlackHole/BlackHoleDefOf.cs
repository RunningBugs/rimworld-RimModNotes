using RimWorld;
using Verse;
using Verse.AI;

namespace BlackHole;

[DefOf]
public static class BlackHoleDefOf
{
    public static ThingDef BlackHole;
    public static JobDef BlackHoleHaul;
    public static JobDef BlackHoleEnter;
    public static DutyDef DevourToBlackHole;

    static BlackHoleDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(BlackHoleDefOf));
    }
}
