using System;
using _CarryPolicy;
using Verse;

var a = new ThingDef { defName = "Unit_A" };
var b = new ThingDef { defName = "Unit_B" };
var original = new CarryPolicy();
original.entries.Add(new CarryPolicyEntry { thing = a, count = 3, countBuffer = "3" });
original.entries.Add(new CarryPolicyEntry { thing = b, count = 4 });
var copy = new CarryPolicy(); copy.CopyFrom(original);
Require(copy.Count == 2 && copy.EntryFor(a) == copy[0] && copy.EntryFor(new ThingDef()) == null, "Actual policy indexing/lookups preserve native Def identity.");
Require(copy.entries != original.entries && copy[0] != original[0] && copy[0].thing == a && copy[0].countBuffer == null,
    "Actual CopyFrom duplicates editable entries while retaining Def references and resetting unsaved UI buffers.");
copy[0].count = 99; Require(original[0].count == 3, "Editing copied policy does not mutate original.");
copy.CopyFrom(null); Require(copy[0].count == 99, "Incompatible/null policy source is ignored.");
foreach (var input in new[] { (count: -1f, expected: 0), (count: 0f, expected: 0), (count: 2.4f, expected: 2), (count: 2.5f, expected: 2), (count: 2.6f, expected: 3) })
{
    var entry = new CarryPolicyEntry { thing = a, count = input.count };
    Require(!entry.IsFood && CarryPolicyUtility.TargetCount(entry, null) == input.expected && CarryPolicyUtility.BaselineTargetCount(entry) == input.expected,
        "Actual nonfood rounding/zero guards agree for pawn and baseline paths.");
}
Require(CarryPolicyUtility.TargetCount(null, null) == 0 && CarryPolicyUtility.BaselineTargetCount(new CarryPolicyEntry { count = 2 }) == 0 &&
    Math.Abs(CarryPolicyUtility.NutritionPerPortion(null) - 0.9f) < 0.0001f, "Actual missing-entry/Def/Pawn boundaries are defined.");
Console.WriteLine("PASS actual CarryPolicy data copy/identity/indexing, nonfood rounding and zero/null fallbacks; real native data types");
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
