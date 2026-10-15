using System;
using UsefulStats.Core;

static void AssertEqual(float expected, float actual, string name, float tolerance = 0.0001f)
{
    if (float.IsNaN(actual) || float.IsInfinity(actual) || Math.Abs(expected - actual) > tolerance)
    {
        throw new Exception($"{name}: expected {expected}, got {actual}");
    }
}

AssertEqual(0.5f, CraftableEfficiencyMetrics.Ratio(30f, 60f), "generic ratio");
AssertEqual(30f, CraftableEfficiencyMetrics.PerDisplayedWork(30f, 60f), "value/displayed work");
AssertEqual(120f, CraftableEfficiencyMetrics.PerDisplayedWork(80f, 40f), "material/displayed work");
AssertEqual(0f, CraftableEfficiencyMetrics.PerDisplayedWork(80f, 0f), "zero work safe");
AssertEqual(0f, CraftableEfficiencyMetrics.PerDisplayedWork(float.NaN, 20f), "nan safe");

var row = new CraftableStatRow
{
    MarketValue = 150f,
    WorkAmount = 300f,
    IngredientLabel = "cloth",
    IngredientCount = 80f,
    MaterialSummary = "80 x cloth",
    IngredientPerWorkMin = 16f,
    IngredientPerWorkMax = 16f,
    MaterialVariantCount = 1
};
AssertEqual(30f, row.ValuePerWork, "row value/displayed work");
AssertEqual(16f, row.IngredientPerWork, "row ingredient/displayed work");
if (!row.HasSingleIngredient) throw new Exception("row should report a single ingredient");

var grouped = new CraftableStatRow
{
    WorkAmount = 30f,
    MaterialSummary = "25-250 x stuff (24)",
    IngredientPerWorkMin = 0.01f,
    IngredientPerWorkMax = 0.15f,
    MaterialVariantCount = 24
};
if (!grouped.HasSingleIngredient) throw new Exception("grouped material row should still be material-comparable");
AssertEqual(0.01f, grouped.IngredientPerWork, "grouped material sort value");

Console.WriteLine("UsefulStats.Tests: OK");

foreach (float denominator in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    AssertEqual(0f, CraftableEfficiencyMetrics.Ratio(3f, denominator), "invalid denominator " + denominator);
foreach (float numerator in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    AssertEqual(0f, CraftableEfficiencyMetrics.Ratio(numerator, 3f), "invalid numerator " + numerator);
AssertEqual(-.5f, CraftableEfficiencyMetrics.Ratio(-5f, 10f), "finite negative numerator is retained");
var oldCulture = System.Globalization.CultureInfo.CurrentCulture;
try
{
    System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    foreach (var example in new[] { (123.45f, "123"), (12.345f, "12.3"), (1.2345f, "1.23"), (.12345f, "0.123"), (.0012345f, "0.0012"), (.00012345f, "0.00012"), (0f, "0"), (-12.345f, "-12.3") })
        if (CraftableEfficiencyMetrics.FormatRatio(example.Item1) != example.Item2) throw new Exception("FormatRatio branch failed: " + example.Item1);
}
finally { System.Globalization.CultureInfo.CurrentCulture = oldCulture; }
var variant = new MaterialVariantStat { Count = 20f, WorkAmount = 120f, MarketValue = 40f, Label = "steel", IsDefault = true };
AssertEqual(10f, variant.MaterialPerWork, "variant material/work");
AssertEqual(20f, variant.ValuePerWork, "variant value/work");
AssertEqual(2f, variant.ValuePerMaterial, "variant value/material");
variant.Count = 0f; AssertEqual(0f, variant.ValuePerMaterial, "zero material safety");
var empty = new CraftableStatRow { Source = "Build", DefName = "Wall", KindLabel = "Building" };
if (empty.HasSingleIngredient || empty.HasExpandableMaterials || empty.RowKey != "Build|Wall" || empty.CategoryLabel != "Building" || empty.CategoryTooltip != "Wall") throw new Exception("empty row defaults/key/category fallback failed");
empty.KindLabels.Add("Item"); empty.KindLabels.Add("Building"); empty.KindDefPaths.Add("Items > Buildings");
if (empty.CategoryLabel != "Item / Building" || empty.CategoryTooltip != "Items > Buildings") throw new Exception("multi-category display/path failed");
empty.MaterialSummary = "20 x steel"; empty.IngredientPerWorkMax = 1f; empty.IngredientPerWorkMin = .5f; empty.DefaultValuePerMaterial = 2f;
if (!empty.HasSingleIngredient) throw new Exception("material-comparable nonempty row failed");
AssertEqual(.5f, empty.IngredientPerWork, "material interval lower bound sort"); AssertEqual(2f, empty.ValuePerMaterial, "default material value");
empty.MaterialVariants.Add(new MaterialVariantStat()); empty.MaterialVariants.Add(new MaterialVariantStat());
if (!empty.HasExpandableMaterials) throw new Exception("actual variant list determines expandability");
empty.MaterialSummary = ""; if (empty.HasSingleIngredient) throw new Exception("missing summary forbids material ratio"); AssertEqual(0f, empty.ValuePerMaterial, "missing summary value ratio");
Console.WriteLine("UsefulStats 1.7 core: extended guard/format/variant/row boundaries OK");
