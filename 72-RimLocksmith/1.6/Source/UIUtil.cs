using RunningBugs.RimLocksmith.Core;
using Verse;

namespace RunningBugs.RimLocksmith;

public static class UIUtil
{
    public static void DrawConfigToggles(Listing_Standard listing, LockConfigData config, bool markConfigured)
    {
        DrawToggle(listing, "RimLocksmith.AllowColonists", ref config.AllowColonists);
        DrawToggle(listing, "RimLocksmith.AllowSlaves", ref config.AllowSlaves);
        DrawToggle(listing, "RimLocksmith.AllowPrisoners", ref config.AllowPrisoners);
        DrawToggle(listing, "RimLocksmith.AllowColonyAnimals", ref config.AllowColonyAnimals);
        DrawToggle(listing, "RimLocksmith.AllowColonyMechanoids", ref config.AllowColonyMechanoids);
        DrawToggle(listing, "RimLocksmith.AllowGuests", ref config.AllowGuests);
        DrawToggle(listing, "RimLocksmith.AllowAllies", ref config.AllowAllies);
        DrawToggle(listing, "RimLocksmith.AllowTraders", ref config.AllowTraders);
        DrawToggle(listing, "RimLocksmith.AllowHostiles", ref config.AllowHostiles);
        DrawToggle(listing, "RimLocksmith.AllowWildAnimals", ref config.AllowWildAnimals);
        DrawToggle(listing, "RimLocksmith.AllowOthers", ref config.AllowOthers);
        if (markConfigured) config.UserConfigured = true;
    }

    private static void DrawToggle(Listing_Standard listing, string key, ref bool value)
    {
        listing.CheckboxLabeled(key.Translate(), ref value, "RimLocksmith.AllowOpenTooltip".Translate());
    }
}
