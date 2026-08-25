using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace _CarryPolicy;

// 搬运前卸货：方案条目内且未超目标件数的物品不卸。
//
// 注意这里必须是 prefix 整体重写，不能用 postfix 把"第一件可卸载物品"清空：
// 原 getter 的语义是"第一件"可卸载物品，postfix 清空结果等于宣称"没有"可卸载物品，
// 会让 CaravanShuttleUtility.LoadCaravanItemsIntoContainer 的 while 循环在撞到第一件
// 方案保留物品（如营养膏）时直接退出，导致穿梭机返程时所有货物都留在小人背包里；
// 同时 Pawn_InventoryTracker.UnloadEverything 的 setter 也会因为
// HasAnyUnloadableThing == false 而无法置位，小人落地后永远背着全部货物不卸。
// 正确做法是在原版保留规则（成瘾品方案、库存常备、打包食物）之上，按 def 叠加
// 携带方案的保留配额（取两者最大值），再完整扫描出第一件真正可卸载的物品。
[HarmonyPatch(typeof(Pawn_InventoryTracker), "FirstUnloadableThing", MethodType.Getter)]
public static class Patch_Pawn_InventoryTracker_FirstUnloadableThing
{
	// 复用静态列表避免每次调用分配（该 getter 在穿梭机装载循环、UnloadEverything 判定中高频触发）。
	// 游戏逻辑单线程，无需锁；每次入口必须 Clear。
	private static readonly List<ThingDefCount> itemsToKeep = new List<ThingDefCount>();

	public static bool Prefix(Pawn_InventoryTracker __instance, ref ThingCount __result)
	{
		__result = GetFirstUnloadableThing(__instance);
		return false;
	}

	// 与原版 Pawn_InventoryTracker.FirstUnloadableThing 逻辑一致，
	// 仅额外把携带方案的目标件数合并进保留清单（tmpItemsToKeep 的本地化等价物）。
	private static ThingCount GetFirstUnloadableThing(Pawn_InventoryTracker tracker)
	{
		Pawn pawn = tracker.pawn;
		ThingOwner<Thing> innerContainer = tracker.innerContainer;
		if (innerContainer.Count == 0)
		{
			return default(ThingCount);
		}
		itemsToKeep.Clear();
		if (pawn.drugs?.CurrentPolicy != null)
		{
			DrugPolicy currentPolicy = pawn.drugs.CurrentPolicy;
			for (int i = 0; i < currentPolicy.Count; i++)
			{
				if (currentPolicy[i].takeToInventory > 0)
				{
					itemsToKeep.Add(new ThingDefCount(currentPolicy[i].drug, currentPolicy[i].takeToInventory));
				}
			}
		}
		Pawn_InventoryStockTracker inventoryStock = pawn.inventoryStock;
		if (inventoryStock != null && inventoryStock.stockEntries?.Count > 0)
		{
			foreach (InventoryStockEntry value in inventoryStock.stockEntries.Values)
			{
				itemsToKeep.Add(new ThingDefCount(value.thingDef, value.count));
			}
		}
		CarryPolicy carryPolicy = CarryPolicyUtility.CurrentPolicyFor(pawn);
		if (carryPolicy != null)
		{
			foreach (CarryPolicyEntry entry in carryPolicy.entries)
			{
				if (entry?.thing == null)
				{
					continue;
				}
				int target = CarryPolicyUtility.TargetCount(entry, pawn);
				if (target > 0)
				{
					MergeKeep(itemsToKeep, entry.thing, target);
				}
			}
		}
		foreach (Thing item in innerContainer)
		{
			int num = -1;
			for (int j = 0; j < itemsToKeep.Count; j++)
			{
				if (item.def == itemsToKeep[j].ThingDef)
				{
					num = j;
					break;
				}
			}
			if (pawn.IsColonist && item.def.IsNutritionGivingIngestible && !item.def.IsDrug && JobGiver_PackFood.IsGoodPackableFoodFor(item, pawn, checkMass: false))
			{
				float inventoryPackableFoodNutrition = JobGiver_PackFood.GetInventoryPackableFoodNutrition(pawn);
				float maxLevel = pawn.needs.food.MaxLevel;
				if (inventoryPackableFoodNutrition - item.GetStatValue(StatDefOf.Nutrition) * (float)item.stackCount <= maxLevel)
				{
					int k;
					for (k = 0; inventoryPackableFoodNutrition - item.GetStatValue(StatDefOf.Nutrition) * (float)k > maxLevel; k++)
					{
					}
					int foodKeep = item.stackCount - k;
					if (foodKeep > 0)
					{
						if (num >= 0)
						{
							if (foodKeep > itemsToKeep[num].Count)
							{
								itemsToKeep[num] = new ThingDefCount(item.def, foodKeep);
							}
						}
						else
						{
							itemsToKeep.Add(new ThingDefCount(item.def, foodKeep));
							num = itemsToKeep.Count - 1;
						}
					}
				}
			}
			if (num < 0)
			{
				return new ThingCount(item, item.stackCount);
			}
			if (item.stackCount > itemsToKeep[num].Count)
			{
				return new ThingCount(item, item.stackCount - itemsToKeep[num].Count);
			}
			itemsToKeep[num] = new ThingDefCount(itemsToKeep[num].ThingDef, itemsToKeep[num].Count - item.stackCount);
		}
		return default(ThingCount);
	}

	// 按 def 合并保留配额，取较大值（避免与成瘾品方案/打包食物的保留量重复累加）
	private static void MergeKeep(List<ThingDefCount> itemsToKeep, ThingDef def, int count)
	{
		for (int i = 0; i < itemsToKeep.Count; i++)
		{
			if (itemsToKeep[i].ThingDef == def)
			{
				if (count > itemsToKeep[i].Count)
				{
					itemsToKeep[i] = new ThingDefCount(def, count);
				}
				return;
			}
		}
		itemsToKeep.Add(new ThingDefCount(def, count));
	}
}

// 原版会把低偏好食物（生食/牛奶）在 2.5 天后丢地上、把成瘾品方案外的药品丢掉：
// 两条路径最终都走 Drop，所以只需拦这一个方法（原 ShouldKeepDrugInInventory postfix 属冗余，已删）。
// 方案内物品未超目标件数则不丢；超量只丢超出部分——原版 Drop 丢整叠，会造成「全丢→再搬回」振荡。
[HarmonyPatch(typeof(JobGiver_DropUnusedInventory), "Drop")]
public static class Patch_JobGiver_DropUnusedInventory_Drop
{
	public static bool Prefix(Pawn pawn, Thing thing)
	{
		CarryPolicyEntry entry = CarryPolicyUtility.CurrentPolicyFor(pawn)?.EntryFor(thing.def);
		if (entry == null || entry.count <= 0f)
		{
			return true;
		}
		int num = pawn.inventory.Count(thing.def) - CarryPolicyUtility.TargetCount(entry, pawn);
		if (num <= 0)
		{
			return false;
		}
		pawn.inventory.innerContainer.TryDrop(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near, Mathf.Min(num, thing.stackCount), out var _);
		return false;
	}
}
