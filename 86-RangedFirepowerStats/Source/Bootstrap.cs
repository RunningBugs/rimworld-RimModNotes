using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RangedFirepowerStats;

public class RangedFirepowerStatsMod : Mod
{
    public RangedFirepowerStatsMod(ModContentPack content) : base(content)
    {
        var harmony = new Harmony("RunningBugs.RangedFirepowerStats");
        harmony.PatchAll();
        Log.Message("[Ranged Firepower Stats] Loaded ranged data entries and charts.");
    }

    public static Command_Action Command(Thing thing) => new()
    {
        defaultLabel = Labels.Text("Open"), defaultDesc = Labels.Text("OpenDescription"),
        icon = TexCommand.Attack, action = () => Find.WindowStack.Add(new DpsWindow(thing))
    };
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
internal static class PawnGizmosPatch
{
    private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
    {
        foreach (var gizmo in __result) yield return gizmo;
        if (DpsModel.Create(__instance) != null) yield return RangedFirepowerStatsMod.Command(__instance);
    }
}

[HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.GetGizmos))]
internal static class TurretGizmosPatch
{
    private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_TurretGun __instance)
    {
        foreach (var gizmo in __result) yield return gizmo;
        if (DpsModel.Create(__instance) != null) yield return RangedFirepowerStatsMod.Command(__instance);
    }
}

[HarmonyPatch(typeof(Dialog_InfoCard), nameof(Dialog_InfoCard.DoWindowContents))]
internal static class InfoCardPatch
{
    private static void Postfix(Rect inRect, Thing ___thing, Def ___def, ThingDef ___stuff)
    {
        Thing inspected = ___thing;
        if (inspected == null && ___def is ThingDef def && (def.IsRangedWeapon || def.building?.turretGunDef != null))
            inspected = def.GetConcreteExample(___stuff);
        if (inspected == null || DpsModel.Create(inspected) == null) return;
        if (Widgets.ButtonText(new Rect(inRect.xMax - 215, inRect.yMax - 35, 205, 30), Labels.Text("Open")))
            Find.WindowStack.Add(new DpsWindow(inspected));
    }
}
