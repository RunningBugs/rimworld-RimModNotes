using System;
using System.Collections;
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

public static class Mod78Checks
{
    public const string PackageId="RunningBugs.RimFlix.AnimeShows";
    static void Check(bool ok,string why){if(!ok)throw new Exception("Mod78: "+why);}
    public static void Run(Map map)
    {
        Check(LoadedModManager.RunningMods.Any(m=>m.PackageId.Equals("zal.rimflix",StringComparison.OrdinalIgnoreCase)),"actual RimFlix dependency not active");
        var asm=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="RimFlix");var type=asm.GetType("RimFlix.ShowDef",true);var screenType=asm.GetType("RimFlix.CompScreen",true);
        var db=typeof(DefDatabase<>).MakeGenericType(type);var named=db.GetMethod("GetNamed",new[]{typeof(string),typeof(bool)});
        var settingsType=asm.GetType("RimFlix.RimFlixSettings",true);var settingsMod=LoadedModManager.ModHandles.Single(m=>m.GetType().Assembly==asm);var settings=AccessTools.Field(settingsMod.GetType(),"settings").GetValue(settingsMod);
        var playAlways=settingsType.GetField("PlayAlways");object oldPlay=playAlways.GetValue(settings);playAlways.SetValue(settings,true);
        var fixtures=new List<Thing>();var outcomes=new List<object>();
        bool renderRequested=Environment.GetEnvironmentVariable("RW17_ANIME78_RENDER")=="1",retainForRender=false;
        IntVec3 renderAnchor=IntVec3.Invalid;int tvSlot=0;
        if(renderRequested)renderAnchor=map.AllCells.OrderBy(c=>c.DistanceToSquared(map.Center)).First(c=>new[]{"TubeTelevision","FlatscreenTelevision","MegascreenTelevision"}.SelectMany((name,i)=>GenAdj.OccupiedRect(c+new IntVec3(-5+i*5,0,0),Rot4.South,ThingDef.Named(name).size).Cells).All(x=>x.InBounds(map)&&x.Standable(map)&&!x.Fogged(map)&&x.GetThingList(map).Count==0));
        try
        {
            foreach(var row in new[]{("hypnosis_tube","TubeTelevision",157,128),("hypnosis_flat","FlatscreenTelevision",620,256),("hypnosis_mega","MegascreenTelevision",902,256)})
            {
                var show=(Def)named.Invoke(null,new object[]{row.Item1,true});var frames=(List<GraphicData>)type.GetField("frames").GetValue(show);var tvDefs=(IList)type.GetField("televisionDefs").GetValue(show);
                Check(show.modContentPack.PackageId.Equals(PackageId,StringComparison.OrdinalIgnoreCase)&&frames.Count==60&&Math.Abs((float)type.GetField("secondsBetweenFrames").GetValue(show)-0.25f)<0.0001f&&tvDefs.Count==1&&((ThingDef)tvDefs[0]).defName==row.Item2,"actual loaded ShowDef owner/frames/timing/TV schema");
                // Read all actual frame textures on the game main thread, not just path existence.
                bool variedFrame=false;
                foreach(var frame in frames){var tex=ContentFinder<Texture2D>.Get(frame.texPath,true);Check(tex!=BaseContent.BadTex&&tex.width==row.Item3&&tex.height==row.Item4,"actual frame texture decode/dimensions "+frame.texPath);var pixels=tex.GetPixels32();Check(pixels.Length==row.Item3*row.Item4&&pixels.Any(p=>p.a>0),"actual frame pixels missing "+frame.texPath);variedFrame|=pixels.Distinct().Count()>1;}
                Check(variedFrame,"entire show must contain actual varied decoded image data");
                var television=(ThingWithComps)ThingMaker.MakeThing(ThingDef.Named(row.Item2));fixtures.Add(television);television.SetFaction(Faction.OfPlayer);
                var cell=renderRequested?renderAnchor+new IntVec3(-5+tvSlot++*5,0,0):map.AllCells.Where(c=>c.x>10&&c.z>10).First(c=>GenAdj.OccupiedRect(c,Rot4.South,television.def.size).Cells.All(x=>x.InBounds(map)&&x.Standable(map)&&!x.Fogged(map)&&x.GetThingList(map).Count==0));GenSpawn.Spawn(television,cell,map,Rot4.South);television.GetComp<CompPowerTrader>().PowerOn=true;
                var screen=television.AllComps.Single(c=>c.GetType()==screenType);var shows=(IList)screenType.GetProperty("Shows").GetValue(screen);Check(shows.Contains(show)&&shows.Cast<Def>().All(s=>((IList)type.GetField("televisionDefs").GetValue(s)).Contains(television.def)),"real TV Shows selection/filter");
                screenType.GetMethod("ChangeShow",new[]{type}).Invoke(screen,new object[]{show});Check((bool)AccessTools.Method(screenType,"IsPlaying").Invoke(screen,null),"real South/power/PlayAlways playback entry");
                var index=AccessTools.Field(screenType,"frameIndex");Check((int)index.GetValue(screen)==0,"initial frame index");int ticks=GenTicks.SecondsToTicks(0.25f)+1;for(int i=0;i<ticks;i++)television.DoTick();Check((int)index.GetValue(screen)==1,"actual TV DoTick frame progression");
                var graphic=(Graphic)AccessTools.Property(screenType,"FrameGraphic").GetValue(screen);Check(graphic!=null&&graphic.MatSingle.mainTexture.width==row.Item3&&graphic.MatSingle.mainTexture.height==row.Item4,"actual TV FrameGraphic texture path/size");
                television.GetComp<CompPowerTrader>().PowerOn=false;int before=(int)index.GetValue(screen);for(int i=0;i<ticks;i++)television.DoTick();Check((int)index.GetValue(screen)==before,"unpowered TV playback negative control");
                television.GetComp<CompPowerTrader>().PowerOn=true;television.Rotation=Rot4.North;for(int i=0;i<ticks;i++)television.DoTick();Check((int)index.GetValue(screen)==before,"wrongrotation playback negative control");
                outcomes.Add(new{show=row.Item1,tv=row.Item2,frames=60,width=row.Item3,height=row.Item4,progressed_to=before,real_graphic=true,rendered=false});
            }
            File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"rw17-mod78-content.json"),JsonSerializer.Serialize(new{passed=true,upstream_assembly=asm.Location,shows=outcomes,rendering="not covered by this native content/TV tick test"}));
            Log.Message("RW17_MOD78_THREE_REAL_TV_SHOWS_180_TEXTURES_AND_FRAME_TICK_CONTROLS_PASSED");
            if(renderRequested)
            {
                foreach(var tv in fixtures){tv.Rotation=Rot4.South;tv.TryGetComp<CompPowerTrader>().PowerOn=true;}
                playAlways.SetValue(settings,true);Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
                Current.Game.CurrentMap=map;Find.CameraDriver.JumpToCurrentMapLoc(renderAnchor);
                File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"rw17-mod78-render-ready.json"),JsonSerializer.Serialize(new{ready=true,anchor=new{x=renderAnchor.x,z=renderAnchor.z},tv=fixtures.Select(t=>new{id=t.thingIDNumber,def=t.def.defName,x=t.Position.x,z=t.Position.z,rotation=t.Rotation.AsInt,power=t.TryGetComp<CompPowerTrader>().PowerOn}).ToArray(),scope="nativechecks passed; three actual TV retained only forRoot GPU PostDraw capture; paused frame display, no animation-speed claim"}));
                retainForRender=true;Log.Message("RW17_MOD78_RENDER_TV_READY");
            }
        }
        finally{if(!retainForRender){playAlways.SetValue(settings,oldPlay);foreach(var t in fixtures.AsEnumerable().Reverse())if(!t.Destroyed)t.Destroy(DestroyMode.Vanish);}}
    }
}
