using Verse;
using HarmonyLib;


namespace MusicPatch
{
	[StaticConstructorOnStartup]
	public static class Start
	{
		static Start()
		{
			Log.Message("Mod template loaded successfully!");
			Harmony harmony = new Harmony("com.runningbugs.musicpatch");
			harmony.PatchAll();
		}
	}

	[HarmonyPatch]
	public class MusicInstrumentsPatch
	{

		[HarmonyPatch(typeof(MusicalInstruments.PerformanceManager), "StartPlaying")]
		public static void Postfix(Pawn musician, Thing instrument)
		{
			if (instrument.TryGetComp<Comp_PlayingMusic>() is Comp_PlayingMusic comp)
			{
				comp.StartPlaying(musician);
			}
		}

		[HarmonyPatch(typeof(MusicalInstruments.PerformanceManager), "StopPlaying")]
		public static bool Prefix(Pawn musician)
		{
			Comp_PlayingMusic.notebook.TryGetValue(musician, out Comp_PlayingMusic comp);
			comp?.StopPlaying(musician);
			return true;
		}
	}
}
