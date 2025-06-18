using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse.AI;
using Verse.Sound;

namespace PDEDontStarve;


public class GenStepFireFliesExtension : DefModExtension
{
	public float density = 0.01f;

	public int minEdgeDistance = 25;
}

public class GenStep_FireFlies : GenStep_SpecialTrees
{
	public float Density => def.GetModExtension<GenStepFireFliesExtension>().density;

	private static readonly FloatRange GrowthRange = new FloatRange(1f, 1f);

	public override int SeedPart => 647816171;

	public override int DesiredTreeCountForMap(Map map)
	{
		return Mathf.Max(Mathf.RoundToInt(Density * map.Area), 1);
	}

	protected override float GetGrowth()
	{
		return GrowthRange.RandomInRange;
	}

	public override void Generate(Map map, GenStepParams parms)
	{
		int num = DesiredTreeCountForMap(map);
		var minEdgeDistance = def.GetModExtension<GenStepFireFliesExtension>().minEdgeDistance;

		while (num > 0 && CellFinderLoose.TryFindRandomNotEdgeCellWith(minEdgeDistance, (IntVec3 x) => CanSpawnAt(x, map, minProximityToArtificialStructures: 0, minProximityToCenter: 0, minProximityToSameTree: Mathf.CeilToInt(ThingDefs.Plant_Fireflies.plant.minSpacingBetweenSamePlant), minDistFromMapEdge: 5, fertilityRequirement: -1f), map, out IntVec3 result))
		{
			if (TrySpawnAt(result, map, GetGrowth(), out var _))
			{
				num--;
			}
		}
	}

	public override bool CanSpawnAt(IntVec3 c, Map map, int minProximityToArtificialStructures = 40, int minProximityToCenter = 0, float fertilityRequirement = 0f, int minFertileUnroofedCells = 22, int maxFertileUnroofedCellRadius = 10, int minProximityToSameTree = 0, int maxProximityToSameTree = -1, int minDistFromMapEdge = 15)
	{
		if (!c.Standable(map) || c.Fogged(map) || !c.GetRoom(map).PsychologicallyOutdoors)
		{
			return false;
		}
		Plant plant = c.GetPlant(map);
		if (plant != null && plant.def.plant.growDays > 10f)
		{
			return false;
		}
		List<Thing> thingList = c.GetThingList(map);
		for (int i = 0; i < thingList.Count; i++)
		{
			if (thingList[i].def == treeDef)
			{
				return false;
			}
		}
		if (minProximityToCenter > 0 && map.Center.InHorDistOf(c, minProximityToCenter))
		{
			return false;
		}
		if (minDistFromMapEdge > 0 && c.DistanceToEdge(map) < minDistFromMapEdge)
		{
			return false;
		}
		if (!map.reachability.CanReachFactionBase(c, map.ParentFaction))
		{
			return false;
		}
		if (c.Roofed(map))
		{
			return false;
		}
		if (minProximityToArtificialStructures != 0 && GenRadial.RadialDistinctThingsAround(c, map, minProximityToArtificialStructures, useCenter: false).Any(MeditationUtility.CountsAsArtificialBuilding))
		{
			return false;
		}
		if (minProximityToSameTree > 0 && GenRadial.RadialDistinctThingsAround(c, map, minProximityToSameTree, useCenter: false).Any((Thing t) => t.def == treeDef))
		{
			return false;
		}
		if (validators != null)
		{
			for (int j = 0; j < validators.Count; j++)
			{
				if (!validators[j].Allows(c, map))
				{
					return false;
				}
			}
		}
		int num = GenRadial.NumCellsInRadius(maxFertileUnroofedCellRadius);
		int num2 = 0;
		for (int k = 0; k < num; k++)
		{
			IntVec3 intVec = c + GenRadial.RadialPattern[k];
			if (WanderUtility.InSameRoom(intVec, c, map))
			{
				if (intVec.InBounds(map) && !intVec.Roofed(map) && intVec.GetFertility(map) > 0f)
				{
					num2++;
				}
				if (num2 >= minFertileUnroofedCells)
				{
					return true;
				}
			}
		}
		return true;
	}
}


