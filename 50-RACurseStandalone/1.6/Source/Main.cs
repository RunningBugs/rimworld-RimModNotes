using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;

using HarmonyLib;

namespace RACurseSA;

[StaticConstructorOnStartup]
public static class Start
{
    static Start()
    {
        new Harmony("com.runningbugs.RACurseSA").PatchAll();
        Log.Message("RACurseSA loaded!");
    }
}

public static class CurseNames
{
    public const string EatingWithoutTable = "EatingWithoutTable";
    public const string Research = "Research";
    public const string KillAnimals = "KillAnimals";
    public const string Wounded = "Wounded";
    public const string CutTree = "CutTree";

    public static readonly List<string> All = new()
    {
        EatingWithoutTable, Research, KillAnimals, Wounded, CutTree
    };
}

public class RACurseSAComponent : WorldComponent
{
    private HashSet<string> curses = new();

    public RACurseSAComponent(World world) : base(world) { }

    public bool IsActive(string curse) => curses.Contains(curse);

    public void Activate(string curse) => curses.Add(curse);

    public bool Deactivate(string curse) => curses.Remove(curse);

    public void Clear() => curses.Clear();

    public List<string> InactiveCurses() => CurseNames.All.Where(c => !curses.Contains(c)).ToList();

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref curses, "curses", LookMode.Value);
        curses ??= new HashSet<string>();
    }
}

public class GameCondition_Curse : GameCondition_ForceWeather
{
    // Which curse this condition instance is responsible for.
    // Only the matching curse is lifted when this condition ends.
    public string curseName;

    public RACurseSAComponent Component
    {
        get
        {
            return Find.World.GetComponent<RACurseSAComponent>();
        }
    }

    public override string Label
    {
        get
        {
            if (!curseName.NullOrEmpty())
            {
                return ("RACurseSA.CurseName." + curseName).Translate();
            }
            return base.Label;
        }
    }

    public override void End()
    {
        if (!curseName.NullOrEmpty() && Component.Deactivate(curseName))
        {
            Find.LetterStack.ReceiveLetter("RACurseSA.CurseEnded".Translate(), "RACurseSA.CurseEndedDesc".Translate(), LetterDefOf.NeutralEvent);
        }
        base.End();
        base.SingleMap.weatherDecider.StartNextWeather();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref curseName, "curseName");
    }
}


[DefOf]
public class RACurseSADefOf
{
    public static GameConditionDef RACurseSA_CurseCondition;

    public static GameConditionDef RACurseSA_CurseDisplay_EatingWithoutTable;

    public static GameConditionDef RACurseSA_CurseDisplay_Research;

    public static GameConditionDef RACurseSA_CurseDisplay_KillAnimals;

    public static GameConditionDef RACurseSA_CurseDisplay_Wounded;

    public static GameConditionDef RACurseSA_CurseDisplay_CutTree;

    public static SoundDef Pawn_Fleshbeast_Bulbfreak_Death;

    public static IncidentDef ColdSnap;

    public static GameConditionDef DisplayDefFor(string curse)
    {
        return curse switch
        {
            "EatingWithoutTable" => RACurseSA_CurseDisplay_EatingWithoutTable,
            "Research" => RACurseSA_CurseDisplay_Research,
            "KillAnimals" => RACurseSA_CurseDisplay_KillAnimals,
            "Wounded" => RACurseSA_CurseDisplay_Wounded,
            "CutTree" => RACurseSA_CurseDisplay_CutTree,
            _ => null,
        };
    }
}

// Permanent curse condition: shows the active curse in the top-right
// condition readout, and doubles as an activation source on its own.
// Adding it (scenario permanent condition, dev mode, or the curse incident
// in permanent mode) activates the linked curse; removing it lifts the curse.
// It expires by itself as soon as the linked curse is no longer active in
// the world component (e.g. cleared via the mod settings).
public class GameCondition_CurseDisplay : GameCondition
{
    public string Curse => def.GetModExtension<CurseExtension>()?.curse;

    public RACurseSAComponent Component => Find.World.GetComponent<RACurseSAComponent>();

    public override void Init()
    {
        base.Init();
        string curse = Curse;
        if (!curse.NullOrEmpty())
        {
            Component.Activate(curse);
        }
    }

    public override void End()
    {
        string curse = Curse;
        if (!curse.NullOrEmpty() && Component.Deactivate(curse))
        {
            Find.LetterStack.ReceiveLetter("RACurseSA.CurseEnded".Translate(), "RACurseSA.CurseEndedDesc".Translate(), LetterDefOf.NeutralEvent);
        }
        base.End();
    }

