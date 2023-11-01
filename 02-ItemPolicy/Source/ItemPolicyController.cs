using RimWorld;
using Verse;
using Verse.AI;
using RimWorld.Planet;

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
        private const int InventoryStockCheckIntervalMin = 6000;

        private const int InventoryStockCheckIntervalMax = 9000;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (Find.TickManager.TicksGame < pawn.mindState.nextInventoryStockTick)
            {
                return null;
            }
            if (pawn.inventory.UnloadEverything)
            {
                return null;
            }
            foreach (var (def, count) in ItemPolicyUtility.GetPawnPolicy(pawn).data)
            {
                if (pawn.inventory.Count(def) < count)
                {
                    Thing thing = FindThingFor(pawn, def);
                    if (thing != null)
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.TakeCountToInventory, thing);
                        job.count = Mathf.Min(b: count - pawn.inventory.Count(thing.def), a: thing.stackCount);
                        // find a bug here, sometimes, when the item is in the inventory, the job is still created
                        //  this is because a bug in the vanilla code, the pawn.inventory.Count(thing.def) is not updated (including pawn.inventory.innerContainer)
                        //  so the job.count is always 0, and the job is always created
                        //  Meanwhile, when saving the game, the item will appear in the inventory, but the inventory.Count and inventory.innerContainer is still not updated

                        // Log.Message("Found " + thing.Label);
                        // Log.Message("job: " + job.ToString());
                        // Log.Message("job count: " + job.count);
                        // Log.Message("thing position: " + thing.Position.x + ", " + thing.Position.y + ", " + thing.Position.z);
                        // Log.Message("inventory count: " + pawn.inventory.Count(thing.def));
                        // Log.Message("b: " + (count - pawn.inventory.Count(thing.def)));
                        // Log.Message("a: " + thing.stackCount);

                        // foreach (Thing item in pawn.inventory.innerContainer)
                        // {
                        //     Log.Message("item: " + item.ToString());
                        //     Log.Message("item count: " + item.stackCount);
                        // }
                        return job;
                    }
                }
            }
            // pawn.mindState.nextInventoryStockTick = Find.TickManager.TicksGame + Rand.Range(6000, 9000);
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
