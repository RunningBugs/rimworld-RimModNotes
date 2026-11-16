using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchPrerequisites
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.StopProject))]
    public static class StopProjectPatch
    {
        public static void Prefix(ResearchProjectDef proj, out bool __state)
        {
            __state = proj != null && Scribe.mode == LoadSaveMode.Inactive && Find.ResearchManager.IsCurrentProject(proj);
        }

        public static void Postfix(ResearchProjectDef proj, bool __state)
        {
            if (!__state)
            {
                return;
            }
            ResearchCategory category = ResearchCategories.Of(proj);
            ResearchQueue.Instance?.ClearQueue(category);
        }
    }
}
