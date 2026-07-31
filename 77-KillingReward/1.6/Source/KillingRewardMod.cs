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
        }

        public override string SettingsCategory()
        {
            return "KillingReward";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Task 6 填充完整设置界面
            base.DoSettingsWindowContents(inRect);
        }
    }
}
