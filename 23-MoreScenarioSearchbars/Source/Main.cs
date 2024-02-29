using Verse;
using RimWorld;

using System.Reflection;
using HarmonyLib;
using ScenarioSearchbar;

namespace MoreScenarioSearchbars
{
	[StaticConstructorOnStartup]
	public static class Start
	{
		static Start()
		{
			Harmony harmony = new Harmony("com.RunningBugs.MoreScenarioSearchbars");
			harmony.PatchAll();
			Log.Message("MoreScenarioSearchbars loaded successfully!");
		}
	}

	[HarmonyPatch]
	class ScenPart_CreateIncident_Patch
	{
		public static MethodBase TargetMethod() {
			return AccessTools.Method(AccessTools.TypeByName("ScenPart_CreateIncident"), "DoEditInterface");
		}
		private static void Postfix(Listing_ScenEdit listing, ScenPart_IncidentBase __instance)
		{
			Tools.DrawSearchbar(listing, (ScenPart)(object)__instance, 35);
		}
	}
}
