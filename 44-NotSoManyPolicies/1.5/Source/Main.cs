using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using Verse.Noise;
using Verse.Grammar;
using RimWorld;
using RimWorld.Planet;

// using System.Reflection;
using HarmonyLib;

namespace Template
{
    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Harmony harmony = new Harmony("com.runningbugs.not.so.many.policies");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(OutfitDatabase), "GenerateStartingOutfits")]
    public static class OutfitDatabase_GenerateStartingOutfits_Patch
    {
        public static void Postfix(OutfitDatabase __instance)
        {
            __instance.outfits.RemoveAll((ApparelPolicy o) => o.label != "OutfitAnything".Translate());
        }
    }

    [HarmonyPatch(typeof(FoodRestrictionDatabase), "GenerateStartingFoodRestrictions")]
    public static class FoodRestrictionDatabase_GenerateStartingFoodRestrictions_Patch
    {
        public static void Postfix(FoodRestrictionDatabase __instance)
        {
            __instance.foodRestrictions.RemoveAll((FoodPolicy o) => o.label != "FoodRestrictionNothing".Translate() && o.label != "FoodRestrictionLavish".Translate());
        }
    }

}
