namespace RunningBugs.RimLocksmith.Core;

public static class LockPolicy
{
    /// <summary>
    /// 该类别是否参与配置;不可配置类别一律跟随原版,Mod 不加锁。
    /// 玩家派系四类(Colonist/Slave/ColonyAnimal/ColonyMechanoid)的可配置性
    /// 另受设置项「管理殖民地单位」门控:设置关闭时由 ShouldDeny 直接豁免,
    /// 不会走到本方法。
    /// </summary>
    public static bool IsConfigurable(AccessCategory category)
    {
        switch (category)
        {
            case AccessCategory.Colonist:
            case AccessCategory.Slave:
            case AccessCategory.ColonyAnimal:
            case AccessCategory.ColonyMechanoid:
            case AccessCategory.Guest:
                return true;
            // ANOMALY-CONTROL(disabled): 异象控制暂禁用。
            // case AccessCategory.Anomaly:
            default:
                return false;
        }
    }

    /// <summary>配置是否允许该 pawn 开门。只用于"在原版允许的基础上收窄"。</summary>
    public static bool Allows(LockConfigData config, PawnAccessFacts pawn)
    {
        switch (pawn.Category)
        {
            case AccessCategory.Colonist:
                return config.AllowColonists;
            case AccessCategory.Slave:
                return config.AllowSlaves;
            case AccessCategory.Guest:
                return config.AllowGuests;
            // ANOMALY-CONTROL(disabled)
            // case AccessCategory.Anomaly:
            //     return config.AllowAnomalies;
            case AccessCategory.ColonyAnimal:
                return config.AnimalAccess == AnimalAccess.All;
            case AccessCategory.ColonyMechanoid:
                switch (config.MechAccess)
                {
                    case MechAccess.All:
                        return true;
                    case MechAccess.OnlyOverseen:
                        return pawn.HasOverseer;
                    default:
                        return false;
                }
            default:
                return true;
        }
    }
}
