using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RW17.Validation;

public static class Mod76Checks
{
    public const string PackageId = "RunningBugs.ColonyGroupsTargetablePortraits";
    private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException("Mod76: " + reason); }
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, All).SetValue(obj, value);
    public static void Run(Map map)
    {
        var own = LoadedModManager.RunningMods.Single(m => m.PackageId.Equals(PackageId, StringComparison.OrdinalIgnoreCase)).assemblies.loadedAssemblies.Single(a => a.GetName().Name == "CGTargetablePortraits");
        var upstream = LoadedModManager.RunningMods.Single(m => m.PackageId.Equals("DerekBickley.LTOColonyGroupsFinal", StringComparison.OrdinalIgnoreCase)).assemblies.loadedAssemblies.Single(a => a.GetName().Name == "TacticalGroups");
        var prefix = own.GetType("CGTargetablePortraits.ColonistBarTryGetEntryAtPatch", true).GetMethod("Prefix", All);
        var target = typeof(ColonistBar).GetMethod("TryGetEntryAt");
        var installed = Harmony.GetPatchInfo(target)?.Prefixes.SingleOrDefault(p => p.owner == "RunningBugs.ColonyGroupsTargetablePortraits" && p.PatchMethod == prefix);
        Require(installed != null, "specific actual native ColonistBar prefix");
        var utils = upstream.GetType("TacticalGroups.TacticUtils", true);
        var barField = utils.GetField("TacticalColonistBar", All); var groupsField = utils.GetField("tacticalGroups", All);
        var oldBar = barField.GetValue(null); var oldGroups = groupsField.GetValue(null);
        var pawn1 = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        var pawn2 = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        var vanilla = new ColonistBar(); typeof(ColonistBar).GetField("entriesDirty", All).SetValue(vanilla, false);
        vanilla.Entries.Add(new ColonistBar.Entry(pawn1, map, 77)); vanilla.DrawLocs.Add(new Vector2(800, 100));
        try
        {
            // Real upstream classes and lists. Only disposable UI layout data is controlled.
            var barType = upstream.GetType("TacticalGroups.TacticalColonistBar", true); var bar = Activator.CreateInstance(barType); Set(bar, "entriesDirty", false); barField.SetValue(null, bar);
            var entries = (IList)barType.GetProperty("Entries", All).GetValue(bar); var rects = (IList)barType.GetProperty("DrawLocs", All).GetValue(bar);
            var entryType = barType.GetNestedType("Entry", All); var constructor = entryType.GetConstructors().Single(c => c.GetParameters().Length == 5);
            entries.Add(constructor.Invoke(new object[] { pawn1, map, 7, null, null })); entries.Add(constructor.Invoke(new object[] { pawn2, null, 9, null, null }));
            var size = (Vector2)barType.GetProperty("Size", All).GetValue(bar); Require(size.x > 0 && size.y > 0, "real tactical Size");
            rects.Add(new Rect(100, 100, size.x, size.y)); rects.Add(new Rect(100 + size.x + 20, 100, size.x, size.y));
            var position1 = new Vector2(101, 101); var position2 = new Vector2(101 + size.x + 20, 101);
            Require(vanilla.TryGetEntryAt(position1, out var result) && result.pawn == pawn1 && result.map == map && result.group == 7, "actual tactical main entry maps pawn/map/group to native result");
            Require(vanilla.TryGetEntryAt(position2, out result) && result.pawn == pawn2 && result.map == null && result.group == 9, "actual off-map tactical entry preserves null map and group");
            Require(!vanilla.TryGetEntryAt(new Vector2(800, 101), out result) && result.pawn == null && result.map == null, "ready tactical no-hit must suppress unrelated vanilla hit");
            var groupsType = upstream.GetType("TacticalGroups.TacticalGroups", true); var groups = Activator.CreateInstance(groupsType, Find.World); groupsType.GetMethod("PreInit", All).Invoke(groups, null); groupsField.SetValue(null, groups);
            var group = Activator.CreateInstance(upstream.GetType("TacticalGroups.PawnGroup", true));
            ((IList)groupsType.GetField("pawnGroups", All).GetValue(groups)).Add(group);
            var popupRects = (System.Collections.Generic.Dictionary<Pawn, Rect>)group.GetType().GetField("pawnRects", All).GetValue(group);
            popupRects[pawn2] = new Rect(300, 350, 40, 40); Set(group, "scrollPosition", new Vector2(0, 25));
            var popupPoint = new Vector2(301, 326);
            Require(!vanilla.TryGetEntryAt(popupPoint, out result), "inactive real group popup negative");
            Set(group, "pawnWindowIsActive", true);
            Require(vanilla.TryGetEntryAt(popupPoint, out result) && result.pawn == pawn2 && result.map == pawn2.Map && result.group == 0, "actual active popup uses real scrolled rect/pawn map/group0");
            Require(!vanilla.TryGetEntryAt(new Vector2(301, 389), out result), "unscrolled rect tail must not count as scrolled popup hit");
            Set(group, "pawnWindowIsActive", false); Set(group, "showPawnIconsRightClickMenu", true);
            Require(vanilla.TryGetEntryAt(popupPoint, out result) && result.pawn == pawn2, "actual right-click popup branch");
            popupRects[pawn2] = new Rect(100, 125, 40, 40);
            Require(vanilla.TryGetEntryAt(position1, out result) && result.pawn == pawn1 && result.group == 7, "main tactical hit has precedence over overlapping popup");
            Set(group, "showPawnIconsRightClickMenu", false);
            Require(!vanilla.TryGetEntryAt(new Vector2(-100, -100), out result) && result.pawn == null, "no-hit default result");
            barField.SetValue(null, null);
            Require(vanilla.TryGetEntryAt(new Vector2(801, 101), out result) && result.pawn == pawn1 && result.map == map && result.group == 77, "actual null-upstream-bar falls through to original native hit test");
            Require(!vanilla.TryGetEntryAt(position1, out result) && result.pawn == null, "native fallback misses absent native rect");
            // Verify the first positive is caused by this exact patch, then restore it.
            barField.SetValue(null, bar); var harmony = new Harmony(installed.owner);
            try { harmony.Unpatch(target, prefix); Require(!vanilla.TryGetEntryAt(position1, out result), "actual original native hit-test cannot see tactical-only position"); }
            finally { harmony.Patch(target, prefix: new HarmonyMethod(prefix) { priority = installed.priority, before = installed.before, after = installed.after }); }
            Require(vanilla.TryGetEntryAt(position1, out result) && result.pawn == pawn1, "restored precise prefix restores actual tactical mapping");
        }
        finally { barField.SetValue(null, oldBar); groupsField.SetValue(null, oldGroups); if (!pawn1.Destroyed) pawn1.Destroy(); if (!pawn2.Destroyed) pawn2.Destroy(); }
        Log.Message("RW17 Mod76: real TacticalGroups Entry/DrawLocs/Size/main mapping/off-map/group/popup active-rightclick-scroll-overlap/no-hit/nullbar native fallback and original/restored native method passed; physical portrait ability validation/highlight/sound/Shift click/render/save/fullupstream workflow pending");
    }
}
