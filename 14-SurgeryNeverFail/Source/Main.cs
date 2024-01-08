using Verse;
using Log = Logger.Log;
using HarmonyLib;
using System.Reflection;
using RimWorld;
using Utils;
using UnityEngine;

namespace SurgeryNeverFail
{
    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Log.prefix = "SurgeryNeverFail";
            ToggleIconData data = ToggleIconData.SetupToggleIcon(null, ContentFinder<Texture2D>.Get("SurgeryNeverFail/surgery", true),
                "SurgeryNeverFailTooltip".Translate(), SoundDefOf.Mouseover_ButtonToggle, null, null);
            ToggleIconPatcher.Data = data;

            Harmony harmony = new Harmony("com.runningbugs.surgeryneverfail");
            harmony.PatchAll();

            Log.Message("loaded successfully!");
        }
    }

    [HarmonyPatch]
    public static class SurgeryFailPatch
    {
        // Targeting a protected method requires a bit more work
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Recipe_Surgery), "CheckSurgeryFail");
        }

        public static bool Prefix(ref bool __result)
        {
            if (ToggleIconPatcher.Flag)
            {
                __result = false; // Override the result to always be false
                return false; // Don't run the original method
            }
            return true;
        }
    }

}
