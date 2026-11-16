using System.Text;
using RimWorld;
using Verse;

namespace RangedFirepowerStats;

public static class Labels
{
    public static string Text(string key, params NamedArgument[] args) => ("RFS_" + key).Translate(args);
    public static string ScenarioName(Scenario scenario) => Text(scenario.ToString());
    public static string Number(float value) => value.ToString("F2");
    public static string Percent(float value) => value.ToStringPercent("F1");
}

public class StatWorker_Dps : StatWorker
{
    private bool Raw => stat.defName.EndsWith("Max");
    private bool Resource => stat.defName.Contains("Resource");
    private bool WeaponOnly => stat.defName == "RFS_TurretWeapon";
    private Scenario Scenario => stat.defName.EndsWith("Trigger") ? Scenario.SpecialistTriggerHappy :
        stat.defName.EndsWith("Specialist") ? Scenario.Specialist : Scenario.Current;

    public static Thing Inspected(StatRequest req) => req.Thing ??
        (req.Def as ThingDef)?.GetConcreteExample(req.StuffDef);

    public override bool ShouldShowFor(StatRequest req)
    {
        var def = req.Def as ThingDef;
        if (stat.defName.StartsWith("RFS_Pawn") && req.Thing is not Pawn) return false;
        if (stat.defName.StartsWith("RFS_Weapon") && def?.IsRangedWeapon != true) return false;
        if ((stat.defName.StartsWith("RFS_Turret") || Resource) &&
            (def?.building?.turretGunDef == null || !typeof(Building_TurretGun).IsAssignableFrom(def.thingClass))) return false;
        if (req.Thing is Pawn p && p.equipment?.Primary?.def.IsRangedWeapon != true) return false;
        var model = DpsModel.Create(Inspected(req));
        return model != null && (!Resource || model.DamagePerResource > 0) &&
            (Scenario == Scenario.Current || model.CanCompare);
    }

    public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
    {
        var model = DpsModel.Create(Inspected(req));
        if (model == null) return 0;
        if (Resource) return model.DamagePerResource * (Raw ? 1 : model.HitChance(model.Best(Scenario).range, Scenario));
        return Raw ? model.RawDps(Scenario) : model.Best(Scenario, weaponOnly: WeaponOnly).dps;
    }

    public override string GetStatDrawEntryLabel(StatDef stat, float value, ToStringNumberSense numberSense,
        StatRequest optionalReq, bool finalized = true)
    {
        var model = DpsModel.Create(Inspected(optionalReq));
        if (model == null || (!Raw && !model.CanEstimate)) return Labels.Text("Unavailable");
        return Labels.Number(value) + (Raw ? "" : " (" + Labels.Text("AtRange", model.Best(Scenario, weaponOnly: WeaponOnly).range.ToString("0.##")) + ")");
    }

    public static string CycleExplanation(DpsModel model, Scenario scenario)
    {
        var t = model.Timing(scenario);
        return Labels.Text("Cycle", Labels.Number(model.Damage), model.Shots, t.warmup, t.burst, t.cooldown,
            t.schedule, Labels.Number(model.RawDps(scenario)), model.JobCadence) + "\n" +
            Labels.Text("Interval", model.IntervalTicks, model.Verb.TicksBetweenBurstShots);
    }

    public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
    {
        var model = DpsModel.Create(Inspected(req));
        if (model == null) return Labels.Text("Unsupported");
        var text = new StringBuilder();
        text.AppendLine(CycleExplanation(model, Scenario));
        text.AppendLine(Labels.Text(model.Pawn == null && model.Turret == null ? "IdealShooter" : "StandardConditions"));
        text.AppendLine(Labels.Text("Limitations"));
        if (model.Turret != null) text.AppendLine(Labels.Text("TurretTiming"));
        if (!model.CanEstimate) { text.AppendLine(Labels.Text("Unsupported")); return text.ToString(); }
        if (Resource) text.AppendLine(Labels.Text("Fuel", Labels.Number(model.DamagePerResource)));
        text.AppendLine(Labels.Text("OpenHint"));
        foreach (var distance in model.Distances())
            text.AppendLine(Labels.Text("StatRow", distance.ToString("0.##"),
                Labels.Number(Resource ? model.DamagePerResource * model.HitChance(distance, Scenario) :
                    model.Dps(distance, Scenario, weaponOnly: WeaponOnly)),
                Labels.Percent(model.HitChance(distance, Scenario, weaponOnly: WeaponOnly))));
        return text.ToString();
    }
}
