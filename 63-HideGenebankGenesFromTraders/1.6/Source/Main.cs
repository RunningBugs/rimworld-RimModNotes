using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HideGenebankGenesFromTraders;

[StaticConstructorOnStartup]
public class ApplyPatches
{
    static ApplyPatches()
    {
        // Apply the Harmony patches
        var harmony = new Harmony("com.RunningBugs.HideGenebankGenesFromTraders");
        harmony.PatchAll();
        Log.Message("Patches applied".Colorize(Color.green));
    }
}


// Patch for trader groups (ground traders)
[HarmonyPatch(typeof(Pawn_TraderTracker), "ColonyThingsWillingToBuy")]
public static class HideGenebankGenes_TraderGroups_Patch
{
    static void Postfix(ref IEnumerable<Thing> __result, Pawn_TraderTracker __instance, Pawn
playerNegotiator)
    {
        __result = __result.Where(thing =>
        {
            // Skip genepacks from genebank containers
            if (ModsConfig.BiotechActive && thing is Genepack genepack)
            {
                // Check if this genepack is from a genebank
                if (genepack.ParentHolder is CompGenepackContainer)
                {
                    return false; // Filter out this genepack
                }
            }
            return true; // Keep this item
        });
    }
}

// Patch for orbital traders (trade ships)
[HarmonyPatch(typeof(TradeUtility), "AllLaunchableThingsForTrade")]
public static class HideGenebankGenes_OrbitalTraders_Patch
{
    static void Postfix(ref IEnumerable<Thing> __result, Map map, ITrader trader)
    {
        __result = __result.Where(thing =>
        {
            // Skip genepacks from genebank containers
            if (ModsConfig.BiotechActive && thing is Genepack genepack)
            {
                // Check if this genepack is from a genebank
                if (genepack.ParentHolder is CompGenepackContainer)
                {
                    return false; // Filter out this genepack
                }
            }
            return true; // Keep this item
        });
    }
}