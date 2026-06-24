using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RunningBugs.RimLocksmith.Core;
using Verse;

namespace RunningBugs.RimLocksmith;

public static class RimLocksmithUtility
{
    public static bool IsColonyDoor(Building_Door door) => door != null && !door.Destroyed && door.Faction == Faction.OfPlayer;

    public static bool IsSupportedDoor(object obj) => obj is Building_Door door && door.TryGetComp<CompRimLocksmithDoor>() != null;

    public static CompRimLocksmithDoor GetComp(Building_Door door) => door?.TryGetComp<CompRimLocksmithDoor>();

    public static bool TryAllowsOpen(Building_Door door, Pawn pawn, out bool allowed)
    {
        allowed = false;
        // Do not read the computed free-passage property here. It can call WillCloseSoon,
        // which calls PawnCanOpen, recursively re-entering this prefix and overflowing
        // the stack while doors tick. Open is a direct openInt-backed property.
        if (!IsColonyDoor(door) || pawn == null || door.Open)
        {
            return false;
        }
        CompRimLocksmithDoor comp = GetComp(door);
        if (comp == null)
        {
            return false;
        }
        LockConfigData config = comp.EnsureConfig();
        PawnAccessFacts pawnFacts = PawnAccessFactsFactory.FromPawn(pawn);
        DoorAccessFacts doorFacts = new DoorAccessFacts(isColonyDoor: true, isOpenOrFreePassage: false, roamerCanOpen: door.def?.building?.roamerCanOpen ?? false);
        allowed = LockPolicy.AllowsOpeningClosedDoor(pawnFacts, doorFacts, config);
        return true;
    }

    public static List<Building_Door> SelectedDoors()
    {
        return Find.Selector.SelectedObjects.OfType<Building_Door>().ToList();
    }

    public static List<Building_Door> SelectedConfigurableColonyDoors()
    {
        return SelectedDoors().Where(d => IsColonyDoor(d) && GetComp(d) != null).ToList();
    }

    public static bool SelectionIsOnlyDoorsWithAtLeastOneColonyDoor()
    {
        List<object> selected = Find.Selector.SelectedObjects;
        if (selected == null || selected.Count == 0) return false;
        if (selected.Any(o => !(o is Building_Door))) return false;
        return selected.OfType<Building_Door>().Any(d => IsColonyDoor(d) && GetComp(d) != null);
    }

    public static int ApplyDefaultToColonyDoors(bool overwriteConfigured)
    {
        if (Find.CurrentMap == null || RimLocksmithMod.Settings == null) return 0;
        int changed = 0;
        foreach (Building_Door door in Find.CurrentMap.listerBuildings.AllBuildingsColonistOfClass<Building_Door>())
        {
            if (!IsColonyDoor(door)) continue;
            CompRimLocksmithDoor comp = GetComp(door);
            if (comp == null) continue;
            if (!overwriteConfigured && comp.HasConfig && comp.Config.UserConfigured) continue;
            comp.SetConfig(RimLocksmithMod.Settings.DefaultConfig, userConfigured: false);
            changed++;
        }
        return changed;
    }
}
