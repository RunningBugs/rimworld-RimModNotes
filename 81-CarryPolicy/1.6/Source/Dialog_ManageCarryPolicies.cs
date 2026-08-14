using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace _CarryPolicy;

public class Dialog_ManageCarryPolicies : Dialog_ManagePolicies<CarryPolicy>
{
	private const float EntryRowHeight = 35f;

	private const float OptionRowHeight = 30f;

	private const float HeaderHeight = 26f;

	private const float BottomRowHeight = 40f;

	private const float LeftColumnWidth = 460f;

	private const float IconSize = 27f;

	private const float InfoButtonSize = 16f;

	private const float CountFieldWidth = 70f;

	private const float HintWidth = 110f;

	private const float DeleteWidth = 22f;

	private const float CategoryDropdownWidth = 132f;

	private Vector2 entriesScroll;

	private Vector2 optionsScroll;

	private readonly QuickSearchWidget searchWidget = new QuickSearchWidget();

	private readonly HashSet<ThingDef> checkedDefs = new HashSet<ThingDef>();

	private ThingCategoryDef currentCategory;

	private List<ThingDef> cachedOptions;

	private string cachedOptionsKey;

	protected override string TitleKey => "CarryPolicyTitle";

	protected override string TipKey => "CarryPolicyTip";

	public override Vector2 InitialSize => new Vector2(Mathf.Min(Screen.width - 50f, 1000f), 720f);

	public Dialog_ManageCarryPolicies(CarryPolicy policy)
		: base(policy)
	{
	}

	protected override CarryPolicy CreateNewPolicy()
	{
		return CarryPolicyDatabase.Instance.MakeNewPolicy();
	}

	protected override CarryPolicy GetDefaultPolicy()
	{
		return CarryPolicyDatabase.Instance.DefaultCarryPolicy();
	}

	protected override void SetDefaultPolicy(CarryPolicy policy)
	{
		CarryPolicyDatabase.Instance.SetDefault(policy);
	}

	protected override AcceptanceReport TryDeletePolicy(CarryPolicy policy)
	{
		return CarryPolicyDatabase.Instance.TryDelete(policy);
	}

	protected override List<CarryPolicy> GetPolicies()
	{
		return CarryPolicyDatabase.Instance.AllPolicies;
	}

	protected override void DoContentsRect(Rect rect)
	{
		float num = Mathf.Min(LeftColumnWidth, rect.width * 0.6f);
		Rect rect2 = new Rect(rect.x, rect.y, num, rect.height);
		Rect rect3 = new Rect(rect2.xMax + 10f, rect.y, rect.width - num - 10f, rect.height);
		DoCarryListColumn(rect2);
		DoOptionsColumn(rect3);
	}

	// ---------- 左栏：携带清单 ----------
	private void DoCarryListColumn(Rect rect)
	{
		Rect rect2 = new Rect(rect.x, rect.y, rect.width, HeaderHeight);
		Rect rect3 = new Rect(rect.x, rect2.yMax, rect.width, rect.height - HeaderHeight - BottomRowHeight);
		Rect rect4 = new Rect(rect.x, rect.yMax - 32f, 100f, 32f);
		DoEntryColumnLabels(rect2);
		Widgets.DrawMenuSection(rect3);
		Rect rect5 = rect3.ContractedBy(1f);
		if (SelectedPolicy.Count == 0)
		{
			GUI.color = Color.grey;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(rect5, "NoCarryEntries".Translate());
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
		}
		else
		{
			Rect viewRect = new Rect(0f, 0f, rect5.width, SelectedPolicy.Count * EntryRowHeight);
			Widgets.AdjustRectsForScrollView(rect3, ref rect5, ref viewRect);
			Widgets.BeginScrollView(rect5, ref entriesScroll, viewRect);
			List<int> list = null;
			for (int i = 0; i < SelectedPolicy.Count; i++)
			{
				Rect rect6 = new Rect(0f, i * EntryRowHeight, viewRect.width, EntryRowHeight);
				if (DoEntryRow(rect6, SelectedPolicy[i], i))
				{
					list ??= new List<int>();
					list.Add(i);
				}
			}
			Widgets.EndScrollView();
			if (list != null)
			{
				foreach (int item in list.OrderByDescending((int x) => x))
				{
					SelectedPolicy.entries.RemoveAt(item);
				}
			}
		}
		if (Widgets.ButtonText(rect4, "ClearAllCarryEntries".Translate()))
		{
			SelectedPolicy.entries.Clear();
		}
	}

