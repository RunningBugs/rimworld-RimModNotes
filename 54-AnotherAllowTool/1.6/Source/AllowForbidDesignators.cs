using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AAT;

internal static class AATThingFilters
{
    public static bool IsLiveThingOnMap(Thing thing, Map map)
    {
        return thing != null
            && !thing.Destroyed
            && thing.Spawned
            && thing.Map == map
            && thing.def != null;
    }

    public static bool IsForbiddable(Thing thing)
    {
        return thing is ThingWithComps thingWithComps && thingWithComps.GetComp<CompForbiddable>() != null;
    }

    public static bool CanAllow(Thing thing, Map map)
    {
        return IsLiveThingOnMap(thing, map) && IsForbiddable(thing) && thing.IsForbidden(Faction.OfPlayer);
    }

    public static bool CanForbid(Thing thing, Map map)
    {
        return IsLiveThingOnMap(thing, map) && IsForbiddable(thing) && !thing.IsForbidden(Faction.OfPlayer);
    }
}

public abstract class Designator_ForbidStateBase : Designator
{
    protected abstract bool TargetForbiddenState { get; }
    protected abstract string IconPath { get; }

    public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;

    protected Designator_ForbidStateBase()
    {
        icon = ContentFinder<Texture2D>.Get(IconPath, true);
        soundDragSustain = SoundDefOf.Designate_DragStandard;
        soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        soundSucceeded = SoundDefOf.Designate_DragStandard_Changed;
        useMouseIcon = true;
    }

    public override AcceptanceReport CanDesignateCell(IntVec3 c)
    {
        if (Map == null || !c.InBounds(Map) || c.Fogged(Map))
        {
            return false;
        }

        List<Thing> things = c.GetThingList(Map);
        for (int i = 0; i < things.Count; i++)
        {
            if (CanDesignateThing(things[i]).Accepted)
            {
                return true;
            }
        }
        return false;
    }

    public override AcceptanceReport CanDesignateThing(Thing t)
    {
        if (Map == null)
        {
            return false;
        }

        return TargetForbiddenState ? AATThingFilters.CanForbid(t, Map) : AATThingFilters.CanAllow(t, Map);
    }

    public override void DesignateSingleCell(IntVec3 c)
    {
        List<Thing> things = c.GetThingList(Map);
        for (int i = 0; i < things.Count; i++)
        {
            DesignateThing(things[i]);
        }
    }

    public override void DesignateThing(Thing t)
    {
        if (!CanDesignateThing(t).Accepted)
        {
            return;
        }

        t.SetForbidden(TargetForbiddenState, false);
        if (TargetForbiddenState)
        {
            Map.listerHaulables.Notify_Forbidden(t);
        }
        else
        {
            Map.listerHaulables.Notify_Unforbidden(t);
        }
    }

    public override void SelectedUpdate()
    {
        GenUI.RenderMouseoverBracket();
    }
}

public class Designator_Allow : Designator_ForbidStateBase
{
    protected override bool TargetForbiddenState => false;
    protected override string IconPath => "allow";

    public Designator_Allow()
    {
        defaultLabel = "DesignatorAllow".Translate();
        defaultDesc = "DesignatorAllowDesc".Translate();
    }
}

public class Designator_Forbid : Designator_ForbidStateBase
{
    protected override bool TargetForbiddenState => true;
    protected override string IconPath => "forbid";

    public Designator_Forbid()
    {
        defaultLabel = "DesignatorForbid".Translate();
        defaultDesc = "DesignatorForbidDesc".Translate();
    }
}

public class Designator_AllowAll : Designator_Allow
{
    public Designator_AllowAll()
    {
        defaultLabel = "DesignatorAllowAll".Translate();
        defaultDesc = "DesignatorAllowAllDesc".Translate();
        icon = ContentFinder<Texture2D>.Get("allowAll", true);
    }

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        if (Map == null)
        {
            return;
        }

        int count = 0;
        foreach (Thing thing in Map.listerThings.AllThings)
        {
            if (CanDesignateThing(thing).Accepted)
            {
                thing.SetForbidden(false, false);
                Map.listerHaulables.Notify_Unforbidden(thing);
                count++;
            }
        }

        Messages.Message("AAT_AllowAllComplete".Translate(count), MessageTypeDefOf.TaskCompletion, false);
    }
}
