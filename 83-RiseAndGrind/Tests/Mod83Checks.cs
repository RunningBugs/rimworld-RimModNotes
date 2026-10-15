using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RiseAndGrind.Tests;

public static class Mod83Checks
{
    public const string PackageId = "RunningBugs.RiseAndGrind";
    public static readonly List<string> Passed = new();
    private static Pawn subject;
    private static readonly List<bool> DraftCalls = new();
    private static readonly List<Job> Collapses = new();
    private static readonly List<string> VoiceCalls = new();
    private static readonly List<string> SoundCalls = new();
    private sealed record VoiceSample(string Sound, bool Created, int Samples, int Frequency,
        float Length, float Volume, bool? IsPlaying, string Context, bool OnCamera);
    private static readonly List<VoiceSample> VoiceSamples = new();
    private static readonly List<JobCondition> CollapseInterruptions = new();
    private static readonly HashSet<Message> PreviousMessages = new();
    private static List<Message> LiveMessages => (List<Message>)AccessTools.Field(typeof(Messages), "liveMessages").GetValue(null);
    private static IEnumerable<Message> WakeMessages => LiveMessages.Where(message => !PreviousMessages.Contains(message) &&
        (message.text.Contains("爬起来继续工作") || message.text.Contains("get back up and keep working")));

    public static void ObserveDraft(Pawn_DraftController __instance, bool value)
    {
        if (__instance.pawn == subject) DraftCalls.Add(value);
    }

    public static void ObserveJob(Pawn ___pawn, Job newJob)
    {
        if (___pawn == subject && newJob.def == JobDefOf.LayDown && newJob.startInvoluntarySleep)
            Collapses.Add(newJob);
    }

    public static void ObserveVoice(SoundDef soundDef)
    {
        if (soundDef == null) return;
        SoundCalls.Add(soundDef.defName);
        if (soundDef.defName.StartsWith("RAG_", StringComparison.Ordinal)) VoiceCalls.Add(soundDef.defName);
    }

    public static void ObserveEndJob(Pawn ___pawn, Pawn_JobTracker __instance, JobCondition condition)
    {
        if (___pawn == subject && __instance.curJob?.def == JobDefOf.LayDown && __instance.curJob.startInvoluntarySleep)
            CollapseInterruptions.Add(condition);
    }

    public static void ObserveSample(SubSoundDef def, SampleOneShot __result)
    {
        if (def.parentDef?.defName.StartsWith("RAG_", StringComparison.Ordinal) != true) return;
        var source = __result?.source;
        var clip = source?.clip;
        var sample = new VoiceSample(def.parentDef.defName, __result != null,
            clip?.samples ?? 0, clip?.frequency ?? 0, clip?.length ?? 0,
            source?.volume ?? 0, source?.isPlaying,
            def.parentDef.context.ToString(), def.onCamera);
        VoiceSamples.Add(sample);
        Console.WriteLine("RAG_SOURCE_STATE " + JsonSerializer.Serialize(sample));
    }

