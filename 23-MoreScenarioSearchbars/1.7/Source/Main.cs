using Verse;
using RimWorld;

using System.Reflection;
using System.Linq;
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
			harmony.PatchAll(typeof(Start).Assembly);
			Log.Message("MoreScenarioSearchbars loaded successfully!");
		}
	}

	[HarmonyPatch]
	class ScenPart_CreateIncident_Patch
	{
		public static MethodBase TargetMethod() {
			return AccessTools.Method(typeof(ScenPart_IncidentBase).Assembly.GetType("RimWorld.ScenPart_CreateIncident", true), "DoEditInterface");
		}
        [HarmonyPrepare]
        public static bool Prepare() => Harmony.GetPatchInfo(TargetMethod())?.Postfixes.Any(p =>
            p.PatchMethod.DeclaringType?.FullName == "ScenarioSearchbar.ScenPart_CreateIncident_Patch") != true;

		private static void Postfix(Listing_ScenEdit listing, ScenPart_IncidentBase __instance)
		{
			Tools.DrawSearchbar(listing, (ScenPart)(object)__instance, 35);
		}
	}
}
