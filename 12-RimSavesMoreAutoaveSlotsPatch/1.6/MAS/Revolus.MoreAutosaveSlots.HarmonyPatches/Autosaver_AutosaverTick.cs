using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Revolus.MoreAutosaveSlots.HarmonyPatches;

[HarmonyPatch(typeof(Autosaver), "AutosaverTick")]
public static class Autosaver_AutosaverTick
{
	public static bool Prefix(ref Autosaver __instance, ref int ___ticksSinceSave)
	{
		int hours = MoreAutosaveSlotsMod.Settings.Hours;
		if (hours <= 0)
		{
			return true;
		}
		Autosaver val = __instance;
		int num = hours * 2500;
		if (++___ticksSinceSave < num)
		{
			return false;
		}
		LongEventHandler.QueueLongEvent((Action)val.DoAutosave, "Autosaving", false, (Action<Exception>)null, true, false, (Action)null);
		___ticksSinceSave = 0;
		return false;
	}
}
