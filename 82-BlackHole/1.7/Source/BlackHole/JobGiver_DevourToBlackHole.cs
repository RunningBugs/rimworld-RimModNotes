using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace BlackHole;

/// <summary>给装载 Lord 成员派发搬运工作（结构仿 JobGiver_LoadTransporters）。</summary>
public class JobGiver_DevourToBlackHole : ThinkNode_JobGiver
{
    protected override Job TryGiveJob(Pawn pawn)
    {
        if (pawn.mindState.duty?.focus.Thing is not Building building
            || !building.TryGetComp<CompBlackHole>(out var hole))
        {
            return null;
        }
        if (!BlackHoleJobUtility.CanHaulToHole(pawn, hole))
        {
            return null;
        }
        // 一次搜索直接出 job（货物烘进 targetA），选不到货时返回 null
        return BlackHoleJobUtility.JobOnHole(pawn, hole);
    }
}

/// <summary>被勾选为"货物"的自由小人：自己走进黑洞（结构仿 JobGiver_EnterTransporter 的自主入舱）。</summary>
public class JobGiver_EnterBlackHole : ThinkNode_JobGiver
{
    protected override Job TryGiveJob(Pawn pawn)
    {
        if (pawn.mindState.duty?.focus.Thing is not Building building
            || !building.TryGetComp<CompBlackHole>(out var hole)
            || !hole.loadingInProgress || hole.leftToLoad == null)
        {
            return null;
        }
        // 原版 JobGiver_EnterTransporter：还有人正在往本黑洞搬货时先不进去
        //（等所有搬运结束再集体走入；到位时的 HasJobOnHole 守卫兜底竞态）
        IReadOnlyList<Pawn> allPawnsSpawned = pawn.Map.mapPawns.AllPawnsSpawned;
        for (int i = 0; i < allPawnsSpawned.Count; i++)
        {
            if (allPawnsSpawned[i] != pawn
                && allPawnsSpawned[i].CurJobDef == BlackHoleDefOf.BlackHoleHaul
                && allPawnsSpawned[i].jobs.curDriver is JobDriver_HaulToBlackHole driver
                && driver.HoleBuilding == building)
            {
                return null;
            }
        }
        for (int i = 0; i < hole.leftToLoad.Count; i++)
        {
            TransferableOneWay transferable = hole.leftToLoad[i];
            if (transferable.CountToTransfer > 0 && transferable.things.Contains(pawn)
                && pawn.CanReach(building, PathEndMode.Touch, Danger.Deadly))
            {
                Job job = JobMaker.MakeJob(BlackHoleDefOf.BlackHoleEnter, building);
                job.count = 1;
                return job;
            }
        }
        return null;
    }
}
