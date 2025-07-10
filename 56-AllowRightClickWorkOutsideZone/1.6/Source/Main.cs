using System.Collections.Generic;
using RimWorld;
using Verse;
using System;
using System.Linq;
using Verse.AI;
using Verse.AI.Group;
using System.Reflection;

namespace ARCWOZ;

public class FloatMenuOptionProvider_BypassRestrictions : FloatMenuOptionProvider_WorkGivers
{
    private DefMap<WorkTypeDef, int> GetWorkPrioritiesSettings(Pawn pawn, IEnumerable<WorkTypeDef> workTypes)
    {
        var priorities = new DefMap<WorkTypeDef, int>();
        foreach (var workType in workTypes)
        {
            priorities[workType] = pawn.workSettings.GetPriority(workType);
        }
        return priorities;
    }

    public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
    {
        var pawn = context.FirstSelectedPawn;
        // Find all worktypes that the pawn can do
        var worktypes = DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(wt => !pawn.WorkTypeIsDisabled(wt));
        // backup the pawn worktype settings and area restriction
        var backupWorkSettings = GetWorkPrioritiesSettings(pawn, worktypes);
        var backupAreaRestriction = pawn.playerSettings.AreaRestrictionInPawnCurrentMap;


        // Set all worktypes to enabled
        foreach (var worktype in worktypes)
        {
            pawn.workSettings.SetPriority(worktype, 3); // Set to default priority
        }
        pawn.playerSettings.AreaRestrictionInPawnCurrentMap = null; // Set to allow all areas
        var options = base.GetOptions(context).ToList();
        foreach (var option in options)
        {
            option.Label += " [Bypass Restrictions]";
        }

        // Restore the original area and worktype settings
        pawn.playerSettings.AreaRestrictionInPawnCurrentMap = backupAreaRestriction; // Restore original area restriction
        foreach (var priorities in backupWorkSettings)
        {
            pawn.workSettings.SetPriority(priorities.Key, priorities.Value);
        }
        return options;
    }

    public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
    {
        var pawn = context.FirstSelectedPawn;
        // Find all worktypes that the pawn can do
        var worktypes = DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(wt => !pawn.WorkTypeIsDisabled(wt));
        // backup the pawn worktype settings and area restriction
        var backupWorkSettings = GetWorkPrioritiesSettings(pawn, worktypes);
        var backupAreaRestriction = pawn.playerSettings.AreaRestrictionInPawnCurrentMap;

        // Set all worktypes to enabled
        foreach (var worktype in worktypes)
        {
            pawn.workSettings.SetPriority(worktype, 3); // Set to default priority
        }
        pawn.playerSettings.AreaRestrictionInPawnCurrentMap = null; // Set to allow all areas
        var options = base.GetOptionsFor(clickedThing, context).ToList();
        foreach (var option in options)
        {
            option.Label += " [Bypass Restrictions]";
        }

        // Restore the original worktype settings and area restriction
        pawn.playerSettings.AreaRestrictionInPawnCurrentMap = backupAreaRestriction; 
        foreach (var priorities in backupWorkSettings)
        {
            pawn.workSettings.SetPriority(priorities.Key, priorities.Value);
        }

        return options;
    }
}
