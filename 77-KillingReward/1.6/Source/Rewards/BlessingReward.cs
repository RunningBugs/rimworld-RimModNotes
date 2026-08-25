using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace KillingReward
{
    /// <summary>
    /// 黑暗赐福：面向全体殖民地成员的群体增益，以及对单个小人的特质/基因改写。
    /// 赐福内部分多个类别，后续在 <see cref="BuildOptions"/> 中追加即可。
    /// </summary>
    public static class BlessingReward
    {
        public enum BlessingKind
        {
            Regen,      // 群体生命回复 hediff
            TraitMod,   // 特质修改（进入选人/增删流程）
            GeneMod     // 基因修改（进入选人/系谱异种/增删流程，需 Biotech）
        }

        public class BlessingOption
        {
            public BlessingKind kind;
            public HediffDef hediffDef;
            public string labelKey;
            public string descKey;
            public Func<bool> visible = () => true;

            public string Label => labelKey.Translate();
            public string Desc => descKey.Translate();
            public bool Visible => visible();

            /// <summary>本次领取消耗的恩赐点数（特质/基因随修改次数递增）。</summary>
            public long Cost(KillRewardTracker tracker)
            {
                switch (kind)
                {
                    case BlessingKind.TraitMod:
                        return tracker.TraitModCost;
                    case BlessingKind.GeneMod:
                        return tracker.GeneModCost;
                    default:
                        return 1L;
                }
            }
        }

        private static List<BlessingOption> options;

        public static List<BlessingOption> Options => options ??= BuildOptions();

        private static List<BlessingOption> BuildOptions()
        {
            return new List<BlessingOption>
            {
                new BlessingOption
                {
                    kind = BlessingKind.Regen,
                    hediffDef = KillingRewardDefOf.KillingReward_BlessingRegen,
                    labelKey = "KR_BlessingRegen",
                    descKey = "KR_BlessingRegenDesc"
                },
                new BlessingOption
                {
                    kind = BlessingKind.TraitMod,
                    labelKey = "KR_BlessingTrait",
                    descKey = "KR_BlessingTraitDesc"
                },
                new BlessingOption
                {
                    kind = BlessingKind.GeneMod,
                    labelKey = "KR_BlessingGene",
                    descKey = "KR_BlessingGeneDesc",
                    visible = () => GeneModReward.Available
                }
            };
        }

        public static List<Pawn> Targets()
        {
            return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction
                .OrderBy(p => p.LabelShort)
                .ToList();
        }

        /// <summary>对全体成员施加赐福 hediff；已持有者刷新持续时间。返回覆盖人数。</summary>
        public static int Apply(BlessingOption option)
        {
            List<Pawn> targets = Targets();
            foreach (Pawn pawn in targets)
            {
                Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(option.hediffDef);
                if (existing != null)
                {
                    existing.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
                }
                else
                {
                    pawn.health.AddHediff(option.hediffDef);
                }
            }
            return targets.Count;
        }
    }
}
