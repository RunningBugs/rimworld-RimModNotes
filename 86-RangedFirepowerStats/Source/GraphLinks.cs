using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RangedFirepowerStats;

// A transient hyperlink target, never registered in DefDatabase or saved. Vanilla
// draws it immediately after the precision table; our activation opens a chart.
public sealed class GraphLinkDef : Def
{
    public Func<DpsGraphWindow> CreateWindow;
}

public static class GraphLinks
{
    public static Dialog_InfoCard.Hyperlink? ForStat(StatDef stat, StatRequest request)
    {
        bool shooterAccuracy = stat == StatDefOf.ShootingAccuracyPawn || stat == StatDefOf.ShootingAccuracyTurret;
        if (shooterAccuracy && request.Thing is Thing shooter && !stat.Worker.IsDisabledFor(shooter))
            return Make(Labels.Text("ShooterGraphLinkName"), () => new DpsGraphWindow(RangeGraphData.ForShooter(shooter)));
        if (stat.Worker is not StatWorker_Dps || stat.defName.Contains("Resource")) return null;
        var model = DpsModel.Create(StatWorker_Dps.Inspected(request));
        if (model?.CanEstimate != true) return null;
        return Make(Labels.Text("GraphLinkName"), () => new DpsGraphWindow(
            RangeGraphData.ForWeapon(model, stat.defName == "RFS_TurretWeapon"), showDps: true));
    }

    private static Dialog_InfoCard.Hyperlink Make(string label, Func<DpsGraphWindow> createWindow) =>
        new(new GraphLinkDef { defName = "RFS_TransientGraphLink", label = label, CreateWindow = createWindow });

    public static bool Draw(Rect rect) => Widgets.ButtonText(rect, Labels.Text("GraphOpen"), false, true,
        Widgets.NormalOptionColor, overrideTextAnchor: TextAnchor.MiddleLeft);
}

[HarmonyPatch(typeof(StatWorker), nameof(StatWorker.GetInfoCardHyperlinks))]
internal static class GraphStatHyperlinksPatch
{
    private static IEnumerable<Dialog_InfoCard.Hyperlink> Postfix(IEnumerable<Dialog_InfoCard.Hyperlink> __result,
        StatRequest statRequest, StatDef ___stat)
    {
        // Keep existing gear/health explanation links, but show the chart first.
        var link = GraphLinks.ForStat(___stat, statRequest);
        if (link.HasValue) yield return link.Value;
        if (__result != null) foreach (var item in __result) yield return item;
    }
}

[HarmonyPatch(typeof(Dialog_InfoCard.Hyperlink), nameof(Dialog_InfoCard.Hyperlink.ActivateHyperlink))]
internal static class ActivateGraphHyperlinkPatch
{
    private static bool Prefix(Dialog_InfoCard.Hyperlink __instance)
    {
        if (__instance.def is not GraphLinkDef link) return true;
        Find.WindowStack.Add(link.CreateWindow());
        return false;
    }
}
