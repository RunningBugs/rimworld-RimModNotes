using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace RW17.Validation;

public static class Mod22Checks
{
    public const string PackageId = "RunningBugs.MoreMapSeeds";
    private sealed class FieldFixture { private int value = 47; public int Value => value; }
    private static string copiedSeed;

    public static void Run(Map map)
    {
        var original = typeof(GenText).GetMethod("RandomSeedString");
        Require(Harmony.GetPatchInfo(original)?.Prefixes.Any(p => p.PatchMethod.DeclaringType?.FullName == "MoreMapSeeds.Patch") == true, "Actual random seed prefix not registered");
        var rule = DefDatabase<RulePackDef>.GetNamed("MCS_SeedGenerator");
        Require(rule.include.Contains(RimWorld.RulePackDefOf.SeedGenerator), "Vanilla seed rule fallback include changed");
        Require(LanguageDatabase.activeLanguage.TryGetStringsFromFile("NamesExtended/SeedGenerator", out var words) || LanguageDatabase.defaultLanguage.TryGetStringsFromFile("NamesExtended/SeedGenerator", out words), "Expanded seed string file not loaded");
        var vocabulary = words.Select(w => w.Trim().ToLower()).ToHashSet();
        string[] first;
        Rand.PushState(22017);
        try { first = Enumerable.Range(0, 64).Select(_ => GenText.RandomSeedString()).ToArray(); }
        finally { Rand.PopState(); }
        Rand.PushState(22017);
        try
        {
            Require(first.SequenceEqual(Enumerable.Range(0, 64).Select(_ => GenText.RandomSeedString())), "Actual seeded game grammar sequence is not deterministic");
        }
        finally { Rand.PopState(); }
        Require(first.All(s => !string.IsNullOrWhiteSpace(s) && s == s.ToLower()), "Actual seeds must remain nonempty lowercase strings");
        if (vocabulary.Count > 0)
        {
            Require(vocabulary.Count > 100 && first.Any(vocabulary.Contains), "Actual GenText prefix never used the populated MOD expanded dictionary");
        }
        else
        {
            var registered = Harmony.GetPatchInfo(original).Prefixes.Single(p => p.PatchMethod.DeclaringType?.FullName == "MoreMapSeeds.Patch");
            var binding = new Harmony(registered.owner);
            binding.Unpatch(original, registered.PatchMethod);
            try
            {
                Rand.PushState(22017);
                try { Require(first.SequenceEqual(Enumerable.Range(0, 64).Select(_ => GenText.RandomSeedString())), "Empty corpus fallback differs from actual unpatched vanilla method"); }
                finally { Rand.PopState(); }
            }
            finally { binding.Patch(original, prefix: new HarmonyMethod(registered.PatchMethod)); }
        }
        var letterDef = DefDatabase<LetterDef>.GetNamed("NeutralEventCopyLetter");
        Require(letterDef.letterClass.FullName == "MoreMapSeeds.StandardLetterCopyOnClick", "Custom copy letter class did not load");
        var letter = Find.LetterStack.LettersListForReading.FirstOrDefault(l => l.def == letterDef);
        Require(letter is ChoiceLetter, "Actual GameComponent.FinalizeInit did not issue a seed copy letter");
        Require(((ChoiceLetter)letter).Text.Resolve() == Find.World.info.seedString.CapitalizeFirst(), "Actual seed letter display does not match ChoiceLetter capitalization of world seed");
        Require(Find.TickManager.Paused, "Actual FinalizeInit did not preserve automatic pause");
        var copySetter = typeof(GUIUtility).GetProperty("systemCopyBuffer").GetSetMethod();
        var capture = typeof(Mod22Checks).GetMethod(nameof(CaptureCopy), BindingFlags.Static | BindingFlags.NonPublic);
        var spy = new Harmony("rw17.validation.mod22.copy");
        var windows = Find.WindowStack.Windows.ToArray();
        try
        {
            copiedSeed = null;
            spy.Patch(copySetter, prefix: new HarmonyMethod(capture));
            letter.OpenLetter();
            Require(copiedSeed == Find.World.info.seedString, "Actual OpenLetter did not pass the exact original seed to clipboard setter");
        }
        finally
        {
            spy.Unpatch(copySetter, capture);
            foreach (var window in Find.WindowStack.Windows.Where(w => !windows.Contains(w)).ToArray())
                Find.WindowStack.TryRemove(window, false);
        }
        var start = letterDef.letterClass.Assembly.GetType("MoreMapSeeds.Start", true);
        var ruleHelper = start.GetMethod("GetRulePack", BindingFlags.NonPublic | BindingFlags.Static);
        var pack = (RulePack)ruleHelper.Invoke(null, new object[] { rule });
        Require(pack.Rules.SequenceEqual(rule.RulesImmediate), "Actual private rulePack helper returned the wrong game data");
        var helper = start.GetMethod("GetField", BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(typeof(int));
        var box = new FieldFixture();
        var pair = ((FieldInfo, int))helper.Invoke(null, new object[] { box, "value" });
        Require(pair.Item1.Name == "value" && pair.Item2 == box.Value, "Actual generic private field helper read wrong fixture field/value");
        Log.Message("RW17 Mod22: actual GenText grammar and " + (vocabulary.Count > 0 ? "expanded dictionary" : "empty-corpus vanilla fallback") + ", deterministic replay, startup seed letter/pause, exact OpenLetter clipboard argument and compiled reflection helpers passed; OS clipboard/window pixels pending");
    }

    private static bool CaptureCopy(string __0)
    {
        copiedSeed = __0;
        return false;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Mod22: " + message);
    }
}
