namespace RunningBugs.RimLocksmith.Core;

public static class LockPolicy
{
    /// <summary>仅宠物体型上限(与 Locks 一致)。</summary>
    public const float MaxPetBodySize = 0.86f;

    /// <summary>该类别是否参与配置;不可配置类别一律跟随原版,Mod 不加锁。</summary>
    public static bool IsConfigurable(AccessCategory category)
    {
        switch (category)
        {
            case AccessCategory.Colonist:
            case AccessCategory.Slave:
            case AccessCategory.ColonyAnimal:
            case AccessCategory.ColonyMechanoid:
            case AccessCategory.Guest:
            case AccessCategory.Trader:
                return true;
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
            case AccessCategory.Trader:
                return config.AllowTraders;
            case AccessCategory.ColonyAnimal:
                switch (config.AnimalAccess)
                {
                    case AnimalAccess.All:
                        return true;
                    case AnimalAccess.OnlyPets:
                        return pawn.BodySize <= MaxPetBodySize;
                    default:
                        return false;
                }
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
