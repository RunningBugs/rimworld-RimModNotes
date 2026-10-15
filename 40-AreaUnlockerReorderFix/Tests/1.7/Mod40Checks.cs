using System;
using System.Linq;
using HarmonyLib;
using Verse;
namespace RW17.Validation;
public static class Mod40Checks
{
    public const string PackageId = "RunningBugs.AreaUnlockerReorderFix";
    public static void Run(Map map)
    {
        var sort = AccessTools.Method(typeof(AreaManager), "SortAreas");
        var prefix = AccessTools.Method(AccessTools.TypeByName("AreaUnlockerReorderFix.AreaManager_Disable_Sort_Patch"), "Prefix");
        const string owner = "com.RunningBugs.AreaUnlockerReorderFix";
        if (!Harmony.GetPatchInfo(sort).Prefixes.Any(p => p.owner == owner && p.PatchMethod == prefix))
            throw new Exception("Mod40: exact real sort prefix not registered");
        var areas = map.areaManager.AllAreas;
        var original = areas.ToArray();
        var unsorted = original.OrderBy(a => a.ListPriority).ToArray();
        var harmony = new Harmony(owner);
        bool removed = false;
        try
        {
            areas.Clear(); areas.AddRange(unsorted);
            sort.Invoke(map.areaManager, null);
            if (!areas.SequenceEqual(unsorted)) throw new Exception("Mod40: patched native SortAreas still replaced the order");
            harmony.Unpatch(sort, prefix); removed = true;
            sort.Invoke(map.areaManager, null);
            if (areas.SequenceEqual(unsorted) || !areas.Select(a => a.ListPriority).SequenceEqual(areas.Select(a => a.ListPriority).OrderByDescending(x => x)))
                throw new Exception("Mod40: unpatched native control did not sort by actual priorities");
            Console.WriteLine("RW17_MOD40_NATIVE_SORT_SUPPRESSION_AND_CONTROL_OK; missing AreaUnlocker combination remains untested");
        }
        finally
        {
            areas.Clear(); areas.AddRange(original);
            if (removed) harmony.Patch(sort, prefix: new HarmonyMethod(prefix));
        }
    }
}
