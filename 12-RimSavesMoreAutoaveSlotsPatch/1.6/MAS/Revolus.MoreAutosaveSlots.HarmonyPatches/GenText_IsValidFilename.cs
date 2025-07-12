using System.Text.RegularExpressions;
using HarmonyLib;
using Verse;

namespace Revolus.MoreAutosaveSlots.HarmonyPatches;

[HarmonyPatch(typeof(GenText), "IsValidFilename")]
public static class GenText_IsValidFilename
{
	private static bool Prefix(ref bool __result, string str)
	{
		__result = str.Length <= 60 && !new Regex("[" + Regex.Escape(GenText.GetInvalidFilenameCharacters()) + "]").IsMatch(str);
		return false;
	}
}
