using HarmonyLib;
using RimWorld;

namespace Revolus.MoreAutosaveSlots.HarmonyPatches;

[HarmonyPatch(typeof(Autosaver), "NewAutosaveFileName")]
public static class Autosaver_NewAutosaveFileName
{
	public static bool Prefix(ref string __result)
	{
		__result = MoreAutosaveSlotsSettings.NextName();
		return false;
	}
}
