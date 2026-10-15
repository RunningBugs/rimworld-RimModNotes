namespace RunningBugs.RimLocksmith.Core;

// 殖民地动物/机械体档位;受设置项「管理殖民地单位」门控。

/// <summary>殖民地动物通行档位。</summary>
public enum AnimalAccess
{
    None,
    All
}

/// <summary>殖民地机械体通行档位。仅受控 = 有机械师控制者(Overseer)的机械体。</summary>
public enum MechAccess
{
    None,
    OnlyOverseen,
    All
}
