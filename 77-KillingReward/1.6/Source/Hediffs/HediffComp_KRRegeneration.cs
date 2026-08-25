using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace KillingReward
{
    public class HediffCompProperties_KRRegeneration : HediffCompProperties
    {
        // 每天回复的 HP，换算与原版 regeneration 一致：数值 × 1 = HP/天。
        public float healPerDay = 350f;

        public HediffCompProperties_KRRegeneration()
        {
            compClass = typeof(HediffComp_KRRegeneration);
        }
    }

    // 自包含再生机制：复刻 Pawn_HealthTracker 中 regeneration 的结算逻辑
    // （每 15 tick 结算一次，先平摊治疗全部伤口，点数有剩余再按规则长回一个缺失部位），
    // 但去掉了原版的 IsFlesh / AnomalyActive 门槛——血肉生物与机械体一视同仁。
    public class HediffComp_KRRegeneration : HediffComp
    {
        private const int TickInterval = 15;

        private static List<Hediff_Injury> tmpInjuries = new List<Hediff_Injury>();
        private static List<Hediff_MissingPart> tmpMissingParts = new List<Hediff_MissingPart>();

        private HediffCompProperties_KRRegeneration RegenProps => (HediffCompProperties_KRRegeneration)props;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (!Pawn.IsHashIntervalTick(TickInterval, delta))
            {
                return;
            }
            // 0.00025 × (60000/15) = 1 → healPerDay 即每天的 HP 回复量。
            float points = RegenProps.healPerDay * 0.00025f;
            if (points <= 0f)
            {
                return;
            }
            HediffSet hediffSet = Pawn.health.hediffSet;
            hediffSet.GetHediffs(ref tmpInjuries, (Hediff_Injury h) => true);
            foreach (Hediff_Injury injury in tmpInjuries)
            {
                float heal = Mathf.Min(points, injury.Severity);
                points -= heal;
                injury.Heal(heal);
                if (points <= 0f)
                {
                    break;
                }
            }
            if (points <= 0f)
            {
                return;
            }
            // 长回一个缺失部位，规则与原版一致：父部位须无伤、未缺失、无假体。
            // 新部位以约 90% 重伤状态长出，随后由再生继续养满。
            hediffSet.GetHediffs(ref tmpMissingParts, (Hediff_MissingPart h) => h.Part.parent != null
                && !tmpInjuries.Any(x => x.Part == h.Part.parent)
                && hediffSet.GetFirstHediffMatchingPart<Hediff_MissingPart>(h.Part.parent) == null
                && hediffSet.GetFirstHediffMatchingPart<Hediff_AddedPart>(h.Part.parent) == null);
            if (tmpMissingParts.Count == 0)
            {
                return;
            }
            Hediff_MissingPart missing = tmpMissingParts[0];
            BodyPartRecord part = missing.Part;
            Pawn.health.RemoveHediff(missing);
            Hediff freshWound = Pawn.health.AddHediff(HediffDefOf.Misc, part);
            float partHealth = hediffSet.GetPartHealth(part);
            freshWound.Severity = Mathf.Max(partHealth - 1f, partHealth * 0.9f);
        }
    }
}
