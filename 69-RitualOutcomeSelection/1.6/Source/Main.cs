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

using System.Reflection;
using HarmonyLib;

namespace RitualOutcomeSelection;

[StaticConstructorOnStartup]
public static class Start
{
    static Start()
    {
        Harmony harmony = new("com.RunningBugs.RitualOutcomeSelection");
        harmony.PatchAll();
        Log.Message("[RitualOutcomeSelection] patched successfully!".Colorize(Color.green));
    }
}

[HarmonyPatch(typeof(RitualOutcomeEffectWorker_FromQuality), "GetOutcome")]
public static class RitualOutcomeEffectWorker_FromQuality_GetOutcome_Patch
{
    public static void Postfix(RitualOutcomeEffectWorker_FromQuality __instance, ref RitualOutcomePossibility __result)
    {
        // Custom logic to modify the ritual outcome
        Log.Message("[RitualOutcomeSelection] GetOutcome patched successfully!".Colorize(Color.green));
    }
}
