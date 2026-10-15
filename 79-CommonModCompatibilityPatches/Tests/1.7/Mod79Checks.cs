using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
namespace RW17.Validation;
public static class Mod79Checks
{
 public const string PackageId="RunningBugs.CommonModCompatibilityPatches";
 private const string Owner="com.RunningBugs.CommonModCompatibilityPatches";
 private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 private static Type Product(string n)=>AccessTools.TypeByName("CommonModCompatibilityPatches."+n);
 private static void Check(bool yes,string why) { if(!yes)throw new Exception("Mod79: "+why); }
 private static bool Active(string p)=>ModsConfig.ActiveModsInLoadOrder.Any(m=>string.Equals(m.PackageId,p,StringComparison.OrdinalIgnoreCase));
 private static void Registered(MethodBase m,string group) { Check(m!=null,"actual target exists "+group);var p=Harmony.GetPatchInfo(m);Check(p!=null&&p.Prefixes.Concat(p.Postfixes).Concat(p.Transpilers).Concat(p.Finalizers).Any(x=>x.owner==Owner&&x.PatchMethod.DeclaringType==Product(group)),"specific patch registered on true target "+group); }
 public static void Run(Map map)
 {
  Check(Find.CurrentMap==map && Current.ProgramState==ProgramState.Playing,"current disposable playing map");
  var assembly=Product("CommonCompatibilityBootstrap").Assembly;
  var groups=assembly.GetTypes().Where(t=>t.Name.EndsWith("Compatibility")&&t.GetMethod("TryApply",Flags)!=null).ToArray();
  Check(groups.Length==30,"all thirty groups retained");
  var ledger=new List<object>();
  foreach(var g in groups)
  {
   var packages=g.GetFields(Flags).Where(f=>f.IsLiteral&&f.FieldType==typeof(string)&&f.Name.Contains("PackageId")).Select(f=>(string)f.GetRawConstantValue()).ToList();
   if(g.Name is "InvokeHoraxOfferingCompatibility" or "PawnDuplicatorGeneCopyCompatibility")packages.Add("Ludeon.RimWorld.Anomaly");
   if(g.Name=="QualityBuilderNullMapCompatibility")packages.AddRange(new[]{"hatti.qualitybuilder.fork","hatti.qualitybuilder"});
   bool enabled=packages.Count==0||packages.Any(Active);
   var patched=Harmony.GetAllPatchedMethods().Where(m=>{var p=Harmony.GetPatchInfo(m);return p!=null&&p.Prefixes.Concat(p.Postfixes).Concat(p.Transpilers).Concat(p.Finalizers).Any(x=>x.owner==Owner&&x.PatchMethod.DeclaringType==g);}).ToArray();
   int expected=g.Name switch {"InvokeHoraxOfferingCompatibility"=>2,"GoodwillSituationManagerThreadSafetyCompatibility"=>2,"RimStoryMassFuneralCompatibility"=>2,"TacticalGroupsColonyGroupIdCollisionCompatibility"=>5,_=>1};
   if(enabled)Check(patched.Length>=expected,"active group must register every concrete target, not silently/partially skip: "+g.Name);
   else
   {
    Check(patched.Length==0,"inactive optional group must not register "+g.Name);
    Check(!(bool)g.GetMethod("TryApply",Flags).Invoke(null,new object[]{new Harmony("rw17.mod79.optional-negative")}),"independent absent optional gate false "+g.Name);
   }
   ledger.Add(new {group=g.Name,packages,enabled,targets=patched.Select(m=>m.ToString()).ToArray(),scope=enabled?"concrete registration; business checks separately":"actual optional absence gate/no patch; not a functional pass"});
  }
  var owned=new List<Thing>();
  Pawn MakePawn() { var p=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);owned.Add(p);return p; }
  IntVec3 Cell()=>map.AllCells.First(c=>c.InBounds(map)&&c.Standable(map)&&c.GetThingList(map).Count==0&&!c.Fogged(map));
  try
  {
   Pawn pawn=MakePawn();GenSpawn.Spawn(pawn,Cell(),map);
   Registered(AccessTools.Method(typeof(Pawn_AgeTracker),nameof(Pawn_AgeTracker.LifeStageMinAge)),"LifeStageMinAgeFallbackCompatibility");
   var stage=pawn.RaceProps.lifeStageAges[0];Check(pawn.ageTracker.LifeStageMinAge(stage.def)==stage.minAge,"real existing life stage retains native value");
   Check(pawn.ageTracker.LifeStageMinAge(new LifeStageDef {defName="RW17MissingLifeStage79"})==0,"actual absent life stage graceful value");
   var bed=(Building_Bed)ThingMaker.MakeThing(ThingDef.Named("Bed"),ThingDefOf.WoodLog);owned.Add(bed);var anchor=map.AllCells.First(c=>GenAdj.OccupiedRect(c,Rot4.North,bed.def.size).All(z=>z.InBounds(map)&&z.GetThingList(map).Count==0&&z.Standable(map)));GenSpawn.Spawn(bed,anchor,map);
   bed.GetComp<CompAssignableToPawn_Bed>().TryAssignPawn(pawn);
   Registered(AccessTools.Method(typeof(RestUtility),nameof(RestUtility.GetBedSleepingSlotPosFor)),"SleepingSlotFallbackCompatibility");
   Check(RestUtility.GetBedSleepingSlotPosFor(pawn,bed)==bed.GetSleepingSlotPos(0),"real assigned bed native slot");
   var other=MakePawn();Check(RestUtility.GetBedSleepingSlotPosFor(other,bed)==bed.GetSleepingSlotPos(0),"real full assigned bed fallback without native red error");
   if(ModsConfig.RoyaltyActive)
   {
    var shuttle=ThingMaker.MakeThing(TransportShipDefOf.Ship_Shuttle.shipThing);owned.Add(shuttle);var sc=map.AllCells.First(c=>GenAdj.OccupiedRect(c,Rot4.North,shuttle.def.size).All(z=>z.InBounds(map)&&z.Standable(map)&&z.GetThingList(map).Count==0));GenSpawn.Spawn(shuttle,sc,map);
    var ship=TransportShipMaker.MakeTransportShip(TransportShipDefOf.Ship_Shuttle,null,shuttle);
    try
    {
     Registered(AccessTools.Method(typeof(ShipJob_Wait),nameof(ShipJob_Wait.TickInterval)),"ShipJobWaitDespawnedThingsCompatibility");
     var wait=(ShipJob_WaitTime)ShipJobMaker.MakeShipJob(ShipJobDefOf.WaitTime);wait.transportShip=ship;wait.duration=9999;wait.sendAwayIfAllDespawned=new List<Thing>{pawn};Check(wait.TryStart(),"real native wait starts on spawned shuttle");wait.TickInterval(60);Check(wait.jobState==ShipJobState.Working,"real same-map present requirement prevents send-away");
     wait.sendAwayIfAllDespawned=new List<Thing>{other};wait.TickInterval(60);Check(wait.jobState==ShipJobState.Ended,"actual mapless requirement safely allows real SendAway instead of null map dereference");
    }
    finally {Find.TransportShipManager.DeregisterShipObject(ship);}
   }
   Music();
   var cursor=AccessTools.Field(typeof(ListerHaulables),"groupCycleIndex");var cells=AccessTools.Field(typeof(ListerHaulables),"cellCycleIndices");
   Registered(AccessTools.Method(typeof(ListerHaulables),nameof(ListerHaulables.ListerHaulablesTick)),"ListerHaulablesSharedScanCursorCompatibility");
   int global=(int)cursor.GetValue(null);var saved=((List<int>)cells.GetValue(null)).ToArray();
   try {var a=new ListerHaulables(map);var b=new ListerHaulables(map);a.ListerHaulablesTick();Check((int)cursor.GetValue(null)==1,"first real instance cursor");b.ListerHaulablesTick();Check((int)cursor.GetValue(null)==1,"second real instance independent cursor");a.ListerHaulablesTick();Check((int)cursor.GetValue(null)==2,"first real instance continues own cursor");}
   finally {cursor.SetValue(null,global);var list=(List<int>)cells.GetValue(null);list.Clear();list.AddRange(saved);}
   if(ModsConfig.AnomalyActive)
   {
    Pawn donor=MakePawn(),copy=MakePawn();
    var x=donor.genes.AddGene(DefDatabase<GeneDef>.GetNamed("Aggression_Aggressive"),true);var e=donor.genes.AddGene(DefDatabase<GeneDef>.GetNamed("KindInstinct"),false);x.overriddenByGene=e;
    var method=AccessTools.Method(typeof(GameComponent_PawnDuplicator),"CopyGenes",new[]{typeof(Pawn),typeof(Pawn)});Registered(method,"PawnDuplicatorGeneCopyCompatibility");method.Invoke(null,new object[]{donor,copy});
    var copiedX=copy.genes.Xenogenes.First(g=>g.def==x.def);Check(copiedX.overriddenByGene==copy.genes.Endogenes.First(g=>g.def==e.def),"real duplicator copies both lists before linking cross-list override");
    Pawn invoker=pawn;
    for(int attempt=0;invoker.GetStatValue(StatDefOf.PsychicSensitivity)<=0f && attempt<20;attempt++) {invoker=MakePawn();GenSpawn.Spawn(invoker,Cell(),map);}
    Check(invoker.GetStatValue(StatDefOf.PsychicSensitivity)>0f,"actual role fixture must be psychically sensitive");
    var role=new PsychicRitualRoleDef {defName="RW17Invoker79"};var assignments=new PsychicRitualRoleAssignments(new List<PsychicRitualRoleDef>{role},new TargetInfo(invoker));Check(assignments.TryAssign(invoker,role,out var reason),"real invoker assignment: "+reason.ToPlayerReadable());
    var ingredient=new IngredientCount();ingredient.SetBaseCount(3);ingredient.filter.SetAllow(ThingDefOf.Steel,true);
    var ritual=new PsychicRitual {assignments=assignments};var toil=new PsychicRitualToil_InvokeHorax(role,new[]{invoker.Position},null,Array.Empty<IntVec3>(),null,Array.Empty<IntVec3>(),null,Array.Empty<IntVec3>(),ingredient);
    var steel=ThingMaker.MakeThing(ThingDefOf.Steel);steel.stackCount=5;owned.Add(steel);Check(invoker.inventory.innerContainer.TryAdd(steel),"real inventory accepts offerings");
    toil.HoldRequiredOfferings(ritual,null);Check(invoker.carryTracker.CarriedThing?.stackCount==3&&invoker.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.Steel)==2,"actual offering transfer snapshot / remaining inventory");
    toil.ConsumeRequiredOffering(ritual);Check(invoker.carryTracker.CarriedThing==null&&invoker.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.Steel)==2,"actual carried offerings consumed exactly");
   }
   Optional(map,pawn,owned,Cell);
  }
  finally {foreach(var t in owned.AsEnumerable().Reverse())if(!t.Destroyed)t.Destroy(DestroyMode.Vanish);}
  File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"rw17-mod79-groups.json"),JsonSerializer.Serialize(ledger,new JsonSerializerOptions {WriteIndented=true}));
  Console.WriteLine("RW17_MOD79_REAL_CORE_AND_AVAILABLE_OPTIONAL_CHECKS_OK; per-group ledger separates registration/absence from functional evidence");
 }
 private static void Music()
 {
  var manager=new MusicManagerPlay();var choose=AccessTools.Method(typeof(MusicManagerPlay),"ChooseNextSong");Registered(choose,"ZeroWeightSongSelectionCompatibility");
  var defs=DefDatabase<SongDef>.AllDefsListForReading;var snapshots=defs.Select(s=>(s,s.playOnMap,s.tense,s.commonality,s.allowedTimeOfDay,s.allowedSeasons,s.minRoyalTitle)).ToArray();
  var positive=defs.First(s=>s.commonality>0);var zero=defs.First(s=>s!=positive);var recent=(Queue<SongDef>)AccessTools.Field(typeof(MusicManagerPlay),"recentSongs").GetValue(manager);
  try
  {
   foreach(var s in defs)s.playOnMap=false;
   foreach(var s in new[]{positive,zero}) {s.playOnMap=true;s.tense=manager.DangerMusicMode;s.allowedTimeOfDay=TimeOfDay.Any;s.allowedSeasons=null;s.minRoyalTitle=null;}
   positive.commonality=1;zero.commonality=0;recent.Enqueue(positive);
   Check(ReferenceEquals(choose.Invoke(manager,null),positive),"real ChooseNextSong clears zero-only recent filter and returns weighted song");
   Check(recent.Count==0,"real recent queue reset rather than zero-weight random fallback");
  }
  finally {foreach(var (s,play,tense,weight,time,seasons,title) in snapshots){s.playOnMap=play;s.tense=tense;s.commonality=weight;s.allowedTimeOfDay=time;s.allowedSeasons=seasons;s.minRoyalTitle=title;}}
 }
 private static void Optional(Map map,Pawn pawn,List<Thing> owned,Func<IntVec3> cell)
 {
  if(Active("UnlimitedHugs.AllowTool"))
  {
   var giver=AccessTools.TypeByName("AllowTool.WorkGiver_HaulUrgently");var target=AccessTools.Method(giver,"PotentialWorkThingsGlobal");Registered(target,"AllowToolHaulUrgentlyCompatibility");
   var def=(DesignationDef)AccessTools.Field(AccessTools.TypeByName("AllowTool.AllowToolDefOf"),"HaulUrgentlyDesignation").GetValue(null);
   var steel=ThingMaker.MakeThing(ThingDefOf.Steel);steel.stackCount=8;owned.Add(steel);GenSpawn.Spawn(steel,cell(),map);
   var stock=new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,map.zoneManager);map.zoneManager.RegisterZone(stock);var place=cell();stock.AddCell(place);stock.settings.Priority=StoragePriority.Critical;
   var diagnostic=new List<object>();object cache=null;
   try
   {
    Check(HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn,steel,false),"actual valid urgently haulable positive control");
    var controller=AccessTools.Property(AccessTools.TypeByName("AllowTool.AllowToolController"),"Instance").GetValue(null);Check(controller!=null,"actual AllowTool controller initialized");
    cache=AccessTools.Property(controller.GetType(),"HaulUrgentlyCache").GetValue(controller);Check(cache!=null,"actual AllowTool cache initialized");
    var getCache=AccessTools.Method(cache.GetType(),"GetDesignatedAndHaulableThingsForMap",new[]{typeof(Map),typeof(float)});
    var getDesignated=AccessTools.Method(cache.GetType(),"GetDesignatedThingsForMap",new[]{typeof(Map),typeof(float)});
    var instance=Activator.CreateInstance(giver);Thing[] Candidates()=>((IEnumerable<Thing>)target.Invoke(instance,new object[]{pawn})).ToArray();
    void Observe(string phase,Thing[] candidates)
    {
     var actualCache=((IEnumerable<Thing>)getCache.Invoke(cache,new object[]{map,Time.unscaledTime})).ToArray();var designated=((IEnumerable<Thing>)getDesignated.Invoke(cache,new object[]{map,Time.unscaledTime})).ToArray();
     diagnostic.Add(new {phase,time=Time.unscaledTime,thing=steel.ThingID,spawned=steel.Spawned,destroyed=steel.Destroyed,parentHolder=steel.ParentHolder?.GetType().FullName,map=steel.Map?.uniqueID,mapHeld=steel.MapHeld?.uniqueID,pawnMap=pawn.Map?.uniqueID,forbidden=steel.IsForbidden(Faction.OfPlayer),marked=map.designationManager.DesignationOn(steel,def)!=null,listerContains=map.listerHaulables.ThingsPotentiallyNeedingHauling().Contains(steel),cacheType=cache.GetType().FullName,actualCacheContains=actualCache.Contains(steel),actualCache=actualCache.Select(t=>t.ThingID).ToArray(),designatedCacheContains=designated.Contains(steel),safeFilter=(bool)AccessTools.Method(Product("AllowToolHaulUrgentlyCompatibility"),"IsSafeLiveThingForPawn").Invoke(null,new object[]{pawn,steel}),nativeHaulable=steel.Spawned?(bool?)HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn,steel,false):null,providerContains=candidates.Contains(steel),candidates=candidates.Select(t=>t.ThingID).ToArray()});
    }
    Observe("before-mark-native-cache-read",Candidates());
    // Reproduce the former fixture path for evidence; it deliberately omits the native designator's cache invalidation.
    var direct=new Designation(steel,def);map.designationManager.AddDesignation(direct);Observe("direct-AddDesignation-without-cache-notification",Candidates());map.designationManager.RemoveDesignation(direct);
    var designator=(Designator)Activator.CreateInstance(AccessTools.TypeByName("AllowTool.Designator_HaulUrgently"));Check(designator.CanDesignateThing(steel).Accepted,"actual AllowTool designator accepts valid steel");designator.DesignateThing(steel);
    Observe("actual-Designator.DesignateThing-invalidates-map-cache",Candidates());
    Check(map.listerHaulables.ThingsPotentiallyNeedingHauling().Contains(steel),"actual native lister includes valid urgent thing");
    Check(((IEnumerable<Thing>)getCache.Invoke(cache,new object[]{map,Time.unscaledTime})).Contains(steel),"actual native designator notification refreshes urgent cache");
    var patch=Harmony.GetPatchInfo(target).Prefixes.Single(p=>p.owner==Owner&&p.PatchMethod.DeclaringType==Product("AllowToolHaulUrgentlyCompatibility"));var control=new Harmony(patch.owner);Thing[] native;
    control.Unpatch(target,patch.PatchMethod);
    try {native=Candidates();Observe("specific-79-prefix-removed-native-provider",native);}
    finally {control.Patch(target,prefix:new HarmonyMethod(patch.PatchMethod){priority=patch.priority,before=patch.before,after=patch.after});}
    Registered(target,"AllowToolHaulUrgentlyCompatibility");var values=Candidates();Observe("restored-79-prefix-provider",values);
    Check(native.Contains(steel),"unpatched actual AllowTool provider retains the same valid designated candidate");
    Check(values.Contains(steel),"real available AllowTool provider retains designated valid thing, not silent empty cache");
    steel.DeSpawn();var despawned=Candidates();Observe("despawned-negative",despawned);Check(!despawned.Contains(steel),"real despawned urgent thing excluded");
   }
   finally {map.designationManager.RemoveAllDesignationsOn(steel);if(cache!=null)AccessTools.Method(cache.GetType(),"ClearCacheForMap").Invoke(cache,new object[]{map});map.zoneManager.DeregisterZone(stock);File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"rw17-mod79-allowtool-diagnostic.json"),JsonSerializer.Serialize(diagnostic,new JsonSerializerOptions{WriteIndented=true}));}
   var hunt=AccessTools.Method(AccessTools.TypeByName("AllowTool.Patches.JobDriverWait_CheckForAutoAttack_Patch"),"DoPartyHunting");Registered(hunt,"AllowToolPartyHuntWorldSettingsCompatibility");
  }
  if(Active("Memegoddess.ReplaceStuff"))
  {
   var bridge=AccessTools.TypeByName("Replace_Stuff.PlaceBridges.PlaceBridges");var method=AccessTools.Method(bridge,"GetNeededBridge",new[]{typeof(BuildableDef),typeof(IntVec3),typeof(Map),typeof(ThingDef)});Registered(method,"ReplaceStuffBridgeCompatibility");
   Check(method.Invoke(null,new object[]{null,IntVec3.Invalid,null,null})==null,"true current TerrainDef bridge helper invalid context guarded");
   var c=cell();Check(method.Invoke(null,new object[]{ThingDefOf.Wall,c,map,ThingDefOf.Steel})==null,"actual valid heavy ground bridge negative path still runs");
   var terrain=map.terrainGrid.TerrainAt(c);
   try {map.terrainGrid.SetTerrain(c,TerrainDefOf.Mud);Check(ReferenceEquals(method.Invoke(null,new object[]{ThingDefOf.Wall,c,map,ThingDefOf.WoodLog}),TerrainDefOf.Bridge),"actual valid light wooden wall on mud needs bridge positive path retained");}
   finally {map.terrainGrid.SetTerrain(c,terrain);}
   var over=AccessTools.TypeByName("Replace_Stuff.OverMineable.InterceptBlueprintOverMinable");var target=AccessTools.Method(over,"Prefix",new[]{typeof(BuildableDef),typeof(IntVec3),typeof(Map),typeof(Rot4),typeof(Faction)});Registered(target,"ReplaceStuffOverMineableCompatibility");
   object[] args={null,IntVec3.Invalid,null,Rot4.North,Faction.OfPlayer};target.Invoke(null,args);Check(args[0]==null,"actual five-argument overmineable invalid context safe");
   var rock=ThingMaker.MakeThing(ThingDefOf.Granite);owned.Add(rock);Check(rock is Mineable&&rock.def.mineable,"official Granite is a real native mineable rock");
   var rc=map.AllCells.First(z=>z.Standable(map)&&z.GetThingList(map).Count==0&&!z.Fogged(map)&&GenAdj.AdjacentCells.Any(a=>(z+a).InBounds(map)&&(z+a).Walkable(map)));GenSpawn.Spawn(rock,rc,map);
   var blueprint=GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Wall,rc,map,Rot4.North,Faction.OfPlayer,ThingDefOf.Steel);owned.Add(blueprint);
   Check(blueprint!=null&&map.designationManager.DesignationAt(rc,DesignationDefOf.Mine)!=null,"actual native blueprint placement still executes upstream cell mine designation on real rock");
   Check(new WorkGiver_Miner().PotentialWorkThingsGlobal(pawn).Contains(rock),"real native mining provider resolves blueprint-generated cell designation to the same rock");
  }
  if(Active("hatti.qualitybuilder.fork")||Active("hatti.qualitybuilder"))
  {
   var m=AccessTools.Method(AccessTools.TypeByName("QualityBuilder.QualityBuilder"),"GetFirstBuildingBuildingOrFrame",new[]{typeof(Map),typeof(IntVec3)});Registered(m,"QualityBuilderNullMapCompatibility");Check(m.Invoke(null,new object[]{null,IntVec3.Invalid})==null,"actual QualityBuilder null map lookup");
  }
  if(Active("erdelf.HumanoidAlienRaces"))
  {
   var target=AccessTools.Method(AccessTools.TypeByName("AlienRace.HarmonyPatches"),"RulesForPawnPostfix");Registered(target,"AlienRaceNullPawnRulesCompatibility");
   var rules=Array.Empty<Verse.Grammar.Rule>();Check(ReferenceEquals(target.Invoke(null,new object[]{rules,null,"rw17"}),rules),"actual nested HAR postfix null guard retains upstream result");
   Check(Verse.Grammar.GrammarUtility.RulesForPawn("rw17",pawn).Any(r=>r.ToString().Contains("alienRace")),"real outer grammar chain still executes valid HAR rules");
  }
  if(Active("vanillaexpanded.backgrounds"))
  {
   var type=AccessTools.TypeByName("VBE.BackgroundImageDef");var prop=type.GetProperty("Video");Registered(prop.GetMethod,"VbeVideoPathEscapingCompatibility");
   var f=Path.Combine(GenFilePaths.SaveDataFolderPath,"a folder","b #1.webm");var def=Activator.CreateInstance(type,new object[]{f,null});var value=(string)prop.GetValue(def);Check(value.StartsWith("file://")&&new Uri(value).LocalPath==f,"actual VBE getter encodes exact file path for new VideoPlayer URI decoder; playback separately pending");
  }
  if(Active("Kota.InventoryTab"))
  {
   var type=AccessTools.TypeByName("InventoryTab.Slot");var m=AccessTools.Method(type,"CompareTo",new[]{type});Registered(m,"InventoryTabStableSortCompatibility");var t=ThingMaker.MakeThing(ThingDefOf.Steel);owned.Add(t);var ctor=type.GetConstructors().Single();var kind=Enum.ToObject(ctor.GetParameters()[2].ParameterType,0);var a=ctor.Invoke(new object[]{t,"a",kind});var b=ctor.Invoke(new object[]{t,"b",kind});Check((int)m.Invoke(a,new[]{b})<0&&(int)m.Invoke(b,new[]{a})>0,"actual upstream Slot tie comparator deterministic and antisymmetric");
  }
 }
}
