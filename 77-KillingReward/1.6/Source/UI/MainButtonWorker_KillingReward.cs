using RimWorld;
using Verse;

namespace KillingReward
{
    public class MainButtonWorker_KillingReward : MainButtonWorker
    {
        public override bool Visible => KillingRewardMod.Settings.Enabled && base.Visible;

        public override void Activate()
        {
            Find.WindowStack.Add(new Dialog_KillingReward());
        }
    }
}
