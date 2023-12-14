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
using CommonSense;

namespace RPP_Patch
{
    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            // Log.Message("Mod template loaded successfully!");
            Harmony harmony = new Harmony("com.RunningBugs.RppPatch.CommonSense");
            harmony.PatchAll();
        }
    }


    [HarmonyPatch(typeof(Utility), "IncapableOfCleaning")]
    static class PatchCommonSenseUtilityIncapableOfCleaning
    {
        /**
         * Original method with minor changes
         * 
         * The checks for intelligence is downgrade from Humanlike to ToolUser, remove mental state check
         */
        private static bool IncapableOfCleaning(Pawn pawn)
        {
            return pawn.def.race == null ||
                pawn.def.race.intelligence < Intelligence.ToolUser ||
                pawn.Faction != Faction.OfPlayer ||
                pawn.RaceProps.intelligence < Intelligence.ToolUser ||
                pawn.WorkTypeIsDisabled(Utility.CleaningDef) ||
                pawn.IsBurning() ||
                pawn.workSettings == null || !pawn.workSettings.WorkIsActive(Utility.CleaningDef);
        }

        static void Postfix(ref bool __result, Pawn pawn)
        {
            if (!__result)
            {
                return;
            }
            if (pawn.def.defName.StartsWith("RPP_Bot_"))
            {
                __result = IncapableOfCleaning(pawn);
            }
            return;
        }
    }
}
