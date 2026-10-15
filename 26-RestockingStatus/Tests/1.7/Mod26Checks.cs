using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using UnityEngine;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RW17.Validation;
public static class Mod26Checks
{
    public const string PackageId = "RunningBugs.RestockingStatus";
    public static void Run(Map map)
    {
        var assembly = LoadedModManager.RunningModsListForReading.Single(m => m.PackageId.Equals(PackageId, StringComparison.OrdinalIgnoreCase)).assemblies.loadedAssemblies.Single();
        var utilities = assembly.GetType("RestockingStatus.TextureUtilities", true);
        var start = assembly.GetType("RestockingStatus.Start", true);
        Texture2D background = Make(Color.blue), overlay = Make(new Color(1,0,0,0.5f));
        Texture2D result = null, resized = null, positioned = null, cached = null;
        var iconField = AccessTools.Field(start, "restockingIcon");
        var icons = (IDictionary)AccessTools.Field(start, "factionRestockingIcons").GetValue(null);
        var originalIcon = iconField.GetValue(null);
        var factionDef = new FactionDef { defName = "RW17_Test26", label = "validation" };
        AccessTools.Field(typeof(FactionDef), "factionIcon").SetValue(factionDef, background);
        var faction = new Faction { def = factionDef };
        try
        {
            result = (Texture2D)utilities.GetMethod("TextureOverlay").Invoke(null, new object[]{background,overlay});
            CheckPixel(result.GetPixel(0,0), new Color(0.5f,0,0.5f,1), "source-over color and alpha");
            resized = (Texture2D)utilities.GetMethod("FitToSize").Invoke(null,new object[]{background,new Vector2(2,2)});
            if (resized.width != 2 || resized.height != 2) throw new Exception("Mod26 resize dimensions");
            CheckPixel(resized.GetPixel(1,1),Color.blue,"bilinear solid resize");
            var large = new Texture2D(2,2); large.SetPixels(Enumerable.Repeat(Color.blue,4).ToArray()); large.Apply();
            try
            {
                positioned = (Texture2D)utilities.GetMethod("OverlayTextureAtPosition").Invoke(null,new object[]{large,overlay,new Vector2(1,1)});
                CheckPixel(positioned.GetPixel(0,1), Color.blue, "outside overlay preserved");
                CheckPixel(positioned.GetPixel(1,0), new Color(0.5f,0,0.5f,1), "lower-right placement");
            }
            finally { UnityEngine.Object.Destroy(large); }
            iconField.SetValue(null, overlay);
            cached = (Texture2D)start.GetMethod("GetFactionRestockingIcon").Invoke(null,new object[]{faction});
            CheckPixel(cached.GetPixel(0,0),new Color(0.5f,0,0.5f,1),"faction icon composition");
            if (!ReferenceEquals(cached,start.GetMethod("GetFactionRestockingIcon").Invoke(null,new object[]{faction})))
                throw new Exception("Mod26 faction icon cache");
            var settlement=(Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
            settlement.SetFaction(faction);
            var tracker=settlement.trader;
            AccessTools.Field(typeof(Settlement_TraderTracker),"stock").SetValue(tracker,new ThingOwner<Thing>(tracker));
            AccessTools.Field(typeof(Settlement_TraderTracker),"everGeneratedStock").SetValue(tracker,true);
            AccessTools.Field(typeof(Settlement_TraderTracker),"lastStockGenerationTicks").SetValue(tracker,Find.TickManager.TicksGame);
            if (!ReferenceEquals(settlement.ExpandingIcon,cached)) throw new Exception("Mod26 real restocking icon getter");
            AccessTools.Field(typeof(Settlement_TraderTracker),"everGeneratedStock").SetValue(tracker,false);
            if (!ReferenceEquals(settlement.ExpandingIcon,background)) throw new Exception("Mod26 non-restocking original icon");
            var getter=AccessTools.PropertyGetter(typeof(Settlement),nameof(Settlement.ExpandingIcon));
            if (!Harmony.GetPatchInfo(getter).Postfixes.Any(p=>p.PatchMethod.DeclaringType.Assembly==assembly))
                throw new Exception("Mod26 exact patch registration");
        }
        finally
        {
            iconField.SetValue(null,originalIcon); icons.Remove(faction);
            foreach(var texture in new[]{background,overlay,result,resized,positioned,cached})
                if(texture!=null) UnityEngine.Object.Destroy(texture);
        }
    }
    private static Texture2D Make(Color color){var t=new Texture2D(1,1);t.SetPixel(0,0,color);t.Apply();return t;}
    private static void CheckPixel(Color actual,Color expected,string name)
    {
        if(Math.Abs(actual.r-expected.r)>0.015f || Math.Abs(actual.g-expected.g)>0.015f ||
           Math.Abs(actual.b-expected.b)>0.015f || Math.Abs(actual.a-expected.a)>0.015f)
            throw new Exception("Mod26 "+name+": "+actual+" expected "+expected);
    }
}
