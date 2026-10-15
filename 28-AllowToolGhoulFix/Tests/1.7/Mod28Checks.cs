using System;
using RimWorld;
using Verse;
namespace RW17.Validation;
public static class Mod28Checks
{
    public const string PackageId = "RunningBugs.AllowToolGhoulFix";
    public static void Run(Map map)
    {
        if (!ModsConfig.AnomalyActive) throw new Exception("Mod28: Anomaly is required for actual Ghoul regression fixture");
        var finish = DefDatabase<WorkTypeDef>.GetNamed("FinishingOff");
        if (finish.relevantSkills.Count != 0) throw new Exception("Mod28: actual XML patch did not clear FinishingOff skills");
        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Ghoul, Faction.OfPlayer);
        Rand.PushState(1728);
        try
        {
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(map.Center,map,15),map);
            if (!pawn.IsGhoul || pawn.skills.GetSkill(SkillDefOf.Melee).TotallyDisabled) throw new Exception("Mod28: actual ghoul melee disabled");
            var verb = pawn.meleeVerbs.TryGetMeleeVerb(null);
            if (verb == null || !verb.Available()) throw new Exception("Mod28: real ghoul melee verb unavailable");
            if (!SkillDefOf.Melee.neverDisabledBasedOnWorkTypes) throw new Exception("Mod28: vanilla 1.7 melee disabled-work bypass changed");
            // Real 1.7 vanilla now prevents the old bug even with the original AllowTool skill association.
            finish.relevantSkills.Add(SkillDefOf.Melee); pawn.skills.Notify_SkillDisablesChanged();
            if (pawn.skills.GetSkill(SkillDefOf.Melee).TotallyDisabled) throw new Exception("Mod28: actual vanilla bypass did not preserve melee with legacy association");
        }
        finally { finish.relevantSkills.Clear(); pawn.skills.Notify_SkillDisablesChanged(); pawn.Destroy(); Rand.PopState(); }
        Console.WriteLine("RW17_MOD28_XML_GHOUL_MELEE_AND_VANILLA_BYPASS_OK; original bug already prevented by current vanilla");
    }
}
