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
            List<ResearchProjectDef> list = queue.QueueFor(project);
            if (list.Count == 0)
            {
                return;
            }

            float y = rect.y + __result;
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, Text.LineHeight), "ClearResearchQueue".Translate()))
            {
                queue.ClearQueue(project.knowledgeCategory);
                __result = y - rect.y + Text.LineHeight;
                return;
            }
            y += Text.LineHeight;
            Widgets.Label(new Rect(rect.x, y, rect.width, Text.LineHeight), "CurrentResearchQueue".Translate());
            y += Text.LineHeight;
            foreach (ResearchProjectDef p in list)
            {
                Widgets.Label(new Rect(rect.x + 10f, y, rect.width - 10f, Text.LineHeight), p.LabelCap);
                y += Text.LineHeight;
            }
            __result = y - rect.y;
        }
    }
}