[StaticConstructorOnStartup]
public class CompPlantableFireFly : CompPlantable
{
	private List<IntVec3> plantCells = new List<IntVec3>();
	private static readonly Texture2D CancelCommandTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
	private static TargetingParameters TargetingParams => new TargetingParameters
	{
		canTargetPawns = false,
		canTargetLocations = true
	};

	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		if (PlantCells.Count > 0)
		{
			Command_Action command_Action = new Command_Action();
			command_Action.defaultLabel = "CancelPlantThing".Translate(Props.plantDefToSpawn);
			command_Action.defaultDesc = "CancelPlantThingDesc".Translate(Props.plantDefToSpawn);
			command_Action.icon = CancelCommandTex;
			command_Action.hotKey = KeyBindingDefOf.Designator_Cancel;
			command_Action.action = delegate
			{
				plantCells.Clear();
			};
			yield return command_Action;
		}
		if (PlantCells.Count < parent.stackCount)
		{
			Command_Action command_Action2 = new Command_Action();
			command_Action2.defaultLabel = "PlantThing".Translate(Props.plantDefToSpawn);
			command_Action2.defaultDesc = "PlantThingDesc".Translate(Props.plantDefToSpawn);
			command_Action2.icon = Props.plantDefToSpawn.uiIcon;
			command_Action2.action = BeginTargeting;
			yield return command_Action2;
		}
	}

	private void BeginTargeting()
	{
		Find.Targeter.BeginTargeting(TargetingParams, delegate(LocalTargetInfo t)
		{
			if (ValidateTarget(t))
			{
				plantCells.Add(t.Cell);
				GenDraw.DrawTargetHighlight(t);
				DrawSurroundingsInfo(t.Cell);
				SoundDefOf.Tick_High.PlayOneShotOnCamera();
			}
			else
			{
				BeginTargeting();
			}
		}, delegate(LocalTargetInfo t)
		{
			if (CanPlantAt(t.Cell, parent.MapHeld).Accepted)
			{
				GenDraw.DrawTargetHighlight(t);
				DrawSurroundingsInfo(t.Cell);
			}
		}, (LocalTargetInfo t) => true, null, null, Props.plantDefToSpawn.uiIcon, playSoundOnAction: false);
	}

	private IEnumerable<Thing> NearByFireFlies(IntVec3 cell, float radius)
	{
		return GenRadial.RadialDistinctThingsAround(cell, parent.Map, radius, useCenter: true).Where((Thing t) => t.def == ThingDefs.Plant_Fireflies);
	}

	private void DrawSurroundingsInfo(IntVec3 cell)
	{
		var plant = Props.plantDefToSpawn.plant;
		if (plant != null)
		{
			var radius = plant.minSpacingBetweenSamePlant;
			Color color = Color.white;
			if (NearByFireFlies(cell, radius).Any())
			{
				color = Color.red;
				foreach (Thing item in NearByFireFlies(cell, radius))
				{
					GenDraw.DrawLineBetween(cell.ToVector3ShiftedWithAltitude(AltitudeLayer.Blueprint), item.TrueCenter(), SimpleColor.Red);
				}
			}
			color.a = 0.5f;
			var compProps = Props.plantDefToSpawn.GetCompProperties<CompProperties_Glower>();
			if (compProps != null)
			{
				GenDraw.DrawRadiusRing(cell, compProps.glowRadius, color);
			}
		}
	}
}


public class ScattererValidator_AvoidSpecialThings : ScattererValidator
{
	private static Dictionary<ThingDef, float> thingsToAvoid;

	public override bool Allows(IntVec3 c, Map map)
	{
		return IsValid(c, map);
	}

	public static bool IsValid(IntVec3 c, Map map)
	{
		if (thingsToAvoid == null)
		{
			thingsToAvoid = new Dictionary<ThingDef, float>
			{
				{
					ThingDefOf.SteamGeyser,
					3f
				},
				{
					ThingDefOf.AncientCryptosleepCasket,
					30f
				}
			};
			if (ModsConfig.RoyaltyActive)
			{
				thingsToAvoid.Add(ThingDefOf.Plant_TreeAnima, 5f);
			}
			if (ModsConfig.IdeologyActive)
			{
				thingsToAvoid.Add(ThingDefOf.ArchonexusCore, 10f);
				thingsToAvoid.Add(ThingDefOf.GrandArchotechStructure, 10f);
				thingsToAvoid.Add(ThingDefOf.MajorArchotechStructure, 10f);
			}
			if (ModsConfig.BiotechActive)
			{
				thingsToAvoid.Add(ThingDefOf.AncientExostriderRemains, 6f);
			}
			if (ModsConfig.AnomalyActive)
			{
				thingsToAvoid.Add(ThingDefOf.VoidMonolith, 10f);
			}

			thingsToAvoid.Add(ThingDefs.Plant_Fireflies, ThingDefs.Plant_Fireflies.plant.minSpacingBetweenSamePlant);
		}
		foreach (KeyValuePair<ThingDef, float> item in thingsToAvoid)
		{
			if (item.Key == null)
			{
				continue;
			}
			foreach (Thing item2 in map.listerThings.ThingsOfDef(item.Key))
			{
				if (c.InHorDistOf(item2.Position, item.Value))
				{
					return false;
				}
			}
		}
		return true;
	}
}


public class CompProperties_CompGlowerFireFly : CompProperties
{
	public float dimRadius = 2f;

	public int dimRecoveryTicks = 100;

	public CompProperties_CompGlowerFireFly()
	{
		compClass = typeof(CompGlowerFireFly);
	}
}


public class CompGlowerFireFly : ThingComp, IThingGlower
{
	CompProperties_CompGlowerFireFly Props => (CompProperties_CompGlowerFireFly)props;

	private int lastDimTick = -9999;

	public int NextGlowTick => lastDimTick + Props.dimRecoveryTicks;

	public bool glowing = true;

	public CompGlower glower = null;

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);
		glower = parent.TryGetComp<CompGlower>();
	}

	private void TurnOffLight()
	{
		if (glowing)
		{
			glowing = false;
			glower ??= parent.TryGetComp<CompGlower>();
			glower.UpdateLit(parent.Map);
		}
		lastDimTick = Find.TickManager.TicksGame;
	}

	private void TurnOnLight()
	{
		if (!glowing)
		{
			glowing = true;
			glower ??= parent.TryGetComp<CompGlower>();
			glower.UpdateLit(parent.Map);
		}
		lastDimTick = -9999;
	}

	public override void CompTick()
	{
		base.CompTick();
		if (parent.IsHashIntervalTick(60))
		{
			if (GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, Props.dimRadius, useCenter: true).Any((Thing p) => p is Pawn pawn && pawn.RaceProps.Humanlike))
			{
				TurnOffLight();
			}
			else if (NextGlowTick > 0 && Find.TickManager.TicksGame >= NextGlowTick)
			{
				TurnOnLight();
			}
		}
	}

	public bool ShouldBeLitNow()
	{
		return glowing;
	}

	public override void PostExposeData()
	{
		base.PostExposeData();
		Scribe_Values.Look(ref lastDimTick, "lastDimTick");
		Scribe_Values.Look(ref glowing, "glowing");
	}
}

public class FireFly : Plant
{
	public override int YieldNow() {
		return 1;
	}
}
