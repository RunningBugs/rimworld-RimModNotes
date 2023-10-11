using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;


namespace _RecipeBook
{
    [HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls", MethodType.Normal)]
    public class ToggleIconPatcher
    {
        [HarmonyPostfix]
        public static void AddIcon(WidgetRow row, bool worldView)
        {
            if (worldView) return;
            bool flag = Find.WindowStack.IsOpen(typeof(RecipeBookWindow));
            row.ToggleableIcon(ref flag, ContentFinder<Texture2D>.Get("UI/recipe_book", true), "OpenRecipeBook".Translate(), SoundDefOf.Mouseover_ButtonToggle, (string)null);
            if (flag != Find.WindowStack.IsOpen(typeof(RecipeBookWindow)))
            {
                if (!Find.WindowStack.IsOpen(typeof(RecipeBookWindow)))
                {
                    RecipeBookWindow.DrawWindow();
                }
                else
                {
                    Find.WindowStack.TryRemove(typeof(RecipeBookWindow), false);
                }
            }
        }
    }

    public class RecipeBookWindow : Window
    {
        private QuickSearchWidget searchWidget = new QuickSearchWidget();
        private Vector2 scrollPos;
        private Vector2 scrollPos2;
        private List<ThingDef> defsToShow = new List<ThingDef>();

        public override Vector2 InitialSize => new Vector2(500f, 632f);


        public RecipeBookWindow()
        {
            draggable = true;
        }

        public static void DrawWindow()
        {
            Find.WindowStack.Add((Window)(object)new RecipeBookWindow());
        }

        private void FilterOnChange()
        {
            var searchText = searchWidget.filter.Text.Trim();
            if (searchText == "")
            {
                defsToShow.Clear();
                return;
            }
            //defsToShow = ItemPolicyUtility.Search(searchText);
        }

        public override void DoWindowContents(Rect inRect)
        {
            var view = new Listing_Standard(GameFont.Small);
            view.maxOneColumn = true;
            view.Begin(inRect);
            Text.Anchor = TextAnchor.MiddleLeft;

            var buttonLine = view.GetRect(Text.LineHeight);
            var margin = 0.99f;
            if (Widgets.ButtonText(buttonLine.RightHalf().RightPart(margin), "CloseButton".Translate()))
            {
                Find.WindowStack.TryRemove(typeof(RecipeBookWindow));
            }
            searchWidget.OnGUI(buttonLine.LeftHalf().LeftPart(margin), FilterOnChange);
            view.Gap();
            view.GapLine();
            view.Gap();
            //==============================================
            var policy = ItemPolicyUtility.GetPawnPolicy(pawn);
            var labelHeight = Text.LineHeight * 1.5f;

            var searchResultLine = view.GetRect(labelHeight * 5);
            var searchResultInnerRect = new Rect(0f, 0f, searchResultLine.width - 16f, labelHeight * defsToShow.Count);
            Widgets.BeginScrollView(searchResultLine, ref scrollPos, searchResultInnerRect);
            var searchResultView = new Listing_Standard();
            searchResultView.maxOneColumn = true;
            searchResultView.Begin(searchResultInnerRect);
            foreach (var def in defsToShow)
            {
                var labelWithIconBox = searchResultView.GetRect(labelHeight);
                Widgets.DefLabelWithIcon(labelWithIconBox, def);
                if (Widgets.ButtonInvisible(labelWithIconBox))
                {
                    //ItemPolicyUtility.SetItemPolicyEntry(pawn, def, 1);
                }
            }
            Widgets.EndScrollView();
            searchResultView.End();
            view.Gap();
            view.GapLine();
            view.Gap();
            //==============================================

            var currentSettingsLine = view.GetRect(labelHeight * 10);
            var currentSettingsInnerRect = new Rect(0f, 0f, currentSettingsLine.width - 16f, labelHeight * (policy.data.Count));
            Widgets.BeginScrollView(currentSettingsLine, ref scrollPos2, currentSettingsInnerRect);
            var currentSettingsView = new Listing_Standard();
            currentSettingsView.maxOneColumn = true;
            currentSettingsView.Begin(currentSettingsInnerRect);

            List<ThingDef> defsToChange = new List<ThingDef>();
            List<int> changeToValues = new List<int>();
            List<ThingDef> defsToRemove = new List<ThingDef>();

            foreach (var (def, count) in policy.data)
            {
                var labelWithIconBox = currentSettingsView.GetRect(labelHeight);
                Widgets.DefLabelWithIcon(labelWithIconBox.LeftHalf(), def);
                var num = count;
                string editBuffer = num.ToString();
                var rightBox = labelWithIconBox.RightHalf();
                var textFieldBox = rightBox.LeftPartPixels(rightBox.width - labelHeight);
                var iconBox = textFieldBox.RightBoxPixels(labelHeight);
                Widgets.TextFieldNumericLabeled<int>(textFieldBox, "TakeToInventoryColumnLabel".Translate(), ref num, ref editBuffer);
                if (num != count)
                {
                    defsToChange.Add(def);
                    changeToValues.Add(num);
                }
                if (Widgets.ButtonImage(iconBox.ShrinkPixels(4f), TexButton.CloseXSmall))
                {
                    defsToRemove.Add(def);
                }
            }

            Widgets.EndScrollView();

            for (var i = 0; i < defsToChange.Count; ++i)
            {
                ItemPolicyUtility.SetItemPolicyEntry(pawn, defsToChange[i], changeToValues[i]);
            }

            foreach (var def in defsToRemove)
            {
                ItemPolicyUtility.RemoveItemPolicyEntry(pawn, def);
            }

            currentSettingsView.End();
            //==============================================

            GenUI.ResetLabelAlign();
            view.End();
        }
    }
}
