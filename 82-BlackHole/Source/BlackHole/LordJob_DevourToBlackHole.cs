using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace BlackHole;

/// <summary>黑洞装载 Lord：结构仿 LordJob_LoadAndEnterTransporters，仅保留装载语义（无发射、无入舱乘客——被选中的小人货物由 JobGiver_EnterBlackHole 自己走进黑洞）。</summary>
public class LordJob_DevourToBlackHole : LordJob
{
    private Thing parent;

    public Thing Parent => parent;

    public override bool AllowStartNewGatherings => false;

    public override bool AddFleeToil => false;

    public LordJob_DevourToBlackHole()
    {
    }

    public LordJob_DevourToBlackHole(Thing parent)
    {
        this.parent = parent;
    }

    public override void ExposeData()
    {
        Scribe_References.Look(ref parent, "parent");
    }

    public override StateGraph CreateGraph()
    {
        StateGraph stateGraph = new StateGraph();
        LordToil_DevourToBlackHole startingToil = new LordToil_DevourToBlackHole(parent);
        stateGraph.StartingToil = startingToil;
        stateGraph.AddToil(new LordToil_End());
        return stateGraph;
    }
}

public class LordToil_DevourToBlackHole : LordToil
{
    private readonly Thing parent;

    public override bool AllowSatisfyLongNeeds => false;

    public LordToil_DevourToBlackHole(Thing parent)
    {
        this.parent = parent;
    }

    public override void UpdateAllDuties()
    {
        for (int i = 0; i < lord.ownedPawns.Count; i++)
        {
            lord.ownedPawns[i].mindState.duty = new PawnDuty(BlackHoleDefOf.DevourToBlackHole, parent);
        }
    }
}
