using RimWorld;
using Verse;
using Verse.AI;
using RimWorld.Planet;

using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using HarmonyLib;


namespace _ItemPolicy
{
    [StaticConstructorOnStartup]
    public static class ItemsLoadingScreen
    {
        static ItemsLoadingScreen()
        {
            Harmony harmony = new Harmony("com.RunningBugs.ItemPolicy");
            harmony.PatchAll();
        }
    }

    public class ItemPolicy : IExposable
    {
        public Dictionary<ThingDef, int> data = new Dictionary<ThingDef, int>();

        public ItemPolicy()
        {
        }

        public ItemPolicy MergePolicy(ItemPolicy p2)
        {
            // ItemPolicy res = new ItemPolicy();
            // foreach (var (key, val) in this.data)
            // {
            //     res.data[key] = val;
            // }

            // foreach (var (key, val) in p2.data)
            // {
            //     res.data[key] = val;
            // }

            this.data.Clear();
            this.data = p2.data.ToDictionary(x => x.Key, x => x.Value);
            return this;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref data, "_ItemPolicy.ItemPolicy", LookMode.Def, LookMode.Value);
        }
    }

    public static class ItemPolicyExt
    {
        public static Rect RightBoxPixels(this Rect rect, float pixels)
        {
            var w = rect.width;
            var h = rect.height;
            return new Rect(rect.x + w, rect.y, pixels, h);
        }

        public static Rect ShrinkPixels(this Rect rect, float pixels)
        {
            var w = rect.width - 2 * pixels;
            var h = rect.height - 2 * pixels;
            return new Rect(rect.x + pixels, rect.y + pixels, w, h);
        }
    }


}
