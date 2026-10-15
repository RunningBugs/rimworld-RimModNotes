namespace RunningBugs.RimLocksmith.Core;

/// <summary>
/// 访问类别。分类只回答"这个 pawn 是谁",可配置性由 LockPolicy 决定。
/// 唯一可配置类别是 Guest(非敌对外来单位:访客/商队的人、动物、机械体)。
/// 玩家派系单位(Colonist/Slave/ColonyAnimal/ColonyMechanoid)免管——
/// 原版已有对应机制(禁用门/区域管制/畜栏/机械师),Mod 不重复控制。
/// 敌对/囚犯/无派系单位原版 MachinesLike 本来就拒绝开门,跟随原版即可。
/// </summary>
public enum AccessCategory
{
    Colonist,
    Slave,
    ColonyAnimal,
    ColonyMechanoid,
    Guest,
    Anomaly,
    Prisoner,
    Hostile,
    WildAnimal,
    Other
}
