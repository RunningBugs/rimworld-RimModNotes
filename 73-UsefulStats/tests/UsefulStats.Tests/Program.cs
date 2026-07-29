using System;
using UsefulStats.Core;

static void AssertEqual(float expected, float actual, string name, float tolerance = 0.0001f)
{
    if (Math.Abs(expected - actual) > tolerance)
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
