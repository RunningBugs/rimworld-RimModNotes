using HarmonyLib;
using RimWorld;
using Verse;

namespace Revolus.MoreAutosaveSlots.HarmonyPatches;

[HarmonyPatch(typeof(Window), "PostOpen")]
public static class Window_PostOpen
{
	public static void Postfix(ref Window __instance)
	{
		if (MoreAutosaveSlotsMod.Settings.UseNextSaveName)
		{
			Window obj = __instance;
			Dialog_SaveFileList_Save val = (Dialog_SaveFileList_Save)(object)((obj is Dialog_SaveFileList_Save) ? obj : null);
			if (val != null)
			{
				AccessTools.Field(typeof(Dialog_SaveFileList_Save), "typingName").SetValue(val, MoreAutosaveSlotsSettings.NextName());
			}
		}
	}
}