    public override bool Expired
    {
        get
        {
            World world = Find.World;
            if (world == null)
            {
                return base.Expired;
            }
            RACurseSAComponent component = world.GetComponent<RACurseSAComponent>();
            string curse = Curse;
            if (component == null || curse.NullOrEmpty())
            {
                return base.Expired;
            }
            return !component.IsActive(curse);
        }
    }
}

public class CurseExtension : DefModExtension
{
    // One of CurseNames, or "Random" to pick one of the currently inactive curses.
    public string curse;
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
        string curse = def.GetModExtension<CurseExtension>()?.curse;
        if (curse.NullOrEmpty() || curse == "Random")
        {
            List<string> candidates = Component.InactiveCurses();
            if (candidates.Count == 0)
            {
                return false;
            }
            curse = candidates.RandomElement();
        }
        else if (Component.IsActive(curse))
        {
            // This curse is already hanging over the colony.
            return false;
        }
        Component.Activate(curse);

        NamedArgument curseDesc = ("RACurseSA." + curse + "Desc").Translate().Named("CurseDesc");

        if (RACurseSAMod.settings.permanentCurse)
        {
            // Permanent mode: the curse simply never ends until the player clears
            // it in the mod settings. A display-only condition is registered so
            // the active curse shows up in the top-right condition readout.
            if (parms.target is Map targetMap && RACurseSADefOf.DisplayDefFor(curse) is GameConditionDef displayDef)
            {
                targetMap.GameConditionManager.RegisterCondition(GameConditionMaker.MakeConditionPermanent(displayDef));
            }
            SendStandardLetter(def.letterLabel, RACurseSADefOf.RACurseSA_CurseCondition.letterText.Formatted(curseDesc), def.letterDef, parms, LookTargets.Invalid);
            return true;
        }

        GameConditionManager gameConditionManager = parms.target.GameConditionManager;
        int duration = Mathf.RoundToInt(def.durationDays.RandomInRange * 60000f);
        GameCondition_Curse gameCondition = (GameCondition_Curse)GameConditionMaker.MakeCondition(GetGameConditionDef(parms), duration);
        gameCondition.curseName = curse;
        gameConditionManager.RegisterCondition(gameCondition);
        Map map;
        if (!def.letterLabel.NullOrEmpty() && !gameCondition.def.letterText.NullOrEmpty() && ((map = (parms.target as Map)) == null || !gameCondition.HiddenByOtherCondition(map)))
        {
            parms.letterHyperlinkThingDefs = gameCondition.def.letterHyperlinks;
            SendStandardLetter(def.letterLabel, gameCondition.LetterText.Formatted(curseDesc), def.letterDef, parms, LookTargets.Invalid);
        }

        return true;
    }
}

public class RACurseSASettings : ModSettings
{
    public bool onlyApplyToColonists = true;

    public bool permanentCurse = true;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref onlyApplyToColonists, "onlyApplyToColonists", true);
        Scribe_Values.Look(ref permanentCurse, "permanentCurse", true);
    }
}

public class RACurseSAMod : Mod
{
    public static RACurseSASettings settings;

    public RACurseSAMod(ModContentPack content) : base(content)
    {
        settings = GetSettings<RACurseSASettings>();
    }

    public override string SettingsCategory()
    {
        return "RACurseSA.SettingsCategory".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listingStandard = new();
        listingStandard.Begin(inRect);
        listingStandard.CheckboxLabeled("RACurseSA.OnlyApplyToColonists".Translate(), ref settings.onlyApplyToColonists, "RACurseSA.OnlyApplyToColonistsDesc".Translate());
        listingStandard.CheckboxLabeled("RACurseSA.PermanentCurse".Translate(), ref settings.permanentCurse, "RACurseSA.PermanentCurseDesc".Translate());
        if (Current.Game != null)
        {
            RACurseSAComponent component = Find.World.GetComponent<RACurseSAComponent>();
            List<string> active = CurseNames.All.Where(component.IsActive).ToList();
            string listText = active.Count == 0
                ? (string)"RACurseSA.NoActiveCurses".Translate()
                : string.Join(", ", active.Select(c => (string)("RACurseSA.CurseName." + c).Translate()));
            listingStandard.Label("RACurseSA.ActiveCurses".Translate() + " " + listText);
        }
        if (Current.Game != null && listingStandard.ButtonText("RACurseSA.ClearAllCurses".Translate()))
        {
            Find.World.GetComponent<RACurseSAComponent>().Clear();
            Messages.Message("RACurseSA.CursesCleared".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }
        listingStandard.End();
    }
}
