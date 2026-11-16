using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RangedFirepowerStats;

public sealed class DpsWindow : Window
{
    private readonly Thing inspected;
    private Pawn shooter;
    private Thing target;
    private bool targetRoofed;
    private Vector2 scroll;
    private DpsModel model;
    private Scenario[] scenarios = [];
    private readonly List<(float range, float[] chance, float[] dps)> rows = new();
    private string weather;
    private string[] cycles = [];
    private float?[] targetChances = [];
    public override Vector2 InitialSize => new(Math.Min(1120, UI.screenWidth - 40), Math.Min(860, UI.screenHeight - 40));

    public DpsWindow(Thing inspected)
    {
        this.inspected = inspected;
        shooter = inspected as Pawn ?? Find.Selector?.SelectedObjects.OfType<Pawn>()
            .FirstOrDefault(p => !StatDefOf.ShootingAccuracyPawn.Worker.IsDisabledFor(p));
        if (shooter == null && inspected is not Building_TurretGun)
            shooter = Find.CurrentMap?.mapPawns.FreeColonistsSpawned.FirstOrDefault(p =>
                p.equipment?.Primary != null && !StatDefOf.ShootingAccuracyPawn.Worker.IsDisabledFor(p));
        forcePause = true;
        doCloseX = true;
        doCloseButton = true;
        absorbInputAroundWindow = true;
        resizeable = false;
        Refresh();
    }

    public void Refresh()
    {
        model = DpsModel.Create(inspected, shooter);
        rows.Clear();
        if (model == null) return;
        scenarios = model.CanCompare ? [Scenario.Current, Scenario.Specialist, Scenario.SpecialistTriggerHappy] : [Scenario.Current];
        weather = model.Caster?.Spawned == true ? model.Caster.Map.weatherManager.curWeather.LabelCap : Labels.Text("NoMap");
        cycles = scenarios.Select(s => Labels.ScenarioName(s) + ": " + StatWorker_Dps.CycleExplanation(model, s).Split('\n')[0]).ToArray();
        foreach (float distance in model.Distances())
        {
            var chance = scenarios.Select(s => model.HitChance(distance, s, weather: true, targetRoofed: targetRoofed)).ToArray();
            rows.Add((distance, chance, scenarios.Select((s, index) => model.RawDps(s) * chance[index]).ToArray()));
        }
        targetChances = scenarios.Select(s => model.TargetHitChance(target, s)).ToArray();
    }

    private void SelectShooter()
    {
        var options = new List<FloatMenuOption>();
        if (inspected is not Pawn) options.Add(new FloatMenuOption(Labels.Text("IdealOption"), () => { shooter = null; Refresh(); }));
        var map = shooter?.MapHeld ?? inspected.MapHeld ?? Find.CurrentMap;
        if (map != null)
            foreach (var pawn in map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Humanlike &&
                !StatDefOf.ShootingAccuracyPawn.Worker.IsDisabledFor(p)).OrderBy(p => p.LabelShort))
            {
                var selected = pawn;
                if (inspected is Pawn && pawn.equipment?.Primary?.def.IsRangedWeapon != true) continue;
                options.Add(new FloatMenuOption(pawn.LabelShortCap, () =>
                {
                    if (inspected is Pawn) Find.WindowStack.Add(new DpsWindow(selected));
                    else { shooter = selected; Refresh(); }
                }));
            }
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private void SelectTarget()
    {
        if (model?.Caster?.Spawned != true) return;
        var options = new List<FloatMenuOption> { new(Labels.Text("ClearTarget"), () => { target = null; Refresh(); }) };
        foreach (var pawn in model.Caster.Map.mapPawns.AllPawnsSpawned.Where(p => p != model.Caster)
            .OrderBy(p => p.Position.DistanceToSquared(model.Caster.Position)))
        {
            var selected = pawn;
            options.Add(new FloatMenuOption(pawn.LabelShortCap + " (" +
                (pawn.Position - model.Caster.Position).LengthHorizontal.ToString("F1") + ")",
                () => { target = selected; Refresh(); }));
        }
        Find.WindowStack.Add(new FloatMenu(options));
    }

