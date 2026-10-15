using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RW17.Validation;

public static class Mod29Checks
{
    public const string PackageId = "RunningBugs.PermanentDarkness";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static void Run(Map map)
    {
        Require(ModsConfig.AnomalyActive, "Anomaly dependency missing");
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "PermanentDarkness");
        var controllerType = assembly.GetType("PermanentDarkness.PermanentDarknessController", true);
        var conditionType = assembly.GetType("PermanentDarkness.GameCondition_PermanentDarkness", true);
        var controller = Current.Game.GetComponent(controllerType);
        Require(controller != null, "Actual controller component missing");
        var def = DefDatabase<GameConditionDef>.GetNamed("PermanentDarkness");
        var hediffDef = DefDatabase<HediffDef>.GetNamed("PD_DarknessExposure");
        var stage1 = DefDatabase<WeatherDef>.GetNamed("PermanentDarkness_Stage1");
        var stage2 = DefDatabase<WeatherDef>.GetNamed("PermanentDarkness_Stage2");
        Require(def.conditionClass == conditionType && hediffDef.hediffClass.FullName == "PermanentDarkness.PDHediff_DarknessExposure", "Loaded XML class links incorrect");
        Require(DefDatabase<GameConditionDef>.GetNamed("SolarFlare").silencedByConditions.Contains(def), "Actual SolarFlare XML patch missing");
        Require(DefDatabase<IncidentDef>.GetNamed("PermanentDarkness").gameCondition == def && DefDatabase<RulePackDef>.GetNamed("PermanentDarkness") != null, "Incident/grammar XML references incorrect");
        Require(!Find.Maps.Any(m => m.gameConditionManager.ConditionIsActive(def)), "Fixture already has permanent darkness");
        var staticFields = controllerType.GetFields(Static).Where(f => !f.IsInitOnly).ToArray();
        var oldStatic = staticFields.Select(f => f.GetValue(null)).ToArray();
        var oldTick = Find.TickManager.TicksGame;
        var oldLetters = Find.LetterStack.LettersListForReading.ToArray();
        var oldMat = MatBases.Darkness.color;
        var oldShadow = DebugViewSettings.drawShadows;
        var oldGlow = map.skyManager.CurSkyGlow;
        var tracker = typeof(GameConditionManager).GetField("mapBrightnessTracker", Private).GetValue(map.gameConditionManager);
        var trackerFields = tracker.GetType().GetFields(Private);
        var oldTracker = trackerFields.Select(f => f.GetValue(tracker)).ToArray();
        var oldWeather = (map.weatherManager.curWeather, map.weatherManager.lastWeather, map.weatherManager.curWeatherAge);
        var fireField = typeof(FireWatcher).GetField("fireDanger", Private);
        var oldFire = fireField.GetValue(map.fireWatcher);
        var existingHediffs = Find.Maps.SelectMany(m => m.mapPawns.AllHumanlikeSpawned).ToDictionary(p => p, p => p.health.hediffSet.hediffs.ToArray());
        var oldBattle = Current.Game.battleLog;
        Pawn pawn = null;
        GameCondition condition = null;
        int start = Math.Max(15000, (oldTick / 15000 + 1) * 15000);
        void Set(string name, object value) => controllerType.GetField(name, Static).SetValue(null, value);
        object Get(string name) => controllerType.GetField(name, Static).GetValue(null);
        try
        {
            Current.Game.battleLog = new BattleLog();
            controllerType.GetMethod("Deactivate").Invoke(null, null);
            Find.TickManager.DebugSetTicksGame(start);
            condition = GameConditionMaker.MakeCondition(def, 60000);
            map.gameConditionManager.RegisterCondition(condition);
            Require(condition.Permanent && (bool)Get("IsActive") && (int)Get("StartTick") == start, "Actual condition Init did not activate permanent global controller");
            int delay = (int)Get("Delay");
            Require(delay >= 30001 && delay <= 45001 && condition.ForcedWeather() == stage1, "Initial delay or forced stage1 incorrect");
            Require(condition.SkyTarget(map)?.glow == 0f && condition.TransitionTicks == 300 && condition.SkyOverlays(map).Count == 1, "Actual sky/overlay contract changed");
            controller.GameComponentTick();
            Require((bool)Get("initialLetter") && Find.LetterStack.LettersListForReading.Count == oldLetters.Length + 1, "Actual initial letter tick missing");
            controller.GameComponentTick(); Require(Find.LetterStack.LettersListForReading.Count == oldLetters.Length + 1, "Initial letter repeated");
            int warning = ((start + delay - 10000 + 59) / 60) * 60;
            Find.TickManager.DebugSetTicksGame(warning); controller.GameComponentTick();
            Require((bool)Get("mainWarning") && Find.LetterStack.LettersListForReading.Count == oldLetters.Length + 2, "Actual warning stage missing");
            int main = ((start + delay + 59) / 60) * 60;
            Find.TickManager.DebugSetTicksGame(main); controller.GameComponentTick();
            Require((bool)Get("mainLetter") && condition.ForcedWeather() == stage2 && Find.LetterStack.LettersListForReading.Count == oldLetters.Length + 3, "Actual main-phase weather/letter missing");
            controller.GameComponentTick(); Require(Find.LetterStack.LettersListForReading.Count == oldLetters.Length + 3, "Main letter repeated");
            condition.GameConditionTick();
            Require((float)tracker.GetType().GetField("targetBrightness", Private).GetValue(tracker) == 0f, "Actual main condition tick did not request zero brightness");
            tracker.GetType().GetField("brightness", Private).SetValue(tracker, 0f);
            tracker.GetType().GetField("targetBrightness", Private).SetValue(tracker, 0f);
            tracker.GetType().GetField("lerp", Private).SetValue(tracker, 1f);
            map.skyManager.ForceSetCurSkyGlow(0f);
            var cell = map.AllCells.First(c => c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && c.GetThingList(map).Count == 0 && c.GetDoor(map) == null && map.glowGrid.GroundGlowAt(c) == 0f);
            pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            Require(!(bool)conditionType.GetMethod("AffectedByDarkness").Invoke(null, new object[] { pawn }) && !(bool)conditionType.GetMethod("InUnnaturalDarkness").Invoke(null, new object[] { pawn }), "Unspawned pawn must be excluded");
            GenSpawn.Spawn(pawn, cell, map);
            Require((bool)conditionType.GetMethod("AffectedByDarkness").Invoke(null, new object[] { pawn }) && (bool)conditionType.GetMethod("InUnnaturalDarkness").Invoke(null, new object[] { pawn }), "Actual player pawn/dark cell eligibility missing");
            Require(!(bool)conditionType.GetMethod("UnnaturalDarknessAt").Invoke(null, new object[] { cell, null }), "Null map must be safe");
            pawn.SetFaction(Faction.OfAncients);
            Require(!(bool)conditionType.GetMethod("AffectedByDarkness").Invoke(null, new object[] { pawn }), "Non-player human must be excluded");
            pawn.SetFaction(Faction.OfPlayer);
            Find.TickManager.DebugSetTicksGame(main + 1); condition.GameConditionTick();
            Require(!pawn.health.hediffSet.HasHediff(hediffDef), "Non-interval tick must not add exposure");
            Find.TickManager.DebugSetTicksGame(start); condition.GameConditionTick();
            Require(!pawn.health.hediffSet.HasHediff(hediffDef), "Initial phase must not add exposure");
            Find.TickManager.DebugSetTicksGame(main); condition.GameConditionTick(); condition.GameConditionTick();
            var exposure = pawn.health.hediffSet.hediffs.Single(h => h.def == hediffDef);
            Require(!exposure.ShouldRemove && !exposure.TryMergeWith(exposure), "Actual dark exposure remove/merge contract changed");
            var next = exposure.GetType().GetField("nextDamageTick", Private);
            pawn.DoTick();
            int firstDamage = (int)next.GetValue(exposure);
            Require(firstDamage >= main + 480 && firstDamage <= main + 720, "Actual Pawn.DoTick did not schedule fresh darkness damage");
            Find.TickManager.DebugSetTicksGame(firstDamage);
            float oldInjuries = pawn.health.hediffSet.hediffs.Where(h => h is Hediff_Injury).Sum(h => h.Severity);
            pawn.DoTick();
            Require(pawn.health.hediffSet.hediffs.Where(h => h is Hediff_Injury).Sum(h => h.Severity) > oldInjuries && !pawn.Dead && (int)next.GetValue(exposure) >= firstDamage + 180 && (int)next.GetValue(exposure) <= firstDamage + 300, "Actual pawn health tick did not inflict and reschedule damage");
            exposure.Notify_Downed(); Require((int)next.GetValue(exposure) == -1, "Downed notification did not reset damage schedule");
            map.skyManager.ForceSetCurSkyGlow(1f);
            Require(exposure.ShouldRemove && !(bool)conditionType.GetMethod("InUnnaturalDarkness").Invoke(null, new object[] { pawn }), "Actual bright-cell escape did not remove exposure");
            pawn.health.HealthTick(); Require(!pawn.health.hediffSet.hediffs.Contains(exposure), "Actual health removal did not clear escaped exposure");
            VerifyScribe(controller, controllerType, Set, Get);
            Find.TickManager.DebugSetTicksGame(((main + 299) / 300) * 300);
            fireField.SetValue(map.fireWatcher, 100f);
            var monitor = map.GetComponent(assembly.GetType("PermanentDarkness.RainMonitorMapComponent", true));
            Require(monitor != null, "Actual rain monitor component missing");
            monitor.MapComponentTick(); Require(map.weatherManager.curWeather == DefDatabase<WeatherDef>.GetNamed("Rain"), "Actual rain safety did not respond to controlled real fire-danger state");
            condition.End();
            Require(!(bool)Get("IsActive") && (int)Get("StartTick") == 0 && (int)Get("Delay") == 0 && !Find.Maps.Any(m => m.gameConditionManager.ConditionIsActive(def)), "Actual End/global cleanup failed");
            Require(MatBases.Darkness.color == oldMat, "Actual deactivate did not restore original darkness material color");
        }
        finally
        {
            Scribe.ForceStop();
            foreach (var m in Find.Maps)
                foreach (var c in m.gameConditionManager.ActiveConditions.Where(c => c.def == def).ToArray()) c.End();
            for (int i = 0; i < staticFields.Length; i++) staticFields[i].SetValue(null, oldStatic[i]);
            if (pawn != null && !pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
            foreach (var pair in existingHediffs)
                foreach (var h in pair.Key.health.hediffSet.hediffs.Where(h => h.def == hediffDef && !pair.Value.Contains(h)).ToArray()) pair.Key.health.RemoveHediff(h);
            Find.TickManager.DebugSetTicksGame(oldTick);
            for (int i = 0; i < trackerFields.Length; i++) trackerFields[i].SetValue(tracker, oldTracker[i]);
            map.skyManager.ForceSetCurSkyGlow(oldGlow);
            map.weatherManager.curWeather = oldWeather.Item1; map.weatherManager.lastWeather = oldWeather.Item2; map.weatherManager.curWeatherAge = oldWeather.Item3;
            fireField.SetValue(map.fireWatcher, oldFire);
            MatBases.Darkness.color = oldMat; DebugViewSettings.drawShadows = oldShadow;
            Current.Game.battleLog = oldBattle;
            foreach (var letter in Find.LetterStack.LettersListForReading.Where(l => !oldLetters.Contains(l)).ToArray()) Find.LetterStack.RemoveLetter(letter);
        }
        Log.Message("RW17 Mod29: actual condition/controller/weather/letters, unique exposure and real pawn tick damage/removal, fire rain control and controller/settings Scribe passed; GPU/door/multiple map/reload pending");
    }

