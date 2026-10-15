using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text.Json;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace RW17.Validation;

public static class Mod32Checks
{
    public const string PackageId="RunningBugs.PDE.DontStarve";
    static int fuelTicks;static ThingWithComps observedInner;static readonly List<FleckCreationData> lights=new();
    static void FuelObserver(ThingComp __instance){if(__instance.parent==observedInner)fuelTicks++;}
    static void FleckObserver(FleckCreationData fleckData){if(fleckData.def.defName=="LanternGlowFleck")lights.Add(fleckData);}
    static void Check(bool ok,string why){if(!ok)throw new Exception("Mod32: "+why);}
    static Type T(string name)=>AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="PDEDontStarve").GetType("PDEDontStarve."+name,true);
    static IntVec3 Empty(Map map)=>map.AllCells.Where(c=>c.x>25&&c.z>25&&c.x<map.Size.x-25&&c.z<map.Size.z-25&&c.Standable(map)&&!c.Fogged(map)&&!c.Roofed(map)&&c.GetThingList(map).Count==0).OrderBy(c=>c.DistanceToSquared(map.Center)).First();
    public static void Run(Map map)
    {
        Check(LoadedModManager.RunningMods.Any(m=>m.PackageId.Equals("runningbugs.permanentdarkness",StringComparison.OrdinalIgnoreCase)),"actual29 dependency missing");
        var oldPlants=map.listerThings.ThingsOfDef(ThingDef.Named("Plant_Fireflies")).ToArray();var fixtures=new List<Thing>();var harmony=new Harmony("RW17.Mod32.observer");
        var fuelMethod=AccessTools.Method(T("CompPausibleRefuelable"),"CompTick");var fleckMethod=AccessTools.Method(typeof(FleckManager),"CreateFleck",new[]{typeof(FleckCreationData)});
        var fuelObserver=AccessTools.Method(typeof(Mod32Checks),nameof(FuelObserver));var fleckObserver=AccessTools.Method(typeof(Mod32Checks),nameof(FleckObserver));
        var tick=AccessTools.Field(typeof(TickManager),"ticksGameInt");object oldTick=tick.GetValue(Find.TickManager);
        try
        {
            Check(T("CompProperties_HasLightBulb").GetField("lightDef").FieldType==typeof(FleckDef),"archive1.6 FleckDef contract mismatch");
            var pawn=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);fixtures.Add(pawn);GenSpawn.Spawn(pawn,Empty(map),map);
            foreach(var equipment in pawn.equipment.AllEquipmentListForReading.ToArray()){pawn.equipment.Remove(equipment);equipment.Destroy();}
            var lantern=(MinifiedThing)ThingMaker.MakeThing(ThingDef.Named("MeleeWeapon_Lantern"),ThingDefOf.Steel);fixtures.Add(lantern);
            Check(lantern.GetType()==T("LanternWeapon")&&lantern.InnerThing?.def==ThingDef.Named("Building_Lantern"),"real Minified inner assembly");
            observedInner=(ThingWithComps)lantern.InnerThing;var fuel=observedInner.TryGetComp<CompRefuelable>();Check(fuel!=null&&fuel.Fuel>0,"actual native refuelable fixture");
            pawn.equipment.AddEquipment(lantern);Check(lantern.ParentHolder==pawn.equipment&&lantern.InnerThing.ParentHolder==lantern,"actual owner chain");
            harmony.Patch(fuelMethod,postfix:new HarmonyMethod(fuelObserver));harmony.Patch(fleckMethod,postfix:new HarmonyMethod(fleckObserver));
            fuelTicks=0;lights.Clear();float initial=fuel.Fuel;pawn.DoTick();
            Check(fuelTicks==1&&Math.Abs((initial-fuel.Fuel)-fuel.Props.fuelConsumptionRate/60000f)<0.0000002f,"parent DoTick must call inner fuel comp exactly once");
            Check(lights.Count==1&&new Vector2(lights[0].spawnPosition.x-pawn.DrawPos.x,lights[0].spawnPosition.z-pawn.DrawPos.z).sqrMagnitude<0.001f,"equipped real fleck count/position");
            pawn.Position=Empty(map);lights.Clear();fuelTicks=0;pawn.DoTick();Check(fuelTicks==1&&lights.Count==1&&new Vector2(lights[0].spawnPosition.x-pawn.DrawPos.x,lights[0].spawnPosition.z-pawn.DrawPos.z).sqrMagnitude<0.001f,"moving equipped light must follow pawn once");
            pawn.equipment.Remove(lantern);GenSpawn.Spawn(lantern,Empty(map),map);lights.Clear();fuelTicks=0;lantern.DoTick();Check(lights.Count==0&&fuelTicks==1,"ground outer no portable fleck, native inner once");
            Check(!map.listerThings.AllThings.Any(t=>t.def.defName=="LanternGlowFleck"),"fleck must not leak spawned Thing lights");
            var lightDef=DefDatabase<FleckDef>.GetNamed("LanternGlowFleck");var system=map.flecks.Systems.Single(s=>s.handledDefs.Contains(lightDef));
            var gameData=(List<FleckStatic>)AccessTools.Field(system.GetType(),"dataGametime").GetValue(system);var realtimeData=(List<FleckStatic>)AccessTools.Field(system.GetType(),"dataRealtime").GetValue(system);
            Check(gameData.Concat(realtimeData).Any(f=>f.def==lightDef),"actual fleck-system light records missing");for(int i=0;i<20;i++){system.Tick();system.Update(1f/60f);}Check(!gameData.Concat(realtimeData).Any(f=>f.def==lightDef),"actual short-lived fleck cleanup");
            var stepDef=DefDatabase<GenStepDef>.GetNamed("PDE_FireFlies");var step=(GenStep_SpecialTrees)stepDef.genStep;
            Check(DefDatabase<MapGeneratorDef>.GetNamed("Base_Player").genSteps.Contains(stepDef)&&oldPlants.Length>0,"real map generation patch and generated fireflies missing");
            int before=map.listerThings.ThingsOfDef(ThingDef.Named("Plant_Fireflies")).Count;step.Generate(map,default);Check(map.listerThings.ThingsOfDef(ThingDef.Named("Plant_Fireflies")).Count>before,"actual custom GenStep.Generate did not generate additional plants");
            var seed=(ThingWithComps)ThingMaker.MakeThing(ThingDef.Named("FirefliesSeed"));fixtures.Add(seed);GenSpawn.Spawn(seed,Empty(map),map);var plantable=seed.TryGetComp<CompPlantable>();Check(plantable!=null,"real active vanilla plantable comp");
            var cell=map.AllCells.First(c=>c.InBounds(map)&&c.Standable(map)&&!c.Fogged(map)&&c.GetThingList(map).Count==0&&plantable.CanPlantAt(c,map).Accepted);plantable.PlantCells.Add(cell);Check(plantable.CompGetGizmosExtra().OfType<Command_Action>().Any(),"actual plant command list");
            int priorSown=pawn.records.GetAsInt(RecordDefOf.PlantsSown);plantable.DoPlant(pawn,cell,map);var planted=(Plant)cell.GetThingList(map).Single(t=>t.def==ThingDef.Named("Plant_Fireflies"));Check(seed.Destroyed&&planted.sown&&planted.Growth>0&&pawn.records.GetAsInt(RecordDefOf.PlantsSown)==priorSown+1,"actual seed→plant→record/consumption");
            var glower=planted.AllComps.Single(c=>c.GetType()==T("CompGlowerFireFly"));var glowing=T("CompGlowerFireFly").GetField("glowing");
            pawn.Position=planted.Position+IntVec3.North;for(int i=0;i<60;i++){tick.SetValue(Find.TickManager,(int)oldTick+i);planted.DoTick();}
            Check(!(bool)glowing.GetValue(glower),"actual nearby-human dimming");pawn.Position=map.AllCells.First(c=>c.Standable(map)&&!c.InHorDistOf(planted.Position,15)&&c.GetThingList(map).Count==0);
            for(int i=0;i<180;i++){tick.SetValue(Find.TickManager,(int)oldTick+60+i);planted.DoTick();}Check((bool)glowing.GetValue(glower),"actual dim cooldown recovery");
            Log.Message("RW17_MOD32_NATIVE_OWNERSHIP_SINGLE_TICK_FLECK_POSITION_FUEL_GENSTEP_PLANT_AND_DIM_PASSED");
        }
        finally
        {
            harmony.Unpatch(fuelMethod,fuelObserver);harmony.Unpatch(fleckMethod,fleckObserver);tick.SetValue(Find.TickManager,oldTick);observedInner=null;lights.Clear();
            foreach(var plant in map.listerThings.ThingsOfDef(ThingDef.Named("Plant_Fireflies")).Except(oldPlants).ToArray())if(!plant.Destroyed)plant.Destroy();
            foreach(var item in fixtures.AsEnumerable().Reverse())if(!item.Destroyed)item.Destroy(DestroyMode.Vanish);
        }
    }
    public static void SaveReload(Map map,string mode)
    {
        Check(Scribe.mode==LoadSaveMode.Inactive,"inactive Scribe prerequisite");Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
        string state=Path.Combine(GenFilePaths.SaveDataFolderPath,"rw17-save-state.json"),result=Path.Combine(GenFilePaths.SaveDataFolderPath,"rw17-save-result.json");
        if(mode=="save")
        {
            var pawn=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);GenSpawn.Spawn(pawn,Empty(map),map);foreach(var e in pawn.equipment.AllEquipmentListForReading.ToArray()){pawn.equipment.Remove(e);e.Destroy();}
            var lantern=(MinifiedThing)ThingMaker.MakeThing(ThingDef.Named("MeleeWeapon_Lantern"),ThingDefOf.Steel);pawn.equipment.AddEquipment(lantern);var fuel=lantern.InnerThing.TryGetComp<CompRefuelable>();fuel.ConsumeFuel(fuel.Fuel-0.375f);
            var seed=(ThingWithComps)ThingMaker.MakeThing(ThingDef.Named("FirefliesSeed"));GenSpawn.Spawn(seed,Empty(map),map);var destination=Empty(map);seed.TryGetComp<CompPlantable>().PlantCells.Add(destination);
            var plant=(Plant)ThingMaker.MakeThing(ThingDef.Named("Plant_Fireflies"));GenSpawn.Spawn(plant,Empty(map),map);plant.Growth=1;var dim=plant.AllComps.Single(c=>c.GetType()==T("CompGlowerFireFly"));AccessTools.Field(dim.GetType(),"lastDimTick").SetValue(dim,GenTicks.TicksGame);dim.GetType().GetField("glowing").SetValue(dim,false);
            File.WriteAllText(state,JsonSerializer.Serialize(new{schema="mod32-1.7",map_id=map.uniqueID,pawn_id=pawn.thingIDNumber,lantern_id=lantern.thingIDNumber,inner_id=lantern.InnerThing.thingIDNumber,fuel=0.375f,seed_id=seed.thingIDNumber,x=destination.x,z=destination.z,plant_id=plant.thingIDNumber,last_dim_tick=GenTicks.TicksGame}));
            GameDataSaveLoader.SaveGame("rw17-mod32-fixture");string path=GenFilePaths.FilePathForSavedGame("rw17-mod32-fixture");Check(File.Exists(path)&&new FileInfo(path).Length>10000,"actual whole-game save missing");
            File.WriteAllText(result,JsonSerializer.Serialize(new{passed=true,mode,save=path,expected=state,scope="same-version whole-game save; reload still required"}));
        }
        else
        {
            Check(mode=="reload","unknown mode");using var doc=JsonDocument.Parse(File.ReadAllText(state));var e=doc.RootElement;Check(e.GetProperty("schema").GetString()=="mod32-1.7"&&map.uniqueID==e.GetProperty("map_id").GetInt32(),"wrong state/map");
            var pawn=map.mapPawns.AllPawnsSpawned.Single(p=>p.thingIDNumber==e.GetProperty("pawn_id").GetInt32());var lantern=(MinifiedThing)pawn.equipment.Primary;
            Check(lantern.thingIDNumber==e.GetProperty("lantern_id").GetInt32()&&lantern.InnerThing.thingIDNumber==e.GetProperty("inner_id").GetInt32()&&lantern.ParentHolder==pawn.equipment&&lantern.InnerThing.ParentHolder==lantern,"minified/equipment/inner IDs and ownership lost");
            Check(Math.Abs(lantern.InnerThing.TryGetComp<CompRefuelable>().Fuel-e.GetProperty("fuel").GetSingle())<0.0001f,"fuel non-default value lost");
            var seed=(ThingWithComps)map.listerThings.AllThings.Single(t=>t.thingIDNumber==e.GetProperty("seed_id").GetInt32());Check(seed.TryGetComp<CompPlantable>().PlantCells.SequenceEqual(new[]{new IntVec3(e.GetProperty("x").GetInt32(),0,e.GetProperty("z").GetInt32())}),"actual vanilla seed plantCells lost");
            var plant=(ThingWithComps)map.listerThings.AllThings.Single(t=>t.thingIDNumber==e.GetProperty("plant_id").GetInt32());var dim=plant.AllComps.Single(c=>c.GetType()==T("CompGlowerFireFly"));Check(!(bool)dim.GetType().GetField("glowing").GetValue(dim)&&(int)AccessTools.Field(dim.GetType(),"lastDimTick").GetValue(dim)==e.GetProperty("last_dim_tick").GetInt32(),"firefly saved dim/glowing fields lost");
            File.WriteAllText(result,JsonSerializer.Serialize(new{passed=true,mode,assertions=5,scope="same-version new-process whole-game reload"}));
        }
        Log.Message("RW17_SAVE_RELOAD_"+mode.ToUpperInvariant()+"_OK");
    }

}
