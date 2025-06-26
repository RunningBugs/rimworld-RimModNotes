using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;
using Verse.Grammar;

namespace PermanentDarkness;


public class PermanentDarknessController : GameComponent
{
	public PermanentDarknessController(Game game) : base()
	{
	}

	public static int StartTick = 0;
	public static int Delay = 0;
	public static bool IsActive = false;

	private static bool initialLetter = false;
	private static bool mainWarning = false;
	private static bool mainLetter = false;

	private static readonly FloatRange InitialPhaseDurationDaysRange = new FloatRange(0.5f, 0.75f);
	private readonly int WarningTicks = 10000;

	public static void GenDelay()
	{
		Delay = Mathf.RoundToInt(InitialPhaseDurationDaysRange.RandomInRange * 60000f) + 1;
	}

	public static void Activate()
	{
		IsActive = true;
		if (StartTick == 0)
		{
			StartTick = GenTicks.TicksGame;
			GenDelay();
		}
	}

	public static void Deactivate()
	{
		IsActive = false;
		StartTick = 0;
		Delay = 0;
		initialLetter = false;
		mainWarning = false;
		mainLetter = false;
	}

	public override void GameComponentTick()
	{
		if (!IsActive) return;

		if (GenTicks.IsTickInterval(250))
		{
			ForceWeatherOnAllMaps();
		}

		if (GenTicks.IsTickInterval(60))
		{
			HandleLetters();
		}
	}

	private void HandleLetters()
	{
		int initPhaseStartTick = StartTick;
		int mainPhaseStartTick = StartTick + Delay;

		if (GenTicks.TicksGame >= initPhaseStartTick && !initialLetter)
		{
			Find.LetterStack.ReceiveLetter(ResolveText("initialPhaseLetterLabel"), ResolveText("initialPhaseLetterText"), LetterDefOf.NegativeEvent);
			initialLetter = true;
		}
		else if (GenTicks.TicksGame >= mainPhaseStartTick - WarningTicks && !mainWarning)
		{
			Find.LetterStack.ReceiveLetter("DarknessWarningLetterLabel".Translate(), "DarknessWarningLetterText".Translate(), LetterDefOf.NeutralEvent);
			mainWarning = true;
		}
		else if (GenTicks.TicksGame >= mainPhaseStartTick && !mainLetter)
		{
			Find.LetterStack.ReceiveLetter(ResolveText("mainPhaseLetterLabel"), ResolveText("mainPhaseLetterText"), LetterDefOf.ThreatBig);
			mainLetter = true;
		}
	}

	private string ResolveText(string root)
	{
		GrammarRequest request = new GrammarRequest();
		request.Includes.Add(RulePackDefs.PermanentDarkness);
		return GrammarResolver.Resolve(root, request);
	}

	private void ForceWeatherOnAllMaps()
	{
		WeatherDef targetWeather = GetCurrentWeather();
		
		foreach (Map map in Find.Maps)
		{
			if (!map.gameConditionManager.ConditionIsActive(GameConditionDefs.PermanentDarkness))
			{
				GameCondition_PermanentDarkness condition = (GameCondition_PermanentDarkness)GameConditionMaker.MakeCondition(GameConditionDefs.PermanentDarkness);
				condition.Permanent = true;
				map.gameConditionManager.RegisterCondition(condition);
			}
		}
	}

	private WeatherDef GetCurrentWeather()
	{
		int mainPhaseStartTick = StartTick + Delay;
		if (GenTicks.TicksGame >= mainPhaseStartTick)
		{
			return WeatherDefs.PermanentDarkness_Stage2;
		}
		else
		{
			return WeatherDefs.PermanentDarkness_Stage1;
		}
	}

	public static void EndGlobally()
	{
		foreach (Map map in Find.Maps)
		{
			GameCondition condition = map.gameConditionManager.GetActiveCondition(GameConditionDefs.PermanentDarkness);
			condition?.End();
		}
		Deactivate();
	}

	public override void ExposeData()
	{
		Scribe_Values.Look(ref StartTick, "PD.StartTick", defaultValue: 0);
		Scribe_Values.Look(ref Delay, "PD.Delay", defaultValue: 0);
		Scribe_Values.Look(ref IsActive, "PD.IsActive", defaultValue: false);
		Scribe_Values.Look(ref initialLetter, "PD.initialLetter", defaultValue: false);
		Scribe_Values.Look(ref mainWarning, "PD.mainWarning", defaultValue: false);
		Scribe_Values.Look(ref mainLetter, "PD.mainLetter", defaultValue: false);
	}
}


public class GameCondition_PermanentDarkness : GameCondition_ForceWeather
{
	public bool anyColonistAttacked;

	private List<SkyOverlay> overlays = new List<SkyOverlay>
	{
		new WeatherOverlay_UnnaturalDarkness()
	};

	private int transitionTicks = 300;

	public override int TransitionTicks => transitionTicks;

	public int InitPhaseStartTick => PermanentDarknessController.StartTick;

	public int MainPhaseStartTick => PermanentDarknessController.StartTick + PermanentDarknessController.Delay;

