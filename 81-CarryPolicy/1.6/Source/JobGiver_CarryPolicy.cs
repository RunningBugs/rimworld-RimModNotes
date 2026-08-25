using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace _CarryPolicy;

public class JobGiver_CarryPolicy : ThinkNode_JobGiver
{
	// 自带节流，不共用 mindState.nextInventoryStockTick（那会和原版医药库存补货互相推迟）
	// 表存在 CarryPolicyDatabase（GameComponent）里：每局新实例，读档/开新档天然重置；
	// 不要改成 static——static 会跨存档存活，老档的大 tick 值会让新档长期不补货
	private const int CheckIntervalMin = 2500;

	private const int CheckIntervalMax = 5000;

	// 无缺口时的短间隔：该路径只数背包件数、不做任何寻路，开销可忽略，
	// 所以可以放心查勤一点让补货更及时；只有「有缺口但找不到货」才走上面的长间隔退避
	private const int IdleCheckIntervalMin = 300;

	private const int IdleCheckIntervalMax = 600;

	protected override Job TryGiveJob(Pawn pawn)
	{
		if (!pawn.IsColonist || !pawn.RaceProps.Humanlike || pawn.Map == null || pawn.inventory == null || pawn.inventory.UnloadEverything)
		{
			return null;
		}
		CarryPolicyDatabase database = CarryPolicyDatabase.Instance;
		if (database == null || (database.nextCheckTick.TryGetValue(pawn, out int value) && Find.TickManager.TicksGame < value))
		{
			return null;
		}
		// 已超重的小人不再补货（与原版 JobGiver_PackFood 的门控一致；同时覆盖取膏路径无载重检查的问题）
		if (MassUtility.IsOverEncumbered(pawn))
		{
			return null;
		}
		CarryPolicy carryPolicy = CarryPolicyUtility.CurrentPolicyFor(pawn);
		if (carryPolicy == null || carryPolicy.Count == 0)
		{
			ScheduleNextCheck(pawn, longCooldown: false);
			return null;
		}
		bool deficiency = false;
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
			deficiency = true;
			// 优先拿地图上现成的（含库存里已储存的营养膏），找不到才去营养膏机现场取
			Job job = TryTakeInventoryJob(pawn, entry, num) ?? TryDispenserJob(pawn, entry.thing);
			if (job != null)
			{
				return job;
			}
		}
		ScheduleNextCheck(pawn, deficiency);
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
		// 不做任何"能不能吃/想不想吃"门控：方案是玩家显式指定的携带清单，想带什么就带什么
		Thing thing = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(entry.thing), PathEndMode.ClosestTouch, TraverseParms.For(pawn, Danger.Some), 9999f, (Thing t) => !t.IsForbidden(pawn) && pawn.CanReserve(t, 10, Mathf.Min(t.stackCount, wanted)));
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

	// longCooldown=true：有缺口但找不到货（刚跑过寻路且失败），长退避防反复寻路；
	// longCooldown=false：无缺口（只数了背包件数），短间隔轮询保证补货及时
	private void ScheduleNextCheck(Pawn pawn, bool longCooldown)
	{
		CarryPolicyDatabase instance = CarryPolicyDatabase.Instance;
		if (instance == null)
		{
			return;
		}
		Dictionary<Pawn, int> nextCheckTick = instance.nextCheckTick;
		nextCheckTick[pawn] = Find.TickManager.TicksGame + (longCooldown ? Rand.RangeInclusive(CheckIntervalMin, CheckIntervalMax) : Rand.RangeInclusive(IdleCheckIntervalMin, IdleCheckIntervalMax));
		if (nextCheckTick.Count > 128)
		{
			List<Pawn> list = new List<Pawn>();
			foreach (Pawn key in nextCheckTick.Keys)
			{
				if (key == null || key.Destroyed)
				{
					list.Add(key);
				}
			}
			foreach (Pawn item in list)
			{
				nextCheckTick.Remove(item);
			}
		}
	}
}
