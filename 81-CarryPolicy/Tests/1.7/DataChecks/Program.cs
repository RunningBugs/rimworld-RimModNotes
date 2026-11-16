using System;
using System.IO;
using System.Reflection;
using _CarryPolicy;
using Verse;

typeof(Prefs).GetField("data", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, new PrefsData());

var a = new ThingDef { defName = "Unit_A" };
var b = new ThingDef { defName = "Unit_B" };
DefDatabase<ThingDef>.Add(a);
DefDatabase<ThingDef>.Add(b);

var original = new CarryPolicy();
original.entries.Add(new CarryPolicyEntry { thing = a, count = 3, countBuffer = "3" });
original.entries.Add(new CarryPolicyEntry { thing = b, count = 4 });
original.entries.Add(new CarryPolicyEntry { thing = null, unloadedDefName = "Unit_MissingModItem", count = 2.5f });
var copy = new CarryPolicy();
copy.CopyFrom(original);
Require(copy.Count == 3 && copy.EntryFor(a) == copy[0] && copy.EntryFor(new ThingDef()) == null && copy.EntryFor(null) == null, "Actual policy indexing/lookups preserve native Def identity and ignore null.");
Require(copy.entries != original.entries && copy[0] != original[0] && copy[0].thing == a && copy[0].countBuffer == null && copy[2].unloadedDefName == "Unit_MissingModItem",
    "Actual CopyFrom duplicates editable entries while retaining Def references, unloaded defNames, and resetting unsaved UI buffers.");
copy[0].count = 99;
Require(original[0].count == 3, "Editing copied policy does not mutate original.");
copy.CopyFrom(null);
Require(copy[0].count == 99, "Incompatible/null policy source is ignored.");
foreach (var input in new[] { (count: -1f, expected: 0), (count: 0f, expected: 0), (count: 2.4f, expected: 2), (count: 2.5f, expected: 2), (count: 2.6f, expected: 3) })
{
    var entry = new CarryPolicyEntry { thing = a, count = input.count };
    Require(!entry.IsFood && CarryPolicyUtility.TargetCount(entry, null) == input.expected && CarryPolicyUtility.BaselineTargetCount(entry) == input.expected,
        "Actual nonfood rounding/zero guards agree for pawn and baseline paths.");
}
Require(CarryPolicyUtility.TargetCount(null, null) == 0 && CarryPolicyUtility.BaselineTargetCount(new CarryPolicyEntry { count = 2 }) == 0 &&
    Math.Abs(CarryPolicyUtility.NutritionPerPortion(null) - 0.9f) < 0.0001f, "Actual missing-entry/Def/Pawn boundaries are defined.");

// Cross-save settings CRUD + Option B delete-last recreation
var settings = new CarryPolicySettings();
var sDefault = settings.DefaultCarryPolicy();
var sSecond = settings.MakeNewPolicy();
sDefault.label = "CrossDefault";
sDefault.entries.Add(new CarryPolicyEntry { thing = a, count = 3 });
sDefault.entries.Add(new CarryPolicyEntry { thing = null, unloadedDefName = "Unit_MissingModItem", count = 2.5f });
sSecond.label = "CrossTravel";
sSecond.entries.Add(new CarryPolicyEntry { thing = b, count = 5 });
settings.SetDefault(sSecond);
Require(settings.DefaultCarryPolicy() == sSecond && settings.defaultPolicies[0] == sSecond, "SetDefault moves target policy to index 0.");
Require(settings.TryDelete(sSecond).Accepted && settings.DefaultCarryPolicy() == sDefault, "Option B allows deleting default policy and promotes remaining policy.");
Require(settings.TryDelete(sDefault).Accepted && settings.defaultPolicies.Count == 1 && settings.DefaultCarryPolicy() != sDefault, "Deleting last cross-save policy recreates a fresh fallback default policy.");

