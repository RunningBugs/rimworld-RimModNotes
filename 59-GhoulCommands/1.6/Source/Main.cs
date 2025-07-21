using Verse;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse.AI;

namespace GhoulCommands;

public class GhoulCommandsSettings : ModSettings
{
    public bool enableJuggernautSerumToggle = true;
    public bool enableMetalbloodSerumToggle = false;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref enableJuggernautSerumToggle, "GhoulCommands.enableJuggernautSerumToggle", true);
        Scribe_Values.Look(ref enableMetalbloodSerumToggle, "GhoulCommands.enableMetalbloodSerumToggle", false);
    }
}

public class GhoulCommandsModUI : Mod
{
    public static GhoulCommandsSettings Settings;
    public GhoulCommandsModUI(ModContentPack content) : base(content)
    {
        Settings = GetSettings<GhoulCommandsSettings>();
    }

    public override string SettingsCategory() => "GhoulCommands".Translate();

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new Listing_Standard();
        listing.Begin(inRect);

        listing.CheckboxLabeled("GhoulCommands.EnableJuggernautSerumText".Translate(), ref Settings.enableJuggernautSerumToggle, "GhoulCommands.EnableJuggernautSerumDesc".Translate());
        listing.CheckboxLabeled("GhoulCommands.EnableMetalbloodSerumText".Translate(), ref Settings.enableMetalbloodSerumToggle, "GhoulCommands.EnableMetalbloodSerumDesc".Translate());

        listing.End();
    }
}

public class CompProperties_GhoulCommands : CompProperties
{
    public CompProperties_GhoulCommands()
    {
        compClass = typeof(CompGhoulCommands);
    }
}

public class CompGhoulCommands : ThingComp, IExposable
{
    public bool ghoulTakingJuggernautSerum = false;
    public bool ghoulTakingMetalbloodSerum = false;

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (parent is Pawn pawn && pawn.IsColonySubhumanPlayerControlled && pawn.IsGhoul)
        {
            if (GhoulCommandsModUI.Settings.enableJuggernautSerumToggle)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "GhoulCommands.ToggleJuggernautSerumText".Translate(),
                    defaultDesc = "GhoulCommands.ToggleJuggernautSerumDesc".Translate(),
                    icon = DefDatabase<ThingDef>.GetNamed("JuggernautSerum").uiIcon,
                    isActive = () => ghoulTakingJuggernautSerum,
                    toggleAction = () =>
                    {
                        ghoulTakingJuggernautSerum = !ghoulTakingJuggernautSerum;
                    }
                };
            }

            if (GhoulCommandsModUI.Settings.enableMetalbloodSerumToggle)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "GhoulCommands.ToggleMetalbloodSerumText".Translate(),
                    defaultDesc = "GhoulCommands.ToggleMetalbloodSerumDesc".Translate(),
                    icon = DefDatabase<ThingDef>.GetNamed("MetalbloodSerum").uiIcon,
                    isActive = () => ghoulTakingMetalbloodSerum,
                    toggleAction = () =>
                    {
                        ghoulTakingMetalbloodSerum = !ghoulTakingMetalbloodSerum;
                    }
                };
            }
        }
        else
        {
            yield break;
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref ghoulTakingJuggernautSerum, "ghoulTakingJuggernautSerum", false);
        Scribe_Values.Look(ref ghoulTakingMetalbloodSerum, "ghoulTakingMetalbloodSerum", false);
    }
}

[DefOf]
public static class GhoulCommandsDefOf
{
    public static JobDef TakeJuggernautSerum;
    public static JobDef TakeMetalbloodSerum;
}

public class JobDriver_TakeJuggernautSerum : JobDriver
{
    /// <summary>
    /// Form A => pawn
    /// B => serum
    /// </summary>
    private Thing IngestibleSource => job.GetTarget(TargetIndex.B).Thing;

    private float ChewDurationMultiplier
	{
		get
		{
			Thing ingestibleSource = IngestibleSource;
			if (ingestibleSource.def.ingestible != null && !ingestibleSource.def.ingestible.useEatingSpeedStat)
			{
				return 1f;
			}
			return 1f / pawn.GetStatValue(StatDefOf.EatingSpeed);
		}
	}
    private const TargetIndex SerumInd = TargetIndex.B;
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(SerumInd);
        this.FailOnBurningImmobile(SerumInd);
        this.FailOnForbidden(SerumInd);

        yield return Toils_Goto.GotoThing(SerumInd, PathEndMode.Touch)
            .FailOnSomeonePhysicallyInteracting(SerumInd);

