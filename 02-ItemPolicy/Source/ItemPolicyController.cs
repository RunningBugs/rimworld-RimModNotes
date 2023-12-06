using RimWorld;
using Verse;
using Verse.AI;

using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace _ItemPolicy
{
    public class ItemPolicyUtility : GameComponent
    {
        public static Dictionary<Pawn, ItemPolicy> policies = new Dictionary<Pawn, ItemPolicy>();

        private static Dictionary<string, ThingDef> defDict = new Dictionary<string, ThingDef>();


        public ItemPolicyUtility(Game game)
        {
        }

        public override void FinalizeInit()
        {
            var allDefs = DefDatabase<ThingDef>.AllDefs;

            foreach (var def in allDefs)
            {
                if (def.label == null) continue;

                if (def.thingClass.IsSubclassOf(typeof(Thing)) || def.thingClass.IsSubclassOf(typeof(ThingWithComps)))
                {
                    ItemPolicyUtility.AddDef(def);
                }
            }

            Log.Message("Successfully Initialized!");
        }

        private List<Pawn> keys = new List<Pawn>();
        private List<ItemPolicy> vals = new List<ItemPolicy>();

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref ItemPolicyUtility.policies, "_ItemPolicy.ItemPolicyUtility", LookMode.Reference, LookMode.Deep, ref keys, ref vals);
        }

        public static void AddDef(ThingDef def)
        {
            if (!DefDict.ContainsKey(def.label))
            {
                DefDict[def.label] = def;
            }
        }

        public static List<ThingDef> Search(string text)
        {
            var labels = DefDict.Keys.Where(key => key.Contains(text)).ToList();
            return labels.Select(label => DefDict[label]).ToList();
        }

        public static Dictionary<string, ThingDef> DefDict { get => defDict; set => defDict = value; }

        public static ItemPolicy GetPawnPolicy(Pawn pawn)
        {
            if (!policies.ContainsKey(pawn))
            {
                policies[pawn] = new ItemPolicy();
            }
            return policies[pawn];
        }

        public static void SetItemPolicyEntry(Pawn pawn, ThingDef itemDef, int count)
        {
            var policy = GetPawnPolicy(pawn);
            if (count <= 0)
            {
                count = 0;
            }
            policy.data[itemDef] = count;
        }

        public static int GetItemPolicyEntry(Pawn pawn, ThingDef itemDef)
        {
            var policy = GetPawnPolicy(pawn);
            if (!policy.data.ContainsKey(itemDef))
            {
                return 0;
            }
            return policy.data[itemDef];
        }

        public static void RemoveItemPolicyEntry(Pawn pawn, ThingDef itemDef)
        {
            var policy = GetPawnPolicy(pawn);
            policy.data.Remove(itemDef);
            if (policy.data.Count == 0)
            {
                policies.Remove(pawn);
            }
        }
    }

    public class JobGiver_TakeItemForInventoryStock : ThinkNode_JobGiver
    {
        // private const int InventoryStockCheckIntervalMin = 6000;

        // private const int InventoryStockCheckIntervalMax = 9000;

        // private static int lastInventoryStockTick = -9999;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (Find.TickManager.TicksGame < pawn.mindState.nextInventoryStockTick)
            {
                return null;
            }
            // if (Find.TickManager.TicksGame < lastInventoryStockTick)
            // {
            //     return null;
            // }
            if (pawn.inventory.UnloadEverything)
            {
                return null;
            }
            foreach (var (def, count) in ItemPolicyUtility.GetPawnPolicy(pawn).data)
            {
                if (pawn.inventory.Count(def) < count)
                {
                    Thing thing = FindThingFor(pawn, def);
                    // Log.Message("thing null: " + (thing == null));
                    float weight = MassUtility.GearAndInventoryMass(pawn);
                    float capable = MassUtility.Capacity(pawn);
                    float mass = thing != null ? thing.GetStatValue(StatDefOf.Mass) : capable + 1;
                    if (thing != null && weight + mass <= capable)
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.TakeCountToInventory, thing);
                        job.count = Mathf.Min(b: count - pawn.inventory.Count(thing.def), a: thing.stackCount);
                        long max_to_hold = mass > 0 ? (long)((capable - weight) / mass) : job.count;   //  using long to hold items that are too light that can have too many items to hold which may lead to data overflow; when mass is 0 or less, we assume the cap is job.count

                        job.count = (int)Mathf.Min(job.count, max_to_hold); // since job.count is int, we won't be able to overflow now
                        return job;
                    }
                }
            }
            // if (!thingsToTake)
            // {
            //     lastInventoryStockTick = pawn.mindState.nextInventoryStockTick > 0 ? pawn.mindState.nextInventoryStockTick : Find.TickManager.TicksGame + Rand.Range(InventoryStockCheckIntervalMin, InventoryStockCheckIntervalMax);
            // }
            return null;
        }

        private Thing FindThingFor(Pawn pawn, ThingDef thingDef)
        {
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(thingDef), PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, (Thing x) => ThingValidator(pawn, x));
        }

        private bool ThingValidator(Pawn pawn, Thing thing)
        {
            if (thing.IsForbidden(pawn))
            {
                return false;
            }
            if (!pawn.CanReserve(thing))
            {
                return false;
            }
            return true;
        }
    }
}
