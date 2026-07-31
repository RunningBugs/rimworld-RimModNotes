using KillingReward.Core;
using Verse;

namespace KillingReward
{
    public class KillingRewardSettings : ModSettings
    {
        public bool Enabled = true;
        public int InitialKills = 10;
        public GrowthMode Mode = GrowthMode.Exponential;
        public float ExponentialFactor = 1.2f;
        public int LinearIncrement = 10;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref Enabled, "Enabled", true);
            Scribe_Values.Look(ref InitialKills, "InitialKills", 10);
            Scribe_Values.Look(ref Mode, "Mode", GrowthMode.Exponential);
            Scribe_Values.Look(ref ExponentialFactor, "ExponentialFactor", 1.2f);
            Scribe_Values.Look(ref LinearIncrement, "LinearIncrement", 10);
        }
    }
}
