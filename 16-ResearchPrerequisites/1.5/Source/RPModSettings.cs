using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ResearchPrerequisites;


public class RPModSettings : ModSettings
{
    public bool FinishProjectWithLetter = false;
    public bool DubsMintMenusMod = false;

    public RPModSettings()
    {
        if (ModLister.GetActiveModWithIdentifier("Dubwise.DubsMintMenus")?.Active ?? false)
        {
            DubsMintMenusMod = true;
        }
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref FinishProjectWithLetter, "FinishProjectWithLetter", false);
    }
}


public class RPModSettingsUI : Mod
{
    public RPModSettings Settings;

    public RPModSettingsUI(ModContentPack content) : base(content)
    {
        Settings = GetSettings<RPModSettings>();
    }

    public override string SettingsCategory()
    {
        return "ResearchPrerequisites".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listingStandard = new();
        // bool before = Settings.FinishProjectWithLetter;
        listingStandard.Begin(inRect);
        listingStandard.Label("ResearchPrerequisitesSettings".Translate());
        listingStandard.CheckboxLabeled("FinishProjectWithLetter".Translate(), ref Settings.FinishProjectWithLetter, "FinishProjectWithLetterDesc".Translate());
        listingStandard.End();
        // if (before != Settings.FinishProjectWithLetter)
        // {
        //     WriteSettings();
        // }
    }
}


[HarmonyPatch(typeof(ResearchManager), "FinishProject")]
[StaticConstructorOnStartup]
public static class Patch_FinishProject
{
    private static bool ActualDoComplete = false;

    private static RPModSettings GetSettings()
    {
        return LoadedModManager.GetMod<RPModSettingsUI>()?.GetSettings<RPModSettings>() ?? null;
    }

    public static void Prefix(ref bool doCompletionDialog)
    {
        ActualDoComplete = doCompletionDialog;
        if (!(GetSettings()?.DubsMintMenusMod ?? false) && Scribe.mode == LoadSaveMode.Inactive && (GetSettings()?.FinishProjectWithLetter ?? false))
        {
            doCompletionDialog = false;
        }
    }

    public static void Postfix(ResearchProjectDef proj)
    {
        if (!(GetSettings()?.DubsMintMenusMod ?? false) && Scribe.mode == LoadSaveMode.Inactive && ActualDoComplete && (GetSettings()?.FinishProjectWithLetter ?? false))
        {
            string text = "ResearchFinished".Translate(proj.LabelCap + "\n\n" + proj.description);
            Find.LetterStack.ReceiveLetter("ResearchFinished".Translate(proj.LabelCap), text, LetterDefOf.NeutralEvent, null, 0, true);
        }
    }
}