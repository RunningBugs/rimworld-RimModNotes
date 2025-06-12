using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using Verse.Noise;
using Verse.Grammar;
using RimWorld;
using RimWorld.Planet;

namespace AutomaticBioferriteHavesting;

class CompProperties_BioferriteHarvester : CompProperties
{
    public float threshold = 60f;
    public CompProperties_BioferriteHarvester()
    {
        compClass = typeof(CompBioferriteHarvester);
    }
}

class CompBioferriteHarvester : ThingComp
{
    private CompProperties_BioferriteHarvester Props => (CompProperties_BioferriteHarvester)props;

    private Building_BioferriteHarvester thing => parent as Building_BioferriteHarvester;

    private bool harvestingEnabled = true;

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        yield return new Command_Toggle
        {
            defaultLabel = "AutoHarvestBioferrite".Translate(),
            defaultDesc = "AutoHarvestBioferriteDesc".Translate(),
            icon = ContentFinder<Texture2D>.Get("UI/Commands/EjectBioferrite"),
            isActive = () => harvestingEnabled,
            toggleAction = delegate
            {
                harvestingEnabled = !harvestingEnabled;
            },
            activateSound = SoundDefOf.Tick_Tiny
        };
    }

    public override void CompTick()
    {
        if (thing.IsHashIntervalTick(250))
        {
            if (thing.containedBioferrite >= Props.threshold && harvestingEnabled)
            {
                Thing t = thing.TakeOutBioferrite();
                if (t != null)
                {
                    GenPlace.TryPlaceThing(t, thing.Position, thing.Map, ThingPlaceMode.Near);
                }
            }
        }
    }
}
