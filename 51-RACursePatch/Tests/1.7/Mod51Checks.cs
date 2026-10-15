using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
namespace RW17.Validation;
public static class Mod51Checks
{
    public const string PackageId = "RunningBugs.RACursePatch";
    public static void Run(Map map)
    {
        var asm=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="RatkinAnomaly");
        foreach(var name in new[] { "AteWithoutTable_Patch", "KillAnimals_Patch", "Wounded_Patch" })
        {
            var method=AccessTools.Method(asm.GetType("RatkinAnomaly."+name,true),name=="AteWithoutTable_Patch"?"Postfix":"Prefix");
            if(Harmony.GetPatchInfo(method)?.Prefixes.Any(p=>p.owner=="com.runningbugs.RACursePatch")!=true) throw new Exception("Mod51: actual upstream method intercept absent: "+name);
        }
        var settings=AccessTools.TypeByName("RACursePatch.ModSettingsUI").GetField("settings").GetValue(null);
        var enabled=settings.GetType().GetField("enablePatch"); var oldEnabled=enabled.GetValue(settings);
        var worldComp=Find.World.components.Single(c=>c.GetType().FullName=="RatkinAnomaly.RAComponent");
        var curse=worldComp.GetType().GetField("curse"); var oldCurse=curse.GetValue(worldComp);
        var generated=new List<Pawn>(); var chimerasBefore=map.mapPawns.AllPawnsSpawned.Where(p=>p.kindDef==PawnKindDefOf.Chimera).ToHashSet();
        Rand.PushState(1751);
        Pawn NewHuman(bool colonist)
        {
            var p=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,colonist?Faction.OfPlayer:Faction.OfAncientsHostile);
            generated.Add(p); GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,25),map); return p;
        }
        try
        {
            curse.SetValue(worldComp,"None"); var colony=NewHuman(true); var outsider=NewHuman(false);
            if(!colony.IsColonist || outsider.IsColonist) throw new Exception("Mod51: real faction fixture misclassified");
            curse.SetValue(worldComp,"EatingWithoutTable"); enabled.SetValue(settings,true);
            outsider.needs.mood.thoughts.memories.TryGainMemory((Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.AteWithoutTable));
            if(outsider.health.hediffSet.HasHediff(HediffDefOf.FoodPoisoning)) throw new Exception("Mod51: enabled setting let outsider meal curse through");
            colony.needs.mood.thoughts.memories.TryGainMemory((Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.AteWithoutTable));
            if(!colony.health.hediffSet.HasHediff(HediffDefOf.FoodPoisoning)) throw new Exception("Mod51: actual colonist positive meal curse was blocked");
            enabled.SetValue(settings,false); outsider.needs.mood.thoughts.memories.TryGainMemory((Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.AteWithoutTable));
            if(!outsider.health.hediffSet.HasHediff(HediffDefOf.FoodPoisoning)) throw new Exception("Mod51: disabled setting failed to restore outsider meal curse");
            curse.SetValue(worldComp,"None"); var woundedColony=NewHuman(true); var woundedOutsider=NewHuman(false);
            curse.SetValue(worldComp,"Wounded"); enabled.SetValue(settings,true);
            woundedOutsider.TakeDamage(new DamageInfo(DamageDefOf.Blunt,1f,instigator:colony));
            if(woundedOutsider.Downed || woundedOutsider.Dead) throw new Exception("Mod51: enabled setting let outsider wounded curse through");
            woundedColony.TakeDamage(new DamageInfo(DamageDefOf.Blunt,1f,instigator:outsider));
            if(!woundedColony.Downed || woundedColony.Dead) throw new Exception("Mod51: real colonist positive wounded curse did not down without killing");
            enabled.SetValue(settings,false); woundedOutsider.TakeDamage(new DamageInfo(DamageDefOf.Blunt,1f,instigator:colony));
            if(!woundedOutsider.Downed || woundedOutsider.Dead) throw new Exception("Mod51: disabled setting did not restore outsider wounded curse");
            curse.SetValue(worldComp,"None");
            var animalKind=DefDatabase<PawnKindDef>.AllDefs.First(k=>k.race==ThingDefOf.Cow); var a=PawnGenerator.GeneratePawn(animalKind); var b=PawnGenerator.GeneratePawn(animalKind); generated.Add(a); generated.Add(b);
            GenSpawn.Spawn(a,CellFinder.RandomClosewalkCellNear(map.Center,map,25),map); GenSpawn.Spawn(b,CellFinder.RandomClosewalkCellNear(map.Center,map,25),map);
            curse.SetValue(worldComp,"KillAnimals"); enabled.SetValue(settings,true); int count=map.mapPawns.AllPawnsSpawned.Count(p=>p.kindDef==PawnKindDefOf.Chimera);
            a.Kill(new DamageInfo(DamageDefOf.Blunt,1000f,instigator:colony));
            if(map.mapPawns.AllPawnsSpawned.Count(p=>p.kindDef==PawnKindDefOf.Chimera)!=count) throw new Exception("Mod51: enabled legacy victim-scope allowed animal curse");
            enabled.SetValue(settings,false); b.Kill(new DamageInfo(DamageDefOf.Blunt,1000f,instigator:colony));
            if(map.mapPawns.AllPawnsSpawned.Count(p=>p.kindDef==PawnKindDefOf.Chimera)!=count+1) throw new Exception("Mod51: disabled setting did not restore actual animal-death chimera effect");
        }
        finally
        {
            curse.SetValue(worldComp,"None");
            foreach(var p in map.mapPawns.AllPawnsSpawned.Where(p=>p.kindDef==PawnKindDefOf.Chimera && !chimerasBefore.Contains(p)).ToArray()) p.Destroy();
            foreach(var p in generated) { var corpse=p.Corpse; if(corpse!=null && !corpse.Destroyed) corpse.Destroy(); if(!p.Destroyed) p.Destroy(); }
            curse.SetValue(worldComp,oldCurse); enabled.SetValue(settings,oldEnabled); Rand.PopState();
        }
        Console.WriteLine("RW17_MOD51_REAL_MEAL_DAMAGE_ANIMAL_DEATH_CURSE_ON_OFF_OK; settings UI/reload remains pending");
    }
}
