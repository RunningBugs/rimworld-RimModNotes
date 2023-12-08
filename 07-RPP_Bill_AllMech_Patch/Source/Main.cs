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
using System.Runtime.CompilerServices;
using HarmonyLib;
using System.Reflection;

// using System.Reflection;
// using HarmonyLib;

namespace RPP_Patch
{
    [StaticConstructorOnStartup]
    public static class LoadingScreen
    {
        static LoadingScreen()
        {
            Log.Warning("RPP_Bill_AllMech_Patch Loaded");

            var harmony = new Harmony("com.RunningBugs.RPP_Bill_AllMech_Patch");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
    
    [HarmonyPatch(typeof(Bill), "PawnAllowedToStartAnew")]
    class RobotsPP_Bill_Patch_PawnAllowedToStartAnew
    {
        private static bool Check(Pawn p, Bill b)
        {
            if (b.recipe.workSkill != null && (p.skills != null || p.IsColonyMech))
            {
                int num = ((p.skills != null) ? p.skills.GetSkill(b.recipe.workSkill).Level : p.RaceProps.mechFixedSkillLevel);
                if (num < b.allowedSkillRange.min)
                {
                    JobFailReason.Is("UnderAllowedSkill".Translate(b.allowedSkillRange.min), b.Label);
                    return false;
                }
                if (num > b.allowedSkillRange.max)
                {
                    JobFailReason.Is("AboveAllowedSkill".Translate(b.allowedSkillRange.max), b.Label);
                    return false;
                }
            }
            if (ModsConfig.BiotechActive && b.recipe.mechanitorOnlyRecipe && !MechanitorUtility.IsMechanitor(p))
            {
                JobFailReason.Is("NotAMechanitor".Translate());
                return false;
            }
            return true;
        }

        static void Postfix(Pawn p, Bill __instance, ref bool __result)
        {
            if (!p.IsColonyMech)
            {
                return;
            }
            if (__instance.PawnRestriction != null)
            {
                return;
            }
            if (__instance.SlavesOnly && !p.IsSlave)
            {
                return;
            }
            if (__instance.MechsOnly && !p.IsColonyMechPlayerControlled)
            {
                if (p.def.defName.StartsWith("RPP_Bot_"))
                {
                    __result = Check(p, __instance);
                    return;
                }
            }
        }

    }
    
    public static class Log
    {
        const string prefix = "[RPP_Patch] ";
        public static void Warning(string msg, [CallerFilePath] string fileName = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Verse.Log.Warning(prefix + $"[ {fileName}:{lineNumber} {memberName} ]" + msg);
        }
    }

}
