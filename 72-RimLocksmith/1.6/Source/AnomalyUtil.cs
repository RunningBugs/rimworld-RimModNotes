using Verse;

namespace RunningBugs.RimLocksmith;

public static class AnomalyUtil
{
    /// <summary>
    /// ANOMALY-CONTROL(disabled): 异象控制功能暂禁用,本工具类预留,当前无调用方。
    /// 异象实体判定,与派系无关:变异体(食尸鬼/蹒跚怪等 MutantDef)或
    /// 异象血肉种族(EntityFlesh/EntityMechanical/Fleshbeast)。
    /// 敌对性由调用方结合派系另行判断。
    /// </summary>
    public static bool IsAnomalous(Pawn pawn)
    {
        if (!ModsConfig.AnomalyActive || pawn == null)
        {
            return false;
        }
        return pawn.IsMutant || pawn.RaceProps.IsAnomalyEntity;
    }
}
