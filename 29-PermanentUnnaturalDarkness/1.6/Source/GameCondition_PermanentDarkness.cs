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

	private static readonly FloatRange InitialPhaseDurationDaysRange = new FloatRange(0.5f, 0.75f);

	public static int Delay = 0;

	public static void GenDelay()
	{
		Delay = Mathf.RoundToInt(InitialPhaseDurationDaysRange.RandomInRange * 60000f) + 1;
	}

	public override void ExposeData()
	{
		Scribe_Values.Look(ref StartTick, "PD.StartTick", defaultValue: 0);
		Scribe_Values.Look(ref Delay, "PD.Delay", defaultValue: 0);
	}
}


public class GameCondition_PermanentDarkness : GameCondition_ForceWeather
{
	public bool anyColonistAttacked;

	private static bool initialLetter = false;

	private static bool mainWarning = false;

	private static bool mainLetter = false;

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
		if (PermanentDarknessController.Delay == 0)
		{
			PermanentDarknessController.GenDelay();
			PermanentDarknessController.StartTick = GenTicks.TicksGame;
		}
		else
		{
			transitionTicks = 0;
		}
		Permanent = true;
	}

	public override float SkyTargetLerpFactor(Map map)
	{
		return GameConditionUtility.LerpInOutValue(this, TransitionTicks);
	}

	public override SkyTarget? SkyTarget(Map map)
	{
		return new SkyTarget(0f, GameCondition_NoSunlight.EclipseSkyColors, 1f, 0f);
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

	private bool runOnce = false;
	

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
		if (!runOnce)
		{
			var gcm = Find.World.gameConditionManager;
			if (!gcm.ConditionIsActive(def))
			{
				gcm.RegisterCondition(this);
			}
			runOnce = true;
		}

		base.GameConditionTick();
		List<Map> affectedMaps = base.AffectedMaps;
		for (int i = 0; i < affectedMaps.Count; i++)
		{
			Map map = affectedMaps[i];

			for (int j = 0; j < overlays.Count; j++)
			{
				overlays[j].TickOverlay(affectedMaps[i], 1f);
			}

			if (!GenTicks.IsTickInterval(60))
			{
				continue;
			}

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

		if (GenTicks.IsTickInterval(60))
		{
			if (GenTicks.TicksGame >= InitPhaseStartTick && !initialLetter)
			{
				Find.LetterStack.ReceiveLetter(ResolveText("initialPhaseLetterLabel"), ResolveText("initialPhaseLetterText"), LetterDefOf.NegativeEvent);
				initialLetter = true;
			}
			else if (GenTicks.TicksGame >= MainPhaseStartTick - WarningTicks && !mainWarning)
			{
				Find.LetterStack.ReceiveLetter("DarknessWarningLetterLabel".Translate(), "DarknessWarningLetterText".Translate(), LetterDefOf.NeutralEvent);
				mainWarning = true;
			}
			else if (GenTicks.TicksGame >= MainPhaseStartTick && !mainLetter)
			{
				Find.LetterStack.ReceiveLetter(ResolveText("mainPhaseLetterLabel"), ResolveText("mainPhaseLetterText"), LetterDefOf.ThreatBig);
				mainLetter = true;
			}
		}
	}

	public override void End()
	{
		base.End();
		foreach (Map affectedMap in base.AffectedMaps)
		{
			affectedMap.weatherDecider.StartNextWeather();
		}
	}

	public override void GameConditionDraw(Map map)
	{
		if (!(map.GameConditionManager.MapBrightness > 0.5f))
		{
			for (int i = 0; i < overlays.Count; i++)
			{
				overlays[i].DrawOverlay(map);
			}
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
		if (!ModsConfig.AnomalyActive)
		{
			return false;
		}
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
		Scribe_Values.Look(ref initialLetter, "PD.initialLetter", defaultValue: false);
		Scribe_Values.Look(ref mainWarning, "PD.mainWarning", defaultValue: false);
		Scribe_Values.Look(ref mainLetter, "PD.mainLetter", defaultValue: false);
	}
}
