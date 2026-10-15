using System;
using Verse;
namespace RW17.Validation;
public static class Mod13Checks
{
    public const string PackageId = "RunningBugs.SYR.Doormats.zh";
    public static void Run(Map map)
    {
        if (LanguageDatabase.activeLanguage?.LegacyFolderName != "ChineseSimplified")
            throw new InvalidOperationException("Mod13 requires an isolated ChineseSimplified language case; active=" + LanguageDatabase.activeLanguage?.folderName + "; Prefs=" + Prefs.LangFolderName);
        if (DefDatabase<ThingDef>.GetNamed("DoorMatLeather").label != "皮质防尘垫")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatLeather.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatLeather").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatLeather.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatBigLeather").label != "大号皮质防尘垫")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatBigLeather.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatBigLeather").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatBigLeather.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatRed").label != "布防尘垫（红色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatRed.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatRed").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatRed.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatOrange").label != "布防尘垫（橙色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatOrange.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatOrange").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatOrange.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatGreen").label != "布防尘垫（绿色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatGreen.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatGreen").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatGreen.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatTeal").label != "布防尘垫（蓝绿）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatTeal.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatTeal").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatTeal.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatBlue").label != "布防尘垫（蓝色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatBlue.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatBlue").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatBlue.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatBigRed").label != "大号布防尘垫（红色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatBigRed.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatBigRed").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatBigRed.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatBigOrange").label != "大号布防尘垫（橙色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatBigOrange.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatBigOrange").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatBigOrange.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatBigGreen").label != "大号布防尘垫（绿色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatBigGreen.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatBigGreen").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatBigGreen.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatBigTeal").label != "大号布防尘垫（蓝绿）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatBigTeal.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatBigTeal").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatBigTeal.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatBigBlue").label != "大号布防尘垫（蓝色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatBigBlue.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatBigBlue").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatBigBlue.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatLinkedLeather").label != "皮质拼接防尘垫")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatLinkedLeather.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatLinkedLeather").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatLinkedLeather.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatLinkedRed").label != "拼接布防尘垫（红色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatLinkedRed.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatLinkedRed").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatLinkedRed.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatLinkedOrange").label != "拼接布防尘垫（橙色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatLinkedOrange.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatLinkedOrange").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatLinkedOrange.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatLinkedGreen").label != "拼接布防尘垫（绿色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatLinkedGreen.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatLinkedGreen").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatLinkedGreen.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatLinkedTeal").label != "拼接布防尘垫（蓝绿）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatLinkedTeal.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatLinkedTeal").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatLinkedTeal.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatLinkedBlue").label != "拼接布防尘垫（蓝色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatLinkedBlue.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatLinkedBlue").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatLinkedBlue.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatMediumLeather").label != "中号皮质防尘垫")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatMediumLeather.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatMediumLeather").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatMediumLeather.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatMediumRed").label != "中号布防尘垫（红色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatMediumRed.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatMediumRed").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatMediumRed.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatMediumOrange").label != "中号布防尘垫（橙色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatMediumOrange.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatMediumOrange").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatMediumOrange.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatMediumGreen").label != "中号布防尘垫（绿色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatMediumGreen.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatMediumGreen").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatMediumGreen.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatMediumTeal").label != "中号布防尘垫（蓝绿）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatMediumTeal.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatMediumTeal").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatMediumTeal.description");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatMediumBlue").label != "中号布防尘垫（蓝色）")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatMediumBlue.label");
        if (DefDatabase<ThingDef>.GetNamed("DoorMatMediumBlue").description != "一个可以清理鞋上的泥的防尘垫。")
            throw new InvalidOperationException("Mod13 injection failed: DoorMatMediumBlue.description");
        if ("SyrDoorMatsSettingsCategory".Translate().ToString() != "防尘垫")
            throw new InvalidOperationException("Mod13 keyed translation failed: SyrDoorMatsSettingsCategory");
        if ("SyrDoorMatsSlowdown".Translate().ToString() != "减速强度（默认值：40）")
            throw new InvalidOperationException("Mod13 keyed translation failed: SyrDoorMatsSlowdown");
        if ("SyrDoorMatsSlowdownTooltip".Translate().ToString() != "触发小人在防尘垫上减速从而清理鞋上的泥，不影响寻路")
            throw new InvalidOperationException("Mod13 keyed translation failed: SyrDoorMatsSlowdownTooltip");
        if ("SyrDoorMatsDefaultSettings".Translate().ToString() != "恢复默认设置")
            throw new InvalidOperationException("Mod13 keyed translation failed: SyrDoorMatsDefaultSettings");
        if ("SyrDoorMatsDefaultSettingsTooltip".Translate().ToString() != "把设置恢复为默认值")
            throw new InvalidOperationException("Mod13 keyed translation failed: SyrDoorMatsDefaultSettingsTooltip");
        Log.Message("RW17 Mod13: all 48 DefInjected and 5 keyed Chinese translations passed");
    }
}