        yield return Toils_Ingest.ChewIngestible(pawn, ChewDurationMultiplier, SerumInd).FailOn((Toil x) =>
        {
            return !IngestibleSource.Spawned && (pawn.carryTracker == null || pawn.carryTracker.CarriedThing != IngestibleSource);
        }).FailOnCannotTouch(SerumInd, PathEndMode.Touch);

        // Toil to use the serum immediately after picking it up
        yield return Toils_Ingest.FinalizeIngest(pawn, SerumInd);
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.GetTarget(SerumInd), job, 1, -1, null, errorOnFailed);
    }
}

public class JobGiver_TakeJuggernautSerum : ThinkNode_JobGiver
{
    private static ThingDef JuggerSerumDef => DefDatabase<ThingDef>.GetNamed("JuggernautSerum", true);
    private static HediffDef JuggerHediffDef => DefDatabase<HediffDef>.GetNamed("JuggernautSerum", true);

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!pawn.Spawned || !pawn.IsGhoul || !pawn.IsColonySubhumanPlayerControlled ||
            !GhoulCommandsModUI.Settings.enableJuggernautSerumToggle ||
            pawn.TryGetComp<CompGhoulCommands>()?.ghoulTakingJuggernautSerum != true ||
            pawn.health.hediffSet.HasHediff(JuggerHediffDef))
        {
            return null;
        }

        Thing serum = GenClosest.ClosestThingReachable(
            pawn.Position,
            pawn.Map,
            ThingRequest.ForDef(JuggerSerumDef),
            PathEndMode.ClosestTouch,
            TraverseParms.For(pawn),
            9999f,
            t => t.Spawned && !t.IsForbidden(pawn) && pawn.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Deadly));

        if (serum == null)
        {
            return null;
        }

        return JobMaker.MakeJob(GhoulCommandsDefOf.TakeJuggernautSerum, pawn, serum);
    }
}

public class JobDriver_TakeMetalbloodSerum : JobDriver
{
    /// <summary>
    /// Form A => pawn
    /// B => serum
    /// </summary>
    private Thing IngestibleSource => job.GetTarget(TargetIndex.B).Thing;

    private float ChewDurationMultiplier
	{
		get
		{
			Thing ingestibleSource = IngestibleSource;
			if (ingestibleSource.def.ingestible != null && !ingestibleSource.def.ingestible.useEatingSpeedStat)
			{
				return 1f;
			}
			return 1f / pawn.GetStatValue(StatDefOf.EatingSpeed);
		}
	}
    private const TargetIndex SerumInd = TargetIndex.B;
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(SerumInd);
        this.FailOnBurningImmobile(SerumInd);
        this.FailOnForbidden(SerumInd);

        yield return Toils_Goto.GotoThing(SerumInd, PathEndMode.Touch)
            .FailOnSomeonePhysicallyInteracting(SerumInd);

        yield return Toils_Ingest.ChewIngestible(pawn, ChewDurationMultiplier, SerumInd).FailOn((Toil x) =>
        {
            return !IngestibleSource.Spawned && (pawn.carryTracker == null || pawn.carryTracker.CarriedThing != IngestibleSource);
        }).FailOnCannotTouch(SerumInd, PathEndMode.Touch);

        // Toil to use the serum immediately after picking it up
        yield return Toils_Ingest.FinalizeIngest(pawn, SerumInd);
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.GetTarget(SerumInd), job, 1, -1, null, errorOnFailed);
    }
}

public class JobGiver_TakeMetalbloodSerum : ThinkNode_JobGiver
{
    private static ThingDef MetalbloodSerumDef => DefDatabase<ThingDef>.GetNamed("MetalbloodSerum", true);
    private static HediffDef MetalbloodHediffDef => DefDatabase<HediffDef>.GetNamed("Metalblood", true);

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!pawn.Spawned || !pawn.IsGhoul || !pawn.IsColonySubhumanPlayerControlled ||
            !GhoulCommandsModUI.Settings.enableMetalbloodSerumToggle ||
            pawn.TryGetComp<CompGhoulCommands>()?.ghoulTakingMetalbloodSerum != true ||
            pawn.health.hediffSet.HasHediff(MetalbloodHediffDef))
        {
            return null;
        }

        Thing serum = GenClosest.ClosestThingReachable(
            pawn.Position,
            pawn.Map,
            ThingRequest.ForDef(MetalbloodSerumDef),
            PathEndMode.ClosestTouch,
            TraverseParms.For(pawn),
            9999f,
            t => t.Spawned && !t.IsForbidden(pawn) && pawn.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Deadly));

        if (serum == null)
        {
            return null;
        }

        return JobMaker.MakeJob(GhoulCommandsDefOf.TakeMetalbloodSerum, pawn, serum);
    }
}
