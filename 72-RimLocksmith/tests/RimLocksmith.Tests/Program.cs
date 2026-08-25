using System;
using RunningBugs.RimLocksmith.Core;

static void Eq(bool expected, bool actual, string message)
{
    if (expected != actual) throw new Exception($"{message}: expected {expected}, got {actual}");
}

var cfg = LockConfigData.CreateDefault();

// 可配置类别:玩家派系四类(受「管理殖民地单位」设置项门控,Core 层恒可配置) + 访客
Eq(true, LockPolicy.IsConfigurable(AccessCategory.Colonist), "colonist configurable");
Eq(true, LockPolicy.IsConfigurable(AccessCategory.Slave), "slave configurable");
Eq(true, LockPolicy.IsConfigurable(AccessCategory.ColonyAnimal), "colony animal configurable");
Eq(true, LockPolicy.IsConfigurable(AccessCategory.ColonyMechanoid), "colony mech configurable");
Eq(true, LockPolicy.IsConfigurable(AccessCategory.Guest), "guest configurable");

// 异象控制暂禁用(ANOMALY-CONTROL disabled)
Eq(false, LockPolicy.IsConfigurable(AccessCategory.Anomaly), "anomaly not configurable (feature disabled)");

// 原版自拒类别:不可配置
Eq(false, LockPolicy.IsConfigurable(AccessCategory.Hostile), "hostile not configurable");
Eq(false, LockPolicy.IsConfigurable(AccessCategory.Prisoner), "prisoner not configurable");
Eq(false, LockPolicy.IsConfigurable(AccessCategory.WildAnimal), "wild animal not configurable");
Eq(false, LockPolicy.IsConfigurable(AccessCategory.Other), "other not configurable");

// 不可配置类别永远不被收窄(即使配置全关)
var denyAll = LockConfigData.CreateDefault();
denyAll.AllowColonists = false;
denyAll.AllowSlaves = false;
denyAll.AllowGuests = false;
denyAll.AnimalAccess = AnimalAccess.None;
denyAll.MechAccess = MechAccess.None;
foreach (AccessCategory category in new[] { AccessCategory.Hostile, AccessCategory.Prisoner, AccessCategory.WildAnimal, AccessCategory.Other, AccessCategory.Anomaly })
{
    Eq(true, LockPolicy.Allows(denyAll, new PawnAccessFacts(category)), $"non-configurable {category} always follows vanilla");
}

// 布尔开关
Eq(true, LockPolicy.Allows(cfg, new PawnAccessFacts(AccessCategory.Colonist)), "default colonist allowed");
Eq(true, LockPolicy.Allows(cfg, new PawnAccessFacts(AccessCategory.Guest)), "default guest allowed");
Eq(false, LockPolicy.Allows(denyAll, new PawnAccessFacts(AccessCategory.Colonist)), "denyAll colonist denied");
Eq(false, LockPolicy.Allows(denyAll, new PawnAccessFacts(AccessCategory.Slave)), "denyAll slave denied");
Eq(false, LockPolicy.Allows(denyAll, new PawnAccessFacts(AccessCategory.Guest)), "denyAll guest denied");

// 动物档位(仅 None/All,宠物档已删除)
Eq(false, LockPolicy.Allows(denyAll, new PawnAccessFacts(AccessCategory.ColonyAnimal)), "AnimalAccess.None denies animals");
Eq(true, LockPolicy.Allows(cfg, new PawnAccessFacts(AccessCategory.ColonyAnimal)), "AnimalAccess.All allows animals");

// 机械体三态
var onlyOverseen = cfg.Clone();
onlyOverseen.MechAccess = MechAccess.OnlyOverseen;
Eq(true, LockPolicy.Allows(onlyOverseen, new PawnAccessFacts(AccessCategory.ColonyMechanoid, hasOverseer: true)), "overseen mech allowed");
Eq(false, LockPolicy.Allows(onlyOverseen, new PawnAccessFacts(AccessCategory.ColonyMechanoid, hasOverseer: false)), "overseer-less mech denied");
Eq(false, LockPolicy.Allows(denyAll, new PawnAccessFacts(AccessCategory.ColonyMechanoid, hasOverseer: true)), "MechAccess.None denies even overseen");
Eq(true, LockPolicy.Allows(cfg, new PawnAccessFacts(AccessCategory.ColonyMechanoid, hasOverseer: false)), "MechAccess.All allows overseer-less");

// Clone / Normalize
var clone = denyAll.Clone();
if (clone == denyAll) throw new Exception("clone should be a different instance");
if (clone.AnimalAccess != denyAll.AnimalAccess) throw new Exception("clone preserves animal access");
if (clone.AllowGuests != denyAll.AllowGuests) throw new Exception("clone preserves guest access");
clone.Version = -1;
clone.Normalize();
if (clone.Version != LockConfigData.CurrentVersion) throw new Exception("normalize fixes version");
if (LockConfigData.CurrentVersion != 3) throw new Exception("config schema is v3");

Console.WriteLine("RimLocksmith whitebox tests PASS");
