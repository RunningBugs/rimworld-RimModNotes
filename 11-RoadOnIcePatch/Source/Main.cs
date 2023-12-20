using Verse;
using RimWorld;

using Log = Logger.Log;
// using System.Reflection;
// using HarmonyLib;

namespace RoadOnIce
{
    public class MyMapComponent : MapComponent
    {
        public MyMapComponent(Map map) : base(map){}
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            BiomeDefOf.IceSheet.allowRoads = true;
            BiomeDefOf.SeaIce.allowRoads = true;
            Log.Message("FinalizeInit");
        }
    }

    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Log.prefix = "RoadOnIce";
            Log.Message("loaded successfully!");
        }
    }

}
