using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using Verse;
namespace RW17.Validation;
public static class Mod23Checks
{
    public const string PackageId = "RunningBugs.MoreScenarioSearchbars";
    public static void Run(Map map)
    {
        var targetType = typeof(ScenPart).Assembly.GetType("RimWorld.ScenPart_CreateIncident", true);
        var target = AccessTools.Method(targetType, "DoEditInterface", new[] { typeof(Listing_ScenEdit) });
        var patches = Harmony.GetPatchInfo(target)?.Postfixes;
        var total = patches?.Count(p => p.PatchMethod.DeclaringType?.FullName == "ScenarioSearchbar.ScenPart_CreateIncident_Patch" || p.PatchMethod.DeclaringType?.FullName == "MoreScenarioSearchbars.ScenPart_CreateIncident_Patch") ?? 0;
        if (total != 1) throw new Exception("Mod23: duplicate or missing incident searchbar postfix: " + total);
        var tools = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "ScenarioSearchbar").GetType("ScenarioSearchbar.Tools", true);
        var dict = (IDictionary)AccessTools.Field(tools, "searchStringDict").GetValue(null);
        var part = (ScenPart)Activator.CreateInstance(targetType);
        part.def = DefDatabase<ScenPartDef>.AllDefs.Single(d => d.scenPartClass == targetType);
        var filter = tools.GetMethod("Filter").MakeGenericMethod(typeof(IncidentDef));
        var choices = new[] { IncidentDefOf.Eclipse, IncidentDefOf.SolarFlare, IncidentDefOf.ToxicFallout };
        try
        {
            dict[part] = "";
            var all = ((IEnumerable<IncidentDef>)filter.Invoke(null,new object[] { choices, part })).ToList();
            if (all.Count != choices.Length) throw new Exception("Mod23: empty search discarded incidents");
            dict[part] = "^" + Regex.Escape(IncidentDefOf.Eclipse.label) + "$";
            var selected = ((IEnumerable<IncidentDef>)filter.Invoke(null,new object[] { choices, part })).ToList();
            if (!selected.Contains(IncidentDefOf.Eclipse) || selected.Any(d => d.label != IncidentDefOf.Eclipse.label)) throw new Exception("Mod23: actual search filter did not constrain incidents");
            dict[part] = "RW17-IMPOSSIBLE-INCIDENT-LABEL";
            if (((IEnumerable<IncidentDef>)filter.Invoke(null,new object[] { choices, part })).Any()) throw new Exception("Mod23: absent search kept an incident");
        }
        finally { dict.Remove(part); }
        Console.WriteLine("RW17_MOD23_SINGLE_INCIDENT_SEARCH_PATCH_AND_REAL_FILTER_OK; rendered searchbar interaction remains pending");
    }
}
