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
    private Vector2 scrollPosition = Vector2.zero;
    private bool pawnMode = false;
    private List<Thing> selectedThings = [];

    public Dialog_ChooseApparel(Pawn selPawn, Building_BetterOutfitStand outfitStand)
    {
        this.selPawn = selPawn;
        this.outfitStand = outfitStand;
        this.resizeable = true;
        this.forcePause = true;
        this.closeOnClickedOutside = true;

        pawnMode = outfitStand.HeldItems.Count == 0;
    }

    public override Vector2 InitialSize => new Vector2(400f, 300f);

    public override void DoWindowContents(Rect inRect)
    {
        // Implement the UI for choosing outfits here
        var cachedFont = Text.Font;
        Text.Font = GameFont.Small;
        /// The UI mainly has three parts:
        /// 1. The Window title
        /// 2. The scrollable list of apparels
        /// 3. The Confirm/Cancel buttons

        var titleText = pawnMode ? "PawnViewTitle".Translate() : "OutfitStandViewTitle".Translate();
        var titleHeight = Text.CalcHeight(titleText, inRect.width - 30f);
        var titleRect = inRect.SplitHoriPartPixels(titleHeight + GenUI.ElementStackDefaultElementMargin, out Rect bottom);
        titleRect = titleRect.SplitVertPartPixels(titleRect.width - 30f, out Rect toggleRect);
        var scrollableRect = bottom.SplitHoriPartPixels(bottom.height - 30f - GenUI.ElementStackDefaultElementMargin, out Rect bottomRect);
        bottomRect.SplitHoriPartPixels(GenUI.ElementStackDefaultElementMargin, out bottomRect);


        Widgets.Label(titleRect, titleText);
        bool oldPawnMode = pawnMode;
        Widgets.Checkbox(new Vector2(toggleRect.x + 1, toggleRect.y + 1), ref pawnMode);
        if (oldPawnMode != pawnMode)
        {
            selectedThings.Clear();
        }
        if (Mouse.IsOver(toggleRect))
        {
            GUI.DrawTexture(toggleRect, TexUI.HighlightTex);
            TooltipHandler.TipRegion(toggleRect, pawnMode ? "SwitchToOutfitStandView.Tooltip".Translate() : "SwitchToPawnView.Tooltip".Translate());
        }
        // Add buttons or other UI elements to select outfits
        List<Thing> data = pawnMode ? [.. selPawn.apparel.WornApparel.Cast<Thing>()] : [.. outfitStand.HeldItems];
        var count = data.Count;
        var innerRect = new Rect(0, 0, scrollableRect.width - GenUI.ScrollBarWidth, count * 30f);
        Widgets.BeginScrollView(scrollableRect, ref scrollPosition, innerRect);

        Listing_Standard listing = new();
        listing.Begin(innerRect);
        foreach (var apparel in data)
        {
            Rect rowRect = listing.GetRect(30f);
            DoRow(rowRect, apparel);
        }
        listing.End();
        Widgets.EndScrollView();


        string selectedApparels = string.Join(", ", selectedThings.Select(a => a.LabelCap));
        if (Widgets.ButtonText(new Rect(bottomRect.x, bottomRect.y, 100, 30f), "Confirm".Translate()))
        {
            if (pawnMode)
            {
                outfitStand.WornApparelToTransferToStand = [.. selectedThings.OfType<Apparel>()];
            }
            else
            {
                outfitStand.StandApparelToTransferToPawn = [.. selectedThings.OfType<Apparel>()];
            }

            outfitStand.SetAllowHauling(false);
            selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefOfs.UseOutfitStand_Better, outfitStand), JobTag.Misc);
            Close();
        }

        if (Widgets.ButtonText(new Rect(bottomRect.xMax - 100f, bottomRect.y, 100f, 30f), "Cancel".Translate()))
        {
            Close();
        }
        Text.Font = cachedFont;
    }

    public virtual void DoRow(Rect rect, Thing apparel)
    {
        bool isSelected = selectedThings.Contains(apparel);
        bool modified = isSelected;
        // Implement the UI for each row of outfit options
        // Widgets.CheckboxLabeled(rect, apparel.LabelCap, ref modified);
        // The row has a thingIcon + label region, a select checkbox region.
        var thingIconRect = new Rect(4, rect.y, 28f, 28f);
        var labelRect = new Rect(34f, rect.y + 1, rect.width - 34f - 30f, 28f);
        var checkboxRect = new Rect(rect.width - 29f, rect.y + 1, 28f, 28f);
        var tooltipRect = new Rect(0, rect.y, rect.width - 30f, 30f);

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

        if (modified != isSelected)
        {
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
}