    public string ExportTsv()
    {
        if (model == null) return Labels.Text("Unsupported");
        var text = new StringBuilder();
        text.AppendLine(Labels.Text("Title") + "\t" + model.Weapon.LabelCap + "\t" + (model.Caster?.LabelCap ?? Labels.Text("IdealOption")));
        text.AppendLine(Labels.Text("Weather", weather, Labels.Percent(model.WeatherFactor(targetRoofed))));
        text.AppendLine(Labels.Text("TableAssumptions"));
        foreach (var cycle in cycles) text.AppendLine(cycle);
        text.Append(Labels.Text("Distance"));
        foreach (var scenario in scenarios) text.Append('\t').Append(Labels.ScenarioName(scenario) + " " + Labels.Text("Hit")).Append('\t').Append(Labels.ScenarioName(scenario) + " DPS");
        text.AppendLine();
        foreach (var row in rows)
        {
            text.Append(row.range.ToString("0.##"));
            for (int i = 0; i < scenarios.Length; i++)
                text.Append('\t').Append(model.CanEstimate ? Labels.Percent(row.chance[i]) : Labels.Text("Unavailable"))
                    .Append('\t').Append(model.CanEstimate ? Labels.Number(row.dps[i]) : Labels.Text("Unavailable"));
            text.AppendLine();
        }
        return text.ToString();
    }

