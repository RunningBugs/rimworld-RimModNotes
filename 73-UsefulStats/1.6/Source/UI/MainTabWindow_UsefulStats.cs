using System;
using System.Collections.Generic;
using System.Linq;
using UsefulStats.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace UsefulStats
{
    public sealed class MainTabWindow_UsefulStats : MainTabWindow
    {
        private const string AllKindsKey = "__all";
        private const float RowHeight = 30f;
        private const float NameWidth = 360f;
        private const float WorkWidth = 80f;
        private const float MaterialWidth = 330f;
        private const float MaterialWorkWidth = 115f;
        private const float ValueWidth = 110f;
        private const float ValueWorkWidth = 115f;
        private const float ValueMaterialWidth = 115f;

        private sealed class DisplayRow
        {
            public CraftableStatRow Parent;
            public MaterialVariantStat Variant;
            public bool IsVariant => Variant != null;
        }

        private sealed class KindOption
        {
            public string Key;
            public string Label;
            public int Count;
        }

        private Vector2 scrollPosition = Vector2.zero;
        private List<CraftableStatRow> rows = new List<CraftableStatRow>();
        private List<CraftableStatRow> visibleParentRows = new List<CraftableStatRow>();
        private List<DisplayRow> displayRows = new List<DisplayRow>();
        private HashSet<string> expandedRows = new HashSet<string>();
        private bool dataDirty = true;
        private bool viewDirty = true;
        private bool showAvailableNow = true;
        private bool showFuture = true;
        private bool showAllOnly = true;
        private bool kindMenuOpen = false;
        private string search = string.Empty;
        private string materialSearch = string.Empty;
        private string kindSearch = string.Empty;
        private string selectedKindKey = AllKindsKey;
        private Rect kindButtonRect = Rect.zero;
        private Vector2 kindMenuScroll = Vector2.zero;
        private string sortKey = "valuePerWork";
        private bool sortDescending = true;

        public MainTabWindow_UsefulStats()
        {
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 RequestedTabSize => new Vector2(1250f, 720f);

        public override void PreOpen()
        {
            base.PreOpen();
            dataDirty = true;
            viewDirty = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (dataDirty)
            {
                rows = CraftableRowBuilder.BuildRows(Find.CurrentMap);
                dataDirty = false;
                viewDirty = true;
                if (selectedKindKey != AllKindsKey && !rows.Any(r => r.KindKey == selectedKindKey)) selectedKindKey = AllKindsKey;
            }

            if (viewDirty)
            {
                RebuildVisibleRows();
                viewDirty = false;
            }

            Text.Font = GameFont.Small;
            Rect filterRect = new Rect(inRect.x, inRect.y + 6f, inRect.width, 66f);
            DrawFilters(filterRect);
            float tableY = filterRect.y + 72f;
            Rect tableRect = new Rect(inRect.x, tableY, inRect.width, inRect.yMax - tableY);
            DrawTable(tableRect);
            if (kindMenuOpen) DrawKindMenuOverlay(inRect);
        }

        private void SelectKind(string key)
        {
            if (selectedKindKey == key) return;
            selectedKindKey = key;
            scrollPosition = Vector2.zero;
            kindMenuOpen = false;
            viewDirty = true;
        }

        private string SelectedKindLabel()
        {
            KindOption selected = BuildKindOptions().FirstOrDefault(k => k.Key == selectedKindKey);
            return selected == null ? "All" : selected.Label;
        }

        private List<KindOption> BuildKindOptions()
        {
            var options = new List<KindOption>
            {
                new KindOption { Key = AllKindsKey, Label = "All", Count = rows.Count }
            };
            options.AddRange(rows.GroupBy(r => r.KindKey).Select(g => new KindOption
            {
                Key = g.Key,
                Label = g.First().KindLabel,
                Count = g.Count()
            }).OrderBy(k => k.Label));
            return options;
        }

        private void DrawKindMenuOverlay(Rect inRect)
        {
            const float rowHeight = 30f;
            Rect menuRect = new Rect(kindButtonRect.x, kindButtonRect.yMax + 4f, 360f, 330f);
            if (menuRect.xMax > inRect.xMax) menuRect.x = inRect.xMax - menuRect.width;
            if (menuRect.yMax > inRect.yMax) menuRect.height = inRect.yMax - menuRect.y - 4f;

            Widgets.DrawMenuSection(menuRect);
            Rect searchRect = new Rect(menuRect.x + 8f, menuRect.y + 8f, menuRect.width - 16f, rowHeight);
            kindSearch = Widgets.TextField(searchRect, kindSearch ?? string.Empty);
            if (kindSearch.NullOrEmpty() && !Mouse.IsOver(searchRect))
            {
                GUI.color = new Color(0.65f, 0.65f, 0.65f);
                Rect placeholderRect = new Rect(searchRect.x + 6f, searchRect.y, searchRect.width - 12f, searchRect.height);
                Widgets.Label(placeholderRect, "Filter kind...");
                GUI.color = Color.white;
            }

            string needle = (kindSearch ?? string.Empty).Trim().ToLowerInvariant();
            List<KindOption> options = BuildKindOptions().Where(k => needle.NullOrEmpty() || Contains(k.Label, needle) || Contains(k.Key, needle)).ToList();
            Rect outRect = new Rect(menuRect.x + 8f, searchRect.yMax + 6f, menuRect.width - 16f, menuRect.height - rowHeight - 22f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Math.Max(outRect.height, options.Count * rowHeight));
            Widgets.BeginScrollView(outRect, ref kindMenuScroll, viewRect);
            for (int i = 0; i < options.Count; i++)
            {
                KindOption option = options[i];
                Rect rowRect = new Rect(0f, i * rowHeight, viewRect.width, rowHeight);
                if (option.Key == selectedKindKey) Widgets.DrawHighlightSelected(rowRect);
                else if (Mouse.IsOver(rowRect)) Widgets.DrawHighlight(rowRect);
                string label = option.Label + " (" + option.Count + ")";
                Widgets.Label(rowRect.ContractedBy(6f, 4f), label);
                if (Widgets.ButtonInvisible(rowRect))
                {
                    if (option.Key == selectedKindKey) kindMenuOpen = false;
                    else SelectKind(option.Key);
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawFilters(Rect rect)
        {
            float x = rect.x;
            bool oldNow = showAvailableNow;
            bool oldFuture = showFuture;
            bool oldAllOnly = showAllOnly;
            string oldSearch = search;
            string oldMaterialSearch = materialSearch;

            kindButtonRect = new Rect(x, rect.y, 220f, 30f);
            if (Widgets.ButtonText(kindButtonRect, "Kind: " + SelectedKindLabel()))
            {
                kindMenuOpen = !kindMenuOpen;
                kindMenuScroll = Vector2.zero;
            }
            x += 230f;
            Widgets.CheckboxLabeled(new Rect(x, rect.y, 125f, 30f), "Current", ref showAvailableNow);
            x += 130f;
            Widgets.CheckboxLabeled(new Rect(x, rect.y, 115f, 30f), "Future", ref showFuture);
            x += 130f;
            Widgets.CheckboxLabeled(new Rect(x, rect.y, 115f, 30f), "All-only", ref showAllOnly);
            x += 120f;
            Widgets.Label(new Rect(x, rect.y + 4f, 55f, 30f), "Search");
            x += 55f;
            search = Widgets.TextField(new Rect(x, rect.y, 260f, 30f), search ?? string.Empty);
            x += 270f;
            Widgets.Label(new Rect(x, rect.y + 4f, 65f, 30f), "Material");
            x += 65f;
            materialSearch = Widgets.TextField(new Rect(x, rect.y, 160f, 30f), materialSearch ?? string.Empty);
            x += 170f;
            if (Widgets.ButtonText(new Rect(x, rect.y, 88f, 30f), "Expand all"))
            {
                foreach (CraftableStatRow row in visibleParentRows.Where(r => r.HasExpandableMaterials)) expandedRows.Add(row.RowKey);
                viewDirty = true;
            }
            x += 94f;
            if (Widgets.ButtonText(new Rect(x, rect.y, 92f, 30f), "Collapse all"))
            {
                expandedRows.Clear();
                viewDirty = true;
            }
            x += 98f;
            if (Widgets.ButtonText(new Rect(x, rect.y, 80f, 30f), "Refresh"))
            {
                dataDirty = true;
            }

            Rect summaryRect = new Rect(rect.x, rect.y + 34f, rect.width, 26f);
            GUI.color = new Color(0.72f, 0.78f, 0.95f);
            Widgets.Label(summaryRect, visibleParentRows.Count + " / " + rows.Count + " items, " + displayRows.Count + " visible rows    Kinds are generated from loaded defs; click material rows to expand variants.");
            GUI.color = Color.white;

            if (oldNow != showAvailableNow || oldFuture != showFuture || oldAllOnly != showAllOnly || oldSearch != search || oldMaterialSearch != materialSearch)
            {
                scrollPosition = Vector2.zero;
                viewDirty = true;
            }
        }

        private void RebuildVisibleRows()
        {
            IEnumerable<CraftableStatRow> query = rows;
            if (selectedKindKey != AllKindsKey) query = query.Where(r => r.KindKey == selectedKindKey);
            query = query.Where(r => (r.AvailableNow && showAvailableNow) || (!r.AvailableNow && r.FutureAvailable && showFuture) || (!r.AvailableNow && !r.FutureAvailable && showAllOnly));
            if (!search.NullOrEmpty())
            {
                string s = search.Trim().ToLowerInvariant();
                query = query.Where(r => Contains(r.Label, s) || Contains(r.DefName, s) || Contains(r.Source, s) || Contains(r.KindLabel, s) || Contains(r.MaterialSummary, s) || Contains(r.UnlockInfo, s));
            }
            if (!materialSearch.NullOrEmpty())
            {
                string s = materialSearch.Trim().ToLowerInvariant();
                query = query.Where(r => Contains(r.MaterialSummary, s) || Contains(r.IngredientLabel, s) || r.MaterialVariants.Any(v => Contains(v.Label, s)));
            }
            visibleParentRows = Sort(query).ToList();
            displayRows = new List<DisplayRow>();
            foreach (CraftableStatRow row in visibleParentRows)
            {
                displayRows.Add(new DisplayRow { Parent = row });
                if (row.HasExpandableMaterials && expandedRows.Contains(row.RowKey))
                {
                    foreach (MaterialVariantStat variant in row.MaterialVariants.OrderBy(v => v.Label))
                    {
                        displayRows.Add(new DisplayRow { Parent = row, Variant = variant });
                    }
                }
            }
        }

        private IEnumerable<CraftableStatRow> Sort(IEnumerable<CraftableStatRow> query)
        {
            switch (sortKey)
            {
                case "label": return sortDescending ? query.OrderByDescending(r => r.Label).ThenBy(r => r.Source) : query.OrderBy(r => r.Label).ThenBy(r => r.Source);
                case "kind": return sortDescending ? query.OrderByDescending(r => r.KindLabel).ThenBy(r => r.Label) : query.OrderBy(r => r.KindLabel).ThenBy(r => r.Label);
                case "source": return sortDescending ? query.OrderByDescending(r => r.Source).ThenBy(r => r.Label) : query.OrderBy(r => r.Source).ThenBy(r => r.Label);
                case "work": return sortDescending ? query.OrderByDescending(r => r.WorkAmount).ThenBy(r => r.Label) : query.OrderBy(r => r.WorkAmount).ThenBy(r => r.Label);
                case "value": return sortDescending ? query.OrderByDescending(r => r.MarketValue).ThenBy(r => r.Label) : query.OrderBy(r => r.MarketValue).ThenBy(r => r.Label);
                case "valuePerWork": return sortDescending ? query.OrderByDescending(r => r.ValuePerWork).ThenBy(r => r.Label) : query.OrderBy(r => r.ValuePerWork).ThenBy(r => r.Label);
                case "material": return sortDescending ? query.OrderByDescending(r => r.IngredientPerWorkMax).ThenBy(r => r.Label) : query.OrderBy(r => r.IngredientPerWorkMin).ThenBy(r => r.Label);
                case "valueMaterial": return sortDescending ? query.OrderByDescending(r => r.ValuePerMaterial).ThenBy(r => r.Label) : query.OrderBy(r => r.ValuePerMaterial).ThenBy(r => r.Label);
                case "available": return sortDescending ? query.OrderByDescending(r => r.AvailableNow).ThenBy(r => r.Label) : query.OrderBy(r => r.AvailableNow).ThenBy(r => r.Label);
                default: return query.OrderBy(r => r.Label);
            }
        }

        private void DrawTable(Rect rect)
        {
            Widgets.DrawMenuSection(new Rect(rect.x, rect.y, rect.width, rect.height));
            Rect header = new Rect(rect.x + 4f, rect.y + 2f, rect.width - 20f, RowHeight);
            float x = header.x;
            DrawHeader(ref x, header.y, NameWidth, "Item", "label");
            DrawHeader(ref x, header.y, WorkWidth, "Work", "work");
            DrawHeader(ref x, header.y, MaterialWidth, "Material", "label");
            DrawHeader(ref x, header.y, ValueWidth, "Value", "value");
            DrawHeader(ref x, header.y, MaterialWorkWidth, "Mat/Work", "material");
            DrawHeader(ref x, header.y, ValueWorkWidth, "Val/Work", "valuePerWork");
            DrawHeader(ref x, header.y, ValueMaterialWidth, "Val/Mat", "valueMaterial");

            Rect outRect = new Rect(rect.x, rect.y + RowHeight + 2f, rect.width, rect.height - RowHeight - 6f);
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, Math.Max(outRect.height, displayRows.Count * RowHeight));
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            int first = Mathf.Max(0, Mathf.FloorToInt(scrollPosition.y / RowHeight) - 1);
            int last = Mathf.Min(displayRows.Count - 1, Mathf.CeilToInt((scrollPosition.y + outRect.height) / RowHeight) + 1);
            for (int i = first; i <= last; i++)
            {
                DrawRow(displayRows[i], i, viewRect.width);
            }

            Widgets.EndScrollView();
        }

        private void DrawHeader(ref float x, float y, float width, string label, string key)
        {
            Rect r = new Rect(x, y + 3f, width - 4f, RowHeight - 5f);
            if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);
            if (Widgets.ButtonInvisible(r))
            {
                if (sortKey == key) sortDescending = !sortDescending;
                else { sortKey = key; sortDescending = true; }
                viewDirty = true;
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(r.x + 2f, r.y, r.width - 20f, r.height), label);
            string arrow = sortKey == key ? (sortDescending ? "▼" : "▲") : "";
            if (!arrow.NullOrEmpty())
            {
                GUI.color = new Color(1f, 0.86f, 0.35f);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(r.ContractedBy(4f), arrow);
                GUI.color = Color.white;
            }
            Text.Anchor = TextAnchor.UpperLeft;
            x += width;
        }

        private void DrawRow(DisplayRow displayRow, int index, float width)
        {
            Rect rowRect = new Rect(0f, index * RowHeight, width, RowHeight);
            if (index % 2 == 0) Widgets.DrawLightHighlight(rowRect);
            if (displayRow.IsVariant) GUI.color = new Color(0.82f, 0.86f, 0.92f);
            if (Mouse.IsOver(rowRect)) Widgets.DrawHighlight(rowRect);

            if (displayRow.IsVariant) DrawVariantRow(displayRow.Parent, displayRow.Variant, rowRect.y);
            else DrawParentRow(displayRow.Parent, rowRect.y);
            GUI.color = Color.white;
        }

        private void DrawParentRow(CraftableStatRow row, float y)
        {
            float x = 4f;
            Rect toggleRect = new Rect(x + 2f, y + 5f, 20f, RowHeight - 6f);
            if (row.HasExpandableMaterials)
            {
                bool expanded = expandedRows.Contains(row.RowKey);
                if (Widgets.ButtonText(toggleRect, expanded ? "−" : "+"))
                {
                    if (expanded) expandedRows.Remove(row.RowKey);
                    else expandedRows.Add(row.RowKey);
                    viewDirty = true;
                }
            }
            DrawCell(ref x, y, NameWidth, (row.HasExpandableMaterials ? "    " : string.Empty) + row.Label, row.KindLabel + "\n" + row.DefName);
            DrawCell(ref x, y, WorkWidth, row.WorkAmount.ToStringWorkAmount());
            DrawCell(ref x, y, MaterialWidth, row.HasSingleIngredient ? row.MaterialSummary : "—", row.HasExpandableMaterials ? "Click + to expand material variants." : row.ExtraInfo);
            DrawCell(ref x, y, ValueWidth, row.MarketValue.ToStringMoney());
            DrawCell(ref x, y, MaterialWorkWidth, MaterialRatioText(row));
            DrawCell(ref x, y, ValueWorkWidth, CraftableEfficiencyMetrics.FormatRatio(row.ValuePerWork));
            DrawCell(ref x, y, ValueMaterialWidth, ValueMaterialText(row));
        }

        private void DrawVariantRow(CraftableStatRow parent, MaterialVariantStat variant, float y)
        {
            float x = 4f;
            string defaultMark = variant.IsDefault ? " (default)" : string.Empty;
            DrawCell(ref x, y, NameWidth, "    ↳ " + variant.Label + defaultMark, parent.Label);
            DrawCell(ref x, y, WorkWidth, variant.WorkAmount.ToStringWorkAmount());
            DrawCell(ref x, y, MaterialWidth, variant.Count.ToString("0.##") + " x " + variant.Label);
            DrawCell(ref x, y, ValueWidth, variant.MarketValue.ToStringMoney());
            DrawCell(ref x, y, MaterialWorkWidth, CraftableEfficiencyMetrics.FormatRatio(variant.MaterialPerWork));
            DrawCell(ref x, y, ValueWorkWidth, CraftableEfficiencyMetrics.FormatRatio(variant.ValuePerWork));
            DrawCell(ref x, y, ValueMaterialWidth, CraftableEfficiencyMetrics.FormatRatio(variant.ValuePerMaterial));
        }

        private static string MaterialRatioText(CraftableStatRow row)
        {
            if (!row.HasSingleIngredient) return "—";
            string min = CraftableEfficiencyMetrics.FormatRatio(row.IngredientPerWorkMin);
            string max = CraftableEfficiencyMetrics.FormatRatio(row.IngredientPerWorkMax);
            return min == max ? min : min + "-" + max;
        }

        private static string ValueMaterialText(CraftableStatRow row)
        {
            if (!row.HasSingleIngredient) return "—";
            return CraftableEfficiencyMetrics.FormatRatio(row.ValuePerMaterial);
        }

        private static string StatusText(CraftableStatRow row)
        {
            if (row.AvailableNow) return "Current";
            if (row.FutureAvailable) return row.UnlockInfo.NullOrEmpty() ? "Future" : row.UnlockInfo;
            return "All only";
        }

        private void DrawCell(ref float x, float y, float width, string text, string tooltip = null)
        {
            Rect r = new Rect(x + 2f, y + 5f, width - 4f, RowHeight - 6f);
            Widgets.Label(r, text ?? string.Empty);
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(r, tooltip);
            x += width;
        }

        private static bool Contains(string value, string needle)
        {
            return value != null && value.ToLowerInvariant().Contains(needle);
        }
    }
}
