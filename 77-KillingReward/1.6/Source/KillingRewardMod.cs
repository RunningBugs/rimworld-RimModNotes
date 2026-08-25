using KillingReward.Core;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace KillingReward
{
    public class KillingRewardMod : Mod
    {
        public static KillingRewardSettings Settings { get; private set; }

        private Vector2 settingsScrollPosition;

        public KillingRewardMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<KillingRewardSettings>();
            new Harmony("com.RunningBugs.KillingReward").PatchAll();
        }

        public override string SettingsCategory()
        {
            return "KillingReward";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            KillingRewardSettings s = Settings;
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, 820f);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.CheckboxLabeled("KR_SettingEnabled".Translate(), ref s.Enabled, "KR_SettingEnabledDesc".Translate());

            listing.Label((TaggedString)("KR_SettingInitial".Translate() + ": " + s.InitialKills), -1f, "KR_SettingInitialDesc".Translate());
            s.InitialKills = (int)listing.Slider(s.InitialKills, 1, 200);

            listing.Label("KR_SettingMode".Translate());
            bool exponential = s.Mode == GrowthMode.Exponential;
            if (listing.RadioButton("KR_SettingModeExponential".Translate(), exponential))
            {
                s.Mode = GrowthMode.Exponential;
            }
            if (listing.RadioButton("KR_SettingModeLinear".Translate(), !exponential))
            {
                s.Mode = GrowthMode.Linear;
            }

            if (s.Mode == GrowthMode.Exponential)
            {
                listing.Label((TaggedString)("KR_SettingFactor".Translate() + ": " + s.ExponentialFactor.ToString("F2")), -1f, "KR_SettingFactorDesc".Translate());
                s.ExponentialFactor = listing.Slider(s.ExponentialFactor, 1.0f, 3.0f);
            }
            else
            {
                listing.Label((TaggedString)("KR_SettingIncrement".Translate() + ": " + s.LinearIncrement), -1f, "KR_SettingIncrementDesc".Translate());
                s.LinearIncrement = (int)listing.Slider(s.LinearIncrement, 0, 200);
            }

            // 特质/基因修改的点数消耗曲线（与血祭升级同一套参数结构）
            DoModCostSection(listing, "KR_SettingTraitModHeader",
                ref s.TraitModInitialCost, ref s.TraitModMode, ref s.TraitModFactor, ref s.TraitModIncrement);
            DoModCostSection(listing, "KR_SettingGeneModHeader",
                ref s.GeneModInitialCost, ref s.GeneModMode, ref s.GeneModFactor, ref s.GeneModIncrement);

            listing.End();
            Widgets.EndScrollView();
        }

        private static void DoModCostSection(Listing_Standard listing, string headerKey,
            ref int initialCost, ref GrowthMode mode, ref float factor, ref int increment)
        {
            listing.GapLine();
            listing.Label(headerKey.Translate());
            listing.Label((TaggedString)("KR_SettingModInitialCost".Translate() + ": " + initialCost), -1f, "KR_SettingModInitialCostDesc".Translate());
            initialCost = (int)listing.Slider(initialCost, 1, 20);
            bool exponential = mode == GrowthMode.Exponential;
            if (listing.RadioButton("KR_SettingModeExponential".Translate(), exponential))
            {
                mode = GrowthMode.Exponential;
            }
            if (listing.RadioButton("KR_SettingModeLinear".Translate(), !exponential))
            {
                mode = GrowthMode.Linear;
            }
            if (mode == GrowthMode.Exponential)
            {
                listing.Label((TaggedString)("KR_SettingFactor".Translate() + ": " + factor.ToString("F2")), -1f, "KR_SettingFactorDesc".Translate());
                factor = listing.Slider(factor, 1.0f, 3.0f);
            }
            else
            {
                listing.Label((TaggedString)("KR_SettingIncrement".Translate() + ": " + increment), -1f, "KR_SettingIncrementDesc".Translate());
                increment = (int)listing.Slider(increment, 0, 20);
            }
        }
    }
}
