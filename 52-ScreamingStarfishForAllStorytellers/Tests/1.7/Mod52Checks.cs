using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod52Checks
{
    public const string PackageId = "RunningBugs.ScreamingStarfishForAllStorytellers";

    public static void Run(Map map)
    {
        const string propertyName = "ScreamingStarfish.StorytellerCompProperties_PostSounds";
        const string componentName = "ScreamingStarfish.StorytellerComp_PostSounds";
        var original = Find.Storyteller;
        var property = original.def.comps.FirstOrDefault(p => p.GetType().FullName == propertyName);
        Require(property != null, "Current storyteller did not receive the upstream marker component");
        var assembly = property.GetType().Assembly;
        var patch = assembly.GetType("ScreamingStarfish.PlaySoundAfterReceiveLetterPatch", true);
        var gate = patch.GetMethod("IsStorytellerComp_PostSoundsActive", BindingFlags.NonPublic | BindingFlags.Static);
        Require(gate != null, "Actual upstream gate method missing");
        var target = typeof(LetterStack).GetMethod("ReceiveLetter", new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) });
        Require(Harmony.GetPatchInfo(target)?.Postfixes.Any(p => p.PatchMethod.DeclaringType == patch) == true,
            "Actual upstream ReceiveLetter postfix is not registered");
        var settings = assembly.GetType("ScreamingStarfish.ScreamingStarfishModSetting", true);
        var enabled = settings.GetField("isScreamingActivated", BindingFlags.Public | BindingFlags.Static);
        var volume = settings.GetField("volumeRange", BindingFlags.Public | BindingFlags.Static);
        Require(enabled != null && volume != null, "Actual upstream settings fields missing");
        var oldEnabled = enabled.GetValue(null);
        var oldVolume = volume.GetValue(null);
        var sound = DefDatabase<SoundDef>.GetNamed("ScreamingStarfish_PositiveEvent");
        Require(sound.subSounds.Count > 0, "Actual upstream positive-event sound has no subsound");
        var oldRange = sound.subSounds[0].volumeRange;
        try
        {
            enabled.SetValue(null, true);
            volume.SetValue(null, 31.25f);
            foreach (var def in DefDatabase<StorytellerDef>.AllDefsListForReading)
            {
                var count = def.comps.Count(p => p.GetType().FullName == propertyName);
                Require(count > 0, "Storyteller lacks injected component: " + def.defName);
                Require(def.comps.Where(p => p.GetType().FullName == propertyName).All(p => p.compClass.FullName == componentName),
                    "Marker property constructor has wrong compClass: " + def.defName);
                var storyteller = new Storyteller(def, original.difficultyDef, original.difficulty);
                Current.Game.storyteller = storyteller;
                Require(storyteller.storytellerComps.Any(c => c.GetType().FullName == componentName),
                    "Storyteller runtime constructor did not instantiate marker: " + def.defName);
                Require((bool)gate.Invoke(null, null), "Actual upstream enabled gate failed: " + def.defName);
                sound.subSounds[0].volumeRange = new FloatRange(7f, 7f);
                ReceiveTestLetter();
                Require(sound.subSounds[0].volumeRange.min == 31.25f && sound.subSounds[0].volumeRange.max == 31.25f,
                    "Actual ReceiveLetter did not execute upstream PostSound: " + def.defName);
                Log.Message("RW17 Mod52 storyteller=" + def.defName + " markerCount=" + count + " actual letter hook passed");
            }
            enabled.SetValue(null, false);
            sound.subSounds[0].volumeRange = new FloatRange(7f, 7f);
            ReceiveTestLetter();
            Require(sound.subSounds[0].volumeRange.min == 7f, "Disabled upstream setting still changed sound volume");
            enabled.SetValue(null, true);
            Find.Storyteller.storytellerComps.RemoveAll(c => c.GetType().FullName == componentName);
            Require(!(bool)gate.Invoke(null, null), "Upstream marker absence did not disable gate");
            ReceiveTestLetter();
            Require(sound.subSounds[0].volumeRange.min == 7f, "Absent marker still changed sound volume");
        }
        finally
        {
            Current.Game.storyteller = original;
            enabled.SetValue(null, oldEnabled);
            volume.SetValue(null, oldVolume);
            sound.subSounds[0].volumeRange = oldRange;
        }
        Log.Message("RW17 Mod52: every loaded storyteller property/runtime marker, actual gate, registered ReceiveLetter hook and disabled/absent boundaries passed; audible output not tested");
    }

    private static void ReceiveTestLetter()
    {
        var letter = LetterMaker.MakeLetter("RW17 Mod52", "Disposable validation letter", LetterDefOf.PositiveEvent);
        try { Find.LetterStack.ReceiveLetter(letter, "RW17 Mod52 disposable", 0, false); }
        finally { Find.LetterStack.RemoveLetter(letter); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Mod52: " + message);
    }
}
