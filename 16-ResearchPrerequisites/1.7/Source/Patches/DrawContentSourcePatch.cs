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
            ResearchCategory category = ResearchCategories.Of(project);
            if (!ResearchCategories.IsActive(category))
            {
                return;
            }
            ResearchQueue queue = ResearchQueue.Instance;
            if (queue == null)
            {
                return;
            }
            queue.SanitizeQueue(category);
            ResearchProjectDef current = Find.ResearchManager.GetProject(ResearchCategories.ToVanillaDef(category));
            List<ResearchProjectDef> list = queue.QueueFor(category);
            if (current != null && (list.Count == 0 || list[0] != current))
            {
                queue.MoveToHead(current);
            }
            if (list.Count == 0 && current == null)
            {
                return;
            }

            float y = rect.y + __result + 6f;
            const float clearButtonHeight = 24f;
            if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, clearButtonHeight), "ClearResearchQueue".Translate()))
            {
                queue.ClearQueue(category);
                if (current != null)
                {
                    Find.ResearchManager.StopProject(current);
                }
                __result = y - rect.y + clearButtonHeight + 4f;
                return;
            }
            y += clearButtonHeight + 4f;

            KnowledgeCategoryDef vanillaCat = ResearchCategories.ToVanillaDef(category);
            string header = (ModsConfig.AnomalyActive && vanillaCat != null)
                ? "CurrentResearchQueueCategory".Translate(vanillaCat.LabelCap).ToString()
                : "CurrentResearchQueue".Translate().ToString();
            float headerHeight = Mathf.Max(Text.LineHeight, Text.CalcHeight(header, rect.width));
            Widgets.Label(new Rect(rect.x, y, rect.width, headerHeight), header);
            y += headerHeight + 2f;

            ResearchProjectDef toRemove = null;
            for (int i = 0; i < list.Count; i++)
            {
                ResearchProjectDef p = list[i];
                if (p == null)
                {
                    continue;
                }
                bool isCurrent = p == current;
                string text = isCurrent
                    ? "<b>→ " + p.LabelCap + " (" + "InProgress".Translate() + ")</b>"
                    : (i + 1) + ". " + p.LabelCap;
                float textX = rect.x + 6f;
                float removeReserve = isCurrent ? 0f : 22f;
                float textWidth = Mathf.Max(40f, rect.width - 6f - removeReserve);
                float rowHeight = Mathf.Max(Text.LineHeight, Text.CalcHeight(text, textWidth));
                Widgets.Label(new Rect(textX, y, textWidth, rowHeight), text);
                if (!isCurrent)
                {
                    Rect removeRect = new Rect(rect.xMax - 20f, y + (rowHeight - 18f) / 2f, 18f, 18f);
                    TooltipHandler.TipRegion(removeRect, "ResearchQueueRemoveTip".Translate());
                    if (Widgets.ButtonImage(removeRect, TexButton.CloseXSmall))
                    {
                        toRemove = p;
                    }
                }
                y += rowHeight;

                if (i == 0 && current == null && !p.CanStartNow)
                {
                    string waitText = "ResearchQueueWaitingCondition".Translate(ResearchQueueController.GetWaitingReason(p));
                    float waitX = rect.x + 14f;
                    float waitWidth = Mathf.Max(40f, rect.width - 14f);
                    float waitHeight = Mathf.Max(Text.LineHeight, Text.CalcHeight(waitText, waitWidth));
                    using (new TextBlock(ColoredText.SubtleGrayColor))
                    {
                        Widgets.Label(new Rect(waitX, y, waitWidth, waitHeight), waitText);
                    }
                    y += waitHeight;
                }
            }
            if (toRemove != null)
            {
                ResearchQueueController.RemoveFromQueueAndAdvance(toRemove);
            }
            __result = y - rect.y;
        }
    }
}
