using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BlackHole;

/// <summary>黑洞装载任务查找（结构取自 LoadTransportersJobUtility，替换为黑洞 Comp/JobDef）。</summary>
public static class BlackHoleJobUtility
{
    private static readonly HashSet<Thing> neededThings = new HashSet<Thing>();
    private static readonly Dictionary<TransferableOneWay, int> tmpAlreadyLoading = new Dictionary<TransferableOneWay, int>();

    /// <summary>除"还有货可搬"之外的硬性前置：装载中、有操作能力、能到达黑洞。</summary>
    public static bool CanHaulToHole(Pawn pawn, CompBlackHole hole)
    {
        if (!hole.loadingInProgress || !hole.AnythingLeftToLoad)
        {
            return false;
        }
        if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
        {
            return false;
        }
        if (!pawn.CanReach(hole.parent, PathEndMode.Touch, pawn.NormalMaxDanger()))
        {
            return false;
        }
        return true;
    }

    public static bool HasJobOnHole(Pawn pawn, CompBlackHole hole)
    {
        return CanHaulToHole(pawn, hole) && FindThingToLoad(pawn, hole).Thing != null;
    }

    /// <summary>
    /// 一次搜索直接出 job：货物与数量在发 job 时就烘进 targetA/count。
    /// 原版是"HasJob 判一次 → 发空 job → Notify_Starting 再选一次货"，
    /// 两次搜索之间货物可能被别人预约走（null 预约竞态），这里合并成一次。
    /// </summary>
    public static Job JobOnHole(Pawn p, CompBlackHole hole)
    {
        ThingCount thingCount = FindThingToLoad(p, hole);
        if (thingCount.Thing == null)
        {
            return null;
        }
        Job job = JobMaker.MakeJob(BlackHoleDefOf.BlackHoleHaul, thingCount.Thing, hole.parent);
        job.count = thingCount.Count;
        job.ignoreForbidden = true;
        return job;
    }

    public static ThingCount FindThingToLoad(Pawn p, CompBlackHole hole)
    {
        neededThings.Clear();
        tmpAlreadyLoading.Clear();
        List<TransferableOneWay> leftToLoad = hole.leftToLoad;
        if (leftToLoad != null)
        {
            IReadOnlyList<Pawn> allPawnsSpawned = hole.parent.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawnsSpawned.Count; i++)
            {
                if (allPawnsSpawned[i] == p || allPawnsSpawned[i].CurJobDef != BlackHoleDefOf.BlackHoleHaul)
                {
                    continue;
                }
                if (allPawnsSpawned[i].jobs.curDriver is not JobDriver_HaulToBlackHole driver || driver.HoleBuilding != hole.parent)
                {
                    continue;
                }
                TransferableOneWay transferableOneWay = TransferableUtility.TransferableMatchingDesperate(driver.ThingToCarry, leftToLoad, TransferAsOneMode.PodsOrCaravanPacking);
                if (transferableOneWay != null)
                {
                    tmpAlreadyLoading.TryGetValue(transferableOneWay, out var value);
                    tmpAlreadyLoading[transferableOneWay] = value + driver.initialCount;
                }
            }
            for (int j = 0; j < leftToLoad.Count; j++)
            {
                tmpAlreadyLoading.TryGetValue(leftToLoad[j], out var value2);
                if (leftToLoad[j].CountToTransfer - value2 > 0)
                {
                    for (int k = 0; k < leftToLoad[j].things.Count; k++)
                    {
                        neededThings.Add(leftToLoad[j].things[k]);
                    }
                }
            }
        }
        if (neededThings.Count == 0)
        {
            return default(ThingCount);
        }
        // 原版 validator：排除 forbidden。选中后被禁用的货物搬不到，由 CompTick 的卡死警告兜底
        //（原版同款设计：不擅自改玩家的货单，只警告，等玩家取消或解除禁用）。
        Thing thing = GenClosest.ClosestThingReachable(p.Position, p.Map, ThingRequest.ForGroup(ThingRequestGroup.HaulableEver), PathEndMode.Touch, TraverseParms.For(p), 9999f, (Thing x) => neededThings.Contains(x) && p.CanReserve(x) && !x.IsForbidden(p) && p.carryTracker.AvailableStackSpace(x.def) > 0, null, 0, -1, forceAllowGlobalSearch: false, RegionType.Set_Passable, ignoreEntirelyForbiddenRegions: false, lookInHaulSources: true);
        if (thing == null)
        {
            // 需要被"扛进去"的小人：囚犯/动物/倒地者/低电量自关机的机械体（1.6 原版同款条件）。
            // 自由殖民者和机械体不在此列——他们由 JobGiver_EnterBlackHole 自己走进去；
            // 走不进去的（被反锁等）由对话框的可达性校验在开始前拦下（原版 CheckForErrors）。
            foreach (Thing neededThing in neededThings)
            {
                if (neededThing is Pawn { Spawned: true } pawn
                    && ((!pawn.IsFreeColonist && !pawn.IsColonyMech) || pawn.Downed || pawn.needs?.energy?.IsLowEnergySelfShutdown == true)
                    && !pawn.inventory.UnloadEverything
                    && p.CanReserveAndReach(pawn, PathEndMode.Touch, Danger.Deadly))
                {
                    return new ThingCount(pawn, 1);
                }
            }
            return default(ThingCount);
        }
        TransferableOneWay transferableOneWay3 = null;
        for (int num = 0; num < leftToLoad.Count; num++)
        {
            if (leftToLoad[num].things.Contains(thing))
            {
                transferableOneWay3 = leftToLoad[num];
                break;
            }
        }
        if (transferableOneWay3 == null)
        {
            return default(ThingCount);
        }
        tmpAlreadyLoading.TryGetValue(transferableOneWay3, out var value3);
        int count = Mathf.Min(transferableOneWay3.CountToTransfer - value3, thing.stackCount);
        return count > 0 ? new ThingCount(thing, count) : default(ThingCount);
    }
}
