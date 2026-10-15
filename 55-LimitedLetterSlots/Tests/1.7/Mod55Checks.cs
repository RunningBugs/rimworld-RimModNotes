using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Verse;

namespace RW17.Validation;

public static class Mod55Checks
{
    public const string PackageId = "RunningBugs.LimitedLetterSlots";
    private static readonly List<(int id, float y)> draw = new();
    private static readonly List<(int id, float y)> hover = new();

    private sealed class SpyLetter : Letter
    {
        public int index;
        public override void DrawButtonAt(float y) => draw.Add((index, y));
        public override void CheckForMouseOverTextAt(float y) => hover.Add((index, y));
        public override void OpenLetter() { }
        protected override string GetMouseoverText() => "fixture";
    }

    private sealed class SpyBundle : BundleLetter
    {
        public override void DrawButtonAt(float y) => draw.Add((-1, y));
        public override void CheckForMouseOverTextAt(float y) => hover.Add((-1, y));
    }

    private static (LetterStack stack, SpyBundle bundle) MakeStack(int count)
    {
        var stack = new LetterStack();
        var bundle = new SpyBundle { def = LetterDefOf.BundleLetter };
        typeof(LetterStack).GetField("bundleLetterCache", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stack, bundle);
        for (var i = 0; i < count; i++)
            stack.LettersListForReading.Add(new SpyLetter { index = i, Label = "fixture " + i, def = LetterDefOf.NeutralEvent });
        return (stack, bundle);
    }

    private static void Render(LetterStack stack)
    {
        draw.Clear();
        hover.Clear();
        stack.LettersOnGUI(400f);
    }

    public static void Run(Map map)
    {
        var target = typeof(LetterStack).GetMethod("LettersOnGUI");
        var registered = Harmony.GetPatchInfo(target)?.Prefixes.SingleOrDefault(p => p.PatchMethod.DeclaringType?.FullName == "LimitedLetterSlots.LetterStack_LettersOnGUI_Patch");
        Require(registered != null, "Specific GUI prefix not registered");
        var assembly = registered.PatchMethod.DeclaringType.Assembly;
        var settings = (ModSettings)assembly.GetType("LimitedLetterSlots.ModSettingsUI", true).GetField("settings").GetValue(null);
        var enabled = settings.GetType().GetField("enableMod");
        var limit = settings.GetType().GetField("maxVisibleSlots");
        var oldEnabled = enabled.GetValue(settings);
        var oldLimit = limit.GetValue(settings);
        var oldEvent = Event.current;
        try
        {
            Event.current = new Event { type = EventType.Repaint };
            enabled.SetValue(settings, true);
            limit.SetValue(settings, 3);
            var fixture = MakeStack(8);
            Render(fixture.stack);
            Require(draw.SequenceEqual(new[] { (7, 370f), (6, 328f), (-1, 286f) }), "Actual GUI entry must draw two newest letters plus bundle in three slots");
            Require(hover.SequenceEqual(draw) && fixture.stack.LastTopY == 274f, "Actual repaint mouseover/layout did not match draw slots");
            var bundled = (List<Letter>)typeof(BundleLetter).GetField("bundledLetters", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fixture.bundle);
            Require(bundled.SequenceEqual(fixture.stack.LettersListForReading.Take(6)), "Actual bundle must retain six oldest letters");
            Require(((List<Letter>)typeof(LetterStack).GetField("tmpBundledLetters", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fixture.stack)).Count == 0, "Temporary bundle list was not cleared");
            limit.SetValue(settings, 1);
            Render(fixture.stack);
            Require(draw.SequenceEqual(new[] { (-1, 370f) }) && fixture.stack.LastTopY == 358f, "One-slot boundary must show only one bundle");
            limit.SetValue(settings, 10);
            var large = MakeStack(12);
            Render(large.stack);
            Require(draw.Count == 10 && draw.Take(9).Select(p => p.id).SequenceEqual(Enumerable.Range(3, 9).Reverse()) && draw.Last().id == -1 && large.stack.LastTopY == -20f,
                "Ten-slot valid boundary must show nine newest letters and one bundle");
            limit.SetValue(settings, 3);
            var small = MakeStack(2);
            Render(small.stack);
            Require(draw.SequenceEqual(new[] { (1, 370f), (0, 328f) }) && small.stack.LastTopY == 316f, "Unbundled two-letter case changed");
            var empty = new LetterStack();
            Render(empty);
            Require(draw.Count == 0 && empty.LastTopY == 400f && empty.BundleLetter != null, "Empty/lazy bundle initialization failed");
            var binding = new Harmony(registered.owner);
            binding.Unpatch(target, registered.PatchMethod);
            (int id, float y)[] native;
            float nativeY;
            try { Render(fixture.stack); native = draw.ToArray(); nativeY = fixture.stack.LastTopY; }
            finally { binding.Patch(target, prefix: new HarmonyMethod(registered.PatchMethod)); }
            foreach (var configuration in new[] { (false, 3), (true, 0), (true, 11) })
            {
                enabled.SetValue(settings, configuration.Item1);
                limit.SetValue(settings, configuration.Item2);
                Render(fixture.stack);
                Require(draw.SequenceEqual(native) && fixture.stack.LastTopY == nativeY, "Disabled/invalid limit must fall back to actual unpatched native GUI logic");
            }
            var path = Path.Combine(GenFilePaths.SaveDataFolderPath, "Mod55SettingsFixture.xml");
            Require(!File.Exists(path) && Scribe.mode == LoadSaveMode.Inactive, "Settings fixture exists or Scribe is busy");
            enabled.SetValue(settings, false);
            limit.SetValue(settings, 7);
            Scribe.saver.InitSaving(path, "Mod55SettingsFixture");
            settings.ExposeData();
            Scribe.saver.FinalizeSaving();
            enabled.SetValue(settings, true);
            limit.SetValue(settings, 3);
            Scribe.loader.InitLoading(path);
            settings.ExposeData();
            Scribe.loader.FinalizeLoading();
            Require(!(bool)enabled.GetValue(settings) && (int)limit.GetValue(settings) == 7, "Actual settings Scribe roundtrip failed");
            VerifyDeleteControl(assembly);
        }
        finally
        {
            Scribe.ForceStop();
            enabled.SetValue(settings, oldEnabled);
            limit.SetValue(settings, oldLimit);
            Event.current = oldEvent;
        }
        Log.Message("RW17 Mod55: actual GUI prefix with draw observers/bundles/slots/fallback, settings roundtrip and actual Input.GetKeyDown delete flow passed; visible pixels/physical keyboard pending");
    }

