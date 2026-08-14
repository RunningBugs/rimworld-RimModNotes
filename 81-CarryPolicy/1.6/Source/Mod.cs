using HarmonyLib;
using RimWorld;
using Verse;

namespace _CarryPolicy;

[DefOf]
public static class CarryPolicyDefOf
{
	static CarryPolicyDefOf()
	{
		DefOfHelper.EnsureInitializedInCtor(typeof(CarryPolicyDefOf));
	}

	public static JobDef CarryPolicyTakeFromDispenser;
}

[StaticConstructorOnStartup]
public static class CarryPolicyMod
{
	public const string Id = "com.RunningBugs.CarryPolicy";

	static CarryPolicyMod()
	{
		new Harmony(Id).PatchAll();
	}
}
