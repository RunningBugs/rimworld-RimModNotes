using RimWorld;
using Verse;

namespace KillingReward
{
    public class MainButtonWorker_KillingReward : MainButtonWorker
    {
        public override void Activate()
        {
            Find.WindowStack.Add(new Dialog_KillingReward());
        }
    }
}
