using System;
using System.Collections.Generic;
using RunningBugs.RimLocksmith.Core;

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static void Eq(bool expected, bool actual, string message)
{
    if (expected != actual) throw new Exception($"{message}: expected {expected}, got {actual}");
}

var cfg = LockConfigData.CreateDefault();
var closedColonyDoor = new DoorAccessFacts(isColonyDoor: true, isOpenOrFreePassage: false);
var animalFlapDoor = new DoorAccessFacts(isColonyDoor: true, isOpenOrFreePassage: false, roamerCanOpen: true);
var openColonyDoor = new DoorAccessFacts(isColonyDoor: true, isOpenOrFreePassage: true);
var nonColonyDoor = new DoorAccessFacts(isColonyDoor: false, isOpenOrFreePassage: false);

var expectedDefaults = new Dictionary<AccessCategory, bool>
{
    [AccessCategory.Colonist] = true,
    [AccessCategory.Slave] = true,
    [AccessCategory.Prisoner] = false,
    [AccessCategory.ColonyAnimal] = true,
    [AccessCategory.ColonyMechanoid] = true,
    [AccessCategory.Guest] = true,
    [AccessCategory.Ally] = true,
    [AccessCategory.Trader] = true,
    [AccessCategory.Hostile] = false,
    [AccessCategory.WildAnimal] = false,
    [AccessCategory.Other] = false,
};

foreach (var pair in expectedDefaults)
{
    Eq(pair.Value, LockPolicy.AllowsOpeningClosedDoor(new PawnAccessFacts(pair.Key), closedColonyDoor, cfg), $"default {pair.Key}");
}

foreach (AccessCategory category in Enum.GetValues<AccessCategory>())
{
    Eq(false, LockPolicy.AllowsOpeningClosedDoor(new PawnAccessFacts(category), nonColonyDoor, cfg), $"non-colony door bypass {category}");
    Eq(true, LockPolicy.AllowsOpeningClosedDoor(new PawnAccessFacts(category), openColonyDoor, cfg), $"open door not blocked {category}");
    Eq(false, LockPolicy.AllowsOpeningClosedDoor(new PawnAccessFacts(category, canOpenDoors: false), closedColonyDoor, cfg), $"cannot open physical ability {category}");
}

var overrideCfg = cfg.Clone();
overrideCfg.AllowHostiles = true;
overrideCfg.AllowColonists = false;
overrideCfg.AllowColonyAnimals = true;
overrideCfg.AllowGuests = false;
Eq(true, LockPolicy.AllowsOpeningClosedDoor(new PawnAccessFacts(AccessCategory.Hostile), closedColonyDoor, overrideCfg), "hostile override allow");
Eq(false, LockPolicy.AllowsOpeningClosedDoor(new PawnAccessFacts(AccessCategory.Colonist), closedColonyDoor, overrideCfg), "colonist override deny");
Eq(true, LockPolicy.AllowsOpeningClosedDoor(new PawnAccessFacts(AccessCategory.ColonyAnimal), closedColonyDoor, overrideCfg), "animal override allow");
Eq(false, LockPolicy.AllowsOpeningClosedDoor(new PawnAccessFacts(AccessCategory.Guest), closedColonyDoor, overrideCfg), "guest override deny");
Eq(false, LockPolicy.AllowsOpeningClosedDoor(new PawnAccessFacts(AccessCategory.ColonyAnimal, isFenceBlockedRoamer: true), closedColonyDoor, overrideCfg), "fence-blocked colony animal cannot open normal door even when animal rule allows");
Eq(true, LockPolicy.AllowsOpeningClosedDoor(new PawnAccessFacts(AccessCategory.ColonyAnimal, isFenceBlockedRoamer: true), animalFlapDoor, overrideCfg), "fence-blocked colony animal can open roamerCanOpen door when animal rule allows");
Eq(true, LockPolicy.AllowsOpeningClosedDoor(new PawnAccessFacts(AccessCategory.ColonyAnimal, isFenceBlockedRoamer: true, isRopedByPawn: true, roperCanOpen: true), closedColonyDoor, overrideCfg), "roped fence-blocked colony animal may pass normal door when roper can open");
Eq(false, LockPolicy.AllowsOpeningClosedDoor(new PawnAccessFacts(AccessCategory.ColonyAnimal, isFenceBlockedRoamer: true, isRopedByPawn: true, roperCanOpen: false), closedColonyDoor, overrideCfg), "roped fence-blocked colony animal cannot pass normal door when roper cannot open");
Eq(false, LockPolicy.AllowsOpeningClosedDoor(new PawnAccessFacts(AccessCategory.ColonyAnimal, isFenceBlockedRoamer: true), closedColonyDoor, cfg), "default fence-blocked colony animal cannot open normal door");
Eq(true, LockPolicy.AllowsOpeningClosedDoor(new PawnAccessFacts(AccessCategory.ColonyAnimal, isFenceBlockedRoamer: true), animalFlapDoor, cfg), "default fence-blocked colony animal can open roamerCanOpen door");

var clone = overrideCfg.Clone();
Assert(clone != overrideCfg, "clone should be a different instance");
Eq(overrideCfg.AllowHostiles, clone.AllowHostiles, "clone preserves hostiles");
clone.Version = -1;
clone.Normalize();
Assert(clone.Version == LockConfigData.CurrentVersion, "normalize fixes version");

Console.WriteLine("RimLocksmith whitebox tests PASS");
