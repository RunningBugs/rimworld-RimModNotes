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
            // 按 parent 链判断归属（childCategories 依赖启动期 FinalizeInit，
            // 而 parent 直接来自 XML，任何时刻都可靠）。
            return DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.category == ThingCategory.Item
                    && d.PlayerAcquirable
                    && !d.IsCorpse
                    && !d.isUnfinishedThing
                    && d.stackLimit > 0
                    && d.thingCategories != null
                    && d.thingCategories.Any(c => c == category || c.Parents.Contains(category)))
                .OrderBy(d => d.LabelCap.ToString())
                .ToList();
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
