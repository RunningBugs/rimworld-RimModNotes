using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;
using System;

namespace PDEDontStarve;

public class LanternWeapon : MinifiedThing
{
    protected override Graphic LoadCrateFrontGraphic()
    {
        return GraphicDatabase.Get<Graphic_Single>("PDEDontStarve/CrateFrontTransparent", ShaderDatabase.Cutout, GetMinifiedDrawSize(InnerThing.def.size.ToVector2(), 1.1f) * 1.16f, Color.white);
    }

    public override void PostMake()
    {
        base.PostMake();
        if (InnerThing == null)
        {
            InnerThing = ThingMaker.MakeThing(ThingDefs.Building_Lantern, Stuff);
        }
    }

    public override void Notify_Equipped(Pawn pawn)
    {
        base.Notify_Equipped(pawn);
    }
}


public class CompProperties_HasLightBulb : CompProperties
{
    public FleckDef lightDef;
    public CompProperties_HasLightBulb()
    {
        compClass = typeof(CompHasLightBulb);
    }
}


public class CompHasLightBulb : ThingComp
{
    private CompProperties_HasLightBulb Props => (CompProperties_HasLightBulb)props;

    private CompGlower compGlower;

    private bool ShouldBeLitNow
    {
        get
        {
            if (!(ParentHolder is Pawn_EquipmentTracker) && !(ParentHolder is Pawn_ApparelTracker))
            {
                return false;
            }
            if (!FlickUtility.WantsToBeOn(parent))
            {
                return false;
            }
            if (parent is IThingGlower thingGlower && !thingGlower.ShouldBeLitNow())
            {
                return false;
            }
            ThingWithComps thingWithComps;
            if ((thingWithComps = parent) != null)
            {
                foreach (ThingComp allComp in thingWithComps.AllComps)
                {
                    if (allComp is IThingGlower thingGlower2 && !thingGlower2.ShouldBeLitNow())
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }

    public override void CompTick()
    {
        base.CompTick();

        if (ShouldBeLitNow)
        {
            Pawn pawn = null;
            if (ParentHolder is Pawn_EquipmentTracker equipmentTracker)
            {
                pawn = equipmentTracker.pawn;
            }
            else if (ParentHolder is Pawn_ApparelTracker apparelTracker)
            {
                pawn = apparelTracker.pawn;
            }

            if (pawn != null && pawn.Map != null)
            {
                FleckMaker.Static(pawn.DrawPos, pawn.Map, Props.lightDef);
            }
        }
    }
}


public class CompProperties_LanternFeulTracker : CompProperties
{
    public Type compType;
    public CompProperties_LanternFeulTracker()
    {
        compClass = typeof(CompLanternFeulTracker);
    }
}

public class CompLanternFeulTracker : ThingComp
{
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        var gizmo = new Gizmo_AmoutTracker
        {
            comp = this,
            Label = parent.LabelCap,
            Tooltip = "Amount of fuel in the lantern"
        };
        gizmo.SetAmountFunc(comp =>
        {
            Thing thing;
            if (comp.parent is MinifiedThing mthing)
            {
                thing = mthing.InnerThing;
            }
            else
            {
                thing = comp.parent;
            }
            foreach (var tcomp in (thing as ThingWithComps).AllComps)
            {
                if (tcomp is CompRefuelable refuelable)
                {
                    return refuelable.Fuel;
                }
            }
            return 0f;
        });
        gizmo.SetMaxAmountFunc(comp =>
        {
            Thing thing;
            if (comp.parent is MinifiedThing mthing)
            {
                thing = mthing.InnerThing;
            }
            else
            {
                thing = comp.parent;
            }
            foreach (var tcomp in (thing as ThingWithComps).AllComps)
            {
                if (tcomp is CompRefuelable refuelable)
                {
                    return refuelable.Props.fuelCapacity;
                }
            }
            return 0f;
        });
        yield return gizmo;
    }
}

[StaticConstructorOnStartup]
public class Gizmo_AmoutTracker : Gizmo
{
    public ThingComp comp;

    private Func<ThingComp, float> amountFunc;

    private Func<ThingComp, float> maxAmountFunc;

    public string Tooltip = null;

    public string Label = null;

    private static readonly Texture2D FullBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.24f));

    private static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(Color.clear);

    public Gizmo_AmoutTracker()
    {
        Order = -100f;
    }

    public void SetAmountFunc(Func<ThingComp, float> amountFunc)
    {
        this.amountFunc = amountFunc;
    }

    public void SetMaxAmountFunc(Func<ThingComp, float> maxAmountFunc)
    {
        this.maxAmountFunc = maxAmountFunc;
    }

