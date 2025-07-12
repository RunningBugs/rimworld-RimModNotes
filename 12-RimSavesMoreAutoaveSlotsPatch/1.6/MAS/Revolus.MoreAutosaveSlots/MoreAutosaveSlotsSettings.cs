using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Revolus.MoreAutosaveSlots;

public class MoreAutosaveSlotsSettings : ModSettings
{
	private const int CountDefault = 5;

	private const int CountMin = 1;

	private const int CountMax = 60;

	private const string FormatDefault = "{faction} ({index})";

	private const int HoursDefault = 0;

	private const int HoursMin = 0;

	private const int HoursMax = 360;

	private const bool UseNextSaveNameDefault = true;

	private int count = 5;

	private string format = "{faction} ({index})";

	public int Hours;

	public bool UseNextSaveName = true;

	public override void ExposeData()
	{
		Scribe_Values.Look<int>(ref count, "count", 5, false);
		Scribe_Values.Look<string>(ref format, "format", "{faction} ({index})", false);
		Scribe_Values.Look<int>(ref Hours, "hours", 0, false);
		Scribe_Values.Look<bool>(ref UseNextSaveName, "useNextSaveName", true, false);
		((ModSettings)this).ExposeData();
	}

	private static string[] autoSaveNames(bool onlyOne = false)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		MoreAutosaveSlotsSettings settings = MoreAutosaveSlotsMod.Settings;
		int num = Math.Max(Math.Min(settings.count, 60), 1);
		DateTime now = DateTime.Now;
		string text = null;
		try
		{
			Faction ofPlayerSilentFail = Faction.OfPlayerSilentFail;
			text = ((ofPlayerSilentFail != null) ? ofPlayerSilentFail.Name : null);
		}
		catch
		{
		}
		if (string.IsNullOrEmpty(text))
		{
			text = TaggedString.op_Implicit(Translator.Translate("MoreAutosaveSlots.FactionNameDefault"));
		}
		string text2 = null;
		try
		{
			text2 = ((Settlement)Find.CurrentMap.info.parent).Name;
		}
		catch
		{
		}
		if (string.IsNullOrEmpty(text2))
		{
			text2 = TaggedString.op_Implicit(Translator.Translate("MoreAutosaveSlots.SettlementNameDefault"));
		}
		string text3 = null;
		try
		{
			text3 = Find.World.info.seedString;
		}
		catch
		{
		}
		if (string.IsNullOrEmpty(text3))
		{
			text3 = TaggedString.op_Implicit(Translator.Translate("MoreAutosaveSlots.SeedDefault"));
		}
		string s = settings.format.Replace("{faction}", text).Replace("{settlement}", text2).Replace("{seed}", text3)
			.Replace("{year}", now.Year.ToString("D4"))
			.Replace("{month}", now.Month.ToString("D2"))
			.Replace("{day}", now.Day.ToString("D2"))
			.Replace("{hour}", now.Hour.ToString("D2"))
			.Replace("{minute}", now.Minute.ToString("D2"))
			.Replace("{second}", now.Second.ToString("D2"))
			.Trim();
		if (onlyOne)
		{
			return new string[1] { doFormat(s, num) };
		}
		string[] array = new string[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = doFormat(s, i + 1);
		}
		return array;
		static string doFormat(string text4, int index)
		{
			text4 = GenFile.SanitizedFileName(text4.Replace("{index}", index.ToString()));
			if (text4.Length > 60)
			{
				text4 = text4.Substring(0, 60).Trim();
			}
			return text4;
		}
	}

	public static string NextName()
	{
		string[] array = autoSaveNames();
		return array.Where((string name) => !SaveGameFilesUtility.SavedGameNamedExists(name)).FirstOrDefault() ?? GenCollection.MinBy<string, DateTime>((IEnumerable<string>)array, (Func<string, DateTime>)((string name) => new FileInfo(GenFilePaths.FilePathForSavedGame(name)).LastWriteTime));
	}

	internal bool ShowAndChangeSettings(Listing_Standard listing)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0290: Unknown result type (might be due to invalid IL or missing references)
		//IL_0295: Unknown result type (might be due to invalid IL or missing references)
		//IL_0296: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02be: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_030c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0365: Unknown result type (might be due to invalid IL or missing references)
		//IL_036a: Unknown result type (might be due to invalid IL or missing references)
		//IL_036b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0370: Unknown result type (might be due to invalid IL or missing references)
		//IL_0375: Unknown result type (might be due to invalid IL or missing references)
		//IL_0377: Unknown result type (might be due to invalid IL or missing references)
		//IL_037c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0387: Unknown result type (might be due to invalid IL or missing references)
		//IL_038e: Unknown result type (might be due to invalid IL or missing references)
		int num = count;
		string text = format;
		int hours = Hours;
		bool useNextSaveName = UseNextSaveName;
		bool flag = Widgets.ButtonText(GenUI.Rounded(GenUI.LeftHalf(GenUI.RightHalf(((Listing)listing).GetRect(Text.LineHeight, 1f)))), TaggedString.op_Implicit(Translator.Translate("MoreAutosaveSlots.ButtonReset")), true, true, true, (TextAnchor?)null);
		((Listing)listing).Gap(((Listing)listing).verticalSpacing + Text.LineHeight);
		line(TaggedString.op_Implicit(Translator.Translate("MoreAutosaveSlots.LabelExample")), autoSaveNames(onlyOne: true)[0]);
		((Listing)listing).Gap(Text.LineHeight);
		Rect rect = ((Listing)listing).GetRect(2f * Text.LineHeight, 1f);
		Rect val = GenUI.Rounded(GenUI.LeftHalf(rect));
		Rect val2 = GenUI.Rounded(GenUI.RightHalf(rect));
		Rect val3 = GenUI.Rounded(GenUI.TopHalf(val2));
		Rect val4 = GenUI.Rounded(GenUI.BottomHalf(val2));
		Text.Anchor = (TextAnchor)5;
		Widgets.Label(val, Translator.Translate("MoreAutosaveSlots.LabelCount"));
		Text.Anchor = (TextAnchor)7;
		Widgets.Label(val3, (num == 1) ? Translator.Translate("MoreAutosaveSlots.CountOne") : TranslatorFormattedStringExtensions.Translate("MoreAutosaveSlots.CountMultiple", NamedArgument.op_Implicit(num)));
		Text.Anchor = (TextAnchor)1;
		int num2 = Mathf.RoundToInt(Widgets.HorizontalSlider(val4, (float)num, 1f, 60f, false, (string)null, (string)null, (string)null, -1f));
		((Listing)listing).Gap(((Listing)listing).verticalSpacing + Text.LineHeight);
		Rect rect2 = ((Listing)listing).GetRect(Text.LineHeight, 1f);
		Rect val5 = GenUI.Rounded(GenUI.LeftHalf(rect2));
		Rect val6 = GenUI.Rounded(GenUI.RightHalf(rect2));
		Text.Anchor = (TextAnchor)5;
		Widgets.Label(val5, Translator.Translate("MoreAutosaveSlots.LabelFormat"));
		Text.Anchor = (TextAnchor)4;
		string text2 = Widgets.TextArea(GenUI.Rounded(val6), text, false);
		((Listing)listing).Gap(((Listing)listing).verticalSpacing);
		foreach (Match item in Regex.Matches(TaggedString.op_Implicit(Translator.Translate("MoreAutosaveSlots.FormatDescription")), "\\|([^|]*)\\|([^|]*)\\|\\|"))
		{
			GroupCollection groups = item.Groups;
			line(groups[1].Value, groups[2].Value);
		}
		((Listing)listing).Gap(Text.LineHeight);
		Rect rect3 = ((Listing)listing).GetRect(2f * Text.LineHeight, 1f);
		Rect val7 = GenUI.Rounded(GenUI.LeftHalf(rect3));
		Rect val8 = GenUI.Rounded(GenUI.RightHalf(rect3));
		Rect val9 = GenUI.Rounded(GenUI.TopHalf(val8));
		Rect val10 = GenUI.Rounded(GenUI.BottomHalf(val8));
		string text3 = ((hours > 0) ? GenDate.ToStringTicksToPeriod(hours * 2500, true, false, true, true, false) : TaggedString.op_Implicit(Translator.Translate("MoreAutosaveSlots.HoursDisabled")));
		Text.Anchor = (TextAnchor)5;
		Widgets.Label(val7, Translator.Translate("MoreAutosaveSlots.LabelHours"));
		Text.Anchor = (TextAnchor)7;
		Widgets.Label(val9, text3);
		Text.Anchor = (TextAnchor)1;
		int num3 = (int)Widgets.HorizontalSlider(val10, (float)hours, 0f, 360f, false, (string)null, (string)null, (string)null, 1f);
		((Listing)listing).Gap(((Listing)listing).verticalSpacing + Text.LineHeight);
		Rect rect4 = ((Listing)listing).GetRect(Text.LineHeight, 1f);
		Rect val11 = GenUI.Rounded(GenUI.LeftHalf(rect4));
		Rect val12 = GenUI.Rounded(GenUI.RightHalf(rect4));
		Text.Anchor = (TextAnchor)5;
		Widgets.Label(val11, Translator.Translate("MoreAutosaveSlots.LabelUseNextSaveName"));
		Text.Anchor = (TextAnchor)3;
		bool flag2 = useNextSaveName;
		Widgets.CheckboxLabeled(val12, "", ref flag2, false, (Texture2D)null, (Texture2D)null, true, false);
		((Listing)listing).Gap(((Listing)listing).verticalSpacing);
		if (flag)
		{
			num2 = 5;
			text2 = "{faction} ({index})";
			num3 = 0;
			flag2 = true;
		}
		bool result = false;
		if (num2 != num)
		{
			count = num2;
			result = true;
		}
		if (text2 != text)
		{
			format = text2;
			result = true;
		}
		if (num3 != hours)
		{
			Hours = num3;
			result = true;
		}
		if (flag2 == useNextSaveName)
		{
			return result;
		}
		UseNextSaveName = flag2;
		return true;
		void line(string left, string right)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			Rect rect5 = ((Listing)listing).GetRect(Text.LineHeight, 1f);
			Text.Anchor = (TextAnchor)5;
			Widgets.Label(GenUI.Rounded(GenUI.LeftHalf(rect5)), left);
			Text.Anchor = (TextAnchor)3;
			Widgets.Label(GenUI.Rounded(GenUI.RightHalf(rect5)), right);
			((Listing)listing).Gap(((Listing)listing).verticalSpacing);
		}
	}
}
