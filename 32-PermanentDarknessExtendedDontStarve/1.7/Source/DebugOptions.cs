using Verse;
using RimWorld;
using LudeonTK;

namespace PDEDontStarve;

public static class DebugOptions
{
    [DebugAction("PDEDontStarve", "Dark the map", actionType = DebugActionType.Action)]
    public static void DarkTheMap()
    {
        Find.CurrentMap.gameConditionManager.SetTargetBrightness(0f);
    }
}