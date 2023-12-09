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
    static class Patch
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

        static void Postfix(bool __result, Pawn pawn)
        {
            Log.Warning("RPP_Patch: started origin: " + $"{pawn.Name}: {__result}");
            if (!__result)
            {
                return;
            }
            if (pawn.def.defName.StartsWith("RPP_Bot_"))
            {
                __result = IncapableOfCleaning(pawn);
                Log.Warning("RPP_Patch: " + pawn.def.defName + $" is ({__result}) incapable of cleaning");
                Log.Warning("RPP_Patch: " + $"inspecting: {Settings.adv_cleaning}");
                // Toil FilthList = new Toil();
                // Job curJob = FilthList.actor.jobs.curJob;
                // LocalTargetInfo A = curJob.GetTarget(TargetIndex.A);
                // DoCleanComp comp;
                // //  {Settings.clean_gizmo} {(comp = A.Thing?.TryGetComp<DoCleanComp>()) == null} {comp.Active}
                // Log.Warning("RPP_Patch: " + $"inspecting: {Settings.adv_cleaning}");
                // Log.Warning("RPP_Patch: " + $"inspecting: {Settings.adv_cleaning}");
            }
            return;
        }
    }
}
