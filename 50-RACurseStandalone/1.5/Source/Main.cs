using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;

using System.Reflection;
using HarmonyLib;

namespace RACurseSA;

[StaticConstructorOnStartup]
public static class Start
{
    static Start()
    {
        Log.Message("Mod template loaded successfully!");
    }
}


public class RACurseSAComponent : WorldComponent
{
    public RACurseSAComponent(World world) : base(world) { }

    private String curse = "Null";

    public string Curse { get => curse; set => curse = value; }
}


public class GameCondition_Curse : GameCondition_ForceWeather
{
    public RACurseSAComponent Component
    {
        get
        {
            return Find.World.GetComponent<RACurseSAComponent>();
        }
    }

    public override void End()
    {
        Component.Curse = "Null";
        Find.LetterStack.ReceiveLetter("RACurseSA.CurseEnded".Translate(), "RACurseSA.CurseEndedDesc".Translate(), LetterDefOf.NeutralEvent);
        base.End();
        base.SingleMap.weatherDecider.StartNextWeather();
    }

    public override void ExposeData()
    {
        base.ExposeData();
    }
}


[DefOf]
public class RACurseSADefOf
{
    public static GameConditionDef RACurseSA_CurseCondition;

    public static SoundDef Pawn_Fleshbeast_Bulbfreak_Death;

    public static IncidentDef ColdSnap;
}


public class IncidentWorker_Curse : IncidentWorker_MakeGameCondition
{
    public RACurseSAComponent Component
    {
        get
        {
            return Find.World.GetComponent<RACurseSAComponent>();
        }
    }

    public override GameConditionDef GetGameConditionDef(IncidentParms parms)
    {
        return RACurseSADefOf.RACurseSA_CurseCondition;
    }

    public override bool TryExecuteWorker(IncidentParms parms)
    {
        GameConditionManager gameConditionManager = parms.target.GameConditionManager;
        GameConditionDef gameConditionDef = GetGameConditionDef(parms);
        int duration = Mathf.RoundToInt(def.durationDays.RandomInRange * 60000f);
        GameCondition gameCondition = GameConditionMaker.MakeCondition(gameConditionDef, duration);
        gameConditionManager.RegisterCondition(gameCondition);
        Map map;
        if (!def.letterLabel.NullOrEmpty() && !gameCondition.def.letterText.NullOrEmpty() && ((map = (parms.target as Map)) == null || !gameCondition.HiddenByOtherCondition(map)))
        {
            List<string> curses = new List<string>();
            curses.Add("EatingWithoutTable");
            curses.Add("Research");
            curses.Add("KillAnimals");
            curses.Add("Wounded");
            curses.Add("CutTree");
            string curse = curses.RandomElement();
            Component.Curse = curse;

            parms.letterHyperlinkThingDefs = gameCondition.def.letterHyperlinks;
            SendStandardLetter(def.letterLabel, gameCondition.LetterText.Formatted(("RACurseSA." + curse + "Desc").Translate().Named("CurseDesc")), def.letterDef, parms, LookTargets.Invalid);
        }

        return true;
    }
}

public static class RACurseSA_Settings
{
    public static bool OnlyApplyToColonists = false;
}

public class RACurseSA_SettingsUI : Mod
{
    public RACurseSA_SettingsUI(ModContentPack content) : base(content)
    {
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        base.DoSettingsWindowContents(inRect);
    }

    public override string SettingsCategory()
    {
        return base.SettingsCategory();
    }
}
