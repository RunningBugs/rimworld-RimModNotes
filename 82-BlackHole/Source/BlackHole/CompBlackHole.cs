using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace BlackHole;

/// <summary>
/// 黑洞：完全自研的装载/吞噬组件（不继承 CompTransporter，也不是容器）。
/// 黑洞是"接触即湮灭的目的地"——物品/小人送到即销毁，不经过任何容器。
/// - 装载：Lord 驱动——对话框确认后，选中的小人立刻放下手头工作去搬运/送入
/// - 取消：停止派发新搬运，结束 Lord，进行中的最后一趟搬运自然完成
/// </summary>
public class CompProperties_BlackHole : CompProperties
{
    public CompProperties_BlackHole()
    {
        compClass = typeof(CompBlackHole);
    }
}

public class CompBlackHole : ThingComp
{
    public List<TransferableOneWay> leftToLoad;

    public bool loadingInProgress;

    // 原版 CompTransporter.notifiedCantLoadMore：卡死警告只发一次
    private bool notifiedCantLoadMore;

    // 黑洞吞噬 Gizmo 图标：优先用 Mod 自带贴图（原版 UI/Commands/LoadTransporter 在 1.6 已不可靠）。
    // 必须惰性加载：Blueprints 等模组会在后台线程枚举 Gizmo，静态字段若在后台线程初始化，
    // ContentFinder 会拒绝加载并返回 null，且该静态值将永久为 null（本贴图丢失事故的根因）。
    private static Texture2D devourCommandTex;

    private static Texture2D DevourCommandTex
    {
        get
        {
            if (devourCommandTex == null && UnityData.IsInMainThread)
            {
                devourCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/DevourBlackHole")
                    ?? ContentFinder<Texture2D>.Get("UI/Commands/LoadTransporter", reportFailure: false);
            }
            return devourCommandTex;
        }
    }

    // 取消图标同理：不要直接引用 CompTransporter.CancelLoadCommandTex，
    // 否则若我们首先在后台线程触碰它，会连带把原版 CompTransporter 的静态贴图也毒化成 null。
    private static Texture2D cancelCommandTex;

    private static Texture2D CancelCommandTex
    {
        get
        {
            if (cancelCommandTex == null && UnityData.IsInMainThread)
            {
                cancelCommandTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
            }
            return cancelCommandTex;
        }
    }

    /// <summary>第一个还没装完的东西（原版 CompTransporter.FirstThingLeftToLoad）。</summary>
    public Thing FirstThingLeftToLoad
    {
        get
        {
            if (leftToLoad == null)
            {
                return null;
            }
            for (int i = 0; i < leftToLoad.Count; i++)
            {
                if (leftToLoad[i].CountToTransfer != 0 && leftToLoad[i].HasAnyThing)
                {
                    return leftToLoad[i].AnyThing;
                }
            }
            return null;
        }
    }