	private void DoEntryColumnLabels(Rect rect)
	{
		rect.width -= 16f;
		float x = CalculateEntryColumns(rect, out var _, out var nameWidth, out var _, out var _, out var _);
		Text.Anchor = TextAnchor.LowerLeft;
		Rect rect2 = new Rect(x, rect.y, IconSize + nameWidth, rect.height);
		Widgets.Label(rect2, "CarryNameColumnLabel".Translate());
		Rect rect3 = new Rect(x + IconSize + nameWidth, rect.y, CountFieldWidth, rect.height);
		Text.Anchor = TextAnchor.LowerCenter;
		Widgets.Label(rect3, "CarryCountColumnLabel".Translate());
		Rect rect4 = new Rect(x + IconSize + nameWidth + CountFieldWidth, rect.y, HintWidth, rect.height);
		using (new TextBlock(null, TextAnchor.MiddleLeft, false, Color.grey))
		{
			Widgets.LabelFit(rect4, "CarryEstimateColumnLabel".Translate());
		}
		TooltipHandler.TipRegion(rect, "CarryCountDesc".Translate());
		Text.Anchor = TextAnchor.UpperLeft;
	}

	// 返回 true 表示该行请求删除
	private bool DoEntryRow(Rect rect, CarryPolicyEntry entry, int index)
	{
		bool result = false;
		if (index % 2 == 1)
		{
			Widgets.DrawLightHighlight(rect);
		}
		float x = CalculateEntryColumns(rect, out var deleteX, out var nameWidth, out var countX, out var hintX, out var infoX);
		Text.Anchor = TextAnchor.MiddleLeft;
		Widgets.ThingIcon(new Rect(x, rect.y + (rect.height - IconSize) / 2f, IconSize, IconSize), entry.thing);
		Rect rect2 = new Rect(x + IconSize, rect.y, nameWidth, rect.height).ContractedBy(4f);
		Widgets.Label(rect2, entry.thing.LabelCap);
		Widgets.InfoCardButton(new Rect(infoX, rect.y + (rect.height - InfoButtonSize) / 2f, InfoButtonSize, InfoButtonSize), entry.thing);
		float max = entry.IsFood ? 20f : 999f;
		Widgets.TextFieldNumeric(new Rect(countX, rect.y + 6f, CountFieldWidth, rect.height - 12f), ref entry.count, ref entry.countBuffer, 0f, max);
		GUI.color = Color.grey;
		string text = (entry.IsFood ? ("≈" + CarryPolicyUtility.BaselineTargetCount(entry) + "CarryItemsShort".Translate()) : "CarryItemsShort".Translate());
		Widgets.Label(new Rect(hintX, rect.y, HintWidth, rect.height), text);
		GUI.color = Color.white;
		if (Widgets.ButtonImage(new Rect(deleteX, rect.y + (rect.height - 18f) / 2f, 18f, 18f), TexButton.CloseXSmall))
		{
			result = true;
		}
		Text.Anchor = TextAnchor.UpperLeft;
		return result;
	}

	private float CalculateEntryColumns(Rect rect, out float deleteX, out float nameWidth, out float countX, out float hintX, out float infoX)
	{
		deleteX = rect.xMax - DeleteWidth;
		hintX = deleteX - HintWidth;
		countX = hintX - CountFieldWidth;
		nameWidth = countX - (rect.x + IconSize) - InfoButtonSize - 4f;
		infoX = rect.x + IconSize + nameWidth;
		return rect.x;
	}

	// ---------- 右栏：可选项 ----------
	private void DoOptionsColumn(Rect rect)
	{
		Rect rect2 = new Rect(rect.x, rect.y, rect.width, HeaderHeight);
		Rect rect3 = new Rect(rect.x, rect2.yMax, rect.width, rect.height - HeaderHeight - BottomRowHeight);
		Rect rect4 = new Rect(rect.x, rect.yMax - 32f, rect.width, 32f);
		Rect rect5 = new Rect(rect2.x, rect2.yMax - 24f, CategoryDropdownWidth, 24f);
		Rect rect6 = new Rect(rect2.x + CategoryDropdownWidth + 8f, rect2.yMax - 24f, rect2.width - CategoryDropdownWidth - 8f, 24f);
		if (currentCategory == null)
		{
			currentCategory = AvailableCategories().FirstOrDefault() ?? ThingCategoryDefOf.Root;
		}
		Widgets.Dropdown(rect5, this, (Dialog_ManageCarryPolicies d) => d.currentCategory, GenerateCategoryMenu, currentCategory.LabelCap, null, currentCategory.LabelCap, null, null, paintable: false);
		searchWidget.OnGUI(rect6, RefreshOptions);
		Widgets.DrawMenuSection(rect3);
		Rect rect7 = rect3.ContractedBy(1f);
		List<ThingDef> list = CurrentOptions();
		float num = list.Count * OptionRowHeight;
		Rect viewRect = new Rect(0f, 0f, rect7.width, num);
		Widgets.AdjustRectsForScrollView(rect3, ref rect7, ref viewRect);
		Widgets.BeginScrollView(rect7, ref optionsScroll, viewRect);
		for (int i = 0; i < list.Count; i++)
		{
			Rect rect8 = new Rect(0f, i * OptionRowHeight, viewRect.width, OptionRowHeight);
			DoOptionRow(rect8, list[i], i);
		}
		Widgets.EndScrollView();
		Text.Anchor = TextAnchor.MiddleLeft;
		GUI.color = (checkedDefs.Count > 0) ? Color.white : Color.grey;
		Widgets.Label(new Rect(rect4.x, rect4.y, rect4.width - 120f, rect4.height), "CarrySelectedCount".Translate(checkedDefs.Count));
		GUI.color = Color.white;
		Text.Anchor = TextAnchor.UpperLeft;
		Rect rect9 = new Rect(rect4.xMax - 110f, rect4.y, 110f, rect4.height);
		if (Widgets.ButtonText(rect9, "AddSelectedCarryItems".Translate()) && SelectedPolicy != null)
		{
			foreach (ThingDef checkedDef in checkedDefs)
			{
				if (SelectedPolicy.EntryFor(checkedDef) == null)
				{
					SelectedPolicy.entries.Add(new CarryPolicyEntry
					{
						thing = checkedDef,
						count = 1f
					});
				}
			}
			checkedDefs.Clear();
		}
	}

