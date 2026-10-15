using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace RW17.Validation;
public static class Mod21Checks
{
    public const string PackageId = "RunningBugs.ArmorRacksForceWearFix";
    public static void Run(Map map)
    {
        var upstream = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "ArmorRacks");
        var driverType = upstream.GetType("ArmorRacks.Jobs.JobDriverTransferToRack", true);
        var rackType = upstream.GetType("ArmorRacks.Things.ArmorRack", true);
        var modType = upstream.GetType("ArmorRacks.ArmorRacksMod", true);
        var settingsType = upstream.GetType("ArmorRacks.ArmorRacksModSettings", true);
        var mod = LoadedModManager.ModHandles.Single(m => m.GetType() == modType);
        var getSettings = typeof(Mod).GetMethods().Single(m => m.Name == "GetSettings" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
        var settings = getSettings.MakeGenericMethod(settingsType).Invoke(mod, null);
        var equip = settingsType.GetField("EquipSetForced"); var transfer = settingsType.GetField("TransferSetForced");
        var oldEquip = equip.GetValue(settings); var oldTransfer = transfer.GetValue(settings);
        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        var def = DefDatabase<ThingDef>.AllDefs.First(d => rackType.IsAssignableFrom(d.thingClass) && !d.IsBlueprint && !d.IsFrame);
        var rack = (ThingWithComps)ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
        var shirtDef = DefDatabase<ThingDef>.GetNamed("Apparel_BasicShirt");
        var apparel = (Apparel)ThingMaker.MakeThing(shirtDef, GenStuff.DefaultStuffFor(shirtDef));
        var second = (Apparel)ThingMaker.MakeThing(shirtDef, GenStuff.DefaultStuffFor(shirtDef));
        Rand.PushState(1721);
        try
        {
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(map.Center, map, 15), map); pawn.apparel.DestroyAll();
            GenSpawn.Spawn(rack, CellFinder.RandomClosewalkCellNear(pawn.Position, map, 6), map);
            var storage = (StorageSettings)rackType.GetField("Settings").GetValue(rack); if (storage?.filter == null) throw new Exception("Mod21: actual rack storage field was not initialized"); storage.filter.SetDisallowAll();
            var owner = (ThingOwner)rackType.GetField("InnerContainer").GetValue(rack);
            if (owner == null) throw new Exception("Mod21: actual rack container field was not initialized");
            transfer.SetValue(settings, false); equip.SetValue(settings, true);
            void Transfer(Apparel item)
            {
                if (!owner.TryAdd(item)) throw new Exception("Mod21: actual rack ownership fixture failed");
                var driver = (JobDriver)Activator.CreateInstance(driverType); driver.pawn = pawn;
                driver.job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("ArmorRacks_JobTransferToRack"), rack);
                var toils = (IEnumerable<Toil>)AccessTools.Method(driverType, "MakeNewToils").Invoke(driver, null);
                var final = toils.Last(); final.initAction();
            }
            Transfer(apparel);
            if (apparel.Wearer != pawn || !pawn.outfits.forcedHandler.IsForced(apparel)) throw new Exception("Mod21: real transfer Wear did not honor EquipSetForced");
            pawn.outfits.forcedHandler.SetForced(apparel, false); pawn.apparel.Remove(apparel); apparel.Destroy();
            equip.SetValue(settings, false); Transfer(second);
            if (second.Wearer != pawn || pawn.outfits.forcedHandler.IsForced(second)) throw new Exception("Mod21: disabled forced setting still forced transfer apparel");
        }
        finally
        {
            equip.SetValue(settings, oldEquip); transfer.SetValue(settings, oldTransfer);
            if (!apparel.Destroyed) apparel.Destroy(); if (!second.Destroyed) second.Destroy(); rack.Destroy(); pawn.Destroy(); Rand.PopState();
        }
        Console.WriteLine("RW17_MOD21_REAL_TRANSFER_WEAR_FORCE_AND_OFF_CONTROL_OK");
    }
}