	private readonly int WarningTicks = 10000;

	public GameCondition_PermanentDarkness()
	{
		Permanent = true;
	}


	public override void Init()
	{
		base.Init();
		PermanentDarknessController.Activate();
		Permanent = true;
	}

	public override float SkyTargetLerpFactor(Map map)
	{
		return GameConditionUtility.LerpInOutValue(this, TransitionTicks);
	}

	public override SkyTarget? SkyTarget(Map map)
	{
		SkyColorSet colors = GameCondition_NoSunlight.EclipseSkyColors;
		// if (ModSettingsUI.settings.darknessControl)
		// {
		// 	colors.shadow = new(1f, 0f, 0f, 0f);
		// }
		return new SkyTarget(0f, colors, 1f, 0f);
	}

	public override WeatherDef ForcedWeather()
	{
		if (GenTicks.TicksGame >= MainPhaseStartTick)
		{
			return WeatherDefs.PermanentDarkness_Stage2;
		}
		else
		{
			return WeatherDefs.PermanentDarkness_Stage1;
		}
	}

	private string ResolveText(string root)
	{
		GrammarRequest request = new GrammarRequest();
		request.Includes.Add(RulePackDefs.PermanentDarkness);
		return GrammarResolver.Resolve(root, request);
	}



	public static bool AffectedByDarkness(Pawn pawn)
	{
		if (pawn.Spawned && pawn.RaceProps.Humanlike && !pawn.Downed)
		{
			if (!pawn.IsColonistPlayerControlled)
			{
				return pawn.IsColonySubhumanPlayerControlled;
			}
			return true;
		}
		return false;
	}

	public override void GameConditionTick()
	{
		base.GameConditionTick();
		
		Map map = gameConditionManager.ownerMap;
		if (map == null) return;

		for (int j = 0; j < overlays.Count; j++)
		{
			overlays[j].TickOverlay(map, 1f);
		}

		if (GenTicks.IsTickInterval(60))
		{
			if (GenTicks.TicksGame >= MainPhaseStartTick)
			{
				map.gameConditionManager.SetTargetBrightness(0f);
			}

			foreach (Pawn item in map.mapPawns.AllHumanlikeSpawned)
			{
				if (AffectedByDarkness(item) && InUnnaturalDarkness(item) && item.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.DarknessExposure) == null)
				{
					item.health.AddHediff(HediffDefs.PD_DarknessExposure);
				}
			}
		}
	}

	public override void End()
	{
		base.End();
		DebugViewSettings.drawShadows = true;
		foreach (Map affectedMap in base.AffectedMaps)
		{
			affectedMap.weatherDecider.StartNextWeather();
		}
		
		if (PermanentDarknessController.IsActive)
		{
			PermanentDarknessController.EndGlobally();
		}
	}

	private readonly Color defaultDarknessColor = new(0.049f, 0.064f, 0.094f, 1.000f);
	public static bool shadowControlDirty = true;

	public override void GameConditionDraw(Map map)
	{
		if (!(map.GameConditionManager.MapBrightness > 0.5f))
		{
			for (int i = 0; i < overlays.Count; i++)
			{
				overlays[i].DrawOverlay(map);
			}
		}

		if (ModSettingsUI.settings.darknessControl)
		{
			float level = ModSettingsUI.settings.darknessLevel / 2f;
			Color nightBrightnessColor = new(level, level, level, 1f - level);
			MatBases.Darkness.color = nightBrightnessColor;

			if (shadowControlDirty)
			{
				DebugViewSettings.drawShadows = ModSettingsUI.settings.shadowControl;
				shadowControlDirty = false;
			}
		}
		else
		{
			MatBases.Darkness.color = defaultDarknessColor;
		}
	}

	public override List<SkyOverlay> SkyOverlays(Map map)
	{
		return overlays;
	}

	public static bool InUnnaturalDarkness(Pawn p)
	{
		if (!p.SpawnedOrAnyParentSpawned)
		{
			return false;
		}
		return UnnaturalDarknessAt(p.PositionHeld, p.MapHeld);
	}

	public static bool UnnaturalDarknessAt(IntVec3 cell, Map map)
	{
		if (map == null)
		{
			return false;
		}
		if (map.gameConditionManager.MapBrightness > 0.01f)
		{
			return false;
		}
		if (!map.gameConditionManager.ConditionIsActive(GameConditionDefs.PermanentDarkness))
		{
			return false;
		}
		Building_Door door = cell.GetDoor(map);
		if (door != null)
		{
			float num = 0f;
			int num2 = 0;
			foreach (IntVec3 edgeCell in door.OccupiedRect().ExpandedBy(1).EdgeCells)
			{
				if (edgeCell.InBounds(map) && !edgeCell.Filled(map))
				{
					num += map.glowGrid.GroundGlowAt(edgeCell);
					num2++;
				}
			}
			if (num2 > 0)
			{
				return num / (float)num2 <= 0f;
			}
		}
		return map.glowGrid.GroundGlowAt(cell) <= 0f;
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref anyColonistAttacked, "PD.anyColonistAttacked", defaultValue: false);
	}
}
