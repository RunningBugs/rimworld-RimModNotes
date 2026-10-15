using System;
using System.Linq;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod31Checks
{
    public const string PackageId = "RunningBugs.AutomaticBioferriteHavesting";

    public static void Run(Map map)
    {
        Require(ModsConfig.AnomalyActive, "Anomaly must be enabled");
        var def = DefDatabase<ThingDef>.GetNamed("BioferriteHarvester");
        var property = def.comps.Single(c => c.GetType().FullName == "AutomaticBioferriteHavesting.CompProperties_BioferriteHarvester");
        Require((float)property.GetType().GetField("threshold").GetValue(property) == 60f, "XML threshold must remain 60");
        var harvester = (Building_BioferriteHarvester)ThingMaker.MakeThing(def);
        var existing = map.listerThings.ThingsOfDef(ThingDefOf.Bioferrite).ToDictionary(t => t, t => t.stackCount);
        var oldTick = Find.TickManager.TicksGame;
        try
        {
            var cell = map.AllCells.First(c => GenAdj.OccupiedRect(c, Rot4.North, def.size)
                .All(p => p.InBounds(map) && p.Standable(map) && p.GetThingList(map).Count == 0));
            GenSpawn.Spawn(harvester, cell, map, Rot4.North);
            var component = harvester.AllComps.Single(c => c.GetType().FullName == "AutomaticBioferriteHavesting.CompBioferriteHarvester");
            var toggle = component.CompGetGizmosExtra().OfType<Command_Toggle>().Single();
            Require(toggle.isActive(), "Auto harvest should start enabled");
            Require(toggle.defaultLabel == "AutoHarvestBioferrite".Translate().ToString(), "Gizmo label did not load");
            for (var i = 0; i < 250 && !harvester.IsHashIntervalTick(250); i++)
                Find.TickManager.DebugSetTicksGame(oldTick + i + 1);
            Require(harvester.IsHashIntervalTick(250), "Could not align a real hash-interval tick");
            var aligned = Find.TickManager.TicksGame;
            var baselineCount = existing.Values.Sum();
            harvester.containedBioferrite = 59.99f;
            harvester.DoTick();
            Require(harvester.containedBioferrite == 59.99f && Total(map) == baselineCount, "Below-threshold actual building tick must not eject");
            harvester.containedBioferrite = 60f;
            Find.TickManager.DebugSetTicksGame(aligned + 1);
            harvester.DoTick();
            Require(harvester.containedBioferrite == 60f && Total(map) == baselineCount, "Non-interval actual tick must not eject");
            Find.TickManager.DebugSetTicksGame(aligned);
            harvester.DoTick();
            Require(harvester.containedBioferrite == 0f && Total(map) == baselineCount + 60, "Threshold tick must eject exactly 60 real bioferrite onto the map");
            toggle.toggleAction();
            Require(!toggle.isActive(), "Actual gizmo toggle must disable harvesting");
            harvester.containedBioferrite = 60f;
            harvester.DoTick();
            Require(harvester.containedBioferrite == 60f && Total(map) == baselineCount + 60, "Disabled harvest must preserve full contents");
            toggle.toggleAction();
            harvester.DoTick();
            Require(toggle.isActive() && harvester.containedBioferrite == 0f && Total(map) == baselineCount + 120, "Re-enabled harvest must eject the second stack exactly once");
        }
        finally
        {
            Find.TickManager.DebugSetTicksGame(oldTick);
            if (!harvester.Destroyed)
                harvester.Destroy(DestroyMode.Vanish);
            foreach (var thing in map.listerThings.ThingsOfDef(ThingDefOf.Bioferrite).ToArray())
            {
                if (existing.TryGetValue(thing, out var count))
                    thing.stackCount = count;
                else
                    thing.Destroy(DestroyMode.Vanish);
            }
        }
        Log.Message("RW17 Mod31: actual DoTick/CompTick, XML threshold, below/noninterval/threshold, real placement and gizmo disable/re-enable passed");
    }

    private static int Total(Map map) => map.listerThings.ThingsOfDef(ThingDefOf.Bioferrite).Sum(t => t.stackCount);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Mod31: " + message);
    }
}
