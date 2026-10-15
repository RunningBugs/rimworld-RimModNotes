using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace KillingReward
{
    public static class ResearchReward
    {
        public static List<ResearchProjectDef> Available()
        {
            return DefDatabase<ResearchProjectDef>.AllDefsListForReading
                .Where(p => !p.IsFinished && !p.IsHidden && p.CanStartNow)
                .OrderBy(p => p.LabelCap.ToString())
                .ToList();
        }

        public static void Complete(ResearchProjectDef project)
        {
            // 原版 FinishProject 会自行写满进度、补 techprint、处理解锁与完成信件。
            Find.ResearchManager.FinishProject(project, doCompletionDialog: false, researcher: null, doCompletionLetter: true);
        }
    }
}
