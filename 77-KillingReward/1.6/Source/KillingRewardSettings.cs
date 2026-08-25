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
        public int TraitModInitialCost = 1;
        public GrowthMode TraitModMode = GrowthMode.Exponential;
        public float TraitModFactor = 1.2f;
        public int TraitModIncrement = 1;
        public int GeneModInitialCost = 1;
        public GrowthMode GeneModMode = GrowthMode.Exponential;
        public float GeneModFactor = 1.2f;
        public int GeneModIncrement = 1;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref Enabled, "Enabled", true);
            Scribe_Values.Look(ref InitialKills, "InitialKills", 10);
            Scribe_Values.Look(ref Mode, "Mode", GrowthMode.Exponential);
            Scribe_Values.Look(ref ExponentialFactor, "ExponentialFactor", 1.2f);
            Scribe_Values.Look(ref LinearIncrement, "LinearIncrement", 10);
            Scribe_Values.Look(ref TraitModInitialCost, "TraitModInitialCost", 1);
            Scribe_Values.Look(ref TraitModMode, "TraitModMode", GrowthMode.Exponential);
            Scribe_Values.Look(ref TraitModFactor, "TraitModFactor", 1.2f);
            Scribe_Values.Look(ref TraitModIncrement, "TraitModIncrement", 1);
            Scribe_Values.Look(ref GeneModInitialCost, "GeneModInitialCost", 1);
            Scribe_Values.Look(ref GeneModMode, "GeneModMode", GrowthMode.Exponential);
            Scribe_Values.Look(ref GeneModFactor, "GeneModFactor", 1.2f);
            Scribe_Values.Look(ref GeneModIncrement, "GeneModIncrement", 1);
        }
    }
}
