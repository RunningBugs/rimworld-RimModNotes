using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SimpleGUI
{
    class SampleWindow : Window
    {
        QuickSearchWidget searchWidget = new QuickSearchWidget();

        private float y = 0f;

        private const float lineHeight = 30f;
        private const float scrollbarWidth = 16f;

        private Vector2 scrollPosition = Vector2.zero;
        private Dictionary<ThingDef, Rect> selectedDefs = new Dictionary<ThingDef, Rect>();

        private void GapVertical(float gap = lineHeight)
        {
            y += gap;
        }

        private void GetRow(out Rect top, out Rect bot, Rect inRect, float height = lineHeight)
        {
            top = inRect.TopPartPixels(height);
            bot = inRect.BottomPartPixels(inRect.height - height);
            y += height;
        }

        public SampleWindow() : base()
        {
            draggable = true;
            resizeable = true;
        }
        public override void DoWindowContents(Rect inRect)
        {
            GetRow(out Rect top, out Rect bot, inRect);
            searchWidget.OnGUI(top);

            GetRow(out top, out bot, bot);
            Widgets.Label(top, searchWidget.filter.Text);

            GetRow(out top, out bot, bot, 10 * lineHeight);
            Rect viewRect = top;
            Rect scrollRect = viewRect;
            List<ThingDef> defs = new List<ThingDef>();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                // if (!searchWidget.filter.Text.Trim().NullOrEmpty() && searchWidget.filter.Matches(def.label))
                if (searchWidget.filter.Active && searchWidget.filter.Matches(def.label))
                {
                    defs.Add(def);
                }
                if (defs.Count > 50)
                {
                    break;
                }
            }
            scrollRect.width -= scrollbarWidth;
            var textLineHeight = Text.LineHeight + 2f;
            scrollRect.height = defs.Count * textLineHeight;
            Widgets.BeginScrollView(viewRect, ref scrollPosition, scrollRect, true);
            foreach (ThingDef def in defs)
            {
                GetRow(out top, out scrollRect, scrollRect, textLineHeight);
                Widgets.DefLabelWithIcon(top, def);

                if (Widgets.ButtonInvisible(top))
                {
                    if (selectedDefs.ContainsKey(def))
                    {
                        selectedDefs.Remove(def);
                    }
                    else
                    {
                        selectedDefs.Add(def, top);
                    }
                }
            }

            foreach (KeyValuePair<ThingDef, Rect> kvp in selectedDefs)
            {
                Widgets.DrawHighlight(kvp.Value);
            }
            Widgets.EndScrollView();

            GetRow(out top, out bot, bot);
            Widgets.Label(top, "Hello World!");
        }
    }
}