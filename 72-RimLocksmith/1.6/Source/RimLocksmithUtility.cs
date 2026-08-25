using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RunningBugs.RimLocksmith.Core;
using Verse;
using Verse.AI.Group;

namespace RunningBugs.RimLocksmith;

public static class RimLocksmithUtility
{
    public static bool IsColonyDoor(Building_Door door) => door != null && !door.Destroyed && door.Faction == Faction.OfPlayer;

    public static bool IsSupportedDoor(object obj) => obj is Building_Door door && door.TryGetComp<CompRimLocksmithDoor>() != null;

    public static CompRimLocksmithDoor GetComp(Building_Door door) => door?.TryGetComp<CompRimLocksmithDoor>();

    /// <summary>
    /// postfix 只收窄语义:该门配置是否要把这个(原版已允许开门的)pawn 拦下。
    /// 可配置类别:访客(非敌对外来单位:访客/商队的人、动物、机械体),
    /// 以及玩家派系四类(殖民者/奴隶/殖民地动物/殖民地机械体)——后者受设置项
    /// 「管理殖民地单位」门控,设置关闭时玩家派系单位(含精神崩溃者)免管,
    /// 跟随原版(原版已有禁用门/区域管制/畜栏/机械师机制)。
    /// 敌人/野生动物/囚犯/其他原版本来就拒绝开门,跟随原版;
    /// 原版的特殊放行(CanOpenAnyDoor:越狱/叛乱/远行队/野人;guest.Released:释放的囚犯离场)一律不动。
    /// 例外:商队(LordJob_TradeWithColony)驮兽的 CanOpenAnyDoor 特权不再豁免,
    /// 与其他商队成员一样由访客开关控制。
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
        if (pawn.CanOpenAnyDoor && LordUtility.GetLord(pawn)?.LordJob is not LordJob_TradeWithColony)
        {
            return false;
        }
        // 原版明示放行:释放的囚犯(Building_Door.PawnCanOpen: guest.Released → true),
        // 不拦,避免禁用访客后释放的囚犯被门困在殖民地出不去。
        if (pawn.guest != null && pawn.guest.Released)
        {
            return false;
        }
        // 设置项「管理殖民地单位」关闭时,玩家派系单位(含精神崩溃者)免管,
        // 一律跟随原版(原版已有禁用门/区域管制/畜栏/机械师机制)。
        if (!(RimLocksmithMod.Settings?.ManageColonyPawns ?? true) && pawn.Faction == Faction.OfPlayer)
        {
            return false;
        }
        // ANOMALY-CONTROL(disabled): 设置项关闭时异象实体一律跟随原版。暂不考虑控制异象。
        // if (!(RimLocksmithMod.Settings?.ControlAnomalies ?? false) && AnomalyUtil.IsAnomalous(pawn))
        // {
        //     return false;
        // }
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
