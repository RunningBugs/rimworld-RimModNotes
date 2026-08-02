using RimWorld;
using RunningBugs.RimLocksmith.Core;
using Verse;

namespace RunningBugs.RimLocksmith;

public static class PawnAccessFactsFactory
{
    /// <summary>
    /// Locks 式分类:先按派系敌对性分流(敌对/无派系不可配置,跟随原版),
    /// 再按生物类型与身份细分。商队/访客的动物归入 Guest,不再误当野生动物。
    /// </summary>
    public static PawnAccessFacts FromPawn(Pawn pawn)
    {
        if (pawn == null)
        {
            return new PawnAccessFacts(AccessCategory.Other);
        }

        PawnAccessFacts Make(AccessCategory category)
        {
            float bodySize = pawn.RaceProps?.baseBodySize ?? 0f;
            bool hasOverseer = ModsConfig.BiotechActive && pawn.GetOverseer() != null;
            return new PawnAccessFacts(category, bodySize, hasOverseer);
        }

        if (pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer))
        {
            return Make(AccessCategory.Hostile);
        }
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
        // 非敌对外来派系:动物跟随访客开关(商队驮兽不再误归野生动物)。
        if (pawn.RaceProps.Animal)
        {
            return Make(AccessCategory.Guest);
        }
        if (pawn.TraderKind != null)
        {
            return Make(AccessCategory.Trader);
        }
        return Make(AccessCategory.Guest);
    }
}
