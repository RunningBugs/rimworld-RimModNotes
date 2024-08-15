using System.Collections.Generic;

using Verse;
using RimWorld;
using RimWorld.Planet;

using HarmonyLib;

namespace SFDistressCallFix;

[StaticConstructorOnStartup]
public class Start
{
	static Start()
	{
		Harmony harmony = new Harmony("com.RunningBugs.SFDistressCallFix");
		harmony.PatchAll();
	}
}

[HarmonyPatch(typeof(DistressCallUtility), "SpawnCorpses")]
public static class DistressCallUtilityPatch
{
	public static bool Prefix(Map map, IEnumerable<Pawn> pawns, IEnumerable<Pawn> killers, IntVec3 root, int radius)
	{
		SpawnCorpses(map, pawns, killers, root, radius);
		return false;
	}

	private static readonly IntRange BloodFilthToSpawn = new IntRange(1, 5);

	public static void SpawnCorpses(Map map, IEnumerable<Pawn> pawns, IEnumerable<Pawn> killers, IntVec3 root, int radius)
	{
		int num = Find.TickManager.TicksGame - map.Parent.creationGameTicks;
		foreach (Pawn pawn in pawns)
		{
			HealthUtility.SimulateKilledByPawn(pawn, killers.RandomElement());
			Corpse corpse = pawn.Corpse;
			if (corpse == null)
			{
				continue;
			}
			corpse.timeOfDeath = map.Parent.creationGameTicks;
			CompRottable compRottable = corpse.TryGetComp<CompRottable>();
			if (compRottable != null)
			{
				compRottable.RotProgress += num;
			}
			if (!RCellFinder.TryFindRandomCellNearWith(root, (IntVec3 c) => c.Standable(map) && c.GetEdifice(map) == null, map, out var result, radius))
			{
				break;
			}
			if (corpse.InnerPawn.kindDef.IsFleshBeast() && compRottable.Stage == RotStage.Dessicated)
			{
				FilthMaker.TryMakeFilth(result, map, ThingDefOf.Filth_TwistedFlesh);
				break;
			}
			GenSpawn.Spawn(corpse, result, map);
			pawn.DropAndForbidEverything();
			if (num >= 300000)
			{
				continue;
			}
			int randomInRange = BloodFilthToSpawn.RandomInRange;
			for (int i = 0; i < randomInRange; i++)
			{
				IntVec3 intVec = CellFinder.RandomClosewalkCellNear(result, map, 3);
				if (intVec.InBounds(map) && GenSight.LineOfSight(intVec, result, map))
				{
					FilthMaker.TryMakeFilth(intVec, map, pawn.RaceProps.BloodDef, pawn.LabelIndefinite());
				}
			}
		}
	}
}
