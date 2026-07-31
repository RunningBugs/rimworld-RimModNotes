using System.Collections.Generic;
using RimWorld;
using Verse;

namespace KillingReward
{
    public class ChoiceLetter_KillingReward : ChoiceLetter
    {
        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                yield return new DiaOption("KR_LetterOpen".Translate())
                {
                    action = delegate
                    {
                        Find.WindowStack.Add(new Dialog_KillingReward());
                        Find.LetterStack.RemoveLetter(this);
                    },
                    resolveTree = true
                };
                yield return Option_Close;
            }
        }
    }
}
