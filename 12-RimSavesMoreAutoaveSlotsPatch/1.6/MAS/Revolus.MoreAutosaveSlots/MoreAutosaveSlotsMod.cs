using System.Reflection;
using HarmonyLib;
using Mlie;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Revolus.MoreAutosaveSlots;

public class MoreAutosaveSlotsMod : Mod
{
	public static MoreAutosaveSlotsSettings Settings;

	private static string currentVersion;

	public MoreAutosaveSlotsMod(ModContentPack content)
		: base(content)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		Settings = ((Mod)this).GetSettings<MoreAutosaveSlotsSettings>();
		currentVersion = VersionFromManifest.GetVersionFromModMetaData(content.ModMetaData);
		new Harmony(typeof(MoreAutosaveSlotsMod).AssemblyQualifiedName).PatchAll(Assembly.GetExecutingAssembly());
	}

	public override void DoSettingsWindowContents(Rect rect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected O, but got Unknown
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		Listing_Standard val = new Listing_Standard();
		((Listing)val).Begin(rect);
		bool flag;
		try
		{
			TextAnchor anchor = Text.Anchor;
			try
			{
				flag = Settings.ShowAndChangeSettings(val);
			}
			finally
			{
				Text.Anchor = anchor;
			}
		}
		finally
		{
			if (currentVersion != null)
			{
				((Listing)val).Gap(12f);
				GUI.contentColor = Color.gray;
				val.Label(TranslatorFormattedStringExtensions.Translate("MoreAutosaveSlots.CurrentVersion", NamedArgument.op_Implicit(currentVersion)), -1f, (string)null);
				GUI.contentColor = Color.white;
			}
			((Listing)val).End();
		}
		if (flag)
		{
			SoundStarter.PlayOneShotOnCamera(SoundDefOf.DragSlider, (Map)null);
		}
		((Mod)this).DoSettingsWindowContents(rect);
	}

	public override string SettingsCategory()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		return TaggedString.op_Implicit(Translator.Translate("MoreAutosaveSlots.SettingsName"));
	}
}
