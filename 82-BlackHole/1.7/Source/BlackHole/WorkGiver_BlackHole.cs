using RimWorld;
using Verse;
using Verse.AI;

namespace BlackHole;

/// <summary>
/// 普通搬运帮忙（结构仿 WorkGiver_LoadTransporters）：装载进行中，
/// 所有开了 Hauling 工作的殖民者/机械体都会按正常工作优先级来搬货进黑洞，
/// 不进 Lord、不打断其他工作。右键黑洞可强制优先（playerForced）。
/// </summary>
public class WorkGiver_BlackHole : WorkGiver_Scanner
{
    public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(BlackHoleDefOf.BlackHole);

    public override PathEndMode PathEndMode => PathEndMode.Touch;

    public override Danger MaxPathDanger(Pawn pawn)
    {
        return Danger.Deadly;
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        return t.TryGetComp<CompBlackHole>() is CompBlackHole hole && BlackHoleJobUtility.HasJobOnHole(pawn, hole);
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        return BlackHoleJobUtility.JobOnHole(pawn, t.TryGetComp<CompBlackHole>());
    }
}
