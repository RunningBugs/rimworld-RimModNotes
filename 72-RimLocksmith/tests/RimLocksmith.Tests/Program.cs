using System;
using RunningBugs.RimLocksmith.Core;

static void Eq(bool expected, bool actual, string message)
{
    if (expected != actual) throw new Exception($"{message}: expected {expected}, got {actual}");
}

var cfg = LockConfigData.CreateDefault();

// 可配置类别判定
Eq(true, LockPolicy.IsConfigurable(AccessCategory.Colonist), "colonist configurable");
Eq(true, LockPolicy.IsConfigurable(AccessCategory.Slave), "slave configurable");
Eq(true, LockPolicy.IsConfigurable(AccessCategory.ColonyAnimal), "colony animal configurable");
Eq(true, LockPolicy.IsConfigurable(AccessCategory.ColonyMechanoid), "colony mech configurable");
Eq(true, LockPolicy.IsConfigurable(AccessCategory.Guest), "guest configurable");
Eq(true, LockPolicy.IsConfigurable(AccessCategory.Trader), "trader configurable");

// 不可配置类别(跟随原版)
Eq(false, LockPolicy.IsConfigurable(AccessCategory.Hostile), "hostile not configurable");
Eq(false, LockPolicy.IsConfigurable(AccessCategory.Prisoner), "prisoner not configurable");
Eq(false, LockPolicy.IsConfigurable(AccessCategory.WildAnimal), "wild animal not configurable");
Eq(false, LockPolicy.IsConfigurable(AccessCategory.Other), "other not configurable");

// 不可配置类别永远不被收窄(即使配置全关)
var denyAll = LockConfigData.CreateDefault();
denyAll.AllowColonists = false;
denyAll.AllowSlaves = false;
denyAll.AllowGuests = false;
denyAll.AllowTraders = false;
denyAll.AnimalAccess = AnimalAccess.None;
denyAll.MechAccess = MechAccess.None;
foreach (AccessCategory category in new[] { AccessCategory.Hostile, AccessCategory.Prisoner, AccessCategory.WildAnimal, AccessCategory.Other })
{
    Eq(true, LockPolicy.Allows(denyAll, new PawnAccessFacts(category)), $"non-configurable {category} always follows vanilla");
}

// 布尔开关
Eq(true, LockPolicy.Allows(cfg, new PawnAccessFacts(AccessCategory.Colonist)), "default colonist allowed");
Eq(false, LockPolicy.Allows(denyAll, new PawnAccessFacts(AccessCategory.Colonist)), "denyAll colonist denied");
Eq(false, LockPolicy.Allows(denyAll, new PawnAccessFacts(AccessCategory.Slave)), "denyAll slave denied");
Eq(false, LockPolicy.Allows(denyAll, new PawnAccessFacts(AccessCategory.Guest)), "denyAll guest denied");
Eq(false, LockPolicy.Allows(denyAll, new PawnAccessFacts(AccessCategory.Trader)), "denyAll trader denied");

// 动物三态
var onlyPets = cfg.Clone();
onlyPets.AnimalAccess = AnimalAccess.OnlyPets;
Eq(true, LockPolicy.Allows(onlyPets, new PawnAccessFacts(AccessCategory.ColonyAnimal, bodySize: 0.5f)), "small pet allowed in OnlyPets");
Eq(true, LockPolicy.Allows(onlyPets, new PawnAccessFacts(AccessCategory.ColonyAnimal, bodySize: LockPolicy.MaxPetBodySize)), "boundary pet size allowed");
Eq(false, LockPolicy.Allows(onlyPets, new PawnAccessFacts(AccessCategory.ColonyAnimal, bodySize: 1.2f)), "large animal denied in OnlyPets");
Eq(false, LockPolicy.Allows(denyAll, new PawnAccessFacts(AccessCategory.ColonyAnimal, bodySize: 0.5f)), "AnimalAccess.None denies even pets");
Eq(true, LockPolicy.Allows(cfg, new PawnAccessFacts(AccessCategory.ColonyAnimal, bodySize: 2.0f)), "AnimalAccess.All allows large animal");

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
clone.Version = -1;
clone.Normalize();
if (clone.Version != LockConfigData.CurrentVersion) throw new Exception("normalize fixes version");
if (LockConfigData.CurrentVersion != 2) throw new Exception("config schema is v2");

Console.WriteLine("RimLocksmith whitebox tests PASS");
