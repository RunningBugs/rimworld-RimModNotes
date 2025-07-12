using Verse;

using Log = Logger.Log;

// using System.Reflection;
using HarmonyLib;
using aRandomKiwi.ARS;
using System.Linq;
using System.IO;
using Revolus.MoreAutosaveSlots;
using RimWorld;

namespace RimSavesMASPatch
{
	[StaticConstructorOnStartup]
	public static class Start
	{
		static Start()
		{
			Harmony harmony = new Harmony("com.RunningBugs.RimSavesMoreAutosaveSlotsPatch");
			harmony.PatchAll();
			Log.Message("RimSavesMASPatch loaded.");
		}
	}

	public static class PatchHelper
	{
		public static readonly string VFOLDERSEP = "#§#";

		private static string prefix;

		public static string Prefix
		{
			get
			{
				prefix = Settings.curFolder != "Default" ? Settings.curFolder + VFOLDERSEP : "";
				return prefix;
			}
		}

		public static bool SavedGameNamedExists(string fileName)
		{
			fileName = Prefix + fileName;

			foreach (string item in GenFilePaths.AllSavedGameFiles.Select((FileInfo f) => Path.GetFileNameWithoutExtension(f.Name)))
			{
				if (item == fileName)
				{
					return true;
				}
			}
			return false;
		}

		public static string FilePathForSavedGame(string fileName)
		{
			fileName = Prefix + fileName;
			return GenFilePaths.FilePathForSavedGame(fileName);
		}
	}

	[Harmony]
	class Patches
	{
		[HarmonyPatch(typeof(MoreAutosaveSlotsSettings))]
		[HarmonyPatch("NextName")]
		[HarmonyPrefix]
		public static bool NextName(ref string __result)
		{
			var texts = MoreAutosaveSlotsSettings.AutoSaveNames();
			var text = (from name in texts where !PatchHelper.SavedGameNamedExists(name) select name).FirstOrDefault();
			if (!(text is null))
			{
				// Log.Warning($"{text}");
				__result = text;
				return false;
			}
			else
			{
				// foreach (var txt in texts)
				// {
				// 	Log.Warning($"{txt} {new FileInfo(PatchHelper.FilePathForSavedGame(txt)).LastWriteTime}");
				// }
				var res = texts.MinBy((string name) => new FileInfo(PatchHelper.FilePathForSavedGame(name)).LastWriteTime);
				// Log.Warning($"{res}");
				__result = res;
				return false;
			}
		}
	}
}
