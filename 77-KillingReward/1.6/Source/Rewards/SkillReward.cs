using System.Collections.Generic;
using System.Linq;
using KillingReward.Core;
using RimWorld;
using Verse;

namespace KillingReward
{
    public static class SkillReward
    {
        private const int SkillBump = 3;

        public static List<Pawn> Candidates()
        {
            return PawnsFinder.AllMaps_FreeColonists
                .Where(p => !p.Dead && p.skills != null && p.RaceProps.Humanlike)
                .OrderBy(p => p.LabelShort)
                .ToList();
        }

        public static List<SkillRecord> AvailableSkills(Pawn pawn)
        {
            return pawn.skills.skills
                .Where(s => !s.TotallyDisabled)
                .ToList();
        }

        public static void Apply(SkillRecord skill)
        {
            // Level setter 自带 0-20 clamp；外部 Mod 的子 20 上限由它们自身机制再 clamp，本 Mod 不突破。
            skill.Level = SkillMath.ClampedAdd(skill.Level, SkillBump, SkillRecord.MaxLevel);
        }
    }
}
