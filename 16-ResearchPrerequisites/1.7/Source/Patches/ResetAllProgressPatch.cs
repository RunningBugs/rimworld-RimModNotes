using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchPrerequisites
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.ResetAllProgress))]
    public static class ResetAllProgressPatch
    {
        public static void Postfix()
        {
            if (Scribe.mode != LoadSaveMode.Inactive)
            {
                return;
            }
            ResearchQueue.Instance?.ClearAllQueues();
        }
    }
}
