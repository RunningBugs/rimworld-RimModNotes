using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;

namespace AlertUtility
{
	[HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls", MethodType.Normal)]
	public class ToggleIconPatcher
	{
		[HarmonyPostfix]
		public static void AddIcon(WidgetRow row, bool worldView)
		{
			if (worldView) return;
			bool flag = Find.WindowStack.IsOpen(typeof(TimerSetWindow));
			row.ToggleableIcon(ref flag, ContentFinder<Texture2D>.Get("UI/timer_mail", true), "AlertUtility".Translate(), SoundDefOf.Mouseover_ButtonToggle, (string)null);
			if (flag != Find.WindowStack.IsOpen(typeof(TimerSetWindow)))
			{
				if (!Find.WindowStack.IsOpen(typeof(TimerSetWindow)))
				{
					TimerSetWindow.DrawWindow(AlertUtility.GetEvents());
				}
				else
				{
					Find.WindowStack.TryRemove(typeof(TimerSetWindow), false);
				}
			}
		}
	}

	public static class RectExt
	{
		public static void GetRowRect(this Rect rect, out Rect top, out Rect bot, float height)
		{
			top = new Rect(rect.x, rect.y, rect.width, height);
			rect.y += height;
			bot = rect;
		}
	}

	[StaticConstructorOnStartup]
	public class TimerSetWindow : Window
	{
		public override Vector2 InitialSize => new Vector2(800f, 450f);

		private static Texture2D RadioButOffTex;
		private static Texture2D RadioButOnTex;

		private string buffer = "0";
		private string txtBuffer = "";
		private int unit = 0;
		Vector2 scrollbarPos;

		public TimerSetWindow() : base()
		{
			this.draggable = true;
			RadioButOffTex = ContentFinder<Texture2D>.Get("UI/Widgets/RadioButOff");
			RadioButOnTex = ContentFinder<Texture2D>.Get("UI/Widgets/RadioButOn");
		}

		public float TickRateMultiplier(TimeSpeed speed)
		{
			if (speed == TimeSpeed.Paused)
			{
				speed = Find.TickManager.prePauseTimeSpeed;
			}

			switch (speed)
			{
				case TimeSpeed.Paused:
					return 0f;
				case TimeSpeed.Normal:
					return 1f;
				case TimeSpeed.Fast:
					return 3f;
				case TimeSpeed.Superfast:
					return 6f;
				case TimeSpeed.Ultrafast:
					return 15f;
				default:
					return -1f;
			}
		}



		public void DrawAlreadySetAlarms(Rect inRect)
		{
			List<AlertUtility.Event> events = AlertUtility.GetEvents();
			List<AlertUtility.Event> eventsToRemove = new List<AlertUtility.Event>();
			foreach (var e in events)
			{
				int ticks = Find.TickManager.TicksGame;
				int alertTicks = e.presetGameTicksToAlert;
				float diff = (alertTicks > ticks ? alertTicks - ticks : 0);
				float ticksPerRealSec = TickRateMultiplier(Find.TickManager.CurTimeSpeed) * 60;
				string events_string = "";
				if (diff <= 10 * ticksPerRealSec)
				{
					//  If left real time is less than 10s, then show seconds instead of game hours
					float secs = diff / ticksPerRealSec;
					var tpl = "RealSeconds".Translate();
					events_string = $"{e.message}    {secs:F2} {tpl}";
				}
				else if (diff <= GenDate.TicksPerDay)
				{
					float hours = diff / GenDate.TicksPerHour;
					var tpl = "GameHours".Translate();
					events_string = $"{e.message}    {hours:F2} {tpl}\n";
				}
				else
				{
					float days = diff / GenDate.TicksPerDay;
					var tpl = "GameDays".Translate();
					events_string = $"{e.message}    {days:F2} {tpl}\n";
				}

				Rect rowRect;
				inRect.GetRowRect(out rowRect, out inRect, Text.LineHeight + 4);
				var labelRect = rowRect.LeftPartPixels(rowRect.width - Text.LineHeight - 4).ContractedBy(2f);
				var buttonRect = rowRect.RightPartPixels(Text.LineHeight + 4).ContractedBy(2f);
				Widgets.Label(labelRect, events_string);
				if (Widgets.ButtonImage(buttonRect, TexButton.Delete))
				{
					eventsToRemove.Add(e);
				}
			}

			foreach (var e in eventsToRemove)
			{
				AlertUtility.Remove(e);
			}
		}
		
		protected override void SetInitialSizeAndPosition()
		{
			base.windowRect = new Rect((UI.screenWidth - this.InitialSize.x) / 2, (UI.screenHeight - this.InitialSize.y) / 2, ((Window)this).InitialSize.x, ((Window)this).InitialSize.y);
		}

		public override void PreOpen()
		{
			base.PreOpen();
		}

		private static void RadioButtonDraw(float x, float y, bool chosen)
		{
			Color color = GUI.color;
			GUI.color = Color.white;
			GUI.DrawTexture(image: (!chosen) ? RadioButOffTex : RadioButOnTex, position: new Rect(x, y, 24f, 24f));
			GUI.color = color;
		}

		private static bool RadioButtonLabeled(Rect rect, string labelText, bool chosen)
		{
			Rect labelRect = new Rect(rect.x, rect.y, rect.width - 28f, rect.height);
			Rect radioButtonRect = new Rect(rect.x + rect.width - 24f, rect.y + rect.height / 2f - 12f, 24f, 24f);
			TextAnchor anchor = Text.Anchor;
			Text.Anchor = TextAnchor.MiddleRight;
			Widgets.Label(labelRect, labelText);
			Text.Anchor = anchor;
			bool num = Widgets.ButtonInvisible(rect);
			RadioButtonDraw(radioButtonRect.x, radioButtonRect.y, chosen);
			return num;
		}

		private int radioButtonGroupHorizontal(Rect inRect, List<string> labels, int res)
		{
			int count = labels.Count;
			float width = inRect.width / count;
			List<Rect> rects = new List<Rect>();

			for (int i = 0; i < count; ++i)
			{
				Rect rect = new Rect(inRect.x + width * i, inRect.y, width, inRect.height);
				rects.Add(rect);
			}

			int num = res;
			if (res >= 0 && res < count)
			{
				for (int i = 0; i < count; ++i)
				{
					if (RadioButtonLabeled(rects[i].Rounded(), labels[i], i == res))
					{
						num = i;
					}
				}
			}

			return num;
		}

		private int getMultiplier(int unit)
		{
			switch (unit)
			{
				case 0:
					return 1;
				case 1:
					return GenDate.TicksPerHour;
				case 2:
					return GenDate.TicksPerDay;
				case 3:
					return GenDate.TicksPerYear;
				default:
					return 1;
			}
		}

		float ticks = 0;

		public override void DoWindowContents(Rect inRect)
		{
			var listView = new Listing_Standard(GameFont.Small);
			listView.Begin(inRect);
			Text.Anchor = TextAnchor.MiddleCenter;
			listView.Label("SetTimer".Translate());
			listView.Gap();

			// Unit selection: ticks, hours, days, years
			//  Could create a RadioButtonGroup function
			List<string> labels = new List<string>();
			labels.Add("ticks".Translate());
			labels.Add("hours".Translate());
			labels.Add("days".Translate());
			labels.Add("years".Translate());
			//Log.Warning($"{unit}");
			unit = radioButtonGroupHorizontal(listView.GetRect(Text.LineHeight), labels, unit);
			//Log.Warning($"{unit}");
			listView.Gap();

			// float ticks = 0, real_ticks = 0;
			float real_ticks = 0;
			listView.TextFieldNumericLabeled<float>("TicksToAlert".Translate(), ref ticks, ref buffer);
			Log.Message($"ticks: {ticks}");
			listView.Gap();

			listView.Label("TicksToAlertExplaination".Translate());
			listView.Gap();

			Text.Anchor = TextAnchor.MiddleLeft;
			txtBuffer = listView.TextEntryLabeled("LetterMessage".Translate(), txtBuffer, 2);
			listView.Gap();

			Rect buttonLine = listView.GetRect(30f);
			Rect setButtonRect = buttonLine.LeftHalf().ContractedBy(2f);
			Rect closeButtonRect = buttonLine.RightHalf().ContractedBy(2f);

			if (Widgets.ButtonText(setButtonRect, "Set".Translate()))
			{
				int ticksToAlert = Find.TickManager.TicksGame;
				int multiplier = getMultiplier(unit);
				real_ticks = ticks * multiplier;
				Log.Message($"{ticksToAlert} {ticks} {multiplier}");
				AlertUtility.Add(new AlertUtility.Event((int)(ticksToAlert + real_ticks), txtBuffer));
				ticks = 0;
				Find.WindowStack.TryRemove(typeof(TimerSetWindow));
			}

			GUI.color = new Color(1f, 0.3f, 0.35f);
			if (Widgets.ButtonText(closeButtonRect, "CloseButton".Translate()))
			{
				Find.WindowStack.TryRemove(typeof(TimerSetWindow));
			}
			GUI.color = Color.white;


			listView.Gap();

			var rect = listView.GetRect(Text.LineHeight * 5);
			Text.Anchor = TextAnchor.UpperRight;
			Widgets.Label(rect.LeftHalf(), "AlreaySet".Translate());
			GenUI.ResetLabelAlign();
			// Widgets.LabelScrollable(rect.RightHalf(), getEventsString(), ref scrollbarPos);

			const float scrollbarWidth = 16f;
			Rect listAlarmsRect = rect.RightHalf();
			Rect scrollRect = listAlarmsRect;
			scrollRect.width -= scrollbarWidth;
			scrollRect.height = (Text.LineHeight + 4) * AlertUtility.GetEvents().Count;

			Widgets.BeginScrollView(listAlarmsRect, ref scrollbarPos, scrollRect);
			DrawAlreadySetAlarms(scrollRect);
			Widgets.EndScrollView();

			GenUI.ResetLabelAlign();
			listView.End();

		}

		public override void PostClose()
		{
		}

		public static void DrawWindow(List<AlertUtility.Event> events)
		{
			Find.WindowStack.Add((Window)(object)new TimerSetWindow());
		}
	}
}
