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

namespace LimitedLetterSlots;


[StaticConstructorOnStartup]
public static class Start
{
    static Start()
    {
        var harmony = new Harmony("com.RunningBugs.LimitedLetterSlots");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        Log.Message("LimitedLetterSlots: Patched successfully.".Colorize(Color.green));
    }
}


public class Settings : ModSettings
{
    public bool enableMod = true;
    public int maxVisibleSlots = 3;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref maxVisibleSlots, "LLS.MaxVisibleSlots", 3);
        Scribe_Values.Look(ref enableMod, "LLS.enableMod", true);
    }
}

public class ModSettingsUI : Mod
{
    public static Settings settings;

    public ModSettingsUI(ModContentPack content) : base(content)
    {
        settings = GetSettings<Settings>();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);

        listing.CheckboxLabeled("LLS.EnableMod".Translate(), ref settings.enableMod, "LLS.EnableMod.Tooltip".Translate());

        if (settings.enableMod)
        {
            listing.Label("LLS.MaxVisibleSlots".Translate() + " : " + $"{settings.maxVisibleSlots}");
            settings.maxVisibleSlots = (int)listing.Slider(settings.maxVisibleSlots, 1, 10);
        }

        listing.End();
    }

    public override string SettingsCategory() => "LimitedLetterSlots".Translate();
}


[HarmonyPatch(typeof(LetterStack), nameof(LetterStack.LettersOnGUI))]
public static class LetterStack_LettersOnGUI_Patch
{
    public const float LetterButtonHeight = 30f;
    public const float LetterButtonSpacing = 12f;

    public static bool Prefix(
        float baseY,
        List<Letter> ___letters,
        List<Letter> ___tmpBundledLetters,
        ref BundleLetter ___bundleLetterCache,
        ref float ___lastTopYInt)
    {
        if (!ModSettingsUI.settings.enableMod)
        {
            return true; // Allow the original method to execute if the mod is disabled.
        }

        if (ModSettingsUI.settings.maxVisibleSlots < 1 || ModSettingsUI.settings.maxVisibleSlots > 10)
        {
            return true; // Allow the original method to execute if invalid configuration.
        }

        // Replicate the lazy initialization of the bundle letter cache.
        ___bundleLetterCache ??= (BundleLetter)LetterMaker.MakeLetter(LetterDefOf.BundleLetter);

        float currentY = baseY;

        int individualLettersToShow;
        int numberOfLettersToBundle;

        if (___letters.Count > ModSettingsUI.settings.maxVisibleSlots)
        {
            individualLettersToShow = ModSettingsUI.settings.maxVisibleSlots - 1;
            if (individualLettersToShow < 0) individualLettersToShow = 0;
            numberOfLettersToBundle = ___letters.Count - individualLettersToShow;
        }
        else
        {
            individualLettersToShow = ___letters.Count;
            numberOfLettersToBundle = 0;
        }

        if (numberOfLettersToBundle > 0)
        {
            for (int i = ___letters.Count - 1; i >= ___letters.Count - individualLettersToShow; i--)
            {
                currentY -= LetterButtonHeight;
                ___letters[i].DrawButtonAt(currentY);
                currentY -= LetterButtonSpacing;
            }

            ___tmpBundledLetters.Clear();
            ___tmpBundledLetters.AddRange(___letters.Take(numberOfLettersToBundle));

            currentY -= LetterButtonHeight;
            ___bundleLetterCache.SetLetters(___tmpBundledLetters);
            ___bundleLetterCache.DrawButtonAt(currentY);
            currentY -= LetterButtonSpacing;

            ___tmpBundledLetters.Clear();
        }
        else
        {
            // If there are no letters to bundle, just draw the individual letters.
            for (int i = ___letters.Count - 1; i >= 0; i--)
            {
                currentY -= LetterButtonHeight;
                ___letters[i].DrawButtonAt(currentY);
                currentY -= LetterButtonSpacing;
            }
        }

        ___lastTopYInt = currentY;

        // --- MOUSE-OVER/TOOLTIP PASS ---
        if (Event.current.type == EventType.Repaint)
        {
            currentY = baseY;

            for (int i = ___letters.Count - 1; i >= ___letters.Count - individualLettersToShow; i--)
            {
                currentY -= LetterButtonHeight;
                ___letters[i].CheckForMouseOverTextAt(currentY);
                currentY -= LetterButtonSpacing;
            }

            if (numberOfLettersToBundle > 0)
            {
                currentY -= LetterButtonHeight;
                ___bundleLetterCache.CheckForMouseOverTextAt(currentY);
                currentY -= LetterButtonSpacing;
            }
        }

        // Return false to prevent the original method from executing.
        return false;
    }
}


public class LetterCleaner : GameComponent
{
    public LetterCleaner(Game game)
    {
        // Constructor logic if needed
    }

    public override void GameComponentUpdate()
    {
        // Detect if delete key is pressed
        if (Input.GetKeyDown(KeyCode.Delete))
        {
            foreach (var letter in Find.LetterStack.LettersListForReading.ToList())
            {
                Find.LetterStack.RemoveLetter(letter);
            }
        }
    }
}

