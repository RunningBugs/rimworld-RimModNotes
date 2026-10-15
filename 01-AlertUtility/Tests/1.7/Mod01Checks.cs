using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod01Checks
{
    public const string PackageId = "RunningBugs.AlertUtility";
    public static void Run(Map map)
    {
        var target = AccessTools.Method(typeof(PlaySettings), "DoPlaySettingsGlobalControls");
        if (Harmony.GetPatchInfo(target)?.Postfixes.Any(p => p.PatchMethod.DeclaringType?.FullName
            == "AlertUtility.ToggleIconPatcher" && p.PatchMethod.Name == "AddIcon") != true)
            throw new Exception("Mod01: concrete toolbar postfix not registered");
        var type = AccessTools.TypeByName("AlertUtility.AlertUtility");
        var component = Find.World.GetComponent(type);
        if (component == null) throw new Exception("Mod01: WorldComponent not created");
        var events = (IList)type.GetMethod("GetEvents").Invoke(null, null);
        var saved = events.Cast<object>().ToArray();
        var letters = Find.LetterStack.LettersListForReading.ToArray();
        var oldTicks = Find.TickManager.TicksGame;
        var oldSpeed = Find.TickManager.CurTimeSpeed;
        var oldPrePause = Find.TickManager.prePauseTimeSpeed;
        var fadeFields = new[] { "sourceColor", "targetColor", "sourceTime", "targetTime" }
            .Select(name => AccessTools.Field(typeof(ScreenFader), name)).ToArray();
        var oldFade = fadeFields.Select(field => field.GetValue(null)).ToArray();
        var windowType = AccessTools.TypeByName("AlertUtility.TimerSetWindow");
        var window = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(windowType);
        var multiplier = AccessTools.Method(windowType, "getMultiplier");
        var expected = new[] { 1, GenDate.TicksPerHour, GenDate.TicksPerDay, GenDate.TicksPerYear, 1 };
        for (var i = 0; i < expected.Length; i++)
            if ((int)multiplier.Invoke(window, new object[] { i }) != expected[i])
                throw new Exception("Mod01: timer unit multiplier failed");
        try
        {
            Console.WriteLine($"RW17_MOD01_STAGE entry fading={ScreenFader.IsFading()} player_control={Current.Game.PlayerHasControl} speed={oldSpeed}");
            // The first playable frame can still be in the intro fade, which rejects Pause().
            if (ScreenFader.IsFading()) ScreenFader.SetColor(UnityEngine.Color.clear);
            if (!Current.Game.PlayerHasControl)
                throw new Exception("Mod01: fixture has no player control after completing intro fade");
            Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
            if (Find.TickManager.CurTimeSpeed != TimeSpeed.Normal)
                throw new Exception("Mod01: fixture could not set normal speed before pause test");
            events.Clear();
            var boundary = (oldTicks / 60 + 1) * 60;
            var eventType = type.GetNestedType("Event");
            var due = Activator.CreateInstance(eventType, boundary - 2, "RW17 timer due");
            var future = Activator.CreateInstance(eventType, boundary + 600, "RW17 timer future");
            type.GetMethod("Add").Invoke(null, new[] { due });
            type.GetMethod("Add").Invoke(null, new[] { future });
            Find.TickManager.DebugSetTicksGame(boundary - 1);
            component.WorldComponentTick();
            if (events.Count != 2) throw new Exception("Mod01: timer ran outside 60-tick boundary");
            Find.TickManager.DebugSetTicksGame(boundary);
            component.WorldComponentTick();
            var received = Find.LetterStack.LettersListForReading.Except(letters).ToList();
            if (events.Contains(due)) throw new Exception("Mod01: due event was not removed");
            if (!events.Contains(future)) throw new Exception("Mod01: future event was lost");
            if (received.Count != 1) throw new Exception("Mod01: expected one alarm letter, received " + received.Count);
            if (received[0].def != LetterDefOf.NeutralEvent) throw new Exception("Mod01: alarm letter used wrong Def");
            if (Find.TickManager.CurTimeSpeed != TimeSpeed.Paused)
                throw new Exception($"Mod01: alarm did not pause from Normal; control={Current.Game.PlayerHasControl} fading={ScreenFader.IsFading()} speed={Find.TickManager.CurTimeSpeed}");
            component.WorldComponentTick();
            if (Find.LetterStack.LettersListForReading.Except(letters).Count() != 1)
                throw new Exception("Mod01: alarm repeated after removal");
        }
        finally
        {
            events.Clear();
            foreach (var e in saved) events.Add(e);
            foreach (var letter in Find.LetterStack.LettersListForReading.Except(letters).ToList())
                Find.LetterStack.RemoveLetter(letter);
            Find.TickManager.DebugSetTicksGame(oldTicks);
            Find.TickManager.CurTimeSpeed = oldSpeed;
            Find.TickManager.prePauseTimeSpeed = oldPrePause;
            for (var i = 0; i < fadeFields.Length; i++) fadeFields[i].SetValue(null, oldFade[i]);
            AccessTools.Field(typeof(ScreenFader), "fadeTextureDirty").SetValue(null, true);
        }
    }
}
