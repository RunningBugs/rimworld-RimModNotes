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

    /// <summary>
    /// postfix 只收窄语义:该门配置是否要把这个(原版已允许开门的)pawn 拦下。
    /// 敌人/野生动物/囚犯/其他不可配置类别与原版的特殊放行
    /// (CanOpenAnyDoor:越狱/叛乱/商队/远行队/野人)一律不动。
    /// </summary>
    public static bool ShouldDeny(Building_Door door, Pawn pawn)
    {
        if (!IsColonyDoor(door) || pawn == null)
        {
            return false;
        }
        CompRimLocksmithDoor comp = GetComp(door);
        if (comp == null)
        {
            return false;
        }
        if (pawn.CanOpenAnyDoor)
        {
            return false;
        }
        LockConfigData config = comp.EnsureConfig();
        PawnAccessFacts facts = PawnAccessFactsFactory.FromPawn(pawn);
        if (!LockPolicy.IsConfigurable(facts.Category))
        {
            return false;
        }
        return !LockPolicy.Allows(config, facts);
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
