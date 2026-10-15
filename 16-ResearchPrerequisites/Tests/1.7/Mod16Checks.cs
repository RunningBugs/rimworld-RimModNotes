using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod16Checks
{
    public const string PackageId = "RunningBugs.ResearchPrerequisites";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run(Map map)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "ResearchPrerequisites");
        var queueType = assembly.GetType("ResearchPrerequisites.ResearchQueue", true);
        var categories = assembly.GetType("ResearchPrerequisites.ResearchCategories", true);
        var category = assembly.GetType("ResearchPrerequisites.ResearchCategory", true);
        var controller = assembly.GetType("ResearchPrerequisites.ResearchQueueController", true);
        var queue = Current.Game.GetComponent(queueType);
        Require(queue != null, "Actual queue GameComponent missing");
        object Cat(int i) => Enum.ToObject(category, i);
        List<ResearchProjectDef> List(int i) => (List<ResearchProjectDef>)queueType.GetMethod("QueueFor").Invoke(queue, new[] { Cat(i) });
        object Call(string name, params object[] args) => queueType.GetMethod(name).Invoke(queue, args);
        void Control(string name, params object[] args) => controller.GetMethod(name).Invoke(null, args);
        var oldLists = Enumerable.Range(0, 3).Select(i => List(i).ToArray()).ToArray();
        var attempted = (ResearchProjectDef[])controller.GetField("LastAttemptedHead", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var oldAttempted = attempted.ToArray();
        var oldManager = Current.Game.researchManager;
        var oldLetters = Find.LetterStack.LettersListForReading.ToArray();
        var oldWindows = Find.WindowStack.Windows.ToArray();
        var mod = LoadedModManager.ModHandles.Single(m => m.GetType().FullName == "ResearchPrerequisites.RPModSettingsUI");
        var settings = (ModSettings)mod.GetType().GetField("Settings").GetValue(mod);
        var finishLetter = settings.GetType().GetField("FinishProjectWithLetter");
        var dubs = settings.GetType().GetField("DubsMintMenusMod");
        var oldFinish = finishLetter.GetValue(settings);
        var oldDubs = dubs.GetValue(settings);
        var oldHidden = Find.EntityCodex.debug_UnhideAllResearch;
        try
        {
            foreach (var method in new[] { "SetCurrentProject", "StopProject", "FinishProject" })
                Require(Harmony.GetPatchInfo(typeof(ResearchManager).GetMethod(method))?.Postfixes.Any(p => p.PatchMethod.DeclaringType?.Assembly == assembly) == true, "Actual " + method + " postfix missing");
            foreach (var method in new[] { "DrawStartButton", "DrawContentSource" })
                Require(Harmony.GetPatchInfo(typeof(MainTabWindow_Research).GetMethod(method, Private)) != null, "Actual research UI patch missing: " + method);
            Current.Game.researchManager = new ResearchManager();
            Find.EntityCodex.debug_UnhideAllResearch = true;
            var manager = Find.ResearchManager;
            var progress = (Dictionary<ResearchProjectDef, float>)typeof(ResearchManager).GetField("progress", Private).GetValue(manager);
            for (int i = 0; i < 3; i++) { List(i).Clear(); attempted[i] = null; }
            ResearchProjectDef Def(string suffix) => new ResearchProjectDef { defName = "RW17Mod16" + suffix, label = suffix, description = suffix, baseCost = 100f };
            var a = Def("A"); var b = Def("B"); var c = Def("C"); var h = Def("HiddenPrerequisite"); var goal = Def("Goal"); var other = Def("Other");
            b.prerequisites = new() { a }; c.prerequisites = new() { a }; goal.prerequisites = new() { b, c }; goal.hiddenPrerequisites = new() { h };
            Call("Enqueue", (object)null); Call("MoveToHead", (object)null);
            Require((int)Call("JumpChainToFront", (object)null) == 0 && List(0).Count == 0, "Null entry contract changed");
            Call("Enqueue", goal); Call("Enqueue", goal);
            Require(List(0).SequenceEqual(new[] { a, b, c, h, goal }), "Diamond/hidden prerequisite order or dedup failed");
            List(0).Add(other);
            Require((int)Call("JumpChainToFront", goal) == 5 && List(0).SequenceEqual(new[] { a, b, c, h, goal, other }), "Jump chain count/dedup/preserved tail failed");
            Control("AdvanceCategory", Cat(0));
            Require(manager.GetProject() == a && List(0)[0] == a, "Actual advance did not start first prerequisite and retain queue head");
            manager.SetCurrentProject(other);
            Require(List(0).SequenceEqual(new[] { other, a, b, c, h, goal }), "Actual vanilla SetCurrentProject postfix did not preserve queued projects");
            List(0).Remove(other); queue.GameComponentTick();
            Require(List(0)[0] == other, "Actual GameComponentTick did not self-heal existing research head");
            manager.StopProject(other);
            Require(manager.GetProject() == null && List(0).Count == 0, "Actual StopProject did not clear matching queue");
            var blocked = Def("Blocked");
            // A nonexistent required facility cannot exist on any map, making the block deterministic.
            blocked.requiredResearchBuilding = new ThingDef { defName = "RW17Mod16AbsentBench" };
            List(0).Add(blocked); List(0).Add(other); attempted[0] = null;
            Control("AdvanceCategory", Cat(0));
            Require(manager.GetProject() == null && List(0).SequenceEqual(new[] { blocked, other }), "Blocked head must stay queued without skipping to the later project");
            blocked.requiredResearchBuilding = null; queue.GameComponentTick();
            Require(manager.GetProject() == blocked && List(0)[0] == blocked, "Actual tick did not retry after head became startable");
            Control("JumpChainToFrontAndStart", other);
            Require(manager.GetProject() == other && List(0).SequenceEqual(new[] { other, blocked }), "Actual jump did not preempt and preserve previous research");
            manager.StopProject(other); attempted[0] = null;
            List(0).Add(a); List(0).Add(b); progress[a] = 100f; List(0).Insert(0, null);
            Require(Call("PeekHead", Cat(0)) == b && List(0).SequenceEqual(new[] { b }), "Completed/null head removal failed");
            Call("Enqueue", a); Require(List(0).SequenceEqual(new[] { b }), "Completed project should not enqueue");
            queue.GameComponentTick(); Require(manager.GetProject() == b, "Actual tick did not start next dependency after completion");
            Require((int)categories.GetMethod("Of", new[] { typeof(KnowledgeCategoryDef) }).Invoke(null, new object[] { null }) == 0, "Null vanilla category must mean normal");
            Require(categories.GetMethod("ToVanillaDef").Invoke(null, new[] { Cat(0) }) == null, "Normal category conversion changed");
            if (ModsConfig.AnomalyActive)
            {
                var basic = Def("Basic"); basic.baseCost = 0f; basic.knowledgeCost = 100f; basic.knowledgeCategory = KnowledgeCategoryDefOf.Basic;
                var advanced = Def("Advanced"); advanced.baseCost = 0f; advanced.knowledgeCost = 100f; advanced.knowledgeCategory = KnowledgeCategoryDefOf.Advanced;
                Call("Enqueue", basic); Call("Enqueue", advanced); queue.GameComponentTick();
                Require(manager.GetProject(KnowledgeCategoryDefOf.Basic) == basic && manager.GetProject(KnowledgeCategoryDefOf.Advanced) == advanced && manager.GetProject() == b, "Three category slots failed isolation or tick start");
                manager.StopProject(basic); Require(List(1).Count == 0 && List(2).SequenceEqual(new[] { advanced }), "Stopping basic cleared another category");
            }
            var show = assembly.GetType("ResearchPrerequisites.DrawStartButtonPatch").GetMethod("ShouldShowQueueButton", BindingFlags.Static | BindingFlags.NonPublic);
            Require(!(bool)show.Invoke(null, new object[] { null }) && !(bool)show.Invoke(null, new[] { a }) && !(bool)show.Invoke(null, new[] { b }) && (bool)show.Invoke(null, new[] { other }), "Actual queue button null/completed/current/other eligibility failed");
            finishLetter.SetValue(settings, true); dubs.SetValue(settings, false);
            manager.FinishProject(b, true, null, false);
            Require(b.IsFinished && manager.GetProject() == null && Find.LetterStack.LettersListForReading.Any(l => !oldLetters.Contains(l) && l.Label.ToString().Contains(b.LabelCap.ToString())), "Actual FinishProject did not finish and send configured letter");
            Require(!Find.WindowStack.Windows.Any(w => !oldWindows.Contains(w) && w is Dialog_NodeTree), "Configured completion letter should suppress vanilla modal");
            VerifyScribe(queue, queueType, List, settings, finishLetter, categories, Cat);
        }
        finally
        {
            Scribe.ForceStop();
            Current.Game.researchManager = oldManager;
            Find.EntityCodex.debug_UnhideAllResearch = oldHidden;
            for (int i = 0; i < 3; i++) { List(i).Clear(); List(i).AddRange(oldLists[i]); attempted[i] = oldAttempted[i]; }
            finishLetter.SetValue(settings, oldFinish); dubs.SetValue(settings, oldDubs);
            foreach (var letter in Find.LetterStack.LettersListForReading.Where(l => !oldLetters.Contains(l)).ToArray()) Find.LetterStack.RemoveLetter(letter);
            foreach (var window in Find.WindowStack.Windows.Where(w => !oldWindows.Contains(w)).ToArray()) Find.WindowStack.TryRemove(window, false);
        }
        Log.Message("RW17 Mod16: actual dependency ordering/dedup, queue/controller/tick, native set/stop/finish hooks, three categories, button eligibility and Scribe queue/settings passed; rendered UI/Dubs/reload pending");
    }

    private static void VerifyScribe(GameComponent queue, Type queueType, Func<int, List<ResearchProjectDef>> list, ModSettings settings, FieldInfo finish, Type categories, Func<int, object> cat)
    {
        Require(Scribe.mode == LoadSaveMode.Inactive, "Scribe busy");
        for (int i = 0; i < 3; i++) list(i).Clear();
        var expected = new ResearchProjectDef[3];
        for (int i = 0; i < 3; i++)
        {
            var vanilla = (KnowledgeCategoryDef)categories.GetMethod("ToVanillaDef").Invoke(null, new[] { cat(i) });
            expected[i] = DefDatabase<ResearchProjectDef>.AllDefsListForReading.FirstOrDefault(p => p.knowledgeCategory == vanilla);
            if (expected[i] != null) list(i).Add(expected[i]);
        }
        var path = Path.Combine(GenFilePaths.SaveDataFolderPath, "Mod16QueueFixture.xml");
        Require(!File.Exists(path), "Queue fixture already exists");
        Scribe.saver.InitSaving(path, "Mod16QueueFixture"); queue.ExposeData(); Scribe.saver.FinalizeSaving();
        Require(File.ReadAllText(path).Contains("ResearchPrerequisites.ResearchQueue.researchQueue"), "Legacy normal queue key changed");
        for (int i = 0; i < 3; i++) list(i).Clear();
        Scribe.loader.InitLoading(path); queue.ExposeData(); Scribe.loader.FinalizeLoading();
        for (int i = 0; i < 3; i++) Require(list(i).SequenceEqual(expected[i] == null ? Array.Empty<ResearchProjectDef>() : new[] { expected[i] }), "Actual queue Scribe Def roundtrip failed category " + i);
        var settingsPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "Mod16SettingsFixture.xml");
        Require(!File.Exists(settingsPath), "Settings fixture already exists");
        finish.SetValue(settings, true);
        Scribe.saver.InitSaving(settingsPath, "Mod16SettingsFixture"); settings.ExposeData(); Scribe.saver.FinalizeSaving();
        finish.SetValue(settings, false);
        Scribe.loader.InitLoading(settingsPath); settings.ExposeData(); Scribe.loader.FinalizeLoading();
        Require((bool)finish.GetValue(settings), "Actual finish-letter setting Scribe roundtrip failed");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Mod16: " + message);
    }
}
