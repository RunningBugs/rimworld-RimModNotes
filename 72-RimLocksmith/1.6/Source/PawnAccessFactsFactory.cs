using RimWorld;
using RunningBugs.RimLocksmith.Core;
using Verse;

namespace RunningBugs.RimLocksmith;

public static class PawnAccessFactsFactory
{
    /// <summary>
    /// Locks 式分类:先按派系敌对性分流(敌对/无派系不可配置,跟随原版),
    /// 再按生物类型与身份细分。商队成员(含驮兽)与访客同属 Guest。
    /// </summary>
    public static PawnAccessFacts FromPawn(Pawn pawn)
    {
        if (pawn == null)
        {
            return new PawnAccessFacts(AccessCategory.Other);
        }

        PawnAccessFacts Make(AccessCategory category)
        {
            bool hasOverseer = ModsConfig.BiotechActive && pawn.GetOverseer() != null;
            return new PawnAccessFacts(category, hasOverseer);
        }

        if (pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer))
        {
            return Make(AccessCategory.Hostile);
        }
        // ANOMALY-CONTROL(disabled): 非敌对异象实体单列一类。暂不考虑控制异象;
        // 禁用期间异象实体与普通 pawn 同等分类(玩家派系食尸鬼归入殖民者)。
        // if (AnomalyUtil.IsAnomalous(pawn))
        // {
        //     return Make(pawn.Faction == null ? AccessCategory.Other : AccessCategory.Anomaly);
        // }
        if (pawn.Faction == Faction.OfPlayer)
        {
            if (pawn.RaceProps.Animal)
            {
                return Make(AccessCategory.ColonyAnimal);
            }
            if (ModsConfig.BiotechActive && pawn.IsColonyMech)
            {
                return Make(AccessCategory.ColonyMechanoid);
            }
            if (pawn.IsSlaveOfColony)
            {
                return Make(AccessCategory.Slave);
            }
            return Make(AccessCategory.Colonist);
        }
        if (pawn.IsPrisonerOfColony)
        {
            return Make(AccessCategory.Prisoner);
        }
        if (pawn.Faction == null)
        {
            return Make(pawn.RaceProps?.Animal == true ? AccessCategory.WildAnimal : AccessCategory.Other);
        }
        // 非敌对外来派系:商队(含商人/护卫/驮兽)与访客同属 Guest,动物不再误当野生动物。
        return Make(AccessCategory.Guest);
    }
}
