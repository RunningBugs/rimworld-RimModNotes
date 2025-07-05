using System.Collections.Generic;
using RimWorld;
using Verse;
using System;
using System.Linq;
using Verse.AI;
using Verse.AI.Group;

namespace ARCWOZ;

public class FloatMenuOptionProvider_AreaBypass : FloatMenuOptionProvider_WorkGivers
{
    public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
    {
        // If current mouse position is inside our current area, we should skip this
        var pawn = context.FirstSelectedPawn;
        if (context.ClickedCell.InAllowedArea(pawn))
        {
            return [];
        }
        var backupAreaRestriction = pawn.playerSettings.AreaRestrictionInPawnCurrentMap;
        pawn.playerSettings.AreaRestrictionInPawnCurrentMap = null; // Set to allow all areas
        var options = base.GetOptions(context).ToList();
        pawn.playerSettings.AreaRestrictionInPawnCurrentMap = backupAreaRestriction; // Restore original area restriction

        foreach (var option in options)
        {
            option.Label += " [Bypass Area]";
        }
        return options;
    }

    public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
    {
        var pawn = context.FirstSelectedPawn;
        if (context.ClickedCell.InAllowedArea(pawn))
        {
            return [];
        }
        var backupAreaRestriction = pawn.playerSettings.AreaRestrictionInPawnCurrentMap;
        pawn.playerSettings.AreaRestrictionInPawnCurrentMap = null; // Set to allow all areas
        var options = base.GetOptionsFor(clickedThing, context).ToList();
        pawn.playerSettings.AreaRestrictionInPawnCurrentMap = backupAreaRestriction; // Restore original area restriction

        foreach (var option in options)
        {
            option.Label += " [Bypass Area]";
        }
        return options;
    }
}