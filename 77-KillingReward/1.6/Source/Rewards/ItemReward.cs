using System.Collections.Generic;
using System.Linq;
using KillingReward.Core;
using RimWorld;
using Verse;

namespace KillingReward
{
    public static class ItemReward
    {
        public static readonly ThingCategoryDef[] RootCategories =
        {
            ThingCategoryDefOf.Manufactured,
            ThingCategoryDefOf.ResourcesRaw,
            ThingCategoryDefOf.Items
        };

        public static List<ThingDef> ThingsIn(ThingCategoryDef category)
        {
            List<ThingDef> result = new List<ThingDef>();
            Collect(category, result);
            return result
                .Where(d => d.PlayerAcquirable && !d.IsCorpse && !d.isUnfinishedThing && d.stackLimit > 0)
                .OrderBy(d => d.LabelCap.ToString())
                .ToList();
        }

        private static void Collect(ThingCategoryDef category, List<ThingDef> into)
        {
            into.AddRange(category.childThingDefs);
            foreach (ThingCategoryDef child in category.childCategories)
            {
                Collect(child, into);
            }
        }

        public static void Deliver(ThingDef def, IntVec3 cell, Map map)
        {
            ThingDef stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
            Thing thing = ThingMaker.MakeThing(def, stuff);
            thing.stackCount = StackMath.FullStackCount(def.stackLimit);
            GenSpawn.Spawn(thing, cell, map);
            Messages.Message("KR_ItemDelivered".Translate(), new LookTargets(thing), MessageTypeDefOf.PositiveEvent);
        }
    }
}
