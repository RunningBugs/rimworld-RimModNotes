using HarmonyLib;
using RimWorld;
using Verse;

namespace _CarryPolicy;

// 搬运前卸货：方案条目内且未超目标件数的物品不卸
[HarmonyPatch(typeof(Pawn_InventoryTracker), "FirstUnloadableThing", MethodType.Getter)]
public static class Patch_Pawn_InventoryTracker_FirstUnloadableThing
{
	public static void Postfix(Pawn_InventoryTracker __instance, ref ThingCount __result)
	{
		if (__result.Thing == null || !CarryPolicyUtility.ShouldKeepInInventory(__instance.pawn, __result.Thing.def))
		{
			return;
		}
		__result = default(ThingCount);
	}
}

// 原版会把低偏好食物（生食/牛奶）在 2.5 天后丢地上：方案要带的跳过
[HarmonyPatch(typeof(JobGiver_DropUnusedInventory), "Drop")]
public static class Patch_JobGiver_DropUnusedInventory_Drop
{
	public static bool Prefix(Pawn pawn, Thing thing)
	{
		return !CarryPolicyUtility.ShouldKeepInInventory(pawn, thing.def);
	}
}

// 原版会丢掉不在成瘾品方案里的药品：携带方案同样要保留（ItemPolicy 同款修复）
[HarmonyPatch(typeof(JobGiver_DropUnusedInventory), "ShouldKeepDrugInInventory")]
public static class Patch_JobGiver_DropUnusedInventory_ShouldKeepDrugInInventory
{
	public static bool Postfix(bool __result, Pawn pawn, Thing drug)
	{
		return __result || CarryPolicyUtility.ShouldKeepInInventory(pawn, drug.def);
	}
}