    private static void VerifyDeleteControl(Assembly assembly)
    {
        var original = Current.Game.letterStack;
        var stack = MakeStack(3).stack;
        var cleaner = Current.Game.GetComponent(assembly.GetType("LimitedLetterSlots.LetterCleaner", true));
        Require(cleaner != null, "Actual cleaner GameComponent missing");
        var key = Keyboard.current[Key.Delete];
        var pressed = typeof(ButtonControl).GetField("_wasPressedThisFrame", BindingFlags.Instance | BindingFlags.NonPublic);
        var released = typeof(ButtonControl).GetField("_wasReleasedThisFrame", BindingFlags.Instance | BindingFlags.NonPublic);
        var repeated = typeof(ButtonControl).GetField("_wasRepeatedThisFrame", BindingFlags.Instance | BindingFlags.NonPublic);
        var value = typeof(AxisControl).GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic);
        var snapshot = new[] { pressed.GetValue(key), released.GetValue(key), repeated.GetValue(key), value.GetValue(key) };
        try
        {
            Current.Game.letterStack = stack;
            pressed.SetValue(key, false);
            cleaner.GameComponentUpdate();
            Require(stack.LettersListForReading.Count == 3, "No key press must preserve temporary letters");
            value.SetValue(key, 0f);
            typeof(ButtonControl).GetMethod("SetPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(key, new object[] { true });
            Require(Input.GetKeyDown(KeyCode.Delete), "Actual new input mapping did not observe controlled Delete state");
            cleaner.GameComponentUpdate();
            Require(stack.LettersListForReading.Count == 0, "Actual cleaner did not remove all temporary letters");
        }
        finally
        {
            pressed.SetValue(key, snapshot[0]);
            released.SetValue(key, snapshot[1]);
            repeated.SetValue(key, snapshot[2]);
            value.SetValue(key, snapshot[3]);
            Current.Game.letterStack = original;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Mod55: " + message);
    }
}
