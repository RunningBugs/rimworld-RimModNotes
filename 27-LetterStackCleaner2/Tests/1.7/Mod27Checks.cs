using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RW17.Validation;

public static class Mod27Checks
{
    public const string PackageId = "RunningBugs.LetterStackCleaner2";
    private static LetterStack fixture;
    private static readonly List<Letter> removed = new();
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Mod27: " + message); }
    private static void ObserveRemove(LetterStack __instance, Letter let) { if (__instance == fixture) removed.Add(let); }

    public static void Run(Map map)
    {
        var mod = LoadedModManager.RunningMods.Single(m => m.PackageId.Equals(PackageId, StringComparison.OrdinalIgnoreCase));
        var assembly = mod.assemblies.loadedAssemblies.Single(a => a.GetName().Name == "LetterStackCleaner2");
        var type = assembly.GetType("LetterStackCleaner.LetterStackCleaner", true);
        Require(Current.Game.components.Count(c => c.GetType() == type) == 1, "exact actual auto-created GameComponent registration");
        var cleaner = Current.Game.GetComponent(type);
        Require(cleaner != null && type.GetMethod("GameComponentOnGUI").DeclaringType == type, "real game GUI lifecycle override");
        var original = Current.Game.letterStack; var oldEvent = Event.current; var renderer = Find.World.renderer; var oldWorld = renderer.wantedMode;
        var remove = typeof(LetterStack).GetMethod("RemoveLetter");
        var observer = typeof(Mod27Checks).GetMethod(nameof(ObserveRemove), BindingFlags.Static | BindingFlags.NonPublic);
        var harmony = new Harmony("rw17.validation.mod27.removal");
        harmony.Patch(remove, prefix: new HarmonyMethod(observer));
        try
        {
            foreach (var mode in new[] { WorldRenderMode.None, WorldRenderMode.Planet })
            {
                renderer.wantedMode = mode;
                fixture = new LetterStack(); Current.Game.letterStack = fixture; removed.Clear();
                var letters = new[] {
                    LetterMaker.MakeLetter("RW17 neutral", "temporary actual native letter", LetterDefOf.NeutralEvent),
                    LetterMaker.MakeLetter("RW17 positive", "temporary actual native letter", LetterDefOf.PositiveEvent),
                    LetterMaker.MakeLetter("RW17 threat", "temporary actual native letter", LetterDefOf.ThreatSmall)
                };
                fixture.LettersListForReading.AddRange(letters);
                foreach (var input in new[] {
                    new Event { type = EventType.Layout }, new Event { type = EventType.Repaint },
                    new Event { type = EventType.KeyDown, keyCode = KeyCode.Delete },
                    new Event { type = EventType.KeyUp, keyCode = KeyCode.X },
                    new Event { type = EventType.Used, keyCode = KeyCode.Delete }
                })
                {
                    Event.current = input; cleaner.GameComponentOnGUI();
                    Require(fixture.LettersListForReading.SequenceEqual(letters) && removed.Count == 0, "non-release/wrong-key/consumed-event must preserve all letters in " + mode);
                }
                Require((mode == WorldRenderMode.Planet) == WorldRendererUtility.WorldSelected, "actual renderer world/map state fixture");
                Event.current = new Event { type = EventType.KeyUp, keyCode = KeyCode.Delete };
                // Use the actual GameComponentUtility fanout, not a copied cleanup loop.
                GameComponentUtility.GameComponentOnGUI();
                Require(fixture.LettersListForReading.Count == 0 && removed.SequenceEqual(letters), "real GUI fanout Delete release must remove every native letter exactly once in " + mode);
                cleaner.GameComponentOnGUI(); Require(removed.SequenceEqual(letters), "empty repeated key-release must remove no extra letters");
                fixture.LettersListForReading.Add(letters[0]); removed.Clear();
                Event.current = new Event { type = EventType.KeyUp, keyCode = KeyCode.Delete }; cleaner.GameComponentOnGUI();
                Require(fixture.LettersListForReading.Count == 0 && removed.SequenceEqual(new[] { letters[0] }), "single-letter boundary");
            }
        }
        finally { harmony.Unpatch(remove, observer); Current.Game.letterStack = original; Event.current = oldEvent; renderer.wantedMode = oldWorld; fixture = null; removed.Clear(); }
        Log.Message("RW17 Mod27: actual GameComponent registration/GameComponentUtility GUI fanout/native letters RemoveLetter calls, three/single/empty boundaries and map/world Delete-release/wrong-key/down/consumed negatives passed; physical X11 Delete and arbitrary focused-editor/mod combinations pending");
    }
}
