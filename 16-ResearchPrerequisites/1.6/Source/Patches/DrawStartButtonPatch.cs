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
            // 原版 startButRect 仅占左侧栏中间约 50%+20px，双按钮直接切分会导致文字严重挤压。
            // 当其仍处于原版居中位置(x >= 40)时，扩展到整行可用宽度再左右分栏。
            Rect rowRect = startButRect;
            if (startButRect.x >= 40f)
            {
                float panelWidth = Mathf.Max(startButRect.xMax, startButRect.x * 4f);
                rowRect = new Rect(4f, startButRect.y, panelWidth - 8f, startButRect.height);
            }
            const float gap = 6f;
            float startWidth = Mathf.Floor((rowRect.width - gap) * 0.52f);
            startButRect = new Rect(rowRect.x, rowRect.y, startWidth, rowRect.height);
            queueButtonRect = new Rect(startButRect.xMax + gap, rowRect.y, rowRect.xMax - (startButRect.xMax + gap), rowRect.height);
        }

        public static void Postfix(MainTabWindow_Research __instance)
        {
            ResearchProjectDef project = SelectedProject(__instance);
            if (!ShouldShowQueueButton(project))
            {
                return;
            }
            ResearchQueue queue = ResearchQueue.Instance;
            bool inQueue = queue != null && queue.Contains(project);
            bool jump = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            string label = jump
                ? "ResearchJumpQueueFront".Translate()
                : (inQueue ? "ResearchRemoveFromQueue".Translate() : "ResearchAddToQueue".Translate());
            string tip = inQueue ? "ResearchQueueButtonInQueueTip".Translate() : "ResearchQueueButtonTip".Translate();
            TooltipHandler.TipRegion(queueButtonRect, tip);
            if (Widgets.ButtonText(queueButtonRect, label))
            {
                if (queue == null)
                {
                    return;
                }
                if (jump)
                {
                    ResearchQueueController.JumpChainToFrontAndStart(project);
                }
                else if (inQueue)
                {
                    ResearchQueueController.RemoveFromQueueAndAdvance(project);
                }
                else
                {
                    ResearchQueueController.EnqueueAndAdvance(project);
                }
            }
        }
    }
}
