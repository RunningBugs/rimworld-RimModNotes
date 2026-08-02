namespace RunningBugs.RimLocksmith.Core;

/// <summary>殖民地动物通行档位。仅宠物参考 Locks:体型 ≤ 0.86。</summary>
public enum AnimalAccess
{
    None,
    OnlyPets,
    All
}

/// <summary>殖民地机械体通行档位。仅受控 = 有机械师控制者(Overseer)的机械体。</summary>
public enum MechAccess
{
    None,
    OnlyOverseen,
    All
}
