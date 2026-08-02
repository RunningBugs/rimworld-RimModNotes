using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ResearchPrerequisites
{
    [HarmonyPatch(typeof(MainTabWindow_Research), "DrawStartButton")]
    public static class DrawStartButtonPatch
    {
        private static readonly AccessTools.FieldRef<MainTabWindow_Research, ResearchProjectDef> SelectedProject =
            AccessTools.FieldRefAccess<MainTabWindow_Research, ResearchProjectDef>("selectedProject");

        private static Rect queueButtonRect;

        private static bool ShouldShowQueueButton(ResearchProjectDef project)
        {
            return project != null && !project.IsFinished && !project.IsHidden
                && !Find.ResearchManager.IsCurrentProject(project);
        }

        public static void Prefix(ref Rect startButRect, MainTabWindow_Research __instance)
        {
            if (!ShouldShowQueueButton(SelectedProject(__instance)))
            {
                return;
            }
            queueButtonRect = startButRect.RightPart(0.4f);
            startButRect = startButRect.LeftPart(0.58f);
        }

        public static void Postfix(MainTabWindow_Research __instance)
        {
            ResearchProjectDef project = SelectedProject(__instance);
            if (!ShouldShowQueueButton(project))
            {
                return;
            }
            bool jump = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            string label = jump ? "ResearchJumpQueueFront".Translate() : "ResearchAddToQueue".Translate();
            TooltipHandler.TipRegion(queueButtonRect, "ResearchQueueButtonTip".Translate());
            if (Widgets.ButtonText(queueButtonRect, label))
            {
                ResearchQueue queue = ResearchQueue.Instance;
                if (queue == null)
                {
                    return;
                }
                if (!jump)
                {
                    queue.Enqueue(project);
                    ResearchQueueController.AdvanceCategory(ResearchCategories.Of(project));
                }
                else
                {
                    // 可立即开始的项目其前置链就是自身,统一走链式插队
                    ResearchQueueController.JumpChainToFrontAndStart(project);
                }
            }
        }
    }
}
