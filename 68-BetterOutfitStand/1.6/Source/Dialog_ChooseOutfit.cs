using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;

namespace BetterOutfitStand;

public class Dialog_ChooseApparel : Window
{
    private readonly Pawn selPawn;
    private readonly Building_BetterOutfitStand outfitStand;
    private Vector2 pawnScrollPosition = Vector2.zero;
    private Vector2 standScrollPosition = Vector2.zero;
    private readonly List<Thing> selectedPawnThings = [];
    private readonly List<Thing> selectedStandThings = [];
    private bool defaultSelectionInitialized;

    public Dialog_ChooseApparel(Pawn selPawn, Building_BetterOutfitStand outfitStand)
    {
        this.selPawn = selPawn;
        this.outfitStand = outfitStand;
        resizeable = true;
        forcePause = true;
        closeOnClickedOutside = true;
    }

    public override Vector2 InitialSize => new Vector2(760f, 420f);

    public override void DoWindowContents(Rect inRect)
    {
        var cachedFont = Text.Font;
        var cachedWrap = Text.WordWrap;
        Text.Font = GameFont.Small;

        try
        {
            Rect titleRect = inRect.SplitHoriPartPixels(32f, out Rect bodyAndButtons);
            Rect bodyRect = bodyAndButtons.SplitHoriPartPixels(bodyAndButtons.height - 34f - GenUI.ElementStackDefaultElementMargin, out Rect buttonRect);
            buttonRect.SplitHoriPartPixels(GenUI.ElementStackDefaultElementMargin, out buttonRect);

            Widgets.Label(titleRect, "BetterOutfitStand".Translate());

            List<Thing> pawnThings = [.. selPawn.apparel.WornApparel.Cast<Thing>()];
            List<Thing> standThings = [.. outfitStand.HeldItems.Where(t => t is Apparel)];
            InitializeDefaultSelectionOnce(pawnThings, standThings);

            float gap = 12f;
            float columnWidth = (bodyRect.width - gap) / 2f;
            Rect pawnRect = new Rect(bodyRect.x, bodyRect.y, columnWidth, bodyRect.height);
            Rect standRect = new Rect(pawnRect.xMax + gap, bodyRect.y, columnWidth, bodyRect.height);

            DoColumn(pawnRect, "PawnViewTitle".Translate(), "PawnViewDesc".Translate(), pawnThings, selectedPawnThings, ref pawnScrollPosition);
            DoColumn(standRect, "OutfitStandViewTitle".Translate(), "OutfitStandViewDesc".Translate(), standThings, selectedStandThings, ref standScrollPosition);

            if (Widgets.ButtonText(new Rect(buttonRect.x, buttonRect.y, 120f, 30f), "Confirm".Translate()))
            {
                outfitStand.WornApparelToTransferToStand = [.. selectedPawnThings.OfType<Apparel>()];
                outfitStand.StandApparelToTransferToPawn = [.. selectedStandThings.OfType<Apparel>()];
                outfitStand.SetAllowHauling(false);
                selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefOfs.UseOutfitStand_Better, outfitStand), JobTag.Misc);
                Close();
            }

            if (Widgets.ButtonText(new Rect(buttonRect.xMax - 120f, buttonRect.y, 120f, 30f), "Cancel".Translate()))
            {
                Close();
            }
        }
        finally
        {
            Text.WordWrap = cachedWrap;
            Text.Font = cachedFont;
        }
    }

    private void InitializeDefaultSelectionOnce(List<Thing> pawnThings, List<Thing> standThings)
    {
        if (defaultSelectionInitialized)
        {
            return;
        }
        defaultSelectionInitialized = true;
        if (!BOSMod.Settings.SelectAllByDefault)
        {
            return;
        }
        selectedPawnThings.AddRange(pawnThings);
        selectedStandThings.AddRange(standThings);
    }

    private void DoColumn(Rect rect, TaggedString title, TaggedString description, List<Thing> data, List<Thing> selectedThings, ref Vector2 scrollPosition)
    {
        Widgets.DrawMenuSection(rect);
        Rect innerOuter = rect.ContractedBy(6f);
        Rect titleRect = innerOuter.SplitHoriPartPixels(26f, out Rect rest);
        Widgets.Label(titleRect, title);
        Rect descRect = rest.SplitHoriPartPixels(42f, out Rect scrollableRect);
        Text.Font = GameFont.Tiny;
        Widgets.Label(descRect, description);
        Text.Font = GameFont.Small;

        var innerRect = new Rect(0f, 0f, scrollableRect.width - GenUI.ScrollBarWidth, data.Count * 30f);
        Widgets.BeginScrollView(scrollableRect, ref scrollPosition, innerRect);

        Listing_Standard listing = new();
        listing.Begin(innerRect);
        foreach (var apparel in data)
        {
            Rect rowRect = listing.GetRect(30f);
            DoRow(rowRect, apparel, selectedThings);
        }
        listing.End();
        Widgets.EndScrollView();
    }

    public virtual void DoRow(Rect rect, Thing apparel, List<Thing> selectedThings)
    {
        bool isSelected = selectedThings.Contains(apparel);
        bool modified = isSelected;
        var thingIconRect = new Rect(4f, rect.y, 28f, 28f);
        var labelRect = new Rect(34f, rect.y + 1f, rect.width - 34f - 30f, 28f);
        var checkboxRect = new Rect(rect.width - 29f, rect.y + 1f, 28f, 28f);
        var tooltipRect = new Rect(0f, rect.y, rect.width - 30f, 30f);

        Widgets.ThingIcon(thingIconRect, apparel);
        Text.WordWrap = false;
        Widgets.Label(labelRect, apparel.LabelCap);
        Text.WordWrap = true;
        Widgets.Checkbox(new Vector2(checkboxRect.x, checkboxRect.y), ref modified);
        if (Mouse.IsOver(tooltipRect))
        {
            GUI.DrawTexture(tooltipRect, TexUI.HighlightTex);
            TooltipHandler.TipRegion(tooltipRect, apparel.GetTooltip());
        }

        if (modified == isSelected)
        {
            return;
        }
        if (modified)
        {
            selectedThings.Add(apparel);
        }
        else
        {
            selectedThings.Remove(apparel);
        }
    }
}