    public DpsGraphWindow OpenGraph()
    {
        if (model == null) return null;
        var data = new RangeGraphData(model.Weapon.LabelCap + " · " + (model.Caster?.LabelCap ?? Labels.Text("IdealOption")),
            Labels.Text("Weather", weather, Labels.Percent(model.WeatherFactor(targetRoofed))) + "\n" +
                Labels.Text("GraphSnapshot", Labels.Text(targetRoofed ? "GraphRoofed" : "GraphUnroofed")),
            scenarios, rows.Select(row => new RangeGraphPoint(row.range, row.chance, row.dps)), true, model.CanEstimate);
        var graph = new DpsGraphWindow(data);
        Find.WindowStack.Add(graph);
        return graph;
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (model == null) { Widgets.Label(inRect, Labels.Text("Unsupported")); return; }
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0, 0, inRect.width - 30, 32), Labels.Text("Title"));
        Text.Font = GameFont.Small;
        float y = 40;
        if (model.Turret == null && Widgets.ButtonText(new Rect(0, y, inRect.width * 0.46f, 30),
            Labels.Text("Shooter", model.Pawn?.LabelShortCap ?? Labels.Text("IdealOption")))) SelectShooter();
        Widgets.Label(new Rect(inRect.width * 0.48f, y, inRect.width * 0.52f, 30),
            Labels.Text("Weather", weather, Labels.Percent(model.WeatherFactor(targetRoofed))));
        y += 36;
        Widgets.Label(new Rect(0, y, inRect.width, 25), Labels.Text("Weapon", model.Weapon.LabelCap));
        y += 27;
        bool previous = targetRoofed;
        Widgets.CheckboxLabeled(new Rect(0, y, inRect.width * 0.44f, 28), Labels.Text("TargetRoof"), ref targetRoofed);
        if (targetRoofed != previous) Refresh();
        if (Widgets.ButtonText(new Rect(inRect.width - 244, y, 116, 28), Labels.Text("Refresh"))) Refresh();
        if (Widgets.ButtonText(new Rect(inRect.width - 120, y, 116, 28), Labels.Text("Copy")))
        {
            GUIUtility.systemCopyBuffer = ExportTsv();
            Messages.Message(Labels.Text("Copied"), MessageTypeDefOf.TaskCompletion, false);
        }
        y += 36;
        string assumptions = Labels.Text(model.CanEstimate ? "TableAssumptions" : "Unsupported");
        if (model.Pawn == null && model.Turret == null) assumptions += " " + Labels.Text("IdealShooter");
        if (model.Pawn?.RaceProps.Humanlike == true && !ProfileStats.SpecialistsAvailable) assumptions += " " + Labels.Text("NeedIdeology");
        float height = Text.CalcHeight(assumptions, inRect.width);
        Widgets.Label(new Rect(0, y, inRect.width, height), assumptions);
        y += height + 8;
        Text.Font = GameFont.Tiny;
        foreach (var cycle in cycles)
        {
            height = Text.CalcHeight(cycle, inRect.width);
            Widgets.Label(new Rect(0, y, inRect.width, height), cycle);
            y += height + 3;
        }
        Text.Font = GameFont.Small;
        if (model.Caster?.Spawned == true)
        {
            y += 5;
            if (Widgets.ButtonText(new Rect(0, y, 175, 28), Labels.Text("SelectTarget"))) SelectTarget();
            string result = target?.Spawned == true ? target.LabelShortCap + " " +
                (target.Position - model.Caster.Position).LengthHorizontal.ToString("F1") + ": " +
                string.Join(" | ", scenarios.Select((s, i) => Labels.ScenarioName(s) + " " +
                    (targetChances[i].HasValue ? Labels.Number(model.RawDps(s) * targetChances[i].Value) : Labels.Text("Unavailable")))) : Labels.Text("TargetHint");
            Text.Font = GameFont.Tiny;
            height = Math.Max(30, Text.CalcHeight(result, inRect.width - 185));
            Widgets.Label(new Rect(185, y, inRect.width - 185, height), result);
            Text.Font = GameFont.Small;
            y += height + 8;
        }
        if (GraphLinks.Draw(new Rect(0, y, 200, 27))) OpenGraph();
        y += 31;
        float width = inRect.width - 20;
        float col = (width - 75) / scenarios.Length;
        Widgets.DrawMenuSection(new Rect(0, y, width, 46));
        Widgets.Label(new Rect(8, y + 10, 65, 30), Labels.Text("Distance"));
        for (int i = 0; i < scenarios.Length; i++)
        {
            float x = 75 + i * col;
            Widgets.Label(new Rect(x + 5, y + 2, col - 10, 25), Labels.ScenarioName(scenarios[i]));
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(x + 5, y + 25, col * 0.46f, 20), Labels.Text("Hit"));
            Widgets.Label(new Rect(x + col * 0.49f, y + 25, col * 0.49f, 20), "DPS");
            Text.Font = GameFont.Small;
        }
        y += 48;
        Rect outer = new(0, y, inRect.width, Math.Max(20, inRect.height - y - 90));
        Rect view = new(0, 0, width, rows.Count * 27);
        Widgets.BeginScrollView(outer, ref scroll, view);
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            float rowY = rowIndex * 27;
            if (rowY + 27 < scroll.y || rowY > scroll.y + outer.height) continue;
            if (rowIndex % 2 == 0) Widgets.DrawLightHighlight(new Rect(0, rowY, width, 27));
            Widgets.Label(new Rect(8, rowY + 2, 65, 25), row.range.ToString("0.##"));
            for (int i = 0; i < scenarios.Length; i++)
            {
                float x = 75 + i * col;
                Widgets.Label(new Rect(x + 5, rowY + 2, col * 0.46f, 25), model.CanEstimate ? Labels.Percent(row.chance[i]) : "—");
                Widgets.Label(new Rect(x + col * 0.49f, rowY + 2, col * 0.49f, 25), model.CanEstimate ? Labels.Number(row.dps[i]) : "—");
            }
        }
        Widgets.EndScrollView();
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(0, inRect.height - 83, inRect.width, 43), Labels.Text(model.Turret == null ? "Limitations" : "TurretTiming"));
        Text.Font = GameFont.Small;
    }
}
