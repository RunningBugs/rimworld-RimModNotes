using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace BlackHole;

/// <summary>
/// 搬运物品到黑洞：搬到即销毁（不经过容器）。
/// 结构参考 JobDriver_HaulToTransporter，终点动作改为直接 Devour。
/// </summary>
public class JobDriver_HaulToBlackHole : JobDriver
{
    public int initialCount;

    public Thing HoleBuilding => (Thing)job.GetTarget(TargetIndex.B);

    public CompBlackHole Hole => HoleBuilding?.TryGetComp<CompBlackHole>();

    public Thing ThingToCarry => job.GetTarget(TargetIndex.A).Thing;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref initialCount, "initialCount", 0);
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if (!job.targetA.IsValid)
        {
            // 兼容旧存档/异常路径：货物未烘入 job，交由 Notify_Starting 现场选货
            return true;
        }
        // 货物在发 job 时已选定（BlackHoleJobUtility.JobOnHole），这里做真正的预约；
        // 预约失败（被同事抢先）则 job 直接不发，不会再出现 null 预约。
        return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
    }

    public override void Notify_Starting()
    {
        base.Notify_Starting();
        if (!job.targetA.IsValid)
        {
            // 旧版在途 job 兼容：现场选一次货；选不到则由 toil 的 FailOn 终结本 job
            if (Hole == null)
            {
                return;
            }
            ThingCount thingCount = BlackHoleJobUtility.FindThingToLoad(pawn, Hole);
            if (thingCount.Thing == null)
            {
                return;
            }
            job.targetA = thingCount.Thing;
            job.count = thingCount.Count;
            pawn.Reserve(thingCount.Thing, job);
        }
        // 原版 JobDriver_HaulToTransporter：右键强制搬运时，若手上拿着别的东西先丢掉
        if (job.playerForced && pawn.carryTracker.CarriedThing != null && pawn.carryTracker.CarriedThing != job.targetA.Thing)
        {
            pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var _);
        }
        initialCount = job.count;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(TargetIndex.B);
        this.FailOn(() => CompBlackHole.WasLoadingCanceled(HoleBuilding));
        this.FailOn(() => ThingToCarry == null);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch, canGotoSpawnedParent: true)
            .FailOnSelfAndParentsDespawnedOrNull(TargetIndex.A);
        // 原版 JobDriver_HaulToContainer 的 StartCarryThing 参数：堆数不足不失败、可从背包取货
        yield return Toils_Haul.StartCarryThing(TargetIndex.A, putRemainderInQueue: false, subtractNumTakenFromJobCount: true, failIfStackCountLessThanJobCount: false, reserve: true, canTakeFromInventory: true);
        // 走到黑洞
        Toil carry = ToilMaker.MakeToil("CarryToBlackHole");
        carry.initAction = delegate
        {
            pawn.pather.StartPath(HoleBuilding, PathEndMode.Touch);
        };
        carry.AddFailCondition(() => HoleBuilding.Destroyed || CompBlackHole.WasLoadingCanceled(HoleBuilding));
        carry.defaultCompleteMode = ToilCompleteMode.PatherArrival;
        yield return carry;
        // 到点：从手中取出并直接湮灭
        Toil devour = ToilMaker.MakeToil("Devour");
        devour.initAction = delegate
        {
            Thing carried = pawn.carryTracker.CarriedThing;
            if (carried == null)
            {
                return;
            }
            CompBlackHole hole = Hole;
            if (hole == null)
            {
                // 黑洞在抵达瞬间被毁：就地放下，避免物品被摘出容器后凭空泄漏
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var _);
                return;
            }
            pawn.carryTracker.innerContainer.Remove(carried);
            hole.Devour(carried);
        };
        devour.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return devour;
    }
}

/// <summary>
/// 被选中作为货物的小人自己走进黑洞（结构仿 JobDriver_EnterTransporter：
/// 到位即入，还有搬运任务时先让位）——直接 Devour，无容器。
/// </summary>
public class JobDriver_EnterBlackHole : JobDriver
{
    private Thing BlackHoleThing => job.targetA.Thing;

    private CompBlackHole Hole => BlackHoleThing?.TryGetComp<CompBlackHole>();

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return true;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(TargetIndex.A);
        this.FailOn(() => CompBlackHole.WasLoadingCanceled(BlackHoleThing));
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
        Toil toil = ToilMaker.MakeToil("EnterBlackHole");
        toil.initAction = delegate
        {
            CompBlackHole hole = Hole;
            if (hole == null)
            {
                Log.Error($"[BlackHole] EnterBlackHole 无法解析黑洞: targetA={job.targetA} def={job.def}");
                return;
            }
            if (!pawn.Spawned)
            {
                return;
            }
            if (job.playerForced || !BlackHoleJobUtility.HasJobOnHole(pawn, hole))
            {
                // 直接湮灭（Destroy 内部会处理出图/预约/lord 解绑）
                hole.Devour(pawn);
            }
        };
        yield return toil;
    }
}
