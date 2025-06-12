
using HarmonyLib;
using Verse;

namespace AreaUnlockerReorderFix;

[StaticConstructorOnStartup]
public static class Start
{
    static Start()
    {
        Harmony harmony = new("com.RunningBugs.AreaUnlockerReorderFix");
        harmony.PatchAll();
    }
}

[HarmonyPatch(typeof(AreaManager), "SortAreas")]
public static class AreaManager_Disable_Sort_Patch
{
    public static bool Prefix()
    {
        Log.Warning($"AreaUnlockerReorderFix: SortAreas() was called, but it has been disabled.");
        return false;
    }
}
