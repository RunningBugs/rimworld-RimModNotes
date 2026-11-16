using RimWorld;
using Verse;

namespace RangedFirepowerStats;

// Independent snapshots let the chart retain exactly the table's conditions and
// values even if the original table is refreshed or another pawn is selected.
public sealed record RangeGraphPoint(float Distance, float[] Hit, float[] Dps);

public sealed class RangeGraphData
{
    public string Context { get; }
    public string Conditions { get; }
    public IReadOnlyList<Scenario> Scenarios { get; }
    public IReadOnlyList<RangeGraphPoint> Points { get; }
    public bool SupportsDps { get; }
    public bool Supported { get; }

    public RangeGraphData(string context, string conditions, IEnumerable<Scenario> scenarios,
        IEnumerable<RangeGraphPoint> points, bool supportsDps, bool supported = true)
    {
        Context = context;
        Conditions = conditions;
        Scenarios = Array.AsReadOnly(scenarios.ToArray());
        Points = Array.AsReadOnly(points.Select(p => new RangeGraphPoint(p.Distance,
            (float[])p.Hit.Clone(), (float[])p.Dps.Clone())).ToArray());
        SupportsDps = supportsDps;
        Supported = supported;
    }

    public static RangeGraphData ForWeapon(DpsModel model, bool weaponOnly = false)
    {
        var scenarios = model.CanCompare ? new[] { Scenario.Current, Scenario.Specialist, Scenario.SpecialistTriggerHappy } : [Scenario.Current];
        var points = model.Distances().Select(distance => new RangeGraphPoint(distance,
            scenarios.Select(s => model.HitChance(distance, s, weaponOnly: weaponOnly)).ToArray(),
            scenarios.Select(s => model.Dps(distance, s, weaponOnly: weaponOnly)).ToArray()));
        return new RangeGraphData(model.Weapon.LabelCap + " · " + (model.Caster?.LabelCap ?? Labels.Text("IdealOption")),
            Labels.Text("GraphReference"), scenarios, points, true, model.CanEstimate);
    }

    public static RangeGraphData ForShooter(Thing shooter)
    {
        var pawn = shooter as Pawn;
        var scenarios = pawn?.RaceProps.Humanlike == true && ProfileStats.SpecialistsAvailable ?
            new[] { Scenario.Current, Scenario.Specialist, Scenario.SpecialistTriggerHappy } : [Scenario.Current];
        var points = Enumerable.Range(1, 55).Select(distance => new RangeGraphPoint(distance,
            scenarios.Select(s => pawn == null ? ShotReport.HitFactorFromShooter(shooter, distance) :
                ProfileStats.HitFactor(pawn, distance, s)).ToArray(), []));
        return new RangeGraphData(shooter.LabelCap, Labels.Text("GraphShooterNote"), scenarios, points, false);
    }
}
