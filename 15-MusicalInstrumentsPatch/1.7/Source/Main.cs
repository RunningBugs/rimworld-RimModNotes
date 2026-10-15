using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MusicalInstruments;
using Verse;
namespace MusicPatch;
[StaticConstructorOnStartup]
public static class Start
{
    static Start() { new Harmony("com.runningbugs.musicpatch").PatchAll(typeof(Start).Assembly); Log.Message("MusicPatch loaded using upstream music component"); }
}
[HarmonyPatch(typeof(PerformanceManager), "StartPlaying", new[] { typeof(Pawn), typeof(Thing), typeof(Thing), typeof(bool) })]
public static class MusicInstrumentsPatch
{
    private static readonly Type MusicComp = typeof(PerformanceManager).Assembly.GetType("MusicalInstruments.Comp_PlayingMusic", true);
    private static readonly MethodInfo StartMusic = AccessTools.Method(MusicComp, "StartPlaying", new[] { typeof(Pawn) });
    public static void Postfix(Pawn musician, Thing instrument)
    {
        // The installed upstream already provides the sound defs, component and StopPlaying flow.
        // Keep this mod's unconditional playback without creating a second sound component.
        var comp = (instrument as ThingWithComps)?.AllComps.FirstOrDefault(c => c.GetType() == MusicComp);
        if (comp != null) StartMusic.Invoke(comp, new object[] { musician });
    }
}