    public override float GetWidth(float maxWidth)
    {
        return Mathf.Min(140f, maxWidth);
    }

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        var currentAmount = amountFunc(comp);
        var maxAmount = maxAmountFunc(comp);
        Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
        Rect rect2 = rect.ContractedBy(6f);
        Widgets.DrawWindowBackground(rect);
        Rect rect3 = rect2;
        rect3.height = rect.height / 2f;
        Text.Font = GameFont.Tiny;
        Widgets.Label(rect3, Label ?? comp.parent.LabelCap);
        Rect rect4 = rect2;
        rect4.yMin = rect2.y + rect2.height / 2f;
        float fillPercent = currentAmount / maxAmount;
        Widgets.FillableBar(rect4, fillPercent, FullBarTex, EmptyBarTex, doBorder: false);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect4, (currentAmount * 100f).ToString("F0") + " / " + (maxAmount * 100f).ToString("F0"));
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(rect2, Tooltip);
        return new GizmoResult(GizmoState.Clear);
    }
}

public class CompProperties_CauseHediff_Primary : CompProperties
{
    public HediffDef hediff;
    public BodyPartDef part;
    public CompProperties_CauseHediff_Primary()
    {
        compClass = typeof(CompCauseHediff_Primary);
    }
}

public class CompCauseHediff_Primary : ThingComp
{
    private CompProperties_CauseHediff_Primary Props => (CompProperties_CauseHediff_Primary)props;

    public override void Notify_Equipped(Pawn pawn)
    {
        if (pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediff) == null)
        {
            var hcomp = pawn.health.AddHediff(Props.hediff, pawn.health.hediffSet.GetNotMissingParts().FirstOrFallback((BodyPartRecord p) => p.def == Props.part));
            if (hcomp != null)
            {
                hcomp.TryGetComp<LightSourceHediffComp>().wornPrimary = parent;
            }
        }
    }

    public override void Notify_Unequipped(Pawn pawn)
    {
        var hcomp = pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediff);
        if (hcomp != null)
        {
            hcomp.TryGetComp<LightSourceHediffComp>().wornPrimary = null;
        }
    }
}

public class LightSourceHediffComp : HediffComp
{
    public ThingWithComps wornPrimary;

    public override bool CompShouldRemove => wornPrimary == null;

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_References.Look(ref wornPrimary, "PDE.wornPrimary");
    }

    public override IEnumerable<Gizmo> CompGetGizmos()
    {
        var weapon = Pawn?.equipment?.Primary;
        if (weapon != null)
        {
            foreach (var gizmo in weapon.TryGetComp<CompLanternFeulTracker>()?.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            foreach (var gizmo in weapon.TryGetComp<CompRefuelable>()?.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
        }
    }
}

public class CompGlowUnpausedOrDrafted : ThingComp, IThingGlower
{
    public bool pause = false;

    public bool ShouldBeLitNow()
    {
        var result = ParentHolder is Pawn_EquipmentTracker tracker && tracker.pawn.Drafted || ParentHolder is Pawn_ApparelTracker apparelTracker && apparelTracker.pawn.Drafted;
        return result;
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (parent is MinifiedThing mthing && ParentHolder is not Pawn_EquipmentTracker && ParentHolder is not Pawn_ApparelTracker)
        {
            pause = true;
            yield break;
        }

        foreach (var bgizmo in base.CompGetGizmosExtra())
        {
            yield return bgizmo;
        }

        var gizmo = new Command_Toggle
        {
            defaultLabel = "Pause",
            defaultDesc = "Pause the refuelable",
            icon = null,
            isActive = () => pause,
            toggleAction = () =>
            {
                pause = !pause;
                if (pause)
                {
                    parent.BroadcastCompSignal("PowerTurnedOff");
                }
                else
                {
                    parent.BroadcastCompSignal("PowerTurnedOn");
                }
            }
        };
        yield return gizmo;
    }
}


public class CompPausibleRefuelable : CompRefuelable, IThingGlower
{
    public bool ShouldConsumeFuel
    {
        get
        {
            if (!ShouldBeLitNow())
            {
                return false;
            }

            if (parent is IThingGlower glower && !glower.ShouldBeLitNow())
            {
                return false;
            }

            if (parent is ThingWithComps thingWithComps)
            {
                foreach (var comp in thingWithComps.AllComps)
                {
                    if (comp is IThingGlower glower2 && !glower2.ShouldBeLitNow())
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    public float ConsumptionRatePerTick => Props.fuelConsumptionRate / 60000f;

    public override void CompTick()
    {

        if (ShouldConsumeFuel)
        {
            ConsumeFuel(ConsumptionRatePerTick);
        }
    }
}
