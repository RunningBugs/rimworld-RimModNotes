using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace _CarryPolicy;

public class JobGiver_CarryPolicy : ThinkNode_JobGiver
{
	// 自带节流，不共用 mindState.nextInventoryStockTick（那会和原版医药库存补货互相推迟）
	private static readonly Dictionary<Pawn, int> NextCheckTick = new Dictionary<Pawn, int>();

	private const int CheckIntervalMin = 2500;

	private const int CheckIntervalMax = 5000;

	protected override Job TryGiveJob(Pawn pawn)
	{
		if (!pawn.IsColonist || !pawn.RaceProps.Humanlike || pawn.Map == null || pawn.inventory == null || pawn.inventory.UnloadEverything)
		{
			return null;
		}
		if (NextCheckTick.TryGetValue(pawn, out int value) && Find.TickManager.TicksGame < value)
		{
			return null;
		}
		CarryPolicy carryPolicy = CarryPolicyUtility.CurrentPolicyFor(pawn);
		if (carryPolicy == null || carryPolicy.Count == 0)
		{
			ScheduleNextCheck(pawn);
			return null;
		}
		foreach (CarryPolicyEntry entry in carryPolicy.entries)
		{
			if (entry?.thing == null)
			{
				continue;
			}
			int num = CarryPolicyUtility.TargetCount(entry, pawn) - pawn.inventory.Count(entry.thing);
			if (num <= 0)
			{
				continue;
			}
			Job job = TryDispenserJob(pawn, entry.thing) ?? TryTakeInventoryJob(pawn, entry, num);
			if (job != null)
			{
				return job;
			}
		}
		ScheduleNextCheck(pawn);
		return null;
	}

	private Job TryDispenserJob(Pawn pawn, ThingDef def)
	{
		foreach (Building_NutrientPasteDispenser item in pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_NutrientPasteDispenser>())
		{
			if (item.DispensableDef != def || !item.CanDispenseNow || !pawn.CanReserve(item) || !pawn.CanReach(item, PathEndMode.Touch, Danger.Some))
			{
				continue;
			}
			Job job = JobMaker.MakeJob(CarryPolicyDefOf.CarryPolicyTakeFromDispenser, item);
			return job;
		}
		return null;
	}

	private Job TryTakeInventoryJob(Pawn pawn, CarryPolicyEntry entry, int wanted)
	{
		Thing thing = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(entry.thing), PathEndMode.ClosestTouch, TraverseParms.For(pawn, Danger.Some), 9999f, (Thing t) => !t.IsForbidden(pawn) && pawn.CanReserve(t, 10, Mathf.Min(t.stackCount, wanted)) && (!entry.IsFood || pawn.WillEat(t)));
		if (thing == null)
		{
			return null;
		}
		int num = Mathf.Min(wanted, thing.stackCount);
		float statValue = thing.GetStatValue(StatDefOf.Mass);
		if (statValue > 0.0001f)
		{
			float num2 = MassUtility.Capacity(pawn) - MassUtility.GearAndInventoryMass(pawn);
			num = Mathf.Min(num, Mathf.FloorToInt(num2 / statValue));
		}
		if (num <= 0)
		{
			return null;
		}
		Job job = JobMaker.MakeJob(JobDefOf.TakeInventory, thing);
		job.count = num;
		return job;
	}

	private void ScheduleNextCheck(Pawn pawn)
	{
		NextCheckTick[pawn] = Find.TickManager.TicksGame + Rand.RangeInclusive(CheckIntervalMin, CheckIntervalMax);
		if (NextCheckTick.Count > 128)
		{
			List<Pawn> list = new List<Pawn>();
			foreach (Pawn key in NextCheckTick.Keys)
			{
				if (key == null || key.Destroyed)
				{
					list.Add(key);
				}
			}
			foreach (Pawn item in list)
			{
				NextCheckTick.Remove(item);
			}
		}
	}
}
