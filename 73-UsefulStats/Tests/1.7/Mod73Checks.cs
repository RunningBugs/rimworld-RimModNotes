using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RW17.Validation;

public static class Mod73Checks
{
    public const string PackageId = "RunningBugs.UsefulStats";
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Mod73: " + message); }
    private static object Get(object obj, string name) => obj.GetType().GetField(name, All).GetValue(obj);
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, All).SetValue(obj, value);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, All).Invoke(obj, args);
    private static string Text(object obj, string field) => (string)Get(obj, field);
    private static bool Flag(object obj, string field) => (bool)Get(obj, field);
    private static float Number(object obj, string field) => (float)Get(obj, field);
    private static object[] Objects(object obj) => ((IEnumerable)obj).Cast<object>().ToArray();

    public static void Run(Map map)
    {
        var mod = LoadedModManager.RunningMods.Single(m => m.PackageId.Equals(PackageId, StringComparison.OrdinalIgnoreCase));
        var assembly = mod.assemblies.loadedAssemblies.Single(a => a.GetName().Name == "UsefulStats");
        var builder = assembly.GetType("UsefulStats.CraftableRowBuilder", true);
        var build = builder.GetMethod("BuildRows", All);
        VerifyPureHelpers(assembly, builder);
        object[] Rows(Map source) => Objects(build.Invoke(null, new object[] { source }));
        var mainDef = DefDatabase<MainButtonDef>.GetNamed("UsefulStatsMainButton");
        Require(mainDef.tabWindowClass == assembly.GetType("UsefulStats.MainTabWindow_UsefulStats", true) && mainDef.closesWorldView, "actual main button Def/window binding");
        var before = Rows(map); Require(before.Length > 100, "real loaded Defs must produce substantial actual recipe/buildable table");
        Require(before.All(r => Number(r, "WorkAmount") > 0 && !float.IsNaN(Number(r, "MarketValue")) && Objects(Get(r, "KindKeys")).Length > 0), "real rows need work/finite value/category metadata");
        var wall = before.Single(r => Text(r, "DefName") == "Wall" && Text(r, "Source") == "Build");
        var defaultWallStuff = GenStuff.DefaultStuffFor(ThingDefOf.Wall);
        Require(Mathf.Approximately(Number(wall, "WorkAmount"), ThingDefOf.Wall.GetStatValueAbstract(StatDefOf.WorkToBuild, defaultWallStuff)) && Mathf.Approximately(Number(wall, "MarketValue"), ThingDefOf.Wall.GetStatValueAbstract(StatDefOf.MarketValue, defaultWallStuff)), "actual default-stuff building work/value");
        var wallMaterials = Objects(Get(wall, "MaterialVariants"));
        Require(wallMaterials.Length > 1 && wallMaterials.All(v => Number(v, "Count") == ThingDefOf.Wall.CostStuffCount) && wallMaterials.Count(v => Flag(v, "IsDefault")) == 1, "actual wall material variants/count/default unique");
        Require(Objects(Get(wall, "KindKeys")).Contains(ThingCategoryDefOf.Buildings.defName), "actual building category chain");
        var recipe = DefDatabase<RecipeDef>.GetNamed("CookMealSimple");
        var recipeSource = recipe.LabelCap.ToString(); var recipeProduct = recipe.ProducedThingDef;
        var mealBefore = before.Single(r => Text(r, "DefName") == recipeProduct.defName && Text(r, "Source") == recipeSource);
        bool nativeBefore = recipe.AvailableNow && recipe.AllRecipeUsers.Any(u => u.category != ThingCategory.Pawn && u.IsResearchFinished && map.listerBuildings.AllBuildingsColonistOfDef(u).Any(b => recipe.AvailableOnNow(b)));
        Require(Flag(mealBefore, "AvailableNow") == nativeBefore, "actual map recipe availability matches real eligible benches");
        Require(Mathf.Approximately(Number(mealBefore, "WorkAmount"), recipe.WorkAmountForStuff(null)) && Mathf.Approximately(Number(mealBefore, "MarketValue"), recipeProduct.GetStatValueAbstract(StatDefOf.MarketValue)), "actual recipe work/value source");
        var nullMeal = Rows(null).Single(r => Text(r, "DefName") == recipeProduct.defName && Text(r, "Source") == recipeSource);
        Require(Flag(nullMeal, "AvailableNow") == recipe.AvailableNow, "original null-map potential-user availability fallback");
        Building bench = null; var oldEvent = Event.current; var oldTab = Find.MainTabsRoot.OpenTab; var oldWorld = Find.World.renderer.wantedMode;
        var researchProgress = (Dictionary<ResearchProjectDef, float>)typeof(ResearchManager).GetField("progress", All).GetValue(Find.ResearchManager);
        var researchCopy = researchProgress.ToDictionary(k => k.Key, v => v.Value);
        try
        {
            var benchDef = recipe.AllRecipeUsers.First(u => u.category == ThingCategory.Building && u.IsResearchFinished && !u.MadeFromStuff);
            var candidates = map.AllCells.Where(c => GenAdj.OccupiedRect(c, Rot4.North, benchDef.size).Cells.All(q => q.InBounds(map) && q.Standable(map) && !q.Fogged(map) && q.GetThingList(map).Count == 0)).Take(1).ToArray();
            Require(candidates.Length == 1, "empty exact recipe-bench footprint fixture");
            bench = (Building)ThingMaker.MakeThing(benchDef); bench.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(bench, candidates[0], map);
            var meal = Rows(map).Single(r => Text(r, "DefName") == recipeProduct.defName && Text(r, "Source") == recipeSource);
            Require(Flag(meal, "AvailableNow") && !Flag(meal, "FutureAvailable"), "actual spawned recipe user makes current recipe row available");
            var rice = DefDatabase<ThingDef>.GetNamed("RawRice");
            var riceCandidate = Objects(Get(meal, "MaterialVariants")).Single(v => Text(v, "Label") == rice.LabelCap.ToString());
            Require(Mathf.Approximately(Number(riceCandidate, "Count"), recipe.ingredients.Single().CountRequiredOfFor(rice, recipe)) && Number(riceCandidate, "WorkAmount") == recipe.WorkAmountForStuff(null), "real nutrition ingredient count/work variant");
            var lockedDef = DefDatabase<ThingDef>.AllDefsListForReading.First(d => d.category == ThingCategory.Building && d.BuildableByPlayer && !d.researchPrerequisites.NullOrEmpty() && d.GetStatValueAbstract(StatDefOf.WorkToBuild, d.MadeFromStuff ? GenStuff.DefaultStuffFor(d) : null) > 0);
            foreach (var project in lockedDef.researchPrerequisites) researchProgress[project] = 0;
            var locked = Rows(map).Single(r => Text(r, "DefName") == lockedDef.defName && Text(r, "Source") == "Build");
            Require(!Flag(locked, "AvailableNow") && Flag(locked, "FutureAvailable") && !string.IsNullOrEmpty(Text(locked, "UnlockInfo")), "actual unfinished research creates future row/unlock info");
            foreach (var project in lockedDef.researchPrerequisites) researchProgress[project] = project.Cost;
            var unlocked = Rows(map).Single(r => Text(r, "DefName") == lockedDef.defName && Text(r, "Source") == "Build");
            Require(Flag(unlocked, "AvailableNow") && !Flag(unlocked, "FutureAvailable"), "real research progress unlocks building row");
            var makerDef = DefDatabase<ThingDef>.AllDefsListForReading.First(d => d.category == ThingCategory.Item && d.recipeMaker != null && d.MadeFromStuff && d.CostStuffCount > 0);
            var maker = Rows(map).Single(r => Text(r, "DefName") == makerDef.defName);
            Require(Objects(Get(maker, "MaterialVariants")).Length > 1 && Number(maker, "WorkAmount") > 0 && Text(maker, "Source") != "Build", "actual RecipeMaker material grouping and no generated duplicate row");
            Event.current = new Event { type = EventType.Layout };
            Find.MainTabsRoot.SetCurrentTab(mainDef, false);
            var window = mainDef.TabWindow;
            Require(Find.MainTabsRoot.OpenTab == mainDef && Find.WindowStack.Windows.Contains(window), "actual native main-tab open lifecycle");
            window.DoWindowContents(new Rect(0, 0, 1250, 720));
            Require(!Flag(window, "dataDirty") && !Flag(window, "viewDirty") && Objects(Get(window, "rows")).Length > 100, "actual window data/view cache refresh");
            VerifyWindowFilters(window, wall);
            Find.MainTabsRoot.SetCurrentTab(null, false); Require(!Find.WindowStack.Windows.Contains(window), "actual native main-tab close");
        }
        finally
        {
            researchProgress.Clear(); foreach (var p in researchCopy) researchProgress.Add(p.Key, p.Value);
            Event.current = oldEvent; Find.MainTabsRoot.SetCurrentTab(oldTab, false); Find.World.renderer.wantedMode = oldWorld;
            if (bench != null && !bench.Destroyed) bench.Destroy();
        }
        if (Environment.GetEnvironmentVariable("RW17_STATS73_RENDER") == "1")
        {
            Find.MainTabsRoot.SetCurrentTab(mainDef, false);
            Log.Message("RW17_STATS73_RENDER_READY: actual native main-tab left open for root GPU capture");
        }
        Log.Message("RW17 Mod73: actual recipe/buildable/RecipeMaker rows, default stuff/material variants/nutrition ingredient counts, map bench availability/null-map fallback, real research locking/unlocking, native main-tab open/render/cache/filters/sort/expansion/close passed; pixels/physical input/large thirdparty data and historical saves pending");
    }

    private static void VerifyWindowFilters(MainTabWindow window, object wall)
    {
        Set(window, "search", "Wall"); Set(window, "materialSearch", ""); Set(window, "selectedKindKey", "__all"); Call(window, "RebuildVisibleRows");
        var visible = Objects(Get(window, "visibleParentRows")); Require(visible.Length > 0 && visible.All(r => Text(r, "DefName").ToLowerInvariant().Contains("wall") || Text(r, "Label").ToLowerInvariant().Contains("wall") || Text(r, "Source").ToLowerInvariant().Contains("wall") || Text(r, "UnlockInfo").ToLowerInvariant().Contains("wall") || Text(r, "MaterialSummary").ToLowerInvariant().Contains("wall") || ((string)r.GetType().GetProperty("CategoryLabel").GetValue(r)).ToLowerInvariant().Contains("wall") || ((string)r.GetType().GetProperty("CategoryTooltip").GetValue(r)).ToLowerInvariant().Contains("wall")), "actual full-field search filter");
        Set(window, "search", "RW17_NO_SUCH_CRAFTABLE_ITEM"); Call(window, "RebuildVisibleRows"); Require(Objects(Get(window, "displayRows")).Length == 0, "actual no-match filter");
        Set(window, "search", ""); Set(window, "materialSearch", "wood"); Call(window, "RebuildVisibleRows");
        Require(Objects(Get(window, "visibleParentRows")).Any(r => Text(r, "DefName") == "Wall"), "actual material variants search sees wood wall");
        Set(window, "materialSearch", ""); Call(window, "SelectKind", ThingCategoryDefOf.Buildings.defName); Call(window, "RebuildVisibleRows");
        Require(Objects(Get(window, "visibleParentRows")).All(r => Objects(Get(r, "KindKeys")).Contains(ThingCategoryDefOf.Buildings.defName)), "actual category selection filter");
        var options = Objects(Call(window, "BuildKindOptions"));
        Require(options.Any(o => Text(o, "Key") == "__all") && options.Any(o => Text(o, "Key") == ThingCategoryDefOf.Buildings.defName), "actual category menu derives all/building entries");
        Set(window, "sortKey", "work"); Set(window, "sortDescending", false); Call(window, "RebuildVisibleRows");
        var ordered = Objects(Get(window, "visibleParentRows")); Require(ordered.Select(r => Number(r, "WorkAmount")).SequenceEqual(ordered.Select(r => Number(r, "WorkAmount")).OrderBy(v => v)), "actual ascending work sorting");
        Set(window, "sortDescending", true); Call(window, "RebuildVisibleRows"); ordered = Objects(Get(window, "visibleParentRows")); Require(ordered.Select(r => Number(r, "WorkAmount")).SequenceEqual(ordered.Select(r => Number(r, "WorkAmount")).OrderByDescending(v => v)), "actual descending work sorting");
        var expand = (HashSet<string>)Get(window, "expandedRows"); var key = (string)wall.GetType().GetProperty("RowKey").GetValue(wall); expand.Add(key); Call(window, "RebuildVisibleRows");
        var display = Objects(Get(window, "displayRows"));
        Require(display.Count(d => Text(Get(d, "Parent"), "DefName") == "Wall" && Get(d, "Variant") != null) == Objects(Get(wall, "MaterialVariants")).Length, "actual grouped wall expansion includes every material variant");
        expand.Clear(); Call(window, "RebuildVisibleRows"); Require(Objects(Get(window, "displayRows")).All(d => Get(d, "Variant") == null), "actual collapse removes material subrows");
        Call(window, "SelectKind", "__all"); Set(window, "showAvailableNow", false); Set(window, "showFuture", false); Set(window, "showAllOnly", false); Call(window, "RebuildVisibleRows"); Require(Objects(Get(window, "displayRows")).Length == 0, "all availability toggles off");
        Set(window, "showAvailableNow", true); Set(window, "showFuture", true); Set(window, "showAllOnly", true); Set(window, "viewDirty", true);
    }

    private static void VerifyPureHelpers(Assembly assembly, Type builder)
    {
        var windowType = assembly.GetType("UsefulStats.MainTabWindow_UsefulStats", true);
        object Label(string method, params object[] args) => windowType.GetMethod(method, All).Invoke(null, args);
        var row = Activator.CreateInstance(assembly.GetType("UsefulStats.Core.CraftableStatRow", true));
        Require((string)Label("MaterialRatioText", row) == "—" && (string)Label("ValueMaterialText", row) == "—" && (string)Label("StatusText", row) == "All only", "empty material/status label defaults");
        Set(row, "MaterialSummary", "explicit test fixture"); Set(row, "IngredientPerWorkMin", .125f); Set(row, "IngredientPerWorkMax", .25f); Set(row, "DefaultValuePerMaterial", 2f);
        Require((string)Label("MaterialRatioText", row) == "0.125-0.250" && (string)Label("ValueMaterialText", row) == "2.00", "material range and value labels");
        Set(row, "IngredientPerWorkMax", .125f); Require((string)Label("MaterialRatioText", row) == "0.125", "equal material range collapses to one label");
        Set(row, "FutureAvailable", true); Require((string)Label("StatusText", row) == "Future", "future empty unlock label");
        Set(row, "UnlockInfo", "explicit research requirement"); Require((string)Label("StatusText", row) == "explicit research requirement", "future research label");
        Set(row, "AvailableNow", true); Require((string)Label("StatusText", row) == "Current", "current status takes precedence");
        Require(!(bool)Label("Contains", null, "wall") && (bool)Label("Contains", "WALL", "wall"), "case-insensitive contains/null helper");
        var format = builder.GetMethod("FormatCountRange", All);
        Require((string)format.Invoke(null, new object[] { 2f, 2f }) == "2" && (string)format.Invoke(null, new object[] { 2f, 3f }) == "2-3", "equal/different ingredient count ranges");
    }
}
