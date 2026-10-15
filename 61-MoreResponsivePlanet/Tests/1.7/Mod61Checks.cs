using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RW17.Validation;
public static class Mod61Checks
{
    public const string PackageId = "RunningBugs.MoreResponsivePlanet";
    public static void Run(Map map)
    {
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MoreResponsivePlanet");
        var patchTargets = new[] { AccessTools.Method(typeof(WorldSelector), "WorldSelectorOnGUI"), AccessTools.Method(typeof(WorldSelector), "HandleWorldClicks"),
            AccessTools.Method(typeof(WorldDragBox), "DragBoxOnGUI"), AccessTools.Method(typeof(WorldInterface), "WorldInterfaceOnGUI") };
        foreach (var target in patchTargets)
            Require(Harmony.GetPatchInfo(target)?.Prefixes.Any(p => p.owner == PackageId && p.PatchMethod.DeclaringType.Assembly == assembly) == true,
                "Actual original-replacement prefix registered on " + target.Name);
        Require(Harmony.GetPatchInfo(AccessTools.Method(typeof(WorldSelector), "Notify_DialogOpened"))?.Postfixes.Any(p => p.owner == PackageId) == true,
            "Actual dialog-close postfix registered.");
        Type drag = assembly.GetType("MoreResponsivePlanet.ImmediateDragBox", true);
        Type processorType = assembly.GetType("MoreResponsivePlanet.ThreadSafeSelectionProcessor", true);
        object processor = processorType.GetProperty("Instance").GetValue(null);
        Type dispatcherType = assembly.GetType("MoreResponsivePlanet.UnityMainThreadDispatcher", true);
        object dispatcher = dispatcherType.GetProperty("Instance").GetValue(null);
        Require(dispatcher is MonoBehaviour behaviour && behaviour.gameObject.activeInHierarchy,
            "The real Waylith runtime has registered the MOD's active dispatcher MonoBehaviour.");
        WorldSelector selector = Find.WorldSelector;
        var previous = selector.SelectedObjects.ToList(); PlanetTile previousTile = selector.SelectedTile;
        var oldId = AccessTools.Field(drag, "_currentDragId").GetValue(null);
        var oldActive = AccessTools.Field(drag, "_isActive").GetValue(null);
        var oldStart = AccessTools.Field(drag, "_startPos").GetValue(null);
        var oldApplied = AccessTools.Field(processorType, "_lastAppliedDragId").GetValue(processor);
        bool oldNativeDrag = selector.dragBox.active;
        var objects = Find.WorldObjects.AllWorldObjects.Where(o => o.SelectableNow).Take(2).ToList();
        Require(objects.Count == 2, "The native generated world supplies two real selectable objects.");
        MethodInfo ownCycle = AccessTools.Method(assembly.GetType("MoreResponsivePlanet.WorldSelector_WorldSelectorOnGUI_Patch"), "SelectFirstOrNextFrom");
        MethodInfo nativeCycle = AccessTools.Method(typeof(WorldSelector), "SelectFirstOrNextFrom");
        MethodInfo prepare = AccessTools.Method(processorType, "PrepareSelectionCommand"), apply = AccessTools.Method(processorType, "ApplySelectionCommandNonBlocking");
        List<WorldObject> SelectList(params WorldObject[] values) => values.ToList();
        void SetSelection(WorldSelector target, IEnumerable<WorldObject> values)
        { target.ClearSelection(); foreach (WorldObject value in values) target.Select(value, playSound: false); }
        try
        {
            foreach (var initial in new[] { SelectList(), SelectList(objects[0]), SelectList(objects[1]), objects.ToList() })
            foreach (PlanetTile tile in new[] { PlanetTile.Invalid, map.Tile })
            {
                var control = new WorldSelector(); SetSelection(control, initial); SetSelection(selector, initial);
                nativeCycle.Invoke(control, new object[] { objects, tile }); ownCycle.Invoke(null, new object[] { selector, objects, tile });
                Require(selector.SelectedObjects.SequenceEqual(control.SelectedObjects) && selector.SelectedTile == control.SelectedTile,
                    "Actual MOD cycle produces the native helper's result for the same real world objects/tile.");
            }
            object incremental = prepare.Invoke(processor, new object[] { objects, SelectList(objects[1]) });
            Type commandType = incremental.GetType();
            Require(!(bool)commandType.GetProperty("ClearAllFirst").GetValue(incremental) &&
                ((List<WorldObject>)commandType.GetProperty("ObjectsToDeselect").GetValue(incremental)).SequenceEqual(new[] { objects[0] }) &&
                ((List<WorldObject>)commandType.GetProperty("ObjectsToSelect").GetValue(incremental)).Count == 0,
                "Actual command preparation retains shared objects and removes only the old member.");
            object replacement = prepare.Invoke(processor, new object[] { SelectList(objects[0]), SelectList(objects[1]) });
            Require((bool)commandType.GetProperty("ClearAllFirst").GetValue(replacement), "A complete actual replacement chooses clear-first.");
            int currentId = (int)oldId + 100;
            AccessTools.Field(drag, "_currentDragId").SetValue(null, currentId);
            AccessTools.Field(processorType, "_lastAppliedDragId").SetValue(processor, currentId - 1);
            SetSelection(selector, objects); apply.Invoke(processor, new object[] { selector, incremental, currentId });
            Require(selector.SelectedObjects.SequenceEqual(new[] { objects[1] }), "The real command applies actual WorldSelector.Deselect on the main thread.");
            apply.Invoke(processor, new object[] { selector, replacement, currentId - 1 });
            Require(selector.SelectedObjects.SequenceEqual(new[] { objects[1] }), "A stale drag result cannot overwrite selection.");
            apply.Invoke(processor, new object[] { selector, replacement, currentId + 1 });
            Require(selector.SelectedObjects.SequenceEqual(new[] { objects[1] }), "A non-current future drag result also cannot overwrite selection.");
            commandType.GetProperty("ObjectsToSelect").SetValue(replacement, SelectList(objects[0]));
            AccessTools.Field(drag, "_currentDragId").SetValue(null, currentId + 1);
            apply.Invoke(processor, new object[] { selector, replacement, currentId + 1 });
            Require(selector.SelectedObjects.SequenceEqual(new[] { objects[0] }), "The newest real clear/select command replaces selection.");
            int caller = Environment.CurrentManagedThreadId, dispatched = -1;
            dispatcherType.GetMethod("Enqueue").Invoke(dispatcher, new object[] { (Action)(() => dispatched = Environment.CurrentManagedThreadId) });
            Require(dispatched == -1, "Actual dispatcher enqueues without executing inline.");
            AccessTools.Method(dispatcherType, "Update").Invoke(dispatcher, null);
            Require(dispatched == caller, "The real dispatcher Update drains its actual ConcurrentQueue on the calling main thread.");
            Vector2 mouse = UI.MousePositionOnUIInverted;
            AccessTools.Field(drag, "_isActive").SetValue(null, true); AccessTools.Field(drag, "_startPos").SetValue(null, mouse - new Vector2(12, 24));
            Rect rect = (Rect)drag.GetMethod("GetCurrentRect").Invoke(null, null);
            Require(rect.width == 12f && rect.height == 24f && (bool)drag.GetMethod("IsValidDrag").Invoke(null, null),
                "Actual drag math uses current native mouse coordinates and meaningful-size boundary.");
            selector.dragBox.active = true; selector.Notify_DialogOpened();
            Require(!(bool)drag.GetProperty("IsActive").GetValue(null) && !selector.dragBox.active,
                "Actual dialog notification ends both MOD and original drag state.");
            Log.Message("[RW17.Validation] Mod61 checks passed: actual_patch_targets, native_registered_dispatcher, " +
                "eight_actual_vs_native_cycle_cases, real_command_prepare_apply, stale_current_guards, actual_queue_main_drain, drag_math, dialog_end");
        }
        finally
        {
            SetSelection(selector, previous); selector.SelectedTile = previousTile;
            AccessTools.Field(drag, "_currentDragId").SetValue(null, oldId); AccessTools.Field(drag, "_isActive").SetValue(null, oldActive);
            AccessTools.Field(drag, "_startPos").SetValue(null, oldStart); AccessTools.Field(processorType, "_lastAppliedDragId").SetValue(processor, oldApplied);
            selector.dragBox.active = oldNativeDrag;
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
