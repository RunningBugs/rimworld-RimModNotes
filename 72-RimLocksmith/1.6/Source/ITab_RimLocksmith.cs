using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RunningBugs.RimLocksmith.Core;
using UnityEngine;
using Verse;

namespace RunningBugs.RimLocksmith;

public sealed class ITab_RimLocksmith : ITab
{
    private Vector2 scroll;

    public ITab_RimLocksmith()
    {
        size = new Vector2(480f, 520f);
        labelKey = "RimLocksmith.TabLabel";
    }

    public override bool IsVisible => RimLocksmithUtility.SelectionIsOnlyDoorsWithAtLeastOneColonyDoor();

    protected override void FillTab()
    {
        List<Building_Door> allDoors = RimLocksmithUtility.SelectedDoors();
        List<Building_Door> targets = RimLocksmithUtility.SelectedConfigurableColonyDoors();
        int ignored = allDoors.Count - targets.Count;

        Rect outRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
        Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, 760f);
        Widgets.BeginScrollView(outRect, ref scroll, viewRect);
        Listing_Standard listing = new Listing_Standard();
        listing.Begin(viewRect);
        listing.Label("RimLocksmith.SelectionSummary".Translate(targets.Count, ignored));
        listing.GapLine();
        if (targets.Count == 0)
        {
            listing.Label("RimLocksmith.NoConfigurableDoors".Translate());
            listing.End();
            Widgets.EndScrollView();
            return;
        }

        DrawBulkToggle(listing, targets, "RimLocksmith.AllowColonists", c => c.AllowColonists, (c, v) => c.AllowColonists = v);
        DrawBulkToggle(listing, targets, "RimLocksmith.AllowSlaves", c => c.AllowSlaves, (c, v) => c.AllowSlaves = v);
        DrawBulkToggle(listing, targets, "RimLocksmith.AllowPrisoners", c => c.AllowPrisoners, (c, v) => c.AllowPrisoners = v);
        DrawBulkToggle(listing, targets, "RimLocksmith.AllowColonyAnimals", c => c.AllowColonyAnimals, (c, v) => c.AllowColonyAnimals = v);
        DrawBulkToggle(listing, targets, "RimLocksmith.AllowColonyMechanoids", c => c.AllowColonyMechanoids, (c, v) => c.AllowColonyMechanoids = v);
        DrawBulkToggle(listing, targets, "RimLocksmith.AllowGuests", c => c.AllowGuests, (c, v) => c.AllowGuests = v);
        DrawBulkToggle(listing, targets, "RimLocksmith.AllowAllies", c => c.AllowAllies, (c, v) => c.AllowAllies = v);
        DrawBulkToggle(listing, targets, "RimLocksmith.AllowTraders", c => c.AllowTraders, (c, v) => c.AllowTraders = v);
        DrawBulkToggle(listing, targets, "RimLocksmith.AllowHostiles", c => c.AllowHostiles, (c, v) => c.AllowHostiles = v);
        DrawBulkToggle(listing, targets, "RimLocksmith.AllowWildAnimals", c => c.AllowWildAnimals, (c, v) => c.AllowWildAnimals = v);
        DrawBulkToggle(listing, targets, "RimLocksmith.AllowOthers", c => c.AllowOthers, (c, v) => c.AllowOthers = v);
        listing.GapLine();
        if (listing.ButtonText("RimLocksmith.ResetSelectedToDefault".Translate()))
        {
            foreach (Building_Door door in targets)
            {
                door.TryGetComp<CompRimLocksmithDoor>()?.SetConfig(RimLocksmithMod.Settings.DefaultConfig, userConfigured: false);
            }
        }
        listing.End();
        Widgets.EndScrollView();
    }

    private static void DrawBulkToggle(Listing_Standard listing, List<Building_Door> doors, string key, System.Func<LockConfigData, bool> get, System.Action<LockConfigData, bool> set)
    {
        bool first = get(doors[0].TryGetComp<CompRimLocksmithDoor>().Config);
        bool mixed = doors.Any(d => get(d.TryGetComp<CompRimLocksmithDoor>().Config) != first);
        string label = key.Translate().ToString() + (mixed ? " (" + "RimLocksmith.Mixed".Translate().ToString() + ")" : string.Empty);
        bool value = !mixed && first;
        bool before = value;
        listing.CheckboxLabeled(label, ref value, "RimLocksmith.AllowOpenTooltip".Translate());
        if (value != before || mixed && value)
        {
            foreach (Building_Door door in doors)
            {
                CompRimLocksmithDoor comp = door.TryGetComp<CompRimLocksmithDoor>();
                LockConfigData cfg = comp.Config;
                set(cfg, value);
                cfg.UserConfigured = true;
                comp.NotifyChanged();
            }
        }
    }
}
