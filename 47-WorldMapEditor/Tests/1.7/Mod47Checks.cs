using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RW17.Validation;

public static class Mod47Checks
{
    public const string PackageId = "RunningBugs.WorldMapEditor";
    private static IList pool;
    private static Type invisibleType;
    private static bool desiredMode;
    private static int generations;
    private static int spawnCalls;
    private static int postAddCalls;
    private static int refreshCalls;
    private static bool SkipNativeControls() => false;
    private static bool SetControlledIcon(ref bool toggleable) { toggleable = desiredMode; return false; }
    private static bool ObserveRefresh() { refreshCalls++; return false; }
    private static void ObserveSpawn(WorldObject __instance) { if (__instance.GetType() == invisibleType) spawnCalls++; }
    private static void ObservePostAdd(WorldObject __instance) { if (__instance.GetType() == invisibleType) postAddCalls++; }
    private static void ValidateFullGenerationThenLimitRegistration()
    {
        var all = pool.Cast<WorldObject>().ToArray();
        Require(all.Length == Find.WorldGrid.TilesCount && all.Select(o => o.Tile).Distinct().Count() == all.Length, "Actual full generation accumulated old objects or duplicate tiles");
        Require(all.All(o => o.Tile.Layer == Find.WorldGrid.Surface && !o.Spawned), "Generation must create unregistered surface objects");
        generations++;
        // Keep official Add work bounded. Full all-tiles generator above is real; population/performance registration is a separate pending case.
        while (pool.Count > 32) pool.RemoveAt(pool.Count - 1);
    }

