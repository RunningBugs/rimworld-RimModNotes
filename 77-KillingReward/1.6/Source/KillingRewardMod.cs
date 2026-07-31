using KillingReward.Core;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace KillingReward
{
    public class KillingRewardMod : Mod
    {
        public static KillingRewardSettings Settings { get; private set; }

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
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

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

            listing.End();
        }
    }
}
