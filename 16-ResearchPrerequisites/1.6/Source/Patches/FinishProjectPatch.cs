using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchPrerequisites
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    public static class FinishProjectPatch
    {
        public static void Postfix(ResearchProjectDef proj)
        {
            if (proj == null || Scribe.mode != LoadSaveMode.Inactive)
            {
                return;
            }
            ResearchQueueController.TryStartNext(proj.knowledgeCategory);
        }
    }
}
