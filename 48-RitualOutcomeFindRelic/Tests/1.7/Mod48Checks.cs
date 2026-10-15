using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace RW17.Validation;

public static class Mod48Checks
{
    public const string PackageId = "RunningBugs.RitualOutcomeFindRelic";
    private static int forcedNativeFailure;
    private static float observedParentPoints;
    private static bool ForceNativeFailure(ref bool __result) { forcedNativeFailure++; __result = false; return false; }
    private static void ObserveCanRun(QuestScriptDef __instance, Slate slate)
    {
        if (__instance.defName == "RelicHunt") observedParentPoints = slate.Get("points", float.NaN);
    }

    public static void Run(Map map)
    {
        Require(ModsConfig.IdeologyActive, "Ideology dependency missing");
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "FindRelic");
        var workerType = assembly.GetType("FindRelic.RitualAttachableOutcomeEffectWorker_FindRelic", true);
        var generate = workerType.GetMethod("GenerateAndSendQuest");
        var refresh = assembly.GetType("FindRelic.FR_GameComp", true).GetMethod("RefreshEffectDesc");
        var settingsType = assembly.GetType("FindRelic.Settings", true);
        var chance = settingsType.GetField("FindRelicChance"); var oldChance = chance.GetValue(null);
        var outcome = DefDatabase<RitualAttachableOutcomeEffectDef>.GetNamed("FindRelic");
        Require(outcome.workerClass == workerType && outcome.Worker.GetType() == workerType, "Actual XML worker not loaded");
        var oldDesc = outcome.effectDesc;
        var oldManager = Current.Game.questManager;
        var primary = Faction.OfPlayer.ideos.PrimaryIdeo;
        Require(primary != null, "Player primary ideo absent");
        var oldPrecepts = primary.PreceptsListForReading.ToArray();
        var generated = typeof(Precept_Relic).GetField("relicGenerated", BindingFlags.Instance | BindingFlags.NonPublic);
        var thing = typeof(Precept_Relic).GetField("generatedRelic", BindingFlags.Instance | BindingFlags.NonPublic);
        var oldRelics = primary.GetAllPreceptsOfType<Precept_Relic>().ToDictionary(p => p, p => (generated.GetValue(p), thing.GetValue(p)));
        var oldLetters = Find.LetterStack.LettersListForReading.ToArray();
        var oldObjects = Find.WorldObjects.AllWorldObjects.ToArray();
        var nativeTry = typeof(QuestPart_SubquestGenerator).GetMethod("TryGenerateSubquest", BindingFlags.Instance | BindingFlags.NonPublic);
        var trySpy = typeof(Mod48Checks).GetMethod(nameof(ForceNativeFailure), BindingFlags.Static | BindingFlags.NonPublic);
        var canRun = typeof(QuestScriptDef).GetMethod("CanRun", new[] { typeof(Slate), typeof(IIncidentTarget) });
        var canSpy = typeof(Mod48Checks).GetMethod(nameof(ObserveCanRun), BindingFlags.Static | BindingFlags.NonPublic);
        var observer = new Harmony("RW17.Validation.Mod48");
        var newThings = new List<Thing>();
        Precept_Relic tempRelic = null;
        try
        {
            Current.Game.questManager = new QuestManager();
            tempRelic = (Precept_Relic)PreceptMaker.MakePrecept(PreceptDefOf.IdeoRelic);
            primary.AddPrecept(tempRelic, init: true, generatingFor: Faction.OfPlayer.def);
            foreach (var relic in primary.GetAllPreceptsOfType<Precept_Relic>()) generated.SetValue(relic, true);
            Require(!(bool)generate.Invoke(null, null) && Find.QuestManager.QuestsListForReading.Count == 0, "No existing parent/no legal ungenerated relic must reject new quest");
            generated.SetValue(tempRelic, false);
            var worker = outcome.Worker;
            var look = new LookTargets(map.Center, map);
            chance.SetValue(null, 0f);
            worker.Apply(new Dictionary<Pawn, int>(), null, null, out var text, ref look);
            Require(text == null && Find.QuestManager.QuestsListForReading.Count == 0, "Zero chance must not generate quest or claim ritual success");
            observer.Patch(canRun, prefix: new HarmonyMethod(canSpy)); observedParentPoints = float.NaN;
            float expectedPoints = StorytellerUtility.DefaultThreatPointsNow(Find.World);
            chance.SetValue(null, 1f);
            worker.Apply(new Dictionary<Pawn, int>(), null, null, out text, ref look);
            Require(text == outcome.letterInfoText, "Actual chance1 Apply did not generate valid quest and report success");
            var parent = Find.QuestManager.QuestsListForReading.Single(q => q.root.defName == "RelicHunt" && q.parent == null);
            var generator = parent.PartsListForReading.OfType<QuestPart_SubquestGenerator_RelicHunt>().Single();
            Require(generator.relic == tempRelic && !tempRelic.CanGenerateRelic && parent.GetSubquests().Count() == 1, "Actual parent/relic first subquest linkage invalid");
            Require(!float.IsNaN(observedParentPoints) && Math.Abs(observedParentPoints - expectedPoints) < 0.01f, "Actual parent CanRun did not receive real current threat-points slate");
            Require(primary.GetAllPreceptsOfType<Precept_Relic>().All(p => !p.CanGenerateRelic), "Existing-parent edge fixture must have no remaining ungenerated relic");
            Require((bool)generate.Invoke(null, null) && parent.GetSubquests().Count() == 2 && Find.QuestManager.QuestsListForReading.Count(q => q.root.defName == "RelicHunt" && q.parent == null) == 1, "Actual existing parent must advance without new relic and without creating duplicate parent");
            int count = Find.QuestManager.QuestsListForReading.Count;
            Require(!(bool)generate.Invoke(null, null) && Find.QuestManager.QuestsListForReading.Count == count, "Active-subquest cap must still reject excess generation");
            worker.Apply(new Dictionary<Pawn, int>(), null, null, out text, ref look);
            Require(text == null && Find.QuestManager.QuestsListForReading.Count == count, "Failed capped Apply must not claim outcome success");
            var helper = workerType.GetMethod("GenerateSubquestForExistingRelicHunt", BindingFlags.Static | BindingFlags.NonPublic);
            Require(!(bool)helper.Invoke(null, new object[] { new Quest() }), "Missing relic generator must return false");
            var relicValue = generator.relic; generator.relic = null;
            try { Require(!(bool)helper.Invoke(null, new object[] { parent }), "Null relic generator must return false"); }
            finally { generator.relic = relicValue; }
            int oldActive = generator.maxActiveSubquests, oldSuccess = generator.maxSuccessfulSubquests;
            try
            {
                generator.maxActiveSubquests = 10; generator.maxSuccessfulSubquests = 0;
                Require(!(bool)generate.Invoke(null, null) && Find.QuestManager.QuestsListForReading.Count == count, "Successful+pending ceiling must reject completed information target");
                generator.maxSuccessfulSubquests = 5; forcedNativeFailure = 0;
                observer.Patch(nativeTry, prefix: new HarmonyMethod(trySpy));
                Require((bool)generate.Invoke(null, null) && forcedNativeFailure == 1 && parent.GetSubquests().Count() == 3, "Actual fallback did not try remaining real CanRun scripts and generate/link subquest after controlled native failure");
                observer.Unpatch(nativeTry, trySpy);
            }
            finally { generator.maxActiveSubquests = oldActive; generator.maxSuccessfulSubquests = oldSuccess; }
            chance.SetValue(null, 0.75f); string percent = 0.75f.ToStringPercent("0");
            foreach (var sample in new[] { ("Gain 25% knowledge", "Gain " + percent + " knowledge"), ("Gain 12.5% knowledge", "Gain " + percent + " knowledge"), ("already {0} odds", "already " + percent + " odds"), ("100% foo 20% bar", percent + " foo 20% bar"), ("no number % text", "no number % text") })
                Require((string)refresh.Invoke(null, new object[] { sample.Item1 }) == sample.Item2, "Actual description percentage refresh failed: " + sample.Item1);
            var component = Current.Game.GetComponent(assembly.GetType("FindRelic.FR_GameComp", true));
            Require(component != null, "Actual description GameComponent missing");
            outcome.effectDesc = "{0} chance"; component.FinalizeInit();
            Require(outcome.effectDesc == percent + " chance", "Actual FinalizeInit did not refresh description from configured chance");
            var settings = (ModSettings)Activator.CreateInstance(settingsType);
            var path = Path.Combine(GenFilePaths.SaveDataFolderPath, "Mod48SettingsFixture.xml");
            Require(Scribe.mode == LoadSaveMode.Inactive && !File.Exists(path), "Scribe busy/fixture exists");
            chance.SetValue(null, 0.23f); Scribe.saver.InitSaving(path, "Mod48SettingsFixture"); settings.ExposeData(); Scribe.saver.FinalizeSaving();
            chance.SetValue(null, 1f); Scribe.loader.InitLoading(path); settings.ExposeData(); Scribe.loader.FinalizeLoading();
            Require((float)chance.GetValue(null) == 0.23f, "Actual chance setting Scribe roundtrip failed");
        }
        finally
        {
            Scribe.ForceStop(); observer.Unpatch(canRun, canSpy); observer.Unpatch(nativeTry, trySpy);
            foreach (var relic in primary.GetAllPreceptsOfType<Precept_Relic>())
            {
                var current = (Thing)thing.GetValue(relic);
                if (current != null && (!oldRelics.TryGetValue(relic, out var old) || !ReferenceEquals(current, old.Item2))) newThings.Add(current);
            }
            foreach (var quest in Find.QuestManager.QuestsListForReading.ToArray()) Find.QuestManager.Remove(quest);
            Current.Game.questManager = oldManager;
            primary.PreceptsListForReading.Clear(); primary.PreceptsListForReading.AddRange(oldPrecepts); primary.RecachePrecepts();
            foreach (var pair in oldRelics) { generated.SetValue(pair.Key, pair.Value.Item1); thing.SetValue(pair.Key, pair.Value.Item2); }
            foreach (var item in newThings.Distinct()) if (!item.Destroyed) item.Destroy(DestroyMode.Vanish);
            foreach (var obj in Find.WorldObjects.AllWorldObjects.Where(o => !oldObjects.Contains(o)).ToArray()) Find.WorldObjects.Remove(obj);
            chance.SetValue(null, oldChance); outcome.effectDesc = oldDesc;
            foreach (var letter in Find.LetterStack.LettersListForReading.Where(l => !oldLetters.Contains(l)).ToArray()) Find.LetterStack.RemoveLetter(letter);
        }
        Log.Message("RW17 Mod48: actual Apply/new-parent/real threat slate/existing-parent/no-remaining-relic/caps/missing-generator/fallback real subquests, description refresh/FinalizeInit and setting Scribe passed; completed ritual/site quest completion/UI/reload pending");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Mod48: " + message);
    }
}
