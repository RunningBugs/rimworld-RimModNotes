using RimWorld;
using Verse;
using Verse.AI;
using RimWorld.Planet;

using System.Collections.Generic;
using System.Linq;

using UnityEngine;


namespace _ItemPolicy
{
    [DefOf]
    public static class ItemsMainButtonDefOf
    {
        public static MainButtonDef Items;

        static ItemsMainButtonDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MainButtonDefOf));
        }
    }

    [DefOf]
    public static class ItemsPawnTableDefOf
    {
        public static PawnTableDef Items;

        static ItemsPawnTableDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ItemsPawnTableDefOf));
        }
    }

    public class MainTabWindow_Items : MainTabWindow_PawnTable
    {
        protected override PawnTableDef PawnTableDef => ItemsPawnTableDefOf.Items;

        protected override IEnumerable<Pawn> Pawns => PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonists.Where((Pawn pawn) => !pawn.DevelopmentalStage.Baby());
    }

    public class PawnColumnWorker_ItemPolicy : PawnColumnWorker
    {
        private const int TopAreaHeight = 65;

        public const int ManageDrugPoliciesButtonHeight = 32;

        public override void DoHeader(Rect rect, PawnTable table)
        {
            base.DoHeader(rect, table);
        }

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if (Widgets.ButtonText(rect, "ManageItemPolicies".Translate()))
            {
                Find.WindowStack.TryRemove(typeof(MainTabWindow_Items));
                Find.WindowStack.Add(new Dialog_ItemPolicy(pawn));
            }
        }

        public override int GetMinWidth(PawnTable table)
        {
            return Mathf.Max(base.GetMinWidth(table), Mathf.CeilToInt(194f));
        }

        public override int GetOptimalWidth(PawnTable table)
        {
            return Mathf.Clamp(Mathf.CeilToInt(251f), GetMinWidth(table), GetMaxWidth(table));
        }

        public override int GetMinHeaderHeight(PawnTable table)
        {
            return Mathf.Max(base.GetMinHeaderHeight(table), 65);
        }

        public override int Compare(Pawn a, Pawn b)
        {
            return string.Compare(a.Name.ToStringShort, b.Name.ToStringShort);
        }
    }

    public class Dialog_ItemPolicy : Window
    {
        private Pawn pawn;

        private QuickSearchWidget searchWidget = new QuickSearchWidget();
        private Vector2 scrollPos;
        private Vector2 scrollPos2;
        private List<ThingDef> defsToShow = new List<ThingDef>();

        public Dialog_ItemPolicy(Pawn pawn)
        {
            this.pawn = pawn;
        }

        private void LogDictKeys<Key, _>(Dictionary<Key, _> dict)
        {
            string msg = "log dict:";
            foreach (var (key, _) in dict)
            {
                msg += $" {key}";
            }
            Log.Warning(msg);
        }

        public override void DoWindowContents(Rect inRect)
        {
            var view = new Listing_Standard(GameFont.Small);
            view.Begin(inRect);
            Text.Anchor = TextAnchor.MiddleLeft;

            var buttonLine = view.GetRect(Text.LineHeight);
            var margin = 0.99f;
            if (Widgets.ButtonText(buttonLine.RightHalf().RightPart(margin), "CloseDialogItemPolicy".Translate()))
            {
                Find.WindowStack.TryRemove(typeof(Dialog_ItemPolicy));
            }
            searchWidget.OnGUI(buttonLine.LeftHalf().LeftPart(margin), FilterOnChange);
            view.Gap();
            //==============================================
            var policy = ItemPolicyUtility.GetPawnPolicy(pawn);
            var searchResultLine = view.GetRect(Text.LineHeight * 3);
            var labelHeight = Text.LineHeight * 1.5f;

            var searchResultInnerRect = new Rect(0f, 0f, searchResultLine.width - 16f, labelHeight * defsToShow.Count);
            Widgets.BeginScrollView(searchResultLine, ref scrollPos, searchResultInnerRect);
            var searchResultView = new Listing_Standard();
            searchResultView.Begin(searchResultInnerRect);
            foreach (var def in defsToShow)
            {
                var labelWithIconBox = searchResultView.GetRect(labelHeight);
                Widgets.DefLabelWithIcon(labelWithIconBox, def);
                if (Widgets.ButtonInvisible(labelWithIconBox))
                {
                    ItemPolicyUtility.SetItemPolicyEntry(pawn, def, 0);
                    //Log.Warning($"buttonInvis add {def.defName} to dict");
                    LogDictKeys(policy.data);
                }
            }
            Widgets.EndScrollView();
            searchResultView.End();
            view.Gap();
            //==============================================

            //policy = ItemPolicyUtility.GetPawnPolicy(pawn);
            var currentSettingsLine = view.GetRect(labelHeight * 10);
            var currentSettingsInnerRect = new Rect(0f, 0f, currentSettingsLine.width - 16f, labelHeight * (policy.data.Count) + 1);
            Widgets.BeginScrollView(currentSettingsLine, ref scrollPos2, currentSettingsInnerRect);
            var currentSettingsView = new Listing_Standard();
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
                Widgets.TextFieldNumericLabeled<int>(textFieldBox, "Count".Translate(), ref num, ref editBuffer);
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
                //Log.Warning($"add {defsToChange[i].defName} {changeToValues[i]} to dict");
                LogDictKeys(policy.data);
            }

            foreach (var def in defsToRemove)
            {
                ItemPolicyUtility.RemoveItemPolicyEntry(pawn, def);
                //Log.Warning($"remove {def.defName} to dict");
                LogDictKeys(policy.data);
            }

            currentSettingsView.End();
            //==============================================

            GenUI.ResetLabelAlign();
            view.End();
        }

        private void FilterOnChange()
        {
            var searchText = searchWidget.filter.Text.Trim();
            if (searchText == "")
            {
                defsToShow.Clear();
                return;
            }
            defsToShow = ItemPolicyUtility.Search(searchText);
        }
    }
}