// Cross-save settings Scribe round-trip (preserving loaded & unloaded defs across passes)
var saveSettings = new CarryPolicySettings();
var p1 = saveSettings.MakeNewPolicy();
p1.label = "Starter";
p1.entries.Add(new CarryPolicyEntry { thing = a, count = 4 });
p1.entries.Add(new CarryPolicyEntry { thing = null, unloadedDefName = "Missing_Food_Def", count = 1.5f });
var p2 = saveSettings.MakeNewPolicy();
p2.label = "Expedition";
p2.entries.Add(new CarryPolicyEntry { thing = b, count = 7 });

string tmpPath = "/tmp/opencode/carrypolicy_settings_roundtrip.xml";
Directory.CreateDirectory(Path.GetDirectoryName(tmpPath)!);
ModSettings boxedSettings = saveSettings;
Scribe.saver.InitSaving(tmpPath, "SettingsBlock");
try
{
    Scribe_Deep.Look(ref boxedSettings, "ModSettings");
}
finally
{
    Scribe.saver.FinalizeSaving();
}

CarryPolicySettings loadedSettings = null;
Scribe.loader.InitLoading(tmpPath);
try
{
    Scribe_Deep.Look(ref loadedSettings, "ModSettings");
}
finally
{
    Scribe.loader.FinalizeLoading();
}
Require(loadedSettings != null && loadedSettings.defaultPolicies.Count == 2, "Settings Scribe round-trip restores all cross-save policies.");
Require(loadedSettings.defaultPolicies[0].label == "Starter" && loadedSettings.defaultPolicies[0].entries.Count == 2, "Starter policy entries round-trip.");
Require(loadedSettings.defaultPolicies[0].entries[0].thing == a && loadedSettings.defaultPolicies[0].entries[0].unloadedDefName == null && loadedSettings.defaultPolicies[0].entries[0].count == 4f, "Loaded ThingDef restores cleanly.");
Require(loadedSettings.defaultPolicies[0].entries[1].thing == null && loadedSettings.defaultPolicies[0].entries[1].unloadedDefName == "Missing_Food_Def" && Math.Abs(loadedSettings.defaultPolicies[0].entries[1].count - 1.5f) < 0.001f, "Unloaded ThingDef defName and count are preserved silently.");
Require(loadedSettings.defaultPolicies[1].label == "Expedition" && loadedSettings.defaultPolicies[1].entries[0].thing == b && loadedSettings.defaultPolicies[1].entries[0].count == 7f, "Second policy restores cleanly.");

// New game initialization copies loaded cross-save entries while existing save is untouched
CarryPolicyDatabase.CrossSavePoliciesProvider = () => loadedSettings.defaultPolicies;
var newGameDb = new CarryPolicyDatabase();
newGameDb.StartedNewGame();
Require(newGameDb.policies.Count == 2 && newGameDb.DefaultCarryPolicy().label == "Starter", "StartedNewGame copies cross-save policies with index 0 as default.");
Require(newGameDb.policies[0].entries.Count == 1 && newGameDb.policies[0].entries[0].thing == a && newGameDb.policies[0].entries[0].count == 4f, "New game skips unloaded mod items while keeping loaded items.");
newGameDb.policies[0].entries[0].count = 99f;
Require(loadedSettings.defaultPolicies[0].entries[0].count == 4f, "Mutating new-game policies does not mutate cross-save settings.");
Require(loadedSettings.defaultPolicies[0].entries.Count == 2, "Copying to new game does not strip unloaded items from cross-save settings.");

var existingSaveEmptyDb = new CarryPolicyDatabase();
var fallbackDefault = existingSaveEmptyDb.DefaultCarryPolicy();
Require(existingSaveEmptyDb.policies.Count == 1 && fallbackDefault.entries.Count == 0, "Existing save without StartedNewGame never imports cross-save defaults.");

var existingSaveWithDataDb = new CarryPolicyDatabase();
var legacy = existingSaveWithDataDb.MakeNewPolicy();
legacy.label = "LegacySavePolicy";
existingSaveWithDataDb.StartedNewGame();
Require(existingSaveWithDataDb.policies.Count == 1 && existingSaveWithDataDb.DefaultCarryPolicy() == legacy, "Database with existing policies is never overwritten.");

Console.WriteLine("PASS actual CarryPolicy data, cross-save settings round-trip, new-game vs existing-save isolation, unloaded-def preservation, and Option B deletion checks");
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
