using UnityEngine;
using Verse;

namespace RangedFirepowerStats;

public sealed class DpsGraphWindow : Window
{
    private static readonly Color[] Colors = [new(0.35f, 0.85f, 1f), new(1f, 0.72f, 0.26f), new(0.90f, 0.55f, 1f)];
    public RangeGraphData Data { get; }
    public bool ShowDps { get; private set; }
    public override Vector2 InitialSize => new(Math.Min(1120, UI.screenWidth - 40), Math.Min(800, UI.screenHeight - 40));

    public DpsGraphWindow(RangeGraphData data, bool showDps = false)
    {
        Data = data;
        ShowDps = showDps && data.SupportsDps;
        forcePause = true;
        doCloseX = true;
        doCloseButton = true;
        absorbInputAroundWindow = true;
    }

    public void SetMetric(bool dps) => ShowDps = dps && Data.SupportsDps;

    public override void DoWindowContents(Rect inRect)
    {
        var font = Text.Font;
        var anchor = Text.Anchor;
        try { DrawContents(inRect); }
        finally { Text.Font = font; Text.Anchor = anchor; GUI.color = Color.white; }
    }

    private void DrawContents(Rect rect)
    {
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.Label(new Rect(0, 0, rect.width - 30, 32), Labels.Text("GraphTitle"));
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(0, 38, rect.width, 28), Data.Context);
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(0, 68, rect.width, 55), Data.Conditions);
        Text.Font = GameFont.Small;
        if (!Data.Supported || Data.Points.Count == 0)
        {
            Widgets.Label(new Rect(0, 135, rect.width, 80), Labels.Text("Unsupported"));
            return;
        }

        if (Data.SupportsDps)
        {
            if (Widgets.ButtonText(new Rect(0, 124, 140, 30), (ShowDps ? "" : "✓ ") + Labels.Text("GraphHit"))) SetMetric(false);
            if (Widgets.ButtonText(new Rect(150, 124, 140, 30), (ShowDps ? "✓ " : "") + Labels.Text("GraphDps"))) SetMetric(true);
        }
        else Widgets.Label(new Rect(0, 128, rect.width, 26), Labels.Text("GraphShooter"));

        float legendWidth = rect.width / Data.Scenarios.Count;
        for (int index = 0; index < Data.Scenarios.Count; index++)
        {
            float x = index * legendWidth;
            Widgets.DrawBoxSolid(new Rect(x + 4, 174, 22, 4), Colors[index % Colors.Length]);
            Widgets.Label(new Rect(x + 34, 162, legendWidth - 38, 28), Labels.ScenarioName(Data.Scenarios[index]));
        }

        Rect plot = new(72, 211, rect.width - 96, Math.Max(100, rect.height - 365));
        float xMin = Data.Points[0].Distance;
        float xMax = Math.Max(xMin + 1, Data.Points[^1].Distance);
        float peak = Data.Points.SelectMany(p => ShowDps ? p.Dps : p.Hit).DefaultIfEmpty(0).Max();
        float yMax = ShowDps ? NiceCeiling(peak * 1.05f) : Math.Max(1, NiceCeiling(peak));
        Widgets.DrawBoxSolid(plot, new Color(0.055f, 0.065f, 0.08f));
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(0, plot.y - 21, rect.width, 22), Labels.Text(ShowDps ? "GraphDps" : Data.SupportsDps ? "GraphHit" : "GraphShooter"));
        for (int index = 0; index <= 5; index++)
        {
            float fraction = index / 5f;
            float y = plot.yMax - fraction * plot.height;
            Widgets.DrawLineHorizontal(plot.x, y, plot.width, new Color(0.3f, 0.35f, 0.4f, 0.45f));
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(0, y - 11, 64, 22), ShowDps ? (fraction * yMax).ToString("0.##") : Labels.Percent(fraction * yMax));
            float x = plot.x + fraction * plot.width;
            Widgets.DrawBoxSolid(new Rect(x, plot.y, 1, plot.height), new Color(0.3f, 0.35f, 0.4f, 0.25f));
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(x - 32, plot.yMax + 5, 64, 22), Mathf.Lerp(xMin, xMax, fraction).ToString("0.##"));
        }
        Widgets.Label(new Rect(plot.x, plot.yMax + 29, plot.width, 24), Labels.Text("Distance"));
        Text.Anchor = TextAnchor.UpperLeft;

        Vector2 Project(RangeGraphPoint point, int series) => new(
            plot.x + (point.Distance - xMin) / (xMax - xMin) * plot.width,
            plot.yMax - Mathf.Clamp01((ShowDps ? point.Dps[series] : point.Hit[series]) / yMax) * plot.height);

        if (Event.current.type == EventType.Repaint)
            for (int series = 0; series < Data.Scenarios.Count; series++)
            {
                Color color = Colors[series % Colors.Length];
                for (int index = 1; index < Data.Points.Count; index++)
                    Widgets.DrawLine(Project(Data.Points[index - 1], series), Project(Data.Points[index], series), color, 1.1f);
                if (Data.Points.Count == 1)
                {
                    var point = Project(Data.Points[0], series);
                    Widgets.DrawBoxSolid(new Rect(point.x - 2, point.y - 2, 4, 4), color);
                }
            }

        float readoutY = plot.yMax + 61;
        if (Mouse.IsOver(plot))
        {
            float distance = Mathf.Lerp(xMin, xMax, (Event.current.mousePosition.x - plot.x) / plot.width);
            var nearest = Data.Points.MinBy(p => Math.Abs(p.Distance - distance));
            float x = Project(nearest, 0).x;
            Widgets.DrawBoxSolid(new Rect(x, plot.y, 1, plot.height), new Color(1, 1, 1, 0.5f));
            Widgets.Label(new Rect(0, readoutY, rect.width, 22), Labels.Text("Distance") + ": " + nearest.Distance.ToString("0.##"));
            for (int series = 0; series < Data.Scenarios.Count; series++)
            {
                var point = Project(nearest, series);
                Color color = Colors[series % Colors.Length];
                Widgets.DrawBoxSolid(new Rect(point.x - 3, point.y - 3, 6, 6), color);
                GUI.color = color;
                string value = Data.SupportsDps ? Labels.Text("GraphHoverRow", Labels.ScenarioName(Data.Scenarios[series]),
                    Labels.Percent(nearest.Hit[series]), Labels.Number(nearest.Dps[series])) :
                    Labels.ScenarioName(Data.Scenarios[series]) + ": " + Labels.Percent(nearest.Hit[series]);
                Widgets.Label(new Rect(series * legendWidth, readoutY + 23, legendWidth - 8, 42), value);
            }
            GUI.color = Color.white;
        }
        else Widgets.Label(new Rect(0, readoutY, rect.width, 36), Labels.Text("GraphHover"));
    }

    private static float NiceCeiling(float value)
    {
        if (!float.IsFinite(value) || value <= 0) return 1;
        float magnitude = Mathf.Pow(10, Mathf.Floor(Mathf.Log10(value)));
        return Mathf.Ceil(value / magnitude * 2) / 2 * magnitude;
    }
}
