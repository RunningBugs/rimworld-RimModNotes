using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RiseAndGrind
{
    public sealed class Settings : ModSettings
    {
        public bool enableVoice = true;
        public bool preventCollapseWhenNotInMentalState = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableVoice, "enableVoice", true);
            Scribe_Values.Look(ref preventCollapseWhenNotInMentalState, "preventCollapseWhenNotInMentalState", false);
            base.ExposeData();
        }
    }

    public sealed class RiseAndGrindMod : Mod
    {
        public static Settings Settings;

        public RiseAndGrindMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<Settings>();
            new Harmony("RunningBugs.RiseAndGrind").PatchAll();
        }

        public override string SettingsCategory() => "Rise and Grind 爬起来肝";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("RAG_EnableVoice".Translate(), ref Settings.enableVoice,
                "RAG_EnableVoiceTooltip".Translate());
            listing.CheckboxLabeled("RAG_PreventCollapse".Translate(), ref Settings.preventCollapseWhenNotInMentalState,
                "RAG_PreventCollapseTooltip".Translate());
            var voice = WakeVoicesDef.CurrentVoice;
            if (voice != null)
            {
                listing.Gap();
                if (listing.ButtonText("RAG_PreviewVoice".Translate()))
                    voice.PlayOneShotOnCamera();
            }
            listing.End();
        }
    }

    public sealed class LanguageVoice
    {
        public string language;
        public SoundDef sound;
    }

    // Regular Def cross-references allow translation mods to patch in their own SoundDefs.
    public sealed class WakeVoicesDef : Def
    {
        public List<LanguageVoice> voices = new List<LanguageVoice>();

        public static SoundDef CurrentVoice => DefDatabase<WakeVoicesDef>.GetNamed("RAG_WakeVoices", false)
            ?.ForLanguage(LanguageDatabase.activeLanguage?.LegacyFolderName);

        public SoundDef ForLanguage(string language)
        {
            if (string.IsNullOrEmpty(language)) return null;
            foreach (var voice in voices)
                if (string.Equals(voice.language, language, StringComparison.OrdinalIgnoreCase))
                    return voice.sound;
            return null;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var error in base.ConfigErrors()) yield return error;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var voice in voices)
            {
                if (string.IsNullOrEmpty(voice.language)) yield return "Wake voice needs a language folder name.";
                else if (!seen.Add(voice.language)) yield return "Duplicate wake voice language: " + voice.language;
                if (voice.sound == null) yield return "Wake voice needs a SoundDef: " + voice.language;
            }
        }
    }

    [HarmonyPatch(typeof(Need_Rest), nameof(Need_Rest.NeedInterval))]
    public static class WakeOnExhaustion
    {
        public struct Check
        {
            public bool eligible;
            public Job previousJob;
        }

        public static bool CanWake(Pawn pawn) => pawn != null
            && pawn.IsColonistPlayerControlled && !pawn.Dead && !pawn.Downed
            && !pawn.Drafted && !pawn.Deathresting && pawn.drafter != null
            && pawn.ageTracker.CurLifeStage.canVoluntarilySleep;

        private static bool CanWakeBeforeCollapse(Pawn pawn) => pawn != null
            && pawn.Spawned && pawn.IsColonist && (pawn.HostFaction == null || pawn.IsSlave)
            && !pawn.Dead && !pawn.Downed && !pawn.Drafted && !pawn.Deathresting
            && pawn.drafter != null && pawn.ageTracker.CurLifeStage.canVoluntarilySleep;

        public static void Prefix(Pawn ___pawn, out Check __state)
        {
            // Collapse may recover a mental state; allow mental-state colonists in Prefix and require CanWake in Postfix.
            __state = new Check { eligible = CanWakeBeforeCollapse(___pawn), previousJob = ___pawn.CurJob };
        }

        public static void Postfix(Pawn ___pawn, Check __state)
        {
            var job = ___pawn.CurJob;
            if (!__state.eligible || !CanWake(___pawn) || job == null
                || ReferenceEquals(job, __state.previousJob) || job.def != JobDefOf.LayDown
                || !job.startInvoluntarySleep || !___pawn.jobs.IsCurrentJobPlayerInterruptible()) return;

            // This is the wake-up step used by vanilla, without changing drafting state.
            ___pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);

            // Jobs are pooled, so the replacement can reuse the same object reference.
            if (___pawn.Drafted || ___pawn.CurJob?.startInvoluntarySleep == true) return;

            Messages.Message("RAG_ReturnToWork".Translate(___pawn.LabelShort, ___pawn),
                ___pawn, MessageTypeDefOf.SilentInput);

            if (RiseAndGrindMod.Settings.enableVoice)
                WakeVoicesDef.CurrentVoice?.PlayOneShotOnCamera(___pawn.Map);
        }
    }

    [HarmonyPatch(typeof(Need_Rest), "CanInvoluntarilySleep")]
    public static class PreventExhaustionCollapse
    {
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (RiseAndGrindMod.Settings?.preventCollapseWhenNotInMentalState == true
                && WakeOnExhaustion.CanWake(pawn))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
