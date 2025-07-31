using RimWorld.Planet;
using RimWorld;
using Verse;
// using System.Reflection;
// using HarmonyLib;

namespace ShuttleMassInfo;

public class WorldObjectCompProperties_CaravanShuttleMassInfo : WorldObjectCompProperties
{
    public WorldObjectCompProperties_CaravanShuttleMassInfo()
    {
        compClass = typeof(CaravanShuttleMassInfoComp);
    }
}

public class CaravanShuttleMassInfoComp : WorldObjectComp
{
    public override string CompInspectStringExtra()
    {
        if (parent is Caravan caravan && caravan.Shuttle is Building_PassengerShuttle shuttle && shuttle != null)
        {
            var massUsage = CaravanShuttleUtility.GetCaravanShuttleMass(caravan);
            return $"{"Mass".Translate()}: {massUsage:F1} / {shuttle.TransporterComp.MassCapacity:F1} kg";
        }
        return null;
    }
}
