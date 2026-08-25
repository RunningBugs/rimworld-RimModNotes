using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace BlackHole;

/// <summary>
/// 黑洞装载窗口：结构取自 RimWorld.Dialog_LoadTransporters，按需裁剪并对接自研 CompBlackHole。
/// 已裁剪：远行队弹窗/统计、质量代码、穿梭机分支、CompTransporter 依赖。
/// </summary>
public class Dialog_DevourBlackHole : Window
{
    private enum Tab
    {
        Pawns,
        Items
    }

    private readonly Map map;
    private readonly CompBlackHole hole;
    private List<TransferableOneWay> transferables;
    private TransferableOneWayWidget pawnsTransfer;
    private TransferableOneWayWidget itemsTransfer;
    private Tab tab;

    private const float TitleRectHeight = 35f;
    private readonly Vector2 BottomButtonSize = new Vector2(160f, 40f);

    private static readonly List<TabRecord> TabsList = new List<TabRecord>();

    public override Vector2 InitialSize => new Vector2(1024f, UI.screenHeight);

    protected override float Margin => 0f;

    public Dialog_DevourBlackHole(Map map, CompBlackHole hole)
    {
        this.map = map;
        this.hole = hole;
        forcePause = true;
        absorbInputAroundWindow = true;
    }

    public override void PostOpen()
    {
        base.PostOpen();
        CalculateAndRecacheTransferables();
    }

    public override void DoWindowContents(Rect inRect)
    {
        Rect rect = new Rect(0f, 0f, inRect.width, TitleRectHeight);
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, "BlackHole.DevourDialogTitle".Translate());
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        TabsList.Clear();
        TabsList.Add(new TabRecord("PawnsTab".Translate(), delegate { tab = Tab.Pawns; }, tab == Tab.Pawns));
        TabsList.Add(new TabRecord("ItemsTab".Translate(), delegate { tab = Tab.Items; }, tab == Tab.Items));
        inRect.yMin += 67f;
        Widgets.DrawMenuSection(inRect);
        TabDrawer.DrawTabs(inRect, TabsList);
        inRect = inRect.ContractedBy(17f);
        inRect.height += 17f;
        Widgets.BeginGroup(inRect);
        Rect rect2 = inRect.AtZero();
        DoBottomButtons(rect2);
        Rect inRect2 = rect2;
        inRect2.yMax -= 76f;
        bool anythingChanged = false;
        switch (tab)
        {
            case Tab.Pawns:
                pawnsTransfer.OnGUI(inRect2, out anythingChanged);
                break;
            case Tab.Items:
                itemsTransfer.OnGUI(inRect2, out anythingChanged);
                break;
        }
        if (anythingChanged)
        {
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        }
        Widgets.EndGroup();
    }

    public override bool CausesMessageBackground()
    {
        return true;
    }

    private void AddToTransferables(Thing t)
    {
        TransferableOneWay transferableOneWay = TransferableUtility.TransferableMatching(t, transferables, TransferAsOneMode.PodsOrCaravanPacking);
        if (transferableOneWay == null)
        {
            transferableOneWay = new TransferableOneWay();
            transferables.Add(transferableOneWay);
        }
        transferableOneWay.things.Add(t);
    }

    private void DoBottomButtons(Rect rect)
    {
        if (Widgets.ButtonText(new Rect(rect.width / 2f - BottomButtonSize.x / 2f, rect.height - 55f - 17f, BottomButtonSize.x, BottomButtonSize.y), "ResetButton".Translate()))
        {
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            CalculateAndRecacheTransferables();
        }
        if (Widgets.ButtonText(new Rect(0f, rect.height - 55f - 17f, BottomButtonSize.x, BottomButtonSize.y), "CancelButton".Translate()))
        {
            Close();
        }
        Rect rect2 = new Rect(rect.width - BottomButtonSize.x, rect.height - 55f - 17f, BottomButtonSize.x, BottomButtonSize.y);
        if (Widgets.ButtonText(rect2, "AcceptButton".Translate()))
        {
            onAcceptButton();
        }
    }

    private void onAcceptButton()
    {
        if (TryAccept())
        {
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            Close(doCloseSound: false);
        }
    }

    private void CalculateAndRecacheTransferables()
    {
        transferables = new List<TransferableOneWay>();
        foreach (Pawn item in CaravanFormingUtility.AllSendablePawns(map, allowEvenIfDowned: true))
        {
            AddToTransferables(item);
        }
        foreach (Thing item2 in CaravanFormingUtility.AllReachableColonyItems(map, !map.IsPlayerHome))
        {
            AddToTransferables(item2);
        }
        pawnsTransfer = new TransferableOneWayWidget(null, null, null, "TransporterColonyThingCountTip".Translate());
        CaravanUIUtility.AddPawnsSections(pawnsTransfer, transferables);
        itemsTransfer = new TransferableOneWayWidget(transferables.Where((TransferableOneWay x) => x.ThingDef.category != ThingCategory.Pawn), null, null, "TransporterColonyThingCountTip".Translate(), drawItemNutrition: true);
    }

    private bool TryAccept()
    {
        // 原版 Dialog_LoadTransporters.CheckForErrors：空选拒绝（黑洞没有"空舱出发"概念）
        if (!transferables.Any(x => x.CountToTransfer != 0))
        {
            Messages.Message("BlackHole.CantDevourEmpty".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }
        List<Pawn> pawnsFromTransferables = TransferableUtility.GetPawnsFromTransferables(transferables);
        // 原版 CheckForErrors：选中的小人必须能到达黑洞，否则接受后必然卡装载
        Pawn pawn = pawnsFromTransferables.Find(x => !x.MapHeld.reachability.CanReach(x.PositionHeld, hole.parent, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors)));
        if (pawn != null)
        {
            Messages.Message("BlackHole.PawnCantReachBlackHole".Translate(pawn.LabelShort, pawn), hole.parent, MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }
        // 先建清单再开工（原版 TryAccept：InitiateLoading → AssignTransferables → MakeLordsAsAppropriate）
        AssignTransferablesToHole();
        hole.StartLoading(pawnsFromTransferables);
        Messages.Message("BlackHole.LoadingStarted".Translate(), hole.parent, MessageTypeDefOf.TaskCompletion, historical: false);
        return true;
    }

    private void AssignTransferablesToHole()
    {
        hole.leftToLoad = new List<TransferableOneWay>();
        foreach (TransferableOneWay transferable in transferables)
        {
            if (transferable.CountToTransfer > 0)
            {
                hole.AddToTheToLoadList(transferable, transferable.CountToTransfer);
            }
        }
    }

    public override void OnAcceptKeyPressed()
    {
        onAcceptButton();
    }
}
