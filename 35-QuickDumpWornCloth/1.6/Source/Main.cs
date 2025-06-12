using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

// using System.Reflection;
// using HarmonyLib;

namespace QuickDumpWornCloth
{
    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Log.Message("QuickDumpWornCloth loaded!");
        }
    }

    public class WorldObjectCompProperties_DumpWornCloth : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_DumpWornCloth()
        {
            compClass = typeof(WorldObjectComp_DumpWornCloth);
        }
    }

    public class WorldObjectComp_DumpWornCloth : WorldObjectComp
    {
        private static void DumpThingInCaravan(Thing thing, Caravan caravan)
        {
            Pawn ownerOf = CaravanInventoryUtility.GetOwnerOf(caravan, thing);
            if (ownerOf == null)
            {
                Log.Error("Could not find owner of " + thing);
            }
            else
            {
                thing.Notify_AbandonedAtTile(caravan.GetTileCurrentlyOver());
                ownerOf.inventory.innerContainer.Remove(thing);
                thing.Destroy();
                caravan.RecacheInventory();
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (parent is not Caravan)
            {
                yield break;
            }

            Caravan caravan = (Caravan)parent;
            List<Thing> thingsToDump = new List<Thing>();
            CaravanInventoryUtility.AllInventoryItems(caravan).ForEach(thing =>
            {
                if (thing is Apparel apparel && apparel.WornByCorpse)
                {
                    thingsToDump.Add(thing);
                }

                if (CompBiocodable.IsBiocoded(thing))
                {
                    var comp = thing.TryGetComp<CompBiocodable>();
                    if (comp.CodedPawn != null && comp.CodedPawn.Faction != Faction.OfPlayer)
                    {
                        thingsToDump.Add(thing);
                    }
                    else if (comp.CodedPawn == null && comp.Biocoded)
                    {
                        thingsToDump.Add(thing);
                    }
                }
            });

            if (thingsToDump.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Dump worn cloth",
                    defaultDesc = "Dump worn cloth",
                    action = () =>
                    {
                        foreach (var thing in thingsToDump)
                        {
                            DumpThingInCaravan(thing, caravan);
                        }
                    }
                };
            } else {
                yield break;
            }
        }
    }
}
