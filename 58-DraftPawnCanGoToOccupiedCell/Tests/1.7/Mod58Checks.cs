using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod58Checks
{
    public const string PackageId = "RunningBugs.FreeGoTo";

    public static void Run(Map map)
    {
        foreach (var target in new[]
        {
            AccessTools.Method(typeof(Selector), "HandleMapClicks"),
            AccessTools.Method(typeof(Selector), "HandleMultiselectGoto"),
            AccessTools.Method(typeof(Selector), "SelectorOnGUI_BeforeMainTabs"),
            AccessTools.Method(typeof(Selector), "ClearSelection"),
            AccessTools.Method(typeof(MapInterface), "MapInterfaceUpdate")
        })
        {
            Require(target != null, "A real patch target exists.");
            var info = Harmony.GetPatchInfo(target);
            Require(info != null && info.Prefixes.Concat(info.Postfixes).Any(p =>
                p.PatchMethod.DeclaringType?.Assembly.GetName().Name == "FreeGoTo"),
                "Every actual target has this MOD's registered patch.");
        }
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "FreeGoTo");
        var inputPatch = assembly.GetType("Selector_HandleMapClicks_Patch");
        foreach (string name in new[] { "SelectAllMatchingObjectUnderMouseOnScreen", "HandleMultiselectGoto",
            "SelectUnderMouse", "SelectInsideDragBox" })
        {
            var cached = (System.Reflection.MethodInfo)inputPatch.GetField("m_" + name,
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
            Require(cached != null && cached.DeclaringType == typeof(Selector) && cached.Name == name &&
                cached.GetParameters().Length == (name == "HandleMultiselectGoto" ? 1 : 0),
                "The MOD's actual cached private reflection method has the expected signature.");
        }
        object controller = assembly.GetType("PatchAll").GetField("singlePawnGotoController").GetValue(null);
        var type = controller.GetType();
        bool Active() => (bool)type.GetProperty("Active").GetValue(controller);
        IntVec3 actorOffset = new(6, 0, 0);
        IntVec3 cell = CellRect.CenteredOn(map.Center, 20).FirstOrDefault(c => c.InBounds(map) &&
            c.Standable(map) && !c.Fogged(map) && c.GetThingList(map).Count == 0 &&
            (c + actorOffset).InBounds(map) && (c + actorOffset).Standable(map) &&
            !(c + actorOffset).Fogged(map) && (c + actorOffset).GetThingList(map).Count == 0, IntVec3.Invalid);
        Require(cell.IsValid, "A disposable occupied-cell fixture location exists.");
        Pawn actor = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        Pawn occupant = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        try
        {
            GenSpawn.Spawn(actor, cell + actorOffset, map);
            GenSpawn.Spawn(occupant, cell, map);
            actor.drafter.Drafted = true;
            type.GetMethod("StartInteraction").Invoke(controller, new object[] { actor, cell });
            Require(Active(), "Starting the actual single-pawn controller activates it.");
            new Selector().ClearSelection();
            Require(!Active(), "The real ClearSelection postfix cancels this controller.");
            Require(cell.GetFirstPawn(map) == occupant, "The requested cell is physically occupied.");
            Require(actor.CanReach(cell, Verse.AI.PathEndMode.OnCell, Danger.Deadly),
                "The requested occupied location is actually reachable.");
            IntVec3 expected = RCellFinder.BestOrderedGotoDestNear(cell, actor);
            Require(expected.IsValid && expected != cell && expected != actor.Position,
                "Native collision rules select a real nearby destination for this same-faction human occupant.");
            type.GetMethod("StartInteraction").Invoke(controller, new object[] { actor, cell });
            Require((IntVec3)type.GetField("end", System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance).GetValue(controller) == cell,
                "The actual controller accepts the occupied cell as its drag target.");
            type.GetMethod("FinalizeInteraction").Invoke(controller, null);
            Require(!Active(), "Finalizing deactivates the controller.");
            Require(actor.CurJobDef == JobDefOf.Goto && actor.CurJob.targetA.Cell == expected,
                "Finalizing an occupied drag target creates the actual Goto job at the native-selected destination.");
            Log.Message("[RW17.Validation] Mod58 occupied target=" + cell + ", native destination=" + expected);
            occupant.DeSpawn();
            Require(RCellFinder.BestOrderedGotoDestNear(cell, actor) == cell,
                "Removing the occupant makes the exact original cell a valid native destination.");
            type.GetMethod("StartInteraction").Invoke(controller, new object[] { actor, cell });
            type.GetMethod("FinalizeInteraction").Invoke(controller, null);
            Require(actor.CurJobDef == JobDefOf.Goto && actor.CurJob.targetA.Cell == cell && !Active(),
                "The same actual controller issues an exact-cell Goto for the vacant control.");
            Log.Message("[RW17.Validation] Mod58 checks passed: five_patches_registered, reflection_caches_resolved, " +
                "controller_activated, clear_selection_cancels, occupied_drag_target, native_destination, " +
                "goto_job, vacant_original_cell, finalized_inactive");
        }
        finally
        {
            type.GetMethod("Deactivate").Invoke(controller, null);
            if (!occupant.Destroyed) occupant.Destroy(DestroyMode.Vanish);
            if (!actor.Destroyed) actor.Destroy(DestroyMode.Vanish);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
