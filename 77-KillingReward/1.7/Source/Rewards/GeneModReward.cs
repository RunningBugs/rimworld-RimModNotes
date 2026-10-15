using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace KillingReward
{
    /// <summary>
    /// 黑暗赐福·基因修改：为一名人类小人添加或移除一个基因（系谱或异种）。
    /// 无 Biotech DLC 时不可用（选项不显示）；基因类存在于主程序集，Mod 本身不依赖 DLC。
    /// </summary>
    public static class GeneModReward
    {
        public static bool Available => ModsConfig.BiotechActive;

        public static List<Pawn> Candidates()
        {
            return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction
                .Where(p => p.RaceProps.Humanlike && p.genes != null)
                .OrderBy(p => p.LabelShort)
                .ToList();
        }

        public static bool HasGene(Pawn pawn, GeneDef def)
        {
            return pawn.genes.GenesListForReading.Any(g => g.def == def);
        }

        /// <summary>可添加的基因：排除已持有的（无论系谱还是异种）。</summary>
        public static List<GeneDef> AddableGenes(Pawn pawn)
        {
            return DefDatabase<GeneDef>.AllDefsListForReading
                .Where(d => !HasGene(pawn, d))
                .OrderBy(d => d.LabelCap.ToString())
                .ToList();
        }

        public static List<Gene> RemovableGenes(Pawn pawn, bool xenogene)
        {
            List<Gene> genes = xenogene ? pawn.genes.Xenogenes : pawn.genes.Endogenes;
            return genes.OrderBy(g => g.def.LabelCap.ToString()).ToList();
        }

        public static void Add(Pawn pawn, GeneDef def, bool xenogene)
        {
            pawn.genes.AddGene(def, xenogene);
        }

        public static void Remove(Pawn pawn, Gene gene)
        {
            pawn.genes.RemoveGene(gene);
        }
    }
}
