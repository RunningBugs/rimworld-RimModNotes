using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace _CarryPolicy;

public class CarryPolicyEntry : IExposable
{
	public ThingDef thing;

	// 食物：份数（1 份 = 普通成人一顿的量）；其他物品：件数。
	public float count;

	// UI 编辑缓冲（不存档）
	public string countBuffer;

	public bool IsFood => thing != null && thing.IsNutritionGivingIngestible;

	public void ExposeData()
	{
		Scribe_Defs.Look(ref thing, "thing");
		Scribe_Values.Look(ref count, "count", 1f);
	}
}

public class CarryPolicy : Policy
{
	public List<CarryPolicyEntry> entries = new List<CarryPolicyEntry>();

	public int Count => entries.Count;

	public CarryPolicyEntry this[int index] => entries[index];

	public CarryPolicyEntry EntryFor(ThingDef def)
	{
		return entries.FirstOrDefault(e => e.thing == def);
	}

	protected override string LoadKey => "CarryPolicy";

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
	}

	public override void CopyFrom(Policy other)
	{
		if (!(other is CarryPolicy carryPolicy))
		{
			return;
		}
		entries = carryPolicy.entries.Select(e => new CarryPolicyEntry
		{
			thing = e.thing,
			count = e.count
		}).ToList();
	}
}

public class CarryPolicyDatabase : GameComponent
{
	public List<CarryPolicy> policies = new List<CarryPolicy>();

	public Dictionary<Pawn, CarryPolicy> assignments = new Dictionary<Pawn, CarryPolicy>();

	public List<CarryPolicy> AllPolicies => policies;

	public CarryPolicyDatabase()
	{
	}

	public CarryPolicyDatabase(Game game)
	{
	}

	public static CarryPolicyDatabase Instance => Current.Game.GetComponent<CarryPolicyDatabase>();

	public override void ExposeData()
	{
		Scribe_Collections.Look(ref policies, "carryPolicies", LookMode.Deep);
		Scribe_Collections.Look(ref assignments, "carryAssignments", LookMode.Reference, LookMode.Reference);
	}

	public CarryPolicy MakeNewPolicy()
	{
		int num = 0;
		foreach (CarryPolicy policy in policies)
		{
			if (policy.id > num)
			{
				num = policy.id;
			}
		}
		CarryPolicy carryPolicy = new CarryPolicy
		{
			id = num + 1,
			label = "CarryPolicy".Translate() + " " + (num + 1)
		};
		policies.Add(carryPolicy);
		return carryPolicy;
	}

	public CarryPolicy DefaultCarryPolicy()
	{
		if (policies.Count == 0)
		{
			MakeNewPolicy().label = "DefaultCarryPolicy".Translate();
		}
		return policies[0];
	}

	public void SetDefault(CarryPolicy policy)
	{
		if (policies.Remove(policy))
		{
			policies.Insert(0, policy);
		}
	}

	public AcceptanceReport TryDelete(CarryPolicy policy)
	{
		foreach (Pawn item in assignments.Keys.ToList())
		{
			if (assignments[item] != policy)
			{
				continue;
			}
			if (item == null || item.Destroyed)
			{
				assignments.Remove(item);
			}
			else
			{
				return "CarryPolicyInUse".Translate(item.LabelShortCap);
			}
		}
		if (!policies.Remove(policy))
		{
			return false;
		}
		if (policies.Count == 0)
		{
			DefaultCarryPolicy();
		}
		return true;
	}
}

public static class CarryPolicyUtility
{
	// Need_Food.BaseHungerRate：普通成人 = 1.0 * 1.0 * 2.6666667e-5/刻
	public const float BaselineHungerFallPerHour = 2.6666667e-5f * 2500f;

	public const float MealNutritionBaseline = 0.9f;

	// 1 份 = 普通成人一顿正餐维持的时间（≈13.5 小时）
	public static readonly float HoursPerPortion = MealNutritionBaseline / BaselineHungerFallPerHour;

	public static CarryPolicy CurrentPolicyFor(Pawn pawn)
	{
		CarryPolicyDatabase instance = CarryPolicyDatabase.Instance;
		if (instance == null || pawn == null)
		{
			return null;
		}
		if (!instance.assignments.TryGetValue(pawn, out var value))
		{
			return instance.DefaultCarryPolicy();
		}
		return value ?? instance.DefaultCarryPolicy();
	}

	public static void SetPolicyFor(Pawn pawn, CarryPolicy policy)
	{
		CarryPolicyDatabase.Instance.assignments[pawn] = policy;
	}

	// 该小人 1 份对应的营养量（基准成人恰为 0.9；随基因/体型/疾病等浮动）
	public static float NutritionPerPortion(Pawn pawn)
	{
		if (pawn?.needs?.food == null)
		{
			return MealNutritionBaseline;
		}
		float num = pawn.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed) * 2500f * HoursPerPortion;
		if (num < 0.05f)
		{
			return MealNutritionBaseline;
		}
		return num;
	}

	// 该小人应持有的目标件数
	public static int TargetCount(CarryPolicyEntry entry, Pawn pawn)
	{
		if (entry?.thing == null || entry.count <= 0f)
		{
			return 0;
		}
		if (!entry.IsFood)
		{
			return Mathf.Max(0, Mathf.RoundToInt(entry.count));
		}
		return Mathf.CeilToInt(entry.count * NutritionPerPortion(pawn) / ItemNutrition(entry.thing));
	}

	// UI 用：按普通成人估算的目标件数
	public static int BaselineTargetCount(CarryPolicyEntry entry)
	{
		if (entry?.thing == null || entry.count <= 0f)
		{
			return 0;
		}
		if (!entry.IsFood)
		{
			return Mathf.Max(0, Mathf.RoundToInt(entry.count));
		}
		return Mathf.CeilToInt(entry.count * MealNutritionBaseline / ItemNutrition(entry.thing));
	}

	public static float ItemNutrition(ThingDef def)
	{
		float num = def.GetStatValueAbstract(StatDefOf.Nutrition);
		if (num <= 0f)
		{
			num = 0.05f;
		}
		return num;
	}

	// 可选范围：能进背包的物品（Item 类别 + 可搬运；排除尸体）
	public static bool Selectable(ThingDef def)
	{
		if (def == null || def.thingClass == null || def.category != ThingCategory.Item || !def.EverHaulable)
		{
			return false;
		}
		return !typeof(Corpse).IsAssignableFrom(def.thingClass);
	}

	// 条目仍需保留在背包里（未超目标件数）则 true，供防丢弃补丁使用
	public static bool ShouldKeepInInventory(Pawn pawn, ThingDef def)
	{
		CarryPolicyEntry carryPolicyEntry = CurrentPolicyFor(pawn)?.EntryFor(def);
		if (carryPolicyEntry == null || carryPolicyEntry.count <= 0f)
		{
			return false;
		}
		return pawn.inventory.Count(def) <= TargetCount(carryPolicyEntry, pawn);
	}
}
