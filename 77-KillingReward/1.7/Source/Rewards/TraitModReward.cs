using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace KillingReward
{
    /// <summary>黑暗赐福·特质修改：为一名人类小人添加或移除一项特质。</summary>
    public static class TraitModReward
    {
        public static List<Pawn> Candidates()
        {
            return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction
                .Where(p => p.RaceProps.Humanlike && p.story?.traits != null)
                .OrderBy(p => p.LabelShort)
                .ToList();
        }

        /// <summary>可添加的（特质, 程度）组合：排除已持有及与现有特质冲突的项。</summary>
        public static List<Trait> AddableTraits(Pawn pawn)
        {
            TraitSet traits = pawn.story.traits;
            List<Trait> result = new List<Trait>();
            foreach (TraitDef def in DefDatabase<TraitDef>.AllDefsListForReading)
            {
                // 其他 Mod 可能注册无 degreeDatas 的特质，跳过以免列表构建 NRE。
                if (def.degreeDatas.NullOrEmpty() || traits.HasTrait(def) || traits.allTraits.Any(t => t.def.ConflictsWith(def)))
                {
                    continue;
                }
                foreach (TraitDegreeData data in def.degreeDatas)
                {
                    result.Add(new Trait(def, data.degree));
                }
            }
            return result.OrderBy(t => t.Label).ToList();
        }

        public static List<Trait> RemovableTraits(Pawn pawn)
        {
            return pawn.story.traits.allTraits.OrderBy(t => t.Label).ToList();
        }

        public static void Add(Pawn pawn, Trait trait)
        {
            // GainTrait 内部会处理工作类型禁用、技能、想法、能力、需求等联动刷新。
            pawn.story.traits.GainTrait(trait);
        }

        public static void Remove(Pawn pawn, Trait trait)
        {
            pawn.story.traits.RemoveTrait(trait);
        }
    }
}
