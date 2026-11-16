using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace _CarryPolicy;

[DefOf]
public static class CarryPolicyDefOf
{
	static CarryPolicyDefOf()
	{
		DefOfHelper.EnsureInitializedInCtor(typeof(CarryPolicyDefOf));
	}

	public static JobDef CarryPolicyTakeFromDispenser;
}

public class CarryPolicyModHandle : Mod
{
	public static CarryPolicyModHandle Instance { get; private set; }

	private static CarryPolicySettings settings;

	// 延迟到 Defs 加载完之后再读取 ModSettings，避免在 Mod 构造函数阶段
	// （DefDatabase<ThingDef> 尚为空）反序列化设置导致 ThingDef 引用全部丢失。
	public static CarryPolicySettings Settings => settings ??= Instance?.GetSettings<CarryPolicySettings>();

	public CarryPolicyModHandle(ModContentPack content)
		: base(content)
	{
		Instance = this;
	}

	public override string SettingsCategory()
	{
		return "CarryPolicySettingsCategory".Translate();
	}

	public override void DoSettingsWindowContents(Rect inRect)
	{
		CarryPolicySettings currentSettings = Settings;
		Listing_Standard listing = new Listing_Standard();
		listing.Begin(inRect);
		listing.Label("CrossSaveCarryPoliciesDesc".Translate());
		listing.Gap(8f);
		int count = currentSettings?.defaultPolicies?.Count ?? 0;
		string summary = count > 0
			? "CrossSaveCarryPoliciesSummary".Translate(count, currentSettings.defaultPolicies[0]?.label ?? "DefaultCarryPolicy".Translate().ToString()).ToString()
			: "CrossSaveCarryPoliciesSummaryEmpty".Translate().ToString();
		using (new TextBlock(ColoredText.SubtleGrayColor))
		{
			listing.Label(summary);
		}
		listing.Gap(10f);
		string buttonLabel = "ConfigureCrossSaveCarryPolicies".Translate();
		float buttonWidth = Mathf.Clamp(Text.CalcSize(buttonLabel).x + 36f, 240f, inRect.width);
		Rect buttonRect = listing.GetRect(34f);
		buttonRect.width = buttonWidth;
		if (Widgets.ButtonText(buttonRect, buttonLabel))
		{
			Find.WindowStack.Add(new Dialog_ManageDefaultCarryPolicies());
		}
		listing.End();
	}
}

[StaticConstructorOnStartup]
public static class CarryPolicyMod
{
	public const string Id = "com.RunningBugs.CarryPolicy";

	static CarryPolicyMod()
	{
		_ = CarryPolicyModHandle.Settings;
		CarryPolicyDatabase.CrossSavePoliciesProvider = () => CarryPolicyModHandle.Settings?.defaultPolicies;
		new Harmony(Id).PatchAll(typeof(CarryPolicyMod).Assembly);
	}
}
