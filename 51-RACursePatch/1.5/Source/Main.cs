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

using RatkinAnomaly;

namespace RACursePatch;

public class Settings : ModSettings
{
    public bool enablePatch = true;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref enablePatch, "enablePatch", true);
    }
}

[StaticConstructorOnStartup]
public class ModSettingsUI : Mod
{
    public static Settings settings = null;

    public ModSettingsUI(ModContentPack content) : base(content)
    {
        settings = GetSettings<Settings>();
        Harmony harmony = new("com.runningbugs.RACursePatch");
        harmony.PatchAll();
        Log.Message("RACursePatch: patch done!");
    }

    public override string SettingsCategory()
    {
        return "RACursePatch".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listingStandard = new();
        listingStandard.Begin(inRect);
        // listingStandard.Label("CursesForColonistsOnly");
        bool old = settings.enablePatch;
        listingStandard.CheckboxLabeled("RACursePatch.enablePatch".Translate(), ref settings.enablePatch, "RACursePatch.enablePatchDesc".Translate());
        bool change = old != settings.enablePatch;
        listingStandard.End();
        if (change)
        {
            WriteSettings();
        }
    }
}

[HarmonyPatch]
public static class AteWithoutTable_Patch_Intercept
{
    public static MethodInfo TargetMethod()
    {
        return AccessTools.Method(typeof(AteWithoutTable_Patch), "Postfix");
    }

    public static bool Prefix(MemoryThoughtHandler __0)
    {
        // Add your prefix logic here
        // Return false to skip the original method, true to continue
        if (ModSettingsUI.settings.enablePatch && !__0.pawn.IsColonist)
        {
            return false;
        }
        return true;
    }
}


[HarmonyPatch]
public static class KillAnimals_Patch_Intercept
{
    public static MethodInfo TargetMethod()
    {
        return AccessTools.Method(typeof(KillAnimals_Patch), "Prefix");
    }

    public static bool Prefix(Pawn __0)
    {
        if (ModSettingsUI.settings.enablePatch && !__0.IsColonist)
        {
            return false;
        }
        return true;
    }
}

[HarmonyPatch]
public static class Wounded_Patch_Intercept
{
    public static MethodInfo TargetMethod()
    {
        return AccessTools.Method(typeof(Wounded_Patch), "Prefix");
    }

    public static bool Prefix(Pawn __0)
    {
        if (ModSettingsUI.settings.enablePatch && !__0.IsColonist)
        {
            return false;
        }
        return true;
    }
}
