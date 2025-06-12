using UnityEngine;
using Verse;
using RimWorld;

// using System.Reflection;
using HarmonyLib;
using System.Linq;

namespace TargetLine;


[StaticConstructorOnStartup]
public static class Start
{
    static Start()
    {
        Harmony harmony = new Harmony("com.runningbugs.TargetLine");
        harmony.PatchAll();
    }
}

public class ModSettingsData : ModSettings
{
    public bool showTargetLine = true;

    public bool showTargetLineForNonHostile = true;

    public bool showTargetLineForHostile = true;

    // public bool onlyShowWhenEnemyOnMap = true;

    // public bool ignorePsycInvisible = false;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref showTargetLine, "showTargetLine", true);
        Scribe_Values.Look(ref showTargetLineForNonHostile, "showTargetLineForNonHostile", true);
        Scribe_Values.Look(ref showTargetLineForHostile, "showTargetLineForEnemy", true);
    }
}

public class ModSettingsUI : Mod
{
    public ModSettingsUI(ModContentPack content) : base(content)
    {
        GetSettings<ModSettingsData>();
    }

    public override string SettingsCategory()
    {
        return "TargetLine".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        ModSettingsData settings = GetSettings<ModSettingsData>();
        Listing_Standard listingStandard = new Listing_Standard();
        listingStandard.Begin(inRect);
        listingStandard.CheckboxLabeled("EnableTargetLine".Translate(), ref settings.showTargetLine);

        if (settings.showTargetLine)
        {
            listingStandard.CheckboxLabeled("EnableForNonHostile".Translate(), ref settings.showTargetLineForNonHostile, "EnableForNonHostileDesc".Translate());
            listingStandard.CheckboxLabeled("EnableForHostile".Translate(), ref settings.showTargetLineForHostile, "EnableForHostileDesc".Translate());
        }
        listingStandard.End();
    }
}


[HarmonyPatch(typeof(Pawn_StanceTracker), "StanceTrackerDraw")]
public static class Pawn_StanceTracker_Patch
{
    private static void LogLocalTargetInfo(LocalTargetInfo target)
    {
        if (target.IsValid)
        {
            if (target.Thing == null)
            {
                Log.Warning($"target: pos={target.Cell.ToVector3Shifted()}");
            }
            else
            {
                Log.Warning($"target: thingAt={target.Thing.DrawPos}");
            }
        }
    }

    private static void DrawLineBetweenTargets(LocalTargetInfo targetA, LocalTargetInfo targetB, SimpleColor color)
    {
        if (targetA.IsValid && targetB.IsValid)
        {
            Vector3 targetPosA;
            if (targetA.Thing != null)
            {
                targetPosA = targetA.Thing.DrawPos;
            }
            else
            {
                targetPosA = targetA.Cell.ToVector3Shifted();
            }

            Vector3 targetPosB;
            if (targetB.Thing != null)
            {
                targetPosB = targetB.Thing.DrawPos;
            }
            else
            {
                targetPosB = targetB.Cell.ToVector3Shifted();
            }

            GenDraw.DrawLineBetween(targetPosA, targetPosB, color);
        }
    }

    private static void DrawTargetLine(Pawn_StanceTracker __instance)
    {
        var pawn = __instance.pawn;
        var settings = LoadedModManager.GetMod<ModSettingsUI>().GetSettings<ModSettingsData>();
        if (!settings.showTargetLineForNonHostile && !GenHostility.HostileTo(pawn, Faction.OfPlayer))
        {
            return;
        }

        if (!settings.showTargetLineForHostile && GenHostility.HostileTo(pawn, Faction.OfPlayer))
        {
            return;
        }

        // bool hostilePawn = pawn.Faction.HostileTo(Faction.OfPlayer);
        bool hostilePawn = pawn.HostileTo(Faction.OfPlayer);
        var color1 = hostilePawn ? SimpleColor.Red : SimpleColor.Green;
        var color2 = hostilePawn ? SimpleColor.Orange : SimpleColor.Blue;
        var color3 = hostilePawn ? SimpleColor.Magenta : SimpleColor.Cyan;

        if (pawn != null)
        {
            // Log.Warning($"Pawn_StanceTracker.StanceTrackerDraw called for {pawn.Name}");
            if (pawn.stances.curStance is Stance_Warmup stanceWarmup)
            {
                var target = stanceWarmup.focusTarg;
                DrawLineBetweenTargets(new LocalTargetInfo(pawn), target, color1);
            }
            else
            {
                var job = pawn.CurJob;
                if (job != null)
                {
                    // Log.Warning($"Pawn {pawn.Name} job: {job.GetCachedDriver(pawn).GetReport()}");
                    LocalTargetInfo targetA = pawn.CurJob?.targetA ?? LocalTargetInfo.Invalid;
                    LocalTargetInfo targetB = pawn.CurJob?.targetB ?? LocalTargetInfo.Invalid;
                    LocalTargetInfo targetC = pawn.CurJob?.targetC ?? LocalTargetInfo.Invalid;

                    // LogLocalTargetInfo(targetA);
                    // LogLocalTargetInfo(targetB);
                    // LogLocalTargetInfo(targetC);

                    DrawLineBetweenTargets(new LocalTargetInfo(pawn), targetA, color1);
                    DrawLineBetweenTargets(targetA, targetB, color2);
                    DrawLineBetweenTargets(targetB, targetC, color3);
                }
            }
        }
    }

    public static void Postfix(Pawn_StanceTracker __instance)
    {
        var settings = LoadedModManager.GetMod<ModSettingsUI>().GetSettings<ModSettingsData>();
        if (settings.showTargetLine)
        {
            DrawTargetLine(__instance);
        }
    }
}
