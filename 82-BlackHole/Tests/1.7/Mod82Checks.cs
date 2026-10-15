using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
namespace RW17.Validation;
public static class Mod82Checks
{
 public const string PackageId="RunningBugs.BlackHole";
 public static Thing VisualHole;
 private static Pawn cancelSubject; private static int unloadTrueCalls;
 public static void ObserveUnloadRequest(Pawn_InventoryTracker __instance, bool value)
 {
  if(value && ReferenceEquals(__instance,cancelSubject?.inventory)) unloadTrueCalls++;
 }
 public static void Run(Map map)
 {
  var assembly=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="BlackHole");
  Type Type(string n)=>assembly.GetType("BlackHole."+n,true);
  var compType=Type("CompBlackHole");
  var holeDef=DefDatabase<ThingDef>.GetNamed("BlackHole");
  var hole=(Building)ThingMaker.MakeThing(holeDef);
  var goods=ThingMaker.MakeThing(ThingDefOf.Silver); goods.stackCount=10;
  Pawn hauler=null,cargo=null; Thing cancelItem=null; bool keep=false;
  var observe=new Harmony("RW17.Validation.Mod82.CancelObserve");
  var setter=AccessTools.PropertySetter(typeof(Pawn_InventoryTracker),nameof(Pawn_InventoryTracker.UnloadEverything));
  var observer=AccessTools.Method(typeof(Mod82Checks),nameof(ObserveUnloadRequest));
  Rand.PushState(1782);
  try
  {
   var cell=GenRadial.RadialCellsAround(map.Center,8,true).First(c=>c.InBounds(map)&&c.Standable(map)&&c.GetEdifice(map)==null);
   IntVec3 AwayCell() => GenRadial.RadialCellsAround(cell,7,true).First(c=>c.InBounds(map)&&c.DistanceToSquared(cell)>=9&&c.Standable(map)&&c.GetEdifice(map)==null&&map.reachability.CanReach(c,hole,PathEndMode.Touch,TraverseParms.For(TraverseMode.PassDoors,Danger.Deadly)));
   GenSpawn.Spawn(hole,cell,map); foreach(var c in GenRadial.RadialCellsAround(cell,5,true)) if(c.InBounds(map)) map.fogGrid.Unfog(c);
   hauler=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer); GenSpawn.Spawn(hauler,AwayCell(),map);
   if(!hauler.CanReach(hole,PathEndMode.Touch,Danger.Deadly)) throw new Exception("Mod82: actual spawned hauler cannot reach selected safe hole position");
   GenSpawn.Spawn(goods,hauler.Position,map);
   var comp=hole.AllComps.Single(c=>c.GetType()==compType);
   var add=AccessTools.Method(compType,"AddToTheToLoadList"); var start=AccessTools.Method(compType,"StartLoading"); var cancel=AccessTools.Method(compType,"CancelLoad",new[] { typeof(Map) });
   var queue=AccessTools.Field(compType,"leftToLoad"); var loading=AccessTools.Field(compType,"loadingInProgress");
   var transfer=new TransferableOneWay(); transfer.things.Add(goods); add.Invoke(comp,new object[] { transfer,6 }); start.Invoke(comp,new object[] { new List<Pawn>() });
   if(!(bool)loading.GetValue(comp)||map.lordManager.lords.Any(l=>l.LordJob.GetType().FullName=="BlackHole.LordJob_DevourToBlackHole")) throw new Exception("Mod82: item-only order unexpectedly recruited a Lord");
   var worker=(WorkGiver_Scanner)Activator.CreateInstance(Type("WorkGiver_BlackHole"));
   goods.SetForbidden(true); if(worker.HasJobOnThing(hauler,hole)) throw new Exception("Mod82: forbidden real cargo remained haulable"); goods.SetForbidden(false);
   if(!worker.HasJobOnThing(hauler,hole)) throw new Exception("Mod82: reachable real cargo not offered by workgiver");
   var job=worker.JobOnThing(hauler,hole);
   if(job?.def!=DefDatabase<JobDef>.GetNamed("BlackHoleHaul")||job.targetA.Thing!=goods||job.targetB.Thing!=hole||job.count!=6) throw new Exception("Mod82: real workgiver job lost baked cargo/count");
   hauler.jobs.StartJob(job,JobCondition.InterruptForced);
   var driver=hauler.jobs.curDriver;
   if(driver?.GetType().FullName!="BlackHole.JobDriver_HaulToBlackHole") throw new Exception("Mod82: actual haul driver not active");
   if(hauler.carryTracker.CarriedThing==null && hauler.carryTracker.TryStartCarry(goods,6)!=6) throw new Exception("Mod82: actual six-item carry failed");
   var carried=hauler.carryTracker.CarriedThing;
   if(carried==null||carried.stackCount!=6) throw new Exception("Mod82: actual carried split stack wrong");
   hauler.pather.StopDead(); hauler.Position=CellFinder.RandomClosewalkCellNear(cell,map,1);
   var toils=(List<Toil>)AccessTools.Field(typeof(JobDriver),"toils").GetValue(driver);
   toils.Last().initAction();
   if(!carried.Destroyed||goods.Destroyed||goods.stackCount!=4||hauler.carryTracker.CarriedThing!=null||queue.GetValue(comp)!=null||(bool)loading.GetValue(comp)) throw new Exception("Mod82: actual carry/delivery/devour/completion or leftover stack regressed");
   hauler.jobs.EndCurrentJob(JobCondition.Succeeded,startNewJob:false);
   // Cancellation uses real Lord/duty and must preserve selected objects.
   cargo=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer); GenSpawn.Spawn(cargo,AwayCell(),map);
   if(!cargo.CanReach(hole,PathEndMode.Touch,Danger.Deadly)) throw new Exception("Mod82: actual spawned cargo cannot reach hole from safe cancel distance");
   var waiting=JobMaker.MakeJob(JobDefOf.Wait); waiting.expiryInterval=600; cargo.jobs.StartJob(waiting,JobCondition.InterruptForced);
   void CargoState(string stage) => Console.WriteLine("RW17_MOD82_CARGO_STATE "+JsonSerializer.Serialize(new { stage,destroyed=cargo.Destroyed,spawned=cargo.Spawned,position=cargo.Position.ToString(),hole_position=hole.Position.ToString(),distance_squared=cargo.Position.DistanceToSquared(hole.Position),lord=cargo.GetLord()?.LordJob.GetType().FullName,duty=cargo.mindState?.duty?.def?.defName,duty_focus=cargo.mindState?.duty?.focus.Thing?.thingIDNumber,loading=(bool)loading.GetValue(comp),map_home=map.IsPlayerHome,map_size=map.Size.ToString() }));
   CargoState("before cancel-order start");
   if(cargo.Position.DistanceToSquared(hole.Position)<9) throw new Exception("Mod82: cancel fixture already close enough for immediate entry");
   var pawnTransfer=new TransferableOneWay(); pawnTransfer.things.Add(cargo); add.Invoke(comp,new object[] { pawnTransfer,1 }); start.Invoke(comp,new object[] { new List<Pawn> { cargo } });
   CargoState("after cancel-order start");
   if(cargo.GetLord()?.LordJob.GetType().FullName!="BlackHole.LordJob_DevourToBlackHole"||cargo.mindState.duty?.focus.Thing!=hole) throw new Exception("Mod82: actual cargo Lord/duty missing");
   cargo.inventory.DestroyAll(); cargo.inventory.UnloadEverything=true;
   if(cargo.inventory.UnloadEverything) throw new Exception("Mod82: real empty inventory incorrectly reports unloading");
   cancelItem=ThingMaker.MakeThing(ThingDefOf.Silver); cancelItem.stackCount=2;
   if(!cargo.inventory.innerContainer.TryAdd(cancelItem)) throw new Exception("Mod82: real cancel inventory fixture could not add unloadable Silver");
   bool requestExpected=cargo.IsColonist && map.IsPlayerHome;
   cancelSubject=cargo; unloadTrueCalls=0; observe.Patch(setter,postfix:new HarmonyMethod(observer));
   cancel.Invoke(comp,new object[] { map });
   observe.Unpatch(setter,observer); cancelSubject=null;
   bool itemInInventory=cargo.inventory.innerContainer.Contains(cancelItem);
   Console.WriteLine("RW17_MOD82_CANCEL_VALUES "+JsonSerializer.Serialize(new { pawn_destroyed=cargo.Destroyed,original_goods_destroyed=goods.Destroyed,cancel_item_destroyed=cancelItem.Destroyed,lord_removed=cargo.GetLord()==null,queue_null=queue.GetValue(comp)==null,loading=(bool)loading.GetValue(comp),map_home=map.IsPlayerHome,colonist=cargo.IsColonist,request_expected=requestExpected,observed_true_requests=unloadTrueCalls,unload_getter=cargo.inventory.UnloadEverything,item_in_inventory=itemInInventory,item_spawned=cancelItem.Spawned }));
   if(cargo.Destroyed||goods.Destroyed||cancelItem.Destroyed) throw new Exception("Mod82: actual cancellation destroyed selected Pawn or cargo");
   if(cargo.GetLord()!=null||queue.GetValue(comp)!=null||(bool)loading.GetValue(comp)) throw new Exception("Mod82: cancellation did not clean actual Lord/queue/loading");
   if(unloadTrueCalls!=(requestExpected?1:0)) throw new Exception("Mod82: actual cancellation unload request differs from colonist/home contract");
   if(requestExpected && itemInInventory && !cargo.inventory.UnloadEverything) throw new Exception("Mod82: actual unloadable Silver retained but unloading not active");
   if(!itemInInventory && (!cancelItem.Spawned || cancelItem.Map!=map)) throw new Exception("Mod82: cancel inventory cargo left ownership without being unloaded safely");
   if(itemInInventory) cargo.inventory.innerContainer.Remove(cancelItem); if(!cancelItem.Destroyed) cancelItem.Destroy();
   cargo.inventory.UnloadEverything=false; pawnTransfer=new TransferableOneWay(); pawnTransfer.things.Add(cargo); add.Invoke(comp,new object[] { pawnTransfer,1 });
   cargo.pather.StopDead(); cargo.Position=AwayCell();
   CargoState("before positive enter-order start");
   waiting=JobMaker.MakeJob(JobDefOf.Wait); waiting.expiryInterval=600; cargo.jobs.StartJob(waiting,JobCondition.InterruptForced); start.Invoke(comp,new object[] { new List<Pawn> { cargo } });
   var giver=Activator.CreateInstance(Type("JobGiver_EnterBlackHole")); var enter=(Job)AccessTools.Method(giver.GetType(),"TryGiveJob").Invoke(giver,new object[] { cargo });
   if(enter?.def!=DefDatabase<JobDef>.GetNamed("BlackHoleEnter")||enter.targetA.Thing!=hole) throw new Exception("Mod82: actual cargo duty did not issue enter job");
   cargo.jobs.StartJob(enter,JobCondition.InterruptForced); var entering=cargo.jobs.curDriver;
   if(!cargo.Destroyed)
   {
    cargo.pather.StopDead(); cargo.Position=CellFinder.RandomClosewalkCellNear(cell,map,1);
    var entryToils=(List<Toil>)AccessTools.Field(typeof(JobDriver),"toils").GetValue(entering); entryToils.Last().initAction();
   }
   if(!cargo.Destroyed||cargo.Spawned||queue.GetValue(comp)!=null||(bool)loading.GetValue(comp)||map.lordManager.lords.Any(l=>l.LordJob.GetType().FullName=="BlackHole.LordJob_DevourToBlackHole")) throw new Exception("Mod82: actual selected test Pawn entry did not vanish/complete/remove Lord");
   if(Environment.GetEnvironmentVariable("RW17_BH_RENDER")=="1")
   {
    var graphic=hole.Graphic;
    if(graphic.GetType().FullName!="BlackHole.Graphic_BlackHole" || !(bool)AccessTools.Field(graphic.GetType(),"shaderMode").GetValue(graphic) || graphic.MatSingle.shader.name!="BlackHole/GalaxyTwinkle" || !graphic.MatSingle.shader.isSupported) throw new Exception("Mod82: real Vulkan fixture selected fallback/unsupported shader");
    Find.CameraDriver.SetRootPosAndSize(hole.DrawPos,14f); VisualHole=hole; keep=true;
    Console.WriteLine("RW17_MOD82_RENDER_FIXTURE_READY; real custom shader selected; awaiting paused real-time captures");
   }
   Console.WriteLine("RW17_MOD82_REAL_CARRY_SPLIT_DEVOUR_CANCEL_AND_PAWN_ENTRY_OK; full walking/dialog/save reload remain pending");
  }
  finally
  {
   observe.Unpatch(setter,observer); cancelSubject=null; if(cancelItem!=null&&!cancelItem.Destroyed) cancelItem.Destroy();
   if(hauler!=null) { hauler.jobs.StopAll(); if(!hauler.Destroyed) hauler.Destroy(); if(hauler.IsWorldPawn()) Find.WorldPawns.RemovePawn(hauler); }
   if(cargo!=null) { if(!cargo.Destroyed) cargo.Destroy(); if(cargo.IsWorldPawn()) Find.WorldPawns.RemovePawn(cargo); }
   if(!goods.Destroyed) goods.Destroy(); if(!keep&&!hole.Destroyed) hole.Destroy(); Rand.PopState();
  }
 }
}
public sealed class Mod82VisualProbe : MapComponent
{
 private float started=-1f; private int shots; private int tick1; private float draw1; private bool done;
 public Mod82VisualProbe(Map map):base(map) { }
 public override void MapComponentUpdate()
 {
  if(done||Environment.GetEnvironmentVariable("RW17_BH_RENDER")!="1"||Mod82Checks.VisualHole==null) return;
  var dir=GenFilePaths.SaveDataFolderPath;
  if(started<0)
  {
   if(!Current.Game.PlayerHasControl) return;
   Find.TickManager.Pause(); if(Find.TickManager.CurTimeSpeed!=TimeSpeed.Paused) throw new Exception("Mod82: actual pause request rejected"); started=Time.realtimeSinceStartup;
  }
  var elapsed=Time.realtimeSinceStartup-started;
  var graphic=Mod82Checks.VisualHole.Graphic;
  var props=AccessTools.Field(graphic.GetType(),"propsBlock").GetValue(null) as MaterialPropertyBlock;
  if(props!=null && ((shots==0&&elapsed>=1f)||(shots==1&&elapsed>=2.5f)))
  {
   var draw=props.GetFloat("_RealTimeSecs");
   if(shots==0) { tick1=Find.TickManager.TicksGame; draw1=draw; }
   else if(Find.TickManager.TicksGame!=tick1||draw<=draw1) { Log.Error("Mod82: paused tick/real draw uniform did not stay fixed/advance"); done=true; File.WriteAllText(Path.Combine(dir,"rw17-finish-test"),"failed"); return; }
   ScreenCapture.CaptureScreenshot(Path.Combine(dir,shots==0?"blackhole-a.png":"blackhole-b.png")); shots++;
   File.WriteAllText(Path.Combine(dir,"rw17-blackhole-render.json"),JsonSerializer.Serialize(new { custom_shader=graphic.MatSingle.shader.name,shader_supported=graphic.MatSingle.shader.isSupported,paused=Find.TickManager.CurTimeSpeed==TimeSpeed.Paused,tick1,tick2=Find.TickManager.TicksGame,draw_time1=draw1,draw_time2=draw,elapsed,captures_requested=shots,scope="real rendered-frame screenshot requests; root must inspect pixels/animation" },new JsonSerializerOptions { WriteIndented=true }));
  }
  if(shots==2 && File.Exists(Path.Combine(dir,"blackhole-a.png")) && File.Exists(Path.Combine(dir,"blackhole-b.png"))) { done=true; Console.WriteLine("RW17_MOD82_TWO_PAUSED_REAL_TIME_CAPTURES_READY"); File.WriteAllText(Path.Combine(dir,"rw17-finish-test"),"complete"); }
  else if(elapsed>12f) { done=true; Log.Error("Mod82: real rendered screenshot capture timed out"); File.WriteAllText(Path.Combine(dir,"rw17-finish-test"),"failed"); }
 }
}
