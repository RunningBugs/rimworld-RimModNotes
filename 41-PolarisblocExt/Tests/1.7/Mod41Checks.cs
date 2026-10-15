using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text.Json;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace RW17.Validation;

public static class Mod41Checks
{
    public const string PackageId="RunningBugs.PolarisblocExt";
    static Assembly Own=>AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="PolarisblocExt");
    static Type T(string name)=>Own.GetType("PolarisblocExt."+name,true);
    static object Component=>Current.Game.GetComponent(T("RequestTechCoreNotification"));
    static object Contact=>T("TechCoreGiver").GetProperty("Instance").GetValue(null);
    static FieldInfo Last=>AccessTools.Field(T("TechCoreGiver"),"lastTechCoreRequestTick");
    static bool Available=>(bool)T("TechCoreGiver").GetProperty("ShouldTechCoreAvailable").GetValue(Contact);
    static bool ItemAvailable=>(bool)T("RequestTechCoreNotification").GetProperty("ShouldTechCoreAvailable").GetValue(null);
    static void Check(bool ok,string reason){if(!ok)throw new Exception("Mod41: "+reason);}
    public static void Run(Map map)
    {
        Check(LoadedModManager.RunningMods.Any(m=>m.PackageId.Equals("Vanya.Polarisbloc.CoreLab.tmp",StringComparison.OrdinalIgnoreCase)),"actual current core dependency must be active");
        var target=AccessTools.Method(typeof(Building_CommsConsole),"GetFloatMenuOptions",new[]{typeof(Pawn)});
        Check(Harmony.GetPatchInfo(target)?.Postfixes.Any(p=>p.PatchMethod.DeclaringType==T("Patch_Building_CommsConsole_GetFloatMenuOptions"))==true,"actual comms postfix missing");
        var research=DefDatabase<ResearchProjectDef>.GetNamed("PolarisNanoRepairHazeGenerator");
        var progress=(Dictionary<ResearchProjectDef,float>)AccessTools.Field(typeof(ResearchManager),"progress").GetValue(Find.ResearchManager);var oldProgress=new Dictionary<ResearchProjectDef,float>(progress);
        var last=Last;object oldLast=last.GetValue(Contact);var notify=T("RequestTechCoreNotification").GetField("sendTechCoreRequestReminder");object oldReminder=notify.GetValue(Component);
        var tick=AccessTools.Field(typeof(TickManager),"ticksGameInt");object oldTick=tick.GetValue(Find.TickManager);
        var oldQuests=Find.QuestManager.QuestsListForReading.ToArray();var oldLetters=Find.LetterStack.LettersListForReading.ToArray();var fixtures=new List<Thing>();
        Pawn negotiator=null;Job oldJob=null;
        try
        {
            progress[research]=0;last.SetValue(Contact,GenTicks.TicksGame-GenDate.TicksPerYear-1);Check(!Available&&!ItemAvailable,"unfinished research negative control");
            progress[research]=research.baseCost;Check(Available&&ItemAvailable,"research finished and no-core input");
            var core=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("TechprofSubpersonaCore"));fixtures.Add(core);GenSpawn.Spawn(core,Cell(map),map);
            Check(!ItemAvailable,"existing real core exclusion");core.Destroy();Check(ItemAvailable,"destroyed core must release exclusion");
            notify.SetValue(Component,true);tick.SetValue(Find.TickManager,1999);
            T("RequestTechCoreNotification").GetMethod("GameComponentTick").Invoke(Component,null);
            int ownLetters=Find.LetterStack.LettersListForReading.Except(oldLetters).Count();Check(ownLetters==1&&!(bool)notify.GetValue(Component),"actual prime-tick reminder");
            T("RequestTechCoreNotification").GetMethod("GameComponentTick").Invoke(Component,null);Check(Find.LetterStack.LettersListForReading.Except(oldLetters).Count()==1,"repeat reminder suppression");
            tick.SetValue(Find.TickManager,oldTick);last.SetValue(Contact,GenTicks.TicksGame-GenDate.TicksPerYear-1);
            negotiator=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);fixtures.Add(negotiator);GenSpawn.Spawn(negotiator,Cell(map),map);Check(!negotiator.skills.GetSkill(SkillDefOf.Social).TotallyDisabled,"fixture negotiator social disabled");oldJob=negotiator.CurJob;
            var beacon=(Building_OrbitalTradeBeacon)ThingMaker.MakeThing(ThingDefOf.OrbitalTradeBeacon);fixtures.Add(beacon);beacon.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(beacon,Cell(map),map);beacon.GetComp<CompPowerTrader>().PowerOn=true;
            var console=(Building_CommsConsole)ThingMaker.MakeThing(ThingDefOf.CommsConsole);fixtures.Add(console);console.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(console,map.AllCells.First(c=>!c.InHorDistOf(beacon.Position,12)&&GenAdj.OccupiedRect(c,Rot4.North,console.def.size).Cells.All(x=>x.InBounds(map)&&x.Standable(map)&&x.GetThingList(map).Count==0)),map);console.GetComp<CompPowerTrader>().PowerOn=true;
            DiaOption Request()=> (DiaOption)AccessTools.Method(T("TechCoreGiver"),"RequestTechCoreQuest").Invoke(Contact,new object[]{map,negotiator});
            Check(Request().disabled,"less-than1500 silver negative control");
            foreach(int count in new[]{500,500,500,200}){var silver=ThingMaker.MakeThing(ThingDefOf.Silver);silver.stackCount=count;fixtures.Add(silver);GenSpawn.Spawn(silver,GenRadial.RadialCellsAround(beacon.Position,5,false).First(c=>c.InBounds(map)&&c.Standable(map)&&c.GetThingList(map).Count==0),map);}
            int before=TradeUtility.AllLaunchableThingsForTrade(map).Where(t=>t.def==ThingDefOf.Silver).Sum(t=>t.stackCount);Check(before>=1700,"beacon real silver input");
            var options=console.GetFloatMenuOptions(negotiator).ToArray();string label="CallOnRadio".Translate((string)T("TechCoreGiver").GetMethod("GetCallLabel").Invoke(Contact,null));
            var option=options.Single(o=>o.Label==label);Check(!option.Disabled,"real console contact unavailable");option.action();
            Check(negotiator.CurJob?.def==JobDefOf.UseCommsConsole&&ReferenceEquals(negotiator.CurJob.commTarget,Contact),"actual UseCommsConsole target job");negotiator.jobs.EndCurrentJob(JobCondition.InterruptForced);
            var request=Request();Check(!request.disabled,"real ItemStash request disabled");request.action();
            var quest=Find.QuestManager.QuestsListForReading.Except(oldQuests).Single();var site=quest.PartsListForReading.OfType<QuestPart_SpawnWorldObject>().Select(p=>p.worldObject).OfType<Site>().First();
            Check(site.Spawned&&site.parts.Any(p=>p.things!=null&&p.things.Any(t=>t.def==DefDatabase<ThingDef>.GetNamed("TechprofSubpersonaCore"))),"actual spawned quest-site core reward missing");
            Check(TradeUtility.AllLaunchableThingsForTrade(map).Where(t=>t.def==ThingDefOf.Silver).Sum(t=>t.stackCount)==before-1500&&(int)last.GetValue(Contact)==GenTicks.TicksGame&&!Available&&!ItemAvailable,"exact payment/cooldown/stash exclusion");
            last.SetValue(Contact,GenTicks.TicksGame-GenDate.TicksPerYear);Check(!Available,"one-year exact boundary must remain closed");last.SetValue(Contact,GenTicks.TicksGame-GenDate.TicksPerYear-1);Check(Available,"one-year later boundary");
            Log.Message("RW17_MOD41_REAL_CONTACT_JOB_QUEST_PAYMENT_REMINDER_AND_COOLDOWN_PASSED");
        }
        finally
        {
            if(negotiator?.CurJob!=null)negotiator.jobs.EndCurrentJob(JobCondition.InterruptForced);
            foreach(Quest q in Find.QuestManager.QuestsListForReading.Except(oldQuests).ToArray()){var sites=q.PartsListForReading.OfType<QuestPart_SpawnWorldObject>().Select(p=>p.worldObject).OfType<Site>().ToArray();q.End(QuestEndOutcome.Fail,sendLetter:false,playSound:false);Find.QuestManager.Remove(q);foreach(var site in sites)if(!site.Destroyed)site.Destroy();}
            foreach(var letter in Find.LetterStack.LettersListForReading.Except(oldLetters).ToArray())Find.LetterStack.RemoveLetter(letter);
            progress.Clear();foreach(var kv in oldProgress)progress.Add(kv.Key,kv.Value);last.SetValue(Contact,oldLast);notify.SetValue(Component,oldReminder);tick.SetValue(Find.TickManager,oldTick);
            foreach(var t in fixtures.AsEnumerable().Reverse())if(!t.Destroyed)t.Destroy(DestroyMode.Vanish);
        }
    }
    static IntVec3 Cell(Map map)=>map.AllCells.First(c=>c.x>15&&c.z>15&&c.Standable(map)&&!c.Fogged(map)&&c.GetThingList(map).Count==0);
    public static void SaveReload(Map map,string mode)
    {
        Check(Scribe.mode==LoadSaveMode.Inactive,"inactive Scribe prerequisite");Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
        var state=Path.Combine(GenFilePaths.SaveDataFolderPath,"rw17-save-state.json");var result=Path.Combine(GenFilePaths.SaveDataFolderPath,"rw17-save-result.json");var contact=(ILoadReferenceable)Contact;var reminder=T("RequestTechCoreNotification").GetField("sendTechCoreRequestReminder");
        if(mode=="save")
        {
            Last.SetValue(Contact,GenTicks.TicksGame);reminder.SetValue(Component,false);
            File.WriteAllText(state,JsonSerializer.Serialize(new{schema="mod41-1.7",map_id=map.uniqueID,request_tick=(int)Last.GetValue(Contact),contact_id=contact.GetUniqueLoadID(),reminder=false}));
            GameDataSaveLoader.SaveGame("rw17-mod41-fixture");string path=GenFilePaths.FilePathForSavedGame("rw17-mod41-fixture");Check(File.Exists(path)&&new FileInfo(path).Length>10000,"native full save missing");
            string xml=File.ReadAllText(path);Check(xml.Contains("PolarisblocExt.TechCoreGiver.lastTechCoreRequestTick")&&xml.Contains("PolarisblocExt.TechCoreGiver.Id")&&xml.Contains("PolarisblocExt.TechCoreGiver>"),"deep contact and original internal save keys missing");
            File.WriteAllText(result,JsonSerializer.Serialize(new{passed=true,mode,save=path,expected=state,scope="native entire-game save; new-process reload still required"}));
        }
        else
        {
            Check(mode=="reload","unknown validation mode");using var doc=JsonDocument.Parse(File.ReadAllText(state));var e=doc.RootElement;
            Check(e.GetProperty("schema").GetString()=="mod41-1.7"&&map.uniqueID==e.GetProperty("map_id").GetInt32(),"wrong fixture or map");
            Check((int)Last.GetValue(Contact)==e.GetProperty("request_tick").GetInt32()&&contact.GetUniqueLoadID()==e.GetProperty("contact_id").GetString()&&!(bool)reminder.GetValue(Component)&&!Available,"new-process cooldown/contact ID/reminder lost");
            File.WriteAllText(result,JsonSerializer.Serialize(new{passed=true,mode,assertions=4,scope="same-version new-process native full-game reload"}));
        }
        Log.Message("RW17_SAVE_RELOAD_"+mode.ToUpperInvariant()+"_OK");
    }
}
