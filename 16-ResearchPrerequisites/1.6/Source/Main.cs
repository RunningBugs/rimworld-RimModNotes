using HarmonyLib;
using Verse;

namespace ResearchPrerequisites
{
    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            new Harmony("com.runningbugs.researchprerequisites").PatchAll();
        }
    }
}
