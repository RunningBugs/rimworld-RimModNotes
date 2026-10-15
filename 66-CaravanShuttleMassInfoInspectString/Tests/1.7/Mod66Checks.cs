using System;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RW17.Validation;

public static class Mod66Checks
{
    public const string PackageId = "RunningBugs.CaravanShuttleMassInfoInspectString";

    public static void Run(Map map)
    {
        const string propertiesName = "ShuttleMassInfo.WorldObjectCompProperties_CaravanShuttleMassInfo";
        const string componentName = "ShuttleMassInfo.CaravanShuttleMassInfoComp";
        var properties = WorldObjectDefOf.Caravan.comps.Where(p => p.GetType().FullName == propertiesName).ToList();
        Require(properties.Count == 1, "Caravan Def must contain exactly one MOD component property");
        Require(properties[0].compClass.FullName == componentName, "Component constructor registered wrong class");

        var caravan = (Caravan)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Caravan);
        caravan.Tile = map.Tile;
        caravan.SetFaction(Faction.OfPlayer);
        Pawn pawn = null;
        try
        {
            var components = caravan.AllComps.Where(c => c.GetType().FullName == componentName).ToList();
            Require(components.Count == 1, "WorldObjectMaker must instantiate exactly one MOD component");
            var component = components[0];
            Require(component.CompInspectStringExtra() == null, "Empty caravan must not show shuttle mass");
            component.parent = null;
            Require(component.CompInspectStringExtra() == null, "Null parent must not show shuttle mass");
            component.parent = caravan;

            pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            caravan.AddPawn(pawn, false);
            Require(component.CompInspectStringExtra() == null, "Caravan without shuttle must not show mass");

            var shuttle = (Building_PassengerShuttle)ThingMaker.MakeThing(ThingDefOf.PassengerShuttle);
            Require(pawn.inventory.innerContainer.TryAdd(shuttle), "Could not add test shuttle to inventory");
            caravan.RecacheInventory();
            VerifyInspectString(caravan, component, shuttle);

            var cargo = ThingMaker.MakeThing(ThingDefOf.Steel);
            cargo.stackCount = 20;
            Require(pawn.inventory.innerContainer.TryAdd(cargo, false), "Could not add test cargo to inventory");
            caravan.RecacheInventory();
            VerifyInspectString(caravan, component, shuttle);
            pawn.inventory.innerContainer.Remove(shuttle);
            shuttle.Destroy(DestroyMode.Vanish);
            caravan.RecacheInventory();
            Require(component.CompInspectStringExtra() == null, "Removing shuttle must remove displayed mass");
        }
        finally
        {
            if (pawn != null)
            {
                caravan.RemovePawn(pawn);
                pawn.inventory.innerContainer.ClearAndDestroyContents();
                pawn.Destroy(DestroyMode.Vanish);
            }
            caravan.Destroy();
        }
        Log.Message("RW17 Mod66: component registration, empty/null/no-shuttle, private mass call, cargo refresh, GetInspectString and shuttle removal passed");
    }

    private static void VerifyInspectString(Caravan caravan, WorldObjectComp component, Building_PassengerShuttle shuttle)
    {
        var method = typeof(CaravanShuttleUtility).GetMethod("GetCaravanShuttleMass", BindingFlags.Static | BindingFlags.NonPublic);
        Require(method != null, "Official shuttle mass method was not found");
        var mass = (float)method.Invoke(null, new object[] { caravan });
        var actual = component.CompInspectStringExtra();
        Require(actual != null && actual.StartsWith("Mass".Translate().ToString() + ": "), "Localized mass label missing");
        Require(actual.Contains(mass.ToString("F1") + " / " + shuttle.TransporterComp.MassCapacity.ToString("F1") + " kg"), "Displayed mass/capacity disagrees with official game computation");
        Require(!char.IsWhiteSpace(actual[actual.Length - 1]), "Inspect line has trailing whitespace");
        var inspect = caravan.GetInspectString();
        Require(inspect.Split(new[] { actual }, StringSplitOptions.None).Length == 2, "Actual caravan inspect chain must include the MOD line exactly once");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Mod66: " + message);
    }
}