    private static void VerifyScribe(GameComponent component, Type type, Action<string, object> set, Func<string, object> get)
    {
        var path = Path.Combine(GenFilePaths.SaveDataFolderPath, "Mod29ControllerFixture.xml");
        Require(Scribe.mode == LoadSaveMode.Inactive && !File.Exists(path), "Scribe fixture busy/existing");
        var names = new[] { "StartTick", "Delay", "IsActive", "initialLetter", "mainWarning", "mainLetter" };
        var expected = names.Select(get).ToArray();
        Scribe.saver.InitSaving(path, "Mod29ControllerFixture"); component.ExposeData(); Scribe.saver.FinalizeSaving();
        set("StartTick", 1); set("Delay", 1); set("IsActive", false); set("initialLetter", false); set("mainWarning", false); set("mainLetter", false);
        Scribe.loader.InitLoading(path); component.ExposeData(); Scribe.loader.FinalizeLoading();
        Require(names.Select(get).SequenceEqual(expected), "Actual controller Scribe keys/stages failed roundtrip");
        var settings = (ModSettings)type.Assembly.GetType("PermanentDarkness.ModSettingsUI").GetField("settings").GetValue(null);
        var fields = new[] { "darknessControl", "darknessLevel", "shadowControl" }.Select(n => settings.GetType().GetField(n)).ToArray();
        var old = fields.Select(f => f.GetValue(settings)).ToArray();
        var settingsPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "Mod29SettingsFixture.xml");
        Require(!File.Exists(settingsPath), "Settings fixture exists");
        try
        {
            fields[0].SetValue(settings, true); fields[1].SetValue(settings, 1.2f); fields[2].SetValue(settings, true);
            Scribe.saver.InitSaving(settingsPath, "Mod29SettingsFixture"); settings.ExposeData(); Scribe.saver.FinalizeSaving();
            fields[0].SetValue(settings, false); fields[1].SetValue(settings, 0f); fields[2].SetValue(settings, false);
            Scribe.loader.InitLoading(settingsPath); settings.ExposeData(); Scribe.loader.FinalizeLoading();
            Require((bool)fields[0].GetValue(settings) && (float)fields[1].GetValue(settings) == 1.2f && (bool)fields[2].GetValue(settings), "Actual brightness/shadow settings Scribe roundtrip failed");
        }
        finally { for (int i = 0; i < fields.Length; i++) fields[i].SetValue(settings, old[i]); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Mod29: " + message);
    }
}
