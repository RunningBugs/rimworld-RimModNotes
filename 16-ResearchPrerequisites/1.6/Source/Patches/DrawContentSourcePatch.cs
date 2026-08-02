using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ResearchPrerequisites
{
    [HarmonyPatch(typeof(MainTabWindow_Research), "DrawContentSource")]
    public static class DrawContentSourcePatch
    {
        public static void Postfix(Rect rect, ResearchProjectDef project, ref float __result)
        {
            if (project == null)
            {
                return;
            }
            ResearchQueue queue = ResearchQueue.Instance;
            if (queue == null)
            {
                return;
            }
            List<ResearchProjectDef> list = queue.QueueFor(ResearchCategories.Of(project));
            ResearchProjectDef current = Find.ResearchManager.GetProject(project.knowledgeCategory);
            if (list.Count == 0 && current == null)
            {
                return;
            }

            float y = rect.y + __result;
            if ((list.Count > 0 || current != null) && Widgets.ButtonText(new Rect(rect.x, y, rect.width, Text.LineHeight), "ClearResearchQueue".Translate()))
            {
                // 清空队列并停止当前研究(StopProject 会经 StopProjectPatch 再次清队,为无害 no-op)
                queue.ClearQueue(ResearchCategories.Of(project));
                if (current != null)
                {
                    Find.ResearchManager.StopProject(current);
                }
                __result = y - rect.y + Text.LineHeight;
                return;
            }
            y += Text.LineHeight;
            Widgets.Label(new Rect(rect.x, y, rect.width, Text.LineHeight), "CurrentResearchQueue".Translate());
            y += Text.LineHeight;
            // 队列包含当前研究(队首):单段绘制,进行中的项加粗 + 箭头标记。
            foreach (ResearchProjectDef p in list)
            {
                if (p == null)
                {
                    continue;
                }
                string text = p == current
                    ? "<b>→ " + p.LabelCap + " (" + "InProgress".Translate() + ")</b>"
                    : p.LabelCap;
                Widgets.Label(new Rect(rect.x + 10f, y, rect.width - 10f, Text.LineHeight), text);
                y += Text.LineHeight;
            }
            __result = y - rect.y;
        }
    }
}