    public static void Run(Map map)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "WorldMapEditor");
        invisibleType = assembly.GetType("WorldMapEditor.InvisibleWorldObject", true);
        var utils = assembly.GetType("WorldMapEditor.WorldMapEditorUtils", true);
        var toggleType = assembly.GetType("WorldMapEditor.ToggleIconPatcher", true);
        var edit = toggleType.GetField("InEditMode");
        pool = (IList)toggleType.GetField("invisibleWorldObjects").GetValue(null);
        var oldPool = pool.Cast<object>().ToArray(); var oldEdit = edit.GetValue(null);
        var holder = Find.WorldObjects; var oldObjects = holder.AllWorldObjects.ToArray();
        Require(!oldObjects.Any(o => o.GetType() == invisibleType) && !(bool)oldEdit && pool.Count == 0, "Actual FinalizeInit must leave editor off with no retained generated objects");
        var oldSelected = Find.WorldSelector.SelectedObjects.ToArray(); var oldTile = Find.WorldSelector.SelectedTile; var oldLayer = Find.WorldSelector.SelectedLayer;
        var oldWindows = Find.WindowStack.Windows.ToArray(); var oldEvent = Event.current;
        var factionTextures = Find.FactionManager.AllFactions.Select(f => f.def).Distinct().ToDictionary(d => d, d => d.settlementTexturePath);
        var oldSettlementTexture = WorldObjectDefOf.Settlement.texture;
        var target = typeof(PlaySettings).GetMethod("DoPlaySettingsGlobalControls");
        var generate = utils.GetMethod("GenerateInvisibleWorldObjects");
        var refresh = typeof(WorldRenderer).GetMethod("RegenerateAllLayersNow");
        var icon = typeof(WidgetRow).GetMethod("ToggleableIcon");
        var spawn = typeof(WorldObject).GetMethod("SpawnSetup"); var postAdd = typeof(WorldObject).GetMethod("PostAdd");
        var observer = new Harmony("RW17.Validation.Mod47");
        var owned = new List<(MethodBase target, MethodInfo patch)>();
        MethodInfo Method(string name) => typeof(Mod47Checks).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        void Prefix(MethodBase method, string name) { var patch = Method(name); observer.Patch(method, prefix: new HarmonyMethod(patch)); owned.Add((method, patch)); }
        void Postfix(MethodBase method, string name) { var patch = Method(name); observer.Patch(method, postfix: new HarmonyMethod(patch)); owned.Add((method, patch)); }
        Tile changedTile = null; BiomeDef oldBiome = null; Hilliness oldHill = default;
        try
        {
            Require(Harmony.GetPatchInfo(target)?.Postfixes.Any(p => p.PatchMethod.DeclaringType == toggleType) == true, "Actual toolbar postfix missing");
            Require(Harmony.GetPatchInfo(typeof(WorldObjectsHolder).GetMethod("ExposeData"))?.Prefixes.Any(p => p.PatchMethod.DeclaringType?.FullName == "WorldMapEditor.WorldObjectsHolder_ExposeData_Patch") == true, "Actual save cleanup prefix missing");
            Prefix(target, nameof(SkipNativeControls)); Prefix(icon, nameof(SetControlledIcon)); Prefix(refresh, nameof(ObserveRefresh));
            Postfix(generate, nameof(ValidateFullGenerationThenLimitRegistration)); Postfix(spawn, nameof(ObserveSpawn)); Postfix(postAdd, nameof(ObservePostAdd));
            Event.current = new Event { type = EventType.Layout }; generations = spawnCalls = postAddCalls = refreshCalls = 0;
            void Toggle(bool on, bool worldView = true) { desiredMode = on; Find.PlaySettings.DoPlaySettingsGlobalControls(new WidgetRow(), worldView); }
            Toggle(true, false); Require(!(bool)edit.GetValue(null) && generations == 0, "Map toolbar must not enable world editor");
            Toggle(true);
            var first = pool.Cast<WorldObject>().ToArray();
            Require((bool)edit.GetValue(null) && first.Length == 32 && generations == 1 && spawnCalls == 32 && postAddCalls == 32, "Actual toolbar did not register bounded input through native SpawnSetup/PostAdd lifecycle");
            foreach (var inv in first)
                Require(inv.Spawned && holder.AllWorldObjectsOnLayer(inv.Tile.Layer).Contains(inv) && WorldSelector.SelectableObjectsAt(inv.Tile).Contains(inv), "Actual Add must populate layer renderer cache and real tile selection");
            Toggle(false);
            Require(!(bool)edit.GetValue(null) && pool.Count == 0 && first.All(o => !o.Spawned && !holder.AllWorldObjectsOnLayer(o.Tile.Layer).Contains(o)) && holder.AllWorldObjects.SequenceEqual(oldObjects), "Actual disable did not remove holder/layer objects and generation cache");
            Toggle(true);
            var second = pool.Cast<WorldObject>().ToArray();
            Require(generations == 2 && spawnCalls == 64 && postAddCalls == 64 && second.All(o => !first.Contains(o)) && holder.AllWorldObjects.Count == oldObjects.Length + 32, "Second actual toggle accumulated old registered objects or reused old cache");
            var selected = second.First(o => !holder.AllWorldObjects.Any(other => other != o && other.Tile == o.Tile));
            Find.WorldSelector.ClearSelection(); Find.WorldSelector.Select(selected, false);
            var commands = selected.GetGizmos().OfType<Command_Action>().ToArray();
            Require(commands.Length == 4, "Actual invisible object must provide four editor commands");
            FloatMenu Open(string key)
            {
                commands.Single(c => c.defaultLabel == key.Translate()).action();
                return Find.WindowStack.Windows.OfType<FloatMenu>().Single(w => !oldWindows.Contains(w));
            }
            List<FloatMenuOption> Options(FloatMenu menu) => (List<FloatMenuOption>)typeof(FloatMenu).GetField("options", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
            changedTile = Find.WorldGrid[selected.Tile]; oldBiome = changedTile.PrimaryBiome; oldHill = changedTile.hilliness;
            var biomeMenu = Open("SetBiome"); var newBiome = DefDatabase<BiomeDef>.AllDefsListForReading.First(b => b != oldBiome && b.implemented);
            Options(biomeMenu).Single(o => o.Label == newBiome.LabelCap.ToString()).action(); Find.WindowStack.TryRemove(biomeMenu, false);
            Require(changedTile.PrimaryBiome == newBiome && refreshCalls == 1, "Actual biome action did not change primary biome and request render refresh");
            var hillMenu = Open("SetHilliness"); var newHill = oldHill == Hilliness.SmallHills ? Hilliness.LargeHills : Hilliness.SmallHills;
            Options(hillMenu).Single(o => o.Label == newHill.GetLabelCap()).action(); Find.WindowStack.TryRemove(hillMenu, false);
            Require(changedTile.hilliness == newHill && refreshCalls == 2, "Actual hill action did not change selected tile or request render refresh");
            var settlementMenu = Open("GenerateSettlement"); var faction = Find.FactionManager.AllFactions.First(f => !f.def.isPlayer && !f.Hidden);
            Options(settlementMenu).Single(o => o.Label == faction.Name).action(); Find.WindowStack.TryRemove(settlementMenu, false);
            var settlement = holder.SettlementAt(selected.Tile);
            Require(settlement != null && !oldObjects.Contains(settlement) && settlement.Faction == faction && settlement.Spawned && holder.AllWorldObjectsOnLayer(selected.Tile.Layer).Contains(settlement), "Actual settlement menu action did not create real faction/layer object");
            commands.Single(c => c.defaultLabel == "DestroySettlement".Translate()).action();
            Require(holder.SettlementAt(selected.Tile) == null && !settlement.Spawned, "Actual settlement destroy action did not remove created settlement");
            var path = Path.Combine(GenFilePaths.SaveDataFolderPath, "Mod47WorldFixture.xml");
            Require(Scribe.mode == LoadSaveMode.Inactive && !File.Exists(path), "World Scribe fixture exists/busy");
            Scribe.saver.InitSaving(path, "Mod47WorldFixture"); holder.ExposeData(); Scribe.saver.FinalizeSaving();
            Require(!(bool)edit.GetValue(null) && pool.Count == 0 && !holder.AllWorldObjects.Any(o => o.GetType() == invisibleType) && !File.ReadAllText(path).Contains("WorldMapEditor.InvisibleWorldObject"), "Actual ExposeData save prefix retained invisible objects/edit mode in world or serialization");
        }
        finally
        {
            Scribe.ForceStop();
            foreach (var ownedPatch in owned) observer.Unpatch(ownedPatch.target, ownedPatch.patch);
            utils.GetMethod("CleanUpWorldObjects").Invoke(null, null);
            foreach (var obj in holder.AllWorldObjects.Where(o => !oldObjects.Contains(o)).ToArray()) holder.Remove(obj);
            if (changedTile != null) { changedTile.PrimaryBiome = oldBiome; changedTile.hilliness = oldHill; }
            foreach (var pair in factionTextures) pair.Key.settlementTexturePath = pair.Value;
            WorldObjectDefOf.Settlement.texture = oldSettlementTexture;
            pool.Clear(); foreach (var old in oldPool) pool.Add(old); edit.SetValue(null, oldEdit);
            Find.WorldSelector.ClearSelection(); Find.WorldSelector.SelectedLayer = oldLayer; Find.WorldSelector.SelectedTile = oldTile;
            foreach (var old in oldSelected) Find.WorldSelector.Select(old, false);
            foreach (var window in Find.WindowStack.Windows.Where(w => !oldWindows.Contains(w)).ToArray()) Find.WindowStack.TryRemove(window, false);
            Event.current = oldEvent; pool = null; invisibleType = null;
        }
        Log.Message("RW17 Mod47: real full generation uniqueness, bounded32 native Add/layer/cache/selection/lifecycle toggles, actual biome/hill/settlement actions and real save cleanup passed; full-population performance/GPU/layer gestures/reload pending");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Mod47: " + message);
    }
}
