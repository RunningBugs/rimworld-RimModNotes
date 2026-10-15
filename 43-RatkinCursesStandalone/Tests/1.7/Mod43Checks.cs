using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod43Checks
{
    public const string PackageId="RunningBugs.RatkinCursesStandalone";
    static void Check(bool ok,string why){if(!ok)throw new Exception("Mod43: "+why);}
    public static void Run(Map map)
    {
        Check(ModsConfig.AnomalyActive,"real Anomaly dependency required");
        var own=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="RASL");
        var comp=Find.World.GetComponent(own.GetType("RASL.RAComponent",true));var state=comp.GetType().GetField("curse");object oldState=state.GetValue(comp);
        var target=AccessTools.Method(typeof(Pawn),"PostApplyDamage",new[]{typeof(DamageInfo),typeof(float)});
        Check(Harmony.GetPatchInfo(target)?.Prefixes.Any(p=>p.PatchMethod.DeclaringType==own.GetType("RASL.Wounded_Patch",true))==true,"actual Wounded Prefix missing");
        var oldLetters=Find.LetterStack.LettersListForReading.ToArray();var oldBattle=Current.Game.battleLog;var oldWeather=map.weatherManager.curWeather;var oldLastWeather=map.weatherManager.lastWeather;var oldForcedWeather=map.weatherDecider.ForcedWeather;
        var oldFilth=map.listerThings.ThingsInGroup(ThingRequestGroup.Filth).ToArray();var priorPawns=map.mapPawns.AllPawnsSpawned.ToArray();var fixtures=new List<Pawn>();var addedConditions=new List<GameCondition>();
        Pawn Human(bool player=true,bool spawn=true){var p=PawnGenerator.GeneratePawn(player?PawnKindDefOf.Colonist:PawnKindDefOf.SpaceRefugee,player?Faction.OfPlayer:Faction.OfAncients);fixtures.Add(p);if(spawn)GenSpawn.Spawn(p,CellFinder.StandableCellNear(map.Center,map,12),map);return p;}
        void Damage(Pawn p,float amount=1){var part=p.RaceProps.body.AllParts.First(x=>x.def==BodyPartDefOf.Leg);p.TakeDamage(new DamageInfo(DamageDefOf.Blunt,amount,999,hitPart:part));}
        int Blood(Pawn p)=>map.listerThings.ThingsOfDef(p.RaceProps.BloodDef).OfType<Filth>().Sum(f=>f.thickness);
        try
        {
            Current.Game.battleLog=new BattleLog();state.SetValue(comp,"Null");var normal=Human();Damage(normal);Check(!normal.Downed&&!normal.Dead,"inactive real damage negative control");
            var def=DefDatabase<GameConditionDef>.GetNamed("RA_CurseCondition_Wounded");Check(def.conditionClass==own.GetType("RASL.GameCondition_Curse")&&def.canBePermanent&&def.weatherDef==DefDatabase<WeatherDef>.GetNamed("GrayPall"),"real independent Def/class/weather contract");
            var condition=GameConditionMaker.MakeCondition(def,60000);condition.Permanent=true;addedConditions.Add(condition);Find.World.gameConditionManager.RegisterCondition(condition);
            Check((string)state.GetValue(comp)=="Wounded"&&Find.World.gameConditionManager.ActiveConditions.Contains(condition)&&condition.ForcedWeather()==def.weatherDef&&Find.LetterStack.LettersListForReading.Except(oldLetters).Count()==1,"actual world condition Init/letter/forced weather");
            var zero=Human();Damage(zero,0);Check(!zero.Downed&&!zero.Dead,"zero damage exclusion");
            var unspawned=Human(true,false);Damage(unspawned);Check(!unspawned.Downed&&!unspawned.Dead,"unspawned exclusion");
            var outsider=Human(false);Damage(outsider);Check(!outsider.Downed&&!outsider.Dead,"non-colony human exclusion");
            var animal=PawnGenerator.GeneratePawn(PawnKindDefOf.Muffalo,Faction.OfPlayer);fixtures.Add(animal);GenSpawn.Spawn(animal,CellFinder.StandableCellNear(map.Center,map,12),map);animal.TakeDamage(new DamageInfo(DamageDefOf.Blunt,1,999));Check(!animal.Downed&&!animal.Dead,"animal exclusion");
            var cursed=Human();int blood=Blood(cursed);Damage(cursed);Check(cursed.Downed&&!cursed.Dead&&Blood(cursed)>blood,"actual eligible injury downing/filth");
            var slave=Human(false);slave.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Slave);Check(slave.IsSlaveOfColony,"slave eligibility fixture");Damage(slave);Check(slave.Downed&&!slave.Dead,"actual colony-slave downing");
            condition.End();Check((string)state.GetValue(comp)=="Null"&&!Find.World.gameConditionManager.ActiveConditions.Contains(condition)&&map.weatherDecider.ForcedWeather==oldForcedWeather,"world-scoped End/curse/weather release");
            var after=Human();Damage(after);Check(!after.Downed&&!after.Dead,"ended curse real injury control");
            // Map-scoped activation also retains its own expected lifecycle.
            var local=GameConditionMaker.MakeCondition(def,60000);addedConditions.Add(local);map.gameConditionManager.RegisterCondition(local);Check((string)state.GetValue(comp)=="Wounded"&&local.ForcedWeather()==def.weatherDef,"map Init");local.End();Check((string)state.GetValue(comp)=="Null"&&!map.gameConditionManager.ActiveConditions.Contains(local),"map End");
            Check(priorPawns.All(p=>p.Spawned&&!p.Destroyed),"fixture must preserve original map pawns");
            Log.Message("RW17_MOD43_WORLD_AND_MAP_CONDITION_REAL_DAMAGE_COLONIST_SLAVE_POSITIVE_NEGATIVE_PASSED");
        }
        finally
        {
            foreach(var condition in addedConditions)if(condition.gameConditionManager?.ActiveConditions.Contains(condition)==true)condition.End();
            state.SetValue(comp,oldState);Current.Game.battleLog=oldBattle;map.weatherManager.curWeather=oldWeather;map.weatherManager.lastWeather=oldLastWeather;
            foreach(var letter in Find.LetterStack.LettersListForReading.Except(oldLetters).ToArray())Find.LetterStack.RemoveLetter(letter);
            foreach(var filth in map.listerThings.ThingsInGroup(ThingRequestGroup.Filth).Except(oldFilth).ToArray())if(!filth.Destroyed)filth.Destroy();
            foreach(var pawn in fixtures){if(pawn.Corpse!=null&&!pawn.Corpse.Destroyed)pawn.Corpse.Destroy();if(Find.WorldPawns.Contains(pawn))Find.WorldPawns.RemovePawn(pawn);if(!pawn.Destroyed)pawn.Destroy(DestroyMode.Vanish);}
        }
    }
}
