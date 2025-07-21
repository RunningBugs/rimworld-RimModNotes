using RimWorld;
using Verse;
using HarmonyLib;

namespace Template;


[StaticConstructorOnStartup]
public static class Start
{
    static Start()
    {
        Harmony harmony = new Harmony("com.RunningBugs.Test");
        harmony.PatchAll();

        Log.Message("Test mod loaded successfully!");
    }
}


[HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.BlocksConstruction))]
public static class GenConstruct_BlocksConstruction_Prefix
{
    static void Postfix(Thing constructible, Thing t, ref bool __result)
    {
        // Skip pawn blocking entirely
        if (t is Pawn)
        {
            __result = false;
        }
    }
}