    // Install before the first NeedInterval can JIT its call to the production postfix.
    public static void InstallVoiceObserver()
    {
        var observer = new Harmony("RiseAndGrind.Tests.Observer");
        observer.Patch(AccessTools.Method(typeof(SoundStarter), nameof(SoundStarter.PlayOneShotOnCamera)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(Mod83Checks), nameof(ObserveVoice))));
        observer.Patch(AccessTools.Method(typeof(SampleOneShot), nameof(SampleOneShot.TryMakeAndPlay)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(Mod83Checks), nameof(ObserveSample))));
    }

    public static void Run(Map map)
    {
        var mod = LoadedModManager.RunningModsListForReading.Single(m =>
            m.PackageId.Equals(PackageId, StringComparison.OrdinalIgnoreCase));
        var assembly = mod.assemblies.loadedAssemblies.Single(a => a.GetName().Name == "RiseAndGrind");
        var wake = assembly.GetType("RiseAndGrind.WakeOnExhaustion", true);
        var canWake = wake.GetMethod("CanWake");
        var interval = AccessTools.Method(typeof(Need_Rest), nameof(Need_Rest.NeedInterval));
        Require(Harmony.GetPatchInfo(interval)?.Prefixes.Any(p => p.PatchMethod.DeclaringType == wake) == true &&
            Harmony.GetPatchInfo(interval)?.Postfixes.Any(p => p.PatchMethod.DeclaringType == wake) == true,
            "actual NeedInterval prefix/postfix registered");

        var settings = assembly.GetType("RiseAndGrind.RiseAndGrindMod", true).GetField("Settings").GetValue(null);
        var voiceEnabled = settings.GetType().GetField("enableVoice");
        var oldSetting = voiceEnabled.GetValue(settings);
        Require((bool)oldSetting, "fresh settings default voice to enabled");
        var voicesType = assembly.GetType("RiseAndGrind.WakeVoicesDef", true);
        var database = typeof(DefDatabase<>).MakeGenericType(voicesType);
        var voices = database.GetMethod("GetNamed", new[] { typeof(string), typeof(bool) })
            .Invoke(null, new object[] { "RAG_WakeVoices", true });
        var lookup = voicesType.GetMethod("ForLanguage");
        var chineseSound = DefDatabase<SoundDef>.GetNamed("RAG_WakeUpChinese");
        SoundDef Voice(string language) => (SoundDef)lookup.Invoke(voices, new object[] { language });
        Require(ReferenceEquals(Voice("ChineseSimplified"), chineseSound) &&
            ReferenceEquals(Voice("chinesetraditional"), chineseSound), "Chinese mappings resolve actual SoundDef references");
        Require(Voice("French") == null && Voice("") == null && Voice(null) == null,
            "missing and empty languages are silent");
        Require(!((Def)voices).ConfigErrors().Any(), "voice Def has no configuration errors");
        var extension = Environment.GetEnvironmentVariable("RAG_TEST_EXTENSION") == "1";
        Require((Voice("English")?.defName == "RAG_TestEnglish") == extension,
            "XML language extension presence matches this case");
        var activeLanguage = LanguageDatabase.activeLanguage.LegacyFolderName;
        var expectedVoice = Voice(activeLanguage)?.defName;
        Passed.Add("definitions and language selection: " + activeLanguage);

        var observer = new Harmony("RiseAndGrind.Tests.Observer");
        var draftSetter = AccessTools.PropertySetter(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted));
        var startJob = AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob));
        var endJob = AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob));
        var playVoice = AccessTools.Method(typeof(SoundStarter), nameof(SoundStarter.PlayOneShotOnCamera));
        var makeSample = AccessTools.Method(typeof(SampleOneShot), nameof(SampleOneShot.TryMakeAndPlay));
        var draftObserver = AccessTools.Method(typeof(Mod83Checks), nameof(ObserveDraft));
        var jobObserver = AccessTools.Method(typeof(Mod83Checks), nameof(ObserveJob));
        var voiceObserver = AccessTools.Method(typeof(Mod83Checks), nameof(ObserveVoice));
        var sampleObserver = AccessTools.Method(typeof(Mod83Checks), nameof(ObserveSample));
        var endJobObserver = AccessTools.Method(typeof(Mod83Checks), nameof(ObserveEndJob));
        var pawns = new List<Pawn>();
        Rand.PushState(1783);
        try
        {
            observer.Patch(draftSetter, prefix: new HarmonyMethod(draftObserver));
            observer.Patch(startJob, prefix: new HarmonyMethod(jobObserver));
            observer.Patch(endJob, prefix: new HarmonyMethod(endJobObserver));

            Pawn NewPawn(Faction faction = null)
            {
                var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
                    faction ?? Faction.OfPlayer, forceGenerateNewPawn: true));
                pawns.Add(pawn);
                var cell = GenRadial.RadialCellsAround(map.Center, 20, true)
                    .First(c => c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map) == null);
                GenSpawn.Spawn(pawn, cell, map);
                for (var hour = 0; hour < 24; hour++) pawn.timetable?.SetAssignment(hour, TimeAssignmentDefOf.Work);
                var wait = JobMaker.MakeJob(JobDefOf.Wait); wait.expiryInterval = 1000;
                pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
                pawn.needs.rest.CurLevel = 0;
                pawn.mindState.lastDisturbanceTick = Find.TickManager.TicksGame - 1000;
                AccessTools.Field(typeof(Need_Rest), "ticksAtZero").SetValue(pawn.needs.rest, 45000);
                return pawn;
            }

            void Select(Pawn pawn)
            {
                subject = pawn; DraftCalls.Clear(); Collapses.Clear(); VoiceCalls.Clear(); SoundCalls.Clear();
                VoiceSamples.Clear();
                CollapseInterruptions.Clear(); PreviousMessages.Clear(); PreviousMessages.UnionWith(LiveMessages);
            }

            void Collapse(Pawn pawn)
            {
                Select(pawn);
                for (var count = 0; count < 1000 && Collapses.Count == 0; count++) pawn.needs.rest.NeedInterval();
                Require(Collapses.Count == 1, "real NeedInterval produced one exhaustion collapse");
            }

            void CheckWakeMessage(Pawn pawn)
            {
                var expected = activeLanguage == "ChineseSimplified"
                    ? pawn.LabelShort + "决定爬起来继续工作。"
                    : pawn.LabelShort + " has decided to get back up and keep working.";
                var messages = WakeMessages.ToArray();
                Require(messages.Length == 1 && messages[0].text == expected &&
                    messages[0].lookTargets?.PrimaryTarget.Thing == pawn,
                    "localized return-to-work message includes the pawn name and targets that pawn");
                Require(messages[0].def.sound == null,
                    "return-to-work message has no additional notification sound");
                Console.WriteLine("RAG_MESSAGE_STATE " + JsonSerializer.Serialize(new {
                    activeLanguage, text = messages[0].text, type = messages[0].def.defName,
                    target = messages[0].lookTargets.PrimaryTarget.Thing.ThingID,
                    sounds = SoundCalls.ToArray() }));
            }

            var colonist = NewPawn();
            colonist.drafter.FireAtWill = false;
            Require((bool)canWake.Invoke(null, new object[] { colonist }), "healthy adult colonist is eligible");
            Collapse(colonist);
            Require(DraftCalls.Count == 0 && !colonist.Drafted && !colonist.drafter.FireAtWill,
                "waking does not invoke drafting or reset fire-at-will");
            Require(CollapseInterruptions.SequenceEqual(new[] { JobCondition.InterruptForced }),
                "waking directly interrupts the involuntary laydown once");
            Require(colonist.needs.rest.CurLevel == 0 && !colonist.Downed &&
                !(colonist.CurJob?.startInvoluntarySleep == true), "waking interrupts collapse without adding rest");
            Console.WriteLine("RAG_VOICE_STATE " + JsonSerializer.Serialize(new {
                activeLanguage, expectedVoice, calls = VoiceCalls.ToArray(), job = colonist.CurJobDef?.defName,
                involuntary = colonist.CurJob?.startInvoluntarySleep, colonist.Drafted, setting = voiceEnabled.GetValue(settings) }));
            Require(VoiceCalls.SequenceEqual(expectedVoice == null ? Array.Empty<string>() : new[] { expectedVoice }),
                "actual current language produces its voice or silence");
            Require(expectedVoice == null ? VoiceSamples.Count == 0 :
                VoiceSamples.Count == 1 && VoiceSamples[0].Sound == expectedVoice &&
                VoiceSamples[0].Created && VoiceSamples[0].Samples > 0 && VoiceSamples[0].Frequency > 0 &&
                VoiceSamples[0].Length > 0 && VoiceSamples[0].Volume > 0,
                "current-language voice creates a real source with decoded samples and nonzero volume, or stays silent");
            Require(SoundCalls.All(sound => sound == "Message_NegativeEvent" || sound == expectedVoice),
                "waking produces no drafting sound or extra message sound");
            CheckWakeMessage(colonist);
            Passed.Add("native exhaustion, direct interruption, unchanged rest and draft/fire-at-will, current-language playback");
            Passed.Add("voice sample creation, decoded clip data and source volume match language selection");
            Passed.Add("localized return-to-work message targets the pawn without a notification sound");

            voiceEnabled.SetValue(settings, false);
            colonist = NewPawn(); Collapse(colonist);
            Require(DraftCalls.Count == 0 && VoiceCalls.Count == 0 && VoiceSamples.Count == 0 &&
                CollapseInterruptions.SequenceEqual(new[] { JobCondition.InterruptForced }) &&
                colonist.CurJob?.startInvoluntarySleep != true,
                "disabled audio preserves direct waking and suppresses playback");
            Require(SoundCalls.All(sound => sound == "Message_NegativeEvent"),
                "disabled audio leaves only the original exhaustion notification sound");
            CheckWakeMessage(colonist);
            voiceEnabled.SetValue(settings, true);
            Passed.Add("voice setting disables audio while retaining the localized message");

            var sleeper = NewPawn();
            var sleepJob = JobMaker.MakeJob(JobDefOf.LayDown, sleeper.Position);
            sleeper.jobs.StartJob(sleepJob, JobCondition.InterruptForced);
            Select(sleeper); sleeper.needs.rest.NeedInterval();
            Require(ReferenceEquals(sleeper.CurJob, sleepJob) && DraftCalls.Count == 0 && VoiceCalls.Count == 0 && VoiceSamples.Count == 0 && !WakeMessages.Any(),
                "normal sleep is untouched");
            Passed.Add("voluntary sleep job remains active");

            var patient = NewPawn(); patient.health.AddHediff(HediffDefOf.Anesthetic);
            Require(patient.Downed && !(bool)canWake.Invoke(null, new object[] { patient }),
                "real anesthesia makes patient downed and ineligible");
            Select(patient); patient.needs.rest.NeedInterval();
            Require(patient.Downed && DraftCalls.Count == 0 && VoiceCalls.Count == 0 && VoiceSamples.Count == 0 && !WakeMessages.Any(),
                "medical downing is untouched");
            Passed.Add("actual anesthetic downing remains downed");

            var enemy = NewPawn(Faction.OfAncientsHostile);
            Require(enemy.HostileTo(Faction.OfPlayer) && !(bool)canWake.Invoke(null, new object[] { enemy }),
                "actual enemy is ineligible");
            Collapse(enemy);
            Require(DraftCalls.Count == 0 && VoiceCalls.Count == 0 && VoiceSamples.Count == 0 && !WakeMessages.Any() && enemy.CurJob?.startInvoluntarySleep == true,
                "enemy exhaustion remains native sleep");
            Passed.Add("enemy exhaustion is untouched");

            var mental = NewPawn();
            Require(mental.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Wander_Sad,
                forced: true, forceWake: true), "actual mental state starts");
            Require(mental.InMentalState && !(bool)canWake.Invoke(null, new object[] { mental }),
                "mental-state colonist is ineligible before collapse");
            mental.mindState.lastDisturbanceTick = Find.TickManager.TicksGame - 1000;
            Collapse(mental);
            Require(DraftCalls.Count == 0 && VoiceCalls.Count == 0 && VoiceSamples.Count == 0 && !WakeMessages.Any(),
                "mental-state exhaustion does not trigger automatic waking");
            Passed.Add("mental-state exhaustion preserves native behavior");

            var drafted = NewPawn(); drafted.drafter.Drafted = true;
            Require(!(bool)canWake.Invoke(null, new object[] { drafted }), "drafted colonist is ineligible");
            Select(drafted); drafted.needs.rest.NeedInterval();
            Require(DraftCalls.Count == 0 && VoiceCalls.Count == 0 && VoiceSamples.Count == 0 && !WakeMessages.Any(), "drafted colonist is not toggled");
            Passed.Add("already drafted pawn is untouched");
        }
        finally
        {
            subject = null;
            observer.Unpatch(draftSetter, draftObserver);
            observer.Unpatch(startJob, jobObserver);
            observer.Unpatch(endJob, endJobObserver);
            observer.Unpatch(playVoice, voiceObserver);
            observer.Unpatch(makeSample, sampleObserver);
            foreach (var pawn in pawns) if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
            voiceEnabled.SetValue(settings, oldSetting);
            Rand.PopState();
        }
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Mod83: " + description);
        Log.Message("RAG_ASSERT_OK " + description);
    }
}

