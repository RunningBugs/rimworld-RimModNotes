using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
namespace RW17.Validation;
public static class Mod39Checks
{
    public const string PackageId = "RunningBugs.MoelotlSimpleSidearmsCompatiblePatch";
    public static void Run(Map map)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Axolotl").GetType("Axolotl.Verb_LotlQiIntensifyShoot",true);
        var getter = AccessTools.PropertyGetter(type,"CanUse");
        if (Harmony.GetPatchInfo(getter)?.Prefixes.Any(p=>p.PatchMethod.DeclaringType?.FullName=="MoeLotlSSAPatch.Verb_LotlQiIntensifyShootCanUse_Patch")!=true) throw new Exception("Mod39: actual renamed getter guard missing");
        var def=DefDatabase<ThingDef>.AllDefs.First(d=>d.Verbs?.Any(v=>v.verbClass==type)==true);
        var weapon=(ThingWithComps)ThingMaker.MakeThing(def,GenStuff.DefaultStuffFor(def));
        var pawn=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);
        var verb=weapon.GetComp<CompEquippable>().AllVerbs.Single(v=>v.GetType()==type);
        var getPawn=type.GetProperty("GetPawn");
        try
        {
            GenSpawn.Spawn(pawn,CellFinder.RandomClosewalkCellNear(map.Center,map,15),map);
            GenSpawn.Spawn(weapon,CellFinder.RandomClosewalkCellNear(pawn.Position,map,4),map);
            if (getPawn.GetValue(verb)!=null || (bool)getter.Invoke(verb,null) || ((Verb_LaunchProjectile)verb).Projectile!=verb.verbProps.defaultProjectile) throw new Exception("Mod39: ground weapon CanUse/projectile fallback failed");
            weapon.DeSpawn(); if(!pawn.inventory.innerContainer.TryAdd(weapon)) throw new Exception("Mod39: real inventory ownership fixture failed");
            if(getPawn.GetValue(verb)!=null || (bool)getter.Invoke(verb,null) || ((Verb_LaunchProjectile)verb).Projectile!=verb.verbProps.defaultProjectile) throw new Exception("Mod39: actual sidearm inventory query unsafe or changed projectile");
            var sidearms = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a=>a.GetName().Name=="SimpleSidearms");
            if(sidearms!=null)
            {
                var calculator=sidearms.GetType("PeteTimesSix.SimpleSidearms.Utilities.StatCalculator",true);
                var dps=(float)calculator.GetMethod("RangedDPSAverage").Invoke(null,new object[] { weapon,1f,1f });
                if(!float.IsFinite(dps) || dps<=0) throw new Exception("Mod39: actual SimpleSidearms inventory weapon DPS query failed");
                Console.WriteLine("RW17_MOD39_ACTUAL_SIMPLESIDEARMS_INVENTORY_DPS_OK");
            }
            else Console.WriteLine("RW17_MOD39_SIMPLESIDEARMS_NOT_ENABLED; composite caller remains pending");
            pawn.inventory.innerContainer.Remove(weapon); pawn.equipment.DestroyAllEquipment(); pawn.equipment.AddEquipment(weapon);
            if(getPawn.GetValue(verb)!=pawn) throw new Exception("Mod39: equipped weapon lost actual Pawn_EquipmentTracker chain");
            var patch=AccessTools.TypeByName("MoeLotlSSAPatch.Verb_LotlQiIntensifyShootCanUse_Patch");
            object[] args={false,verb};
            if(!(bool)AccessTools.Method(patch,"Prefix").Invoke(null,args)) throw new Exception("Mod39: non-null owner unexpectedly bypasses original CanUse");
        }
        finally { if(!weapon.Destroyed) weapon.Destroy(); pawn.Destroy(); }
        Console.WriteLine("RW17_MOD39_REAL_GROUND_INVENTORY_EQUIPMENT_VERB_QUERY_OK; active MoeLotl energy/SimpleSidearms UI remains pending");
    }
}
