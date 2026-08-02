namespace RunningBugs.RimLocksmith.Core;

/// <summary>
/// 访问类别。Locks 式分类:先按派系敌对性分流,再按生物类型与身份细分。
/// 仅前 6 个类别可配置;Prisoner/Hostile/WildAnimal/Other 一律跟随原版。
/// </summary>
public enum AccessCategory
{
    Colonist,
    Slave,
    ColonyAnimal,
    ColonyMechanoid,
    Guest,
    Trader,
    Prisoner,
    Hostile,
    WildAnimal,
    Other
}