[StaticConstructorOnStartup]
public static class BootCheck
{
    static BootCheck()
    {
        Mod83Checks.InstallVoiceObserver();
        var mods = LoadedModManager.RunningModsListForReading.Select(mod => new {
            package_id = mod.PackageId, directory = mod.RootDir,
            assemblies = mod.assemblies.loadedAssemblies.Select(a => a.Location).ToArray() }).ToArray();
        File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "rag-boot.json"),
            JsonSerializer.Serialize(mods, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public class ValidationMap : MapComponent
{
    private bool ran;
    public ValidationMap(Map map) : base(map) { }
    public override void MapComponentUpdate()
    {
        if (ran) { Root.Shutdown(); return; }
        if (Current.ProgramState != ProgramState.Playing || LongEventHandler.AnyEventNowOrWaiting) return;
        ran = true;
        string error = null;
        try { Mod83Checks.Run(map); }
        catch (Exception exception) { error = exception.ToString(); Log.Error("RAG_TEST_FAILED " + error); }
        File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "rag-result.json"),
            JsonSerializer.Serialize(new { passed = error == null, error, checks = Mod83Checks.Passed },
                new JsonSerializerOptions { WriteIndented = true }));
        Log.Message(error == null ? "RAG_TEST_PASSED" : "RAG_TEST_FAILED");
    }
}
