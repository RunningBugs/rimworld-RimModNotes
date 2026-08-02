using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchPrerequisites
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.StopProject))]
    public static class StopProjectPatch
    {
        public static void Postfix(ResearchProjectDef proj)
        {
            if (proj == null)
            {
                return;
            }
            ResearchQueue.Instance?.ClearQueue(ResearchCategories.Of(proj));
        }
    }
}
