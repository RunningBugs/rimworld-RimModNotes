using System;
using System.Linq;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod04Checks
{
    public const string PackageId = "RunningBugs.WeatherControlDeviceContZh";

    public static void Run(Map map)
    {
        if (LanguageDatabase.activeLanguage?.LegacyFolderName != "ChineseSimplified")
            throw new InvalidOperationException("Mod04 requires an isolated ChineseSimplified language case; active=" + LanguageDatabase.activeLanguage?.folderName + "; Prefs=" + Prefs.LangFolderName);
        var def = DefDatabase<ThingDef>.GetNamed("BCS_WeatherMachine");
        if (def.label != "天气控制上行信号装置" || def.description != "通过破解停用的气象改造卫星，实现对本地天气的控制。")
            throw new InvalidOperationException("Mod04 weather machine label/description injection failed");
        var usable = def.comps.OfType<CompProperties_Usable>().ToList();
        var expected = new[] { "晴", "雨", "暴风雨", "旱天雷", "雾雨", "雾", "大雪", "小雪" };
        if (usable.Count != expected.Length || !usable.Select(c => c.useLabel).SequenceEqual(expected))
            throw new InvalidOperationException("Mod04 weather command translations differ from the eight indexed targets");
        Log.Message("RW17 Mod04: all ten Chinese ThingDef/CompUsable translation fields passed");
    }
}