    public bool AnythingLeftToLoad => FirstThingLeftToLoad != null;

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
        CancelLoad(previousMap);
    }

    public override void CompTick()
    {
        // 原版 CompTransporter.CompTick 的卡死检测：还有货没装、但从任何角度都无人能继续装载时，
        // 警告玩家一次（CautionInput），由玩家决定是否取消——不擅自修改玩家的货单。
        if (!parent.IsHashIntervalTick(60) || !parent.Spawned)
        {
            return;
        }
        if (loadingInProgress && AnythingLeftToLoad && !notifiedCantLoadMore && !AnyPawnCanLoadAnythingNow)
        {
            notifiedCantLoadMore = true;
            Messages.Message("BlackHole.CantLoadMore".Translate(FirstThingLeftToLoad.LabelNoCount), parent, MessageTypeDefOf.CautionInput, historical: false);
        }
    }

    /// <summary>是否还有任何小人能继续装载（原版 CompTransporter.AnyPawnCanLoadAnythingNow 的黑洞版）。</summary>
    private bool AnyPawnCanLoadAnythingNow
    {
        get
        {
            if (!AnythingLeftToLoad || !parent.Spawned)
            {
                return false;
            }
            IReadOnlyList<Pawn> allPawnsSpawned = parent.Map.mapPawns.AllPawnsSpawned;
            // 1) 还有在进行的本黑洞搬运/走入 job → 装载仍在推进
            for (int i = 0; i < allPawnsSpawned.Count; i++)
            {
                if (allPawnsSpawned[i].CurJobDef == BlackHoleDefOf.BlackHoleHaul
                    && allPawnsSpawned[i].jobs.curDriver is JobDriver_HaulToBlackHole driver
                    && driver.HoleBuilding == parent)
                {
                    return true;
                }
                if (allPawnsSpawned[i].CurJobDef == BlackHoleDefOf.BlackHoleEnter
                    && allPawnsSpawned[i].CurJob.targetA.Thing == parent)
                {
                    return true;
                }
            }
            // 2) 货单里还有能自己走进来的小人
            for (int i = 0; i < allPawnsSpawned.Count; i++)
            {
                Pawn pawn = allPawnsSpawned[i];
                if (pawn.mindState.duty?.focus.Thing == parent
                    && LeftToLoadContains(pawn)
                    && pawn.CanReach(parent, PathEndMode.Touch, Danger.Deadly))
                {
                    return true;
                }
            }
            // 3) 还有殖民者能搬（含 WorkGiver 帮忙的普通搬运工）
            for (int i = 0; i < allPawnsSpawned.Count; i++)
            {
                if (allPawnsSpawned[i].IsColonist && BlackHoleJobUtility.HasJobOnHole(allPawnsSpawned[i], this))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>某东西是否还在待装清单里（原版 CompTransporter.LeftToLoadContains）。</summary>
    public bool LeftToLoadContains(Thing thing)
    {
        if (leftToLoad == null)
        {
            return false;
        }
        for (int i = 0; i < leftToLoad.Count; i++)
        {
            for (int j = 0; j < leftToLoad[i].things.Count; j++)
            {
                if (leftToLoad[i].things[j] == thing)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>湮灭：到场的物品/小人直接销毁并扣减待装清单。</summary>
    public void Devour(Thing thing)
    {
        if (thing == null || thing.Destroyed)
        {
            return;
        }
        if (leftToLoad != null)
        {
            // 原版 SubtractFromToLoadList：Desperate 匹配 + min(stackCount, CountToTransfer) 封顶扣减
            TransferableOneWay transferable = TransferableUtility.TransferableMatchingDesperate(thing, leftToLoad, TransferAsOneMode.PodsOrCaravanPacking);
            if (transferable != null)
            {
                int delta = -Mathf.Min(thing.stackCount, transferable.CountToTransfer);
                if (delta != 0)
                {
                    transferable.AdjustBy(delta);
                }
                leftToLoad.RemoveAll(x => x.CountToTransfer <= 0);
            }
            // 原版 MessageFinishedLoadingTransporters：最后一件送达时立即发完成消息（TaskCompletion）
            if (loadingInProgress && !AnythingLeftToLoad)
            {
                EndLoading();
                Messages.Message("BlackHole.LoadingFinished".Translate(), parent, MessageTypeDefOf.TaskCompletion, historical: false);
            }
        }
        if (thing is Pawn pawn)
        {
            Messages.Message("BlackHole.DevouredPawn".Translate(pawn.LabelShortCap), parent, MessageTypeDefOf.NegativeEvent, historical: false);
        }
        thing.Destroy(DestroyMode.Vanish);
    }

    /// <summary>
    /// 开始装载（原版 TransporterUtility.MakeLordsAsAppropriate 语义）：
    /// 只有货单里的自由小人/机械体进 Lord——他们搬完货会自己走进黑洞。
    /// 只选物品时不建任何 Lord：原版靠 WorkGiver_LoadTransporters 让普通搬运工帮忙，
    /// 本 mod 对应 WorkGiver_BlackHole；无人能搬时由 CompTick 的卡死警告兜底。
    /// </summary>
    public void StartLoading(List<Pawn> cargoPawns)
    {
        List<Pawn> workers = cargoPawns.Where(x => (x.IsColonist || x.IsColonyMechPlayerControlled) && !x.Downed && x.Spawned
            && x.needs?.energy?.IsSelfShutdown != true).ToList();
        loadingInProgress = true;
        notifiedCantLoadMore = false;
        if (workers.Count == 0)
        {
            return;
        }
        Lord lord = FindLord();
        if (lord == null)
        {
            lord = LordMaker.MakeNewLord(Faction.OfPlayer, new LordJob_DevourToBlackHole(parent), parent.Map);
        }
        foreach (Pawn worker in workers)
        {
            if (!lord.ownedPawns.Contains(worker))
            {
                worker.GetLord()?.Notify_PawnLost(worker, PawnLostCondition.ForcedToJoinOtherLord);
                lord.AddPawn(worker);
                worker.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
        for (int num = lord.ownedPawns.Count - 1; num >= 0; num--)
        {
            if (!workers.Contains(lord.ownedPawns[num]))
            {
                lord.Notify_PawnLost(lord.ownedPawns[num], PawnLostCondition.NoLongerEnteringTransportPods);
            }
        }
    }

    public void EndLoading()
    {
        EndLoading(parent.Map);
    }

    private void EndLoading(Map map)
    {
        leftToLoad = null;
        loadingInProgress = false;
        Lord lord = FindLord(map);
        if (lord != null)
        {
            map.lordManager.RemoveLord(lord);
        }
    }

    /// <summary>
    /// 取消装载（原版 CompTransporter.CancelLoad 语义）：解散 Lord、清空清单、置标志位。
    /// 不主动中断在途 job——它们由 WasLoadingCanceled FailOn 自然终结，手中货物留在手里，
    /// 之后按常规搬运逻辑处理；Lord 里的殖民者置 UnloadEverything（原版 TryRemoveLord 同款）。
    /// </summary>
    public void CancelLoad()
    {
        CancelLoad(parent.Map);
    }

    public void CancelLoad(Map map)
    {
        if (!loadingInProgress || map == null)
        {
            return;
        }
        Lord lord = FindLord(map);
        if (lord != null)
        {
            foreach (Pawn ownedPawn in lord.ownedPawns)
            {
                if (ownedPawn.IsColonist && map.IsPlayerHome)
                {
                    ownedPawn.inventory.UnloadEverything = true;
                }
            }
        }
        EndLoading(map);
    }

    private Lord FindLord()
    {
        return FindLord(parent.Map);
    }

    private Lord FindLord(Map map)
    {
        if (map == null)
        {
            return null;
        }
        return map.lordManager.lords.FirstOrDefault(l => l.LordJob is LordJob_DevourToBlackHole lj && lj.Parent == parent);
    }

    public void AddToTheToLoadList(TransferableOneWay transferable, int count)
    {
        if (count <= 0)
        {
            return;
        }
        leftToLoad ??= new List<TransferableOneWay>();
        // 直接入列，绝不做 Desperate 合并：对话框已按 TransferAsOne（区分品质/耐久/原料）合并好条目；
        // TransferableMatchingDesperate 对物品会退化成按 def 模糊匹配——不同品质的同 def 物品会被并到
        // 第一条目上：数量加了但 things 没加 → 剩余计数永远减不掉 → 装载卡死。
        leftToLoad.Add(transferable);
        transferable.AdjustTo(count);
    }

    /// <summary>取消装载后，进行中的 HaulToBlackHole 工作以此判定失败中断。</summary>
    public static bool WasLoadingCanceled(Thing thing)
    {
        return !(thing.TryGetComp<CompBlackHole>()?.loadingInProgress ?? false);
    }

    // ---------------- Gizmo ----------------
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (parent.Map == null)
        {
            yield break; // 未 spawn（被打包等），无地图可开装载流程
        }
        if (loadingInProgress)
        {
            // 装载一旦开始不可修改清单，只能中止（语义明确且无容器状态可同步）
            yield return new Command_Action
            {
                defaultLabel = "BlackHole.CancelDevour".Translate(),
                defaultDesc = "BlackHole.CancelDevourDesc".Translate(),
                icon = CancelCommandTex,
                action = delegate
                {
                    SoundDefOf.Designate_Cancel.PlayOneShotOnCamera();
                    CancelLoad();
                }
            };
            yield break;
        }
        yield return new Command_Action
        {
            defaultLabel = "BlackHole.DevourGizmo".Translate(),
            defaultDesc = "BlackHole.DevourGizmoDesc".Translate(),
            icon = DevourCommandTex,
            action = delegate
            {
                Find.WindowStack.Add(new Dialog_DevourBlackHole(parent.Map, this));
            }
        };
    }

    // ---------------- 存档 ----------------
    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref leftToLoad, "leftToLoad", LookMode.Deep);
        Scribe_Values.Look(ref loadingInProgress, "loadingInProgress", defaultValue: false);
        Scribe_Values.Look(ref notifiedCantLoadMore, "notifiedCantLoadMore", defaultValue: false);
        if (Scribe.mode == LoadSaveMode.PostLoadInit && leftToLoad != null)
        {
            leftToLoad.RemoveAll(x => x == null);
        }
    }
}
