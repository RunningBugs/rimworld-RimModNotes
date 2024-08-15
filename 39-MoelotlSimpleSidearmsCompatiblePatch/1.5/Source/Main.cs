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
using HarmonyLib;

using System.Reflection;
using Axolotl;


namespace MoeLotlSSAPatch;

[StaticConstructorOnStartup]
public static class Start
{
    static Start()
    {
        Harmony harmony = new("com.RunningBugs.MoeLotlSSAPatch");
        harmony.PatchAll();
    }
}

[HarmonyPatch(typeof(Verb_LotiIntensifyShoot), "CanUse", MethodType.Getter)]
public static class Verb_LotiIntensifyShootCanUse_Patch
{
    public static bool Prefix(ref bool __result, Verb_LotiIntensifyShoot __instance)
    {
        if (__instance.GetPawn == null)
        {
            __result = false;
            return false;
        }
        return true;
    }
}