	private void DoOptionRow(Rect rect, ThingDef def, int index)
	{
		bool flag = SelectedPolicy?.EntryFor(def) != null;
		if (flag)
		{
			GUI.color = new Color(0.5f, 0.5f, 0.5f);
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(new Rect(rect.x + 32f, rect.y, rect.width * 0.55f, rect.height), def.LabelCap);
			Text.Anchor = TextAnchor.MiddleRight;
			GUI.color = Color.grey;
			Widgets.Label(new Rect(rect.x + 32f, rect.y, rect.width - 40f, rect.height), "AlreadyInCarryList".Translate());
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
			return;
		}
		if (checkedDefs.Contains(def))
		{
			Widgets.DrawHighlight(rect);
		}
		else if (index % 2 == 1)
		{
			Widgets.DrawLightHighlight(rect);
		}
		bool checkOn = checkedDefs.Contains(def);
		Widgets.Checkbox(rect.x + 8f, rect.y + (rect.height - 18f) / 2f, ref checkOn, 18f, disabled: false, paintable: false);
		if (checkOn)
		{
			checkedDefs.Add(def);
		}
		else
		{
			checkedDefs.Remove(def);
		}
		Text.Anchor = TextAnchor.MiddleLeft;
		Widgets.Label(new Rect(rect.x + 32f, rect.y, rect.width - 40f, rect.height), def.LabelCap);
		Text.Anchor = TextAnchor.UpperLeft;
	}

	private IEnumerable<ThingCategoryDef> AvailableCategories()
	{
		return from c in DefDatabase<ThingCategoryDef>.AllDefs
			where c.parent == ThingCategoryDefOf.Root
			where c != ThingCategoryDefOf.Corpses && c.DescendantThingDefs.Any(CarryPolicyUtility.Selectable)
			orderby c.LabelCap.RawText
			select c;
	}

	private IEnumerable<Widgets.DropdownMenuElement<ThingCategoryDef>> GenerateCategoryMenu(Dialog_ManageCarryPolicies dialog)
	{
		foreach (ThingCategoryDef item in dialog.AvailableCategories())
		{
			ThingCategoryDef cat = item;
			yield return new Widgets.DropdownMenuElement<ThingCategoryDef>
			{
				option = new FloatMenuOption(cat.LabelCap, delegate
				{
					dialog.currentCategory = cat;
					dialog.RefreshOptions();
				}),
				payload = cat
			};
		}
	}

	private void RefreshOptions()
	{
		cachedOptions = null;
	}

	private List<ThingDef> CurrentOptions()
	{
		string text = (searchWidget.filter.Active ? "search:" + searchWidget.filter.Text : "cat:" + currentCategory.defName);
		if (cachedOptions != null && text == cachedOptionsKey)
		{
			return cachedOptions;
		}
		IEnumerable<ThingDef> source = searchWidget.filter.Active
			? DefDatabase<ThingDef>.AllDefs.Where((ThingDef d) => CarryPolicyUtility.Selectable(d) && searchWidget.filter.Matches(d.label))
			: currentCategory.DescendantThingDefs.Where(CarryPolicyUtility.Selectable);
		cachedOptions = (from d in source
			orderby d.label
			select d).ToList();
		cachedOptionsKey = text;
		return cachedOptions;
	}
}
