using System.Collections.Generic;
using RimWorld;
using Verse;
using System;
using System.Linq;
using Verse.AI;
using Verse.AI.Group;

namespace ARCWOZ;

public class FloatMenuOptionProvider_AreaBypass : FloatMenuOptionProvider
{
    private static HashSet<string> tmpUsedLabels = new HashSet<string>();

    protected override bool Drafted => true;
    protected override bool Undrafted => true;
    protected override bool Multiselect => false;
    protected override bool MechanoidCanDo => true;
    protected override bool CanSelfTarget => true;

    public override IEnumerable<FloatMenuOption> GetOptions(FloatMenuContext context)
    {
        tmpUsedLabels.Clear();
        return GetAreaBypassOptions(context.FirstSelectedPawn, context.ClickedCell, context);
    }

    public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
    {
        tmpUsedLabels.Clear();
        return GetAreaBypassOptions(context.FirstSelectedPawn, clickedThing, context);
    }

    private IEnumerable<FloatMenuOption> GetAreaBypassOptions(Pawn pawn, LocalTargetInfo target, FloatMenuContext context)
    {
        if (pawn.thinker.TryGetMainTreeThinkNode<JobGiver_Work>() == null)
        {
            yield break;
        }

        foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
        {
            foreach (WorkGiverDef workGiverDef in workType.workGiversByPriority)
            {
                FloatMenuOption option = TryGetAreaBypassOption(pawn, workGiverDef, target, context);
                if (option != null)
                {
                    if (!tmpUsedLabels.Contains(option.Label))
                    {
                        tmpUsedLabels.Add(option.Label);
                        yield return option;
                    }
                }
            }
        }
    }

    private FloatMenuOption TryGetAreaBypassOption(Pawn pawn, WorkGiverDef workGiverDef, LocalTargetInfo target, FloatMenuContext context)
    {
        // Basic checks
        if (pawn.Drafted && !workGiverDef.canBeDoneWhileDrafted)
            return null;

        if (!(workGiverDef.Worker is WorkGiver_Scanner scanner) || !scanner.def.directOrderable)
            return null;

        // Check if this work giver can work on this target
        JobFailReason.Clear();
        bool canWork = false;

        if (target.HasThing)
        {
            if (scanner.PotentialWorkThingRequest.Accepts(target.Thing) ||
                (scanner.PotentialWorkThingsGlobal(pawn)?.Contains(target.Thing) == true))
            {
                canWork = !scanner.ShouldSkip(pawn, forced: true);
            }
        }
        else
        {
            canWork = scanner.PotentialWorkCellsGlobal(pawn).Contains(target.Cell) &&
                     !scanner.ShouldSkip(pawn, forced: true);
        }

        if (!canWork)
            return null;

        // Try to get the job
        Job job = null;
        if (target.HasThing)
        {
            if (scanner.HasJobOnThing(pawn, target.Thing, forced: true))
                job = scanner.JobOnThing(pawn, target.Thing, forced: true);
        }
        else
        {
            if (scanner.HasJobOnCell(pawn, target.Cell, forced: true))
                job = scanner.JobOnCell(pawn, target.Cell, forced: true);
        }

        if (job == null)
            return null;

        // Check all non-area fail conditions - return null if any of these fail
        WorkTypeDef workType = scanner.def.workType;

        if (scanner.MissingRequiredCapacity(pawn) != null)
            return null;

        if (pawn.WorkTagIsDisabled(scanner.def.workTags))
            return null;

        if (pawn.jobs.curJob != null && pawn.jobs.curJob.JobIsSameAs(pawn, job))
            return null;

        if (pawn.workSettings.GetPriority(workType) == 0)
            return null;

        if (job.def == JobDefOf.Research && target.Thing is Building_ResearchBench)
            return null;

        if ((target.HasThing && !pawn.CanReach(target.Thing, scanner.PathEndMode, Danger.Deadly)) ||
            (!target.HasThing && !pawn.CanReach(target.Cell, PathEndMode.Touch, Danger.Deadly)))
            return null;

        // NOW check for area restrictions - this is the ONLY case we show an option
        bool isAreaRestricted = false;
        string failText = "";

        if (target.HasThing && target.Thing.IsForbidden(pawn))
        {
            if (!target.Thing.Position.InAllowedArea(pawn))
            {
                isAreaRestricted = true;
                failText = "CannotPrioritizeForbiddenOutsideAllowedArea".Translate() +
                          " (" + pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap.Label + ")";
            }
        }
        else if (!target.HasThing && target.Cell.IsForbidden(pawn))
        {
            if (!target.Cell.InAllowedArea(pawn))
            {
                isAreaRestricted = true;
                failText = "CannotPrioritizeForbiddenOutsideAllowedArea".Translate() +
                          " (" + pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap.Label + ")";
            }
        }

        if (!isAreaRestricted)
            return null;

        // Create the bypass option
        string label = target.HasThing
            ? "PrioritizeGeneric".Translate(scanner.PostProcessedGerund(job), target.Thing.Label).CapitalizeFirst()
            : job.def.displayAsAreaInFloatMenu
                ? "PrioritizeGeneric".Translate(scanner.PostProcessedGerund(job), "AreaLower".Translate()).CapitalizeFirst()
                : "PrioritizeGenericSimple".Translate(scanner.PostProcessedGerund(job)).CapitalizeFirst();

        label += " [Bypass Area]"; // Add indicator that this bypasses area restriction

        Action action = delegate
        {
            job.workGiverDef = scanner.def;
            if (pawn.jobs.TryTakeOrderedJobPrioritizedWork(job, scanner, context.ClickedCell))
            {
                if (workGiverDef.forceMote != null)
                    MoteMaker.MakeStaticMote(context.ClickedCell, pawn.Map, workGiverDef.forceMote);
                if (workGiverDef.forceFleck != null)
                    FleckMaker.Static(context.ClickedCell, pawn.Map, workGiverDef.forceFleck);
            }
        };

        FloatMenuOption option = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action), pawn, target);

        if (pawn.Drafted && workGiverDef.autoTakeablePriorityDrafted != -1)
        {
            option.autoTakeable = true;
            option.autoTakeablePriority = workGiverDef.autoTakeablePriorityDrafted;
        }

        return option;
    }
}