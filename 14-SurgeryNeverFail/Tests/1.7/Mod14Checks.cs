using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod14Checks
{
    public const string PackageId = "RunningBugs.SurgeryNeverFail";
    public static void Run(Map map)
    {
        var start = AccessTools.TypeByName("SurgeryNeverFail.Start");
        var toggle = start.Assembly.GetType("Utils.ToggleIconPatcher");
        var flag = toggle.GetProperty("Flag");
        var oldFlag = flag.GetValue(null);
        var original = AccessTools.Method(typeof(Recipe_Surgery), "CheckSurgeryFail");
        Require(Harmony.GetPatchInfo(original)?.Prefixes.Any(p => p.PatchMethod.DeclaringType?.FullName
            == "SurgeryNeverFail.SurgeryFailPatch" && p.PatchMethod.Name == "Prefix") == true,
            "Concrete surgery prefix missing");
        var toolbar = AccessTools.Method(typeof(PlaySettings), "DoPlaySettingsGlobalControls");
        Require(Harmony.GetPatchInfo(toolbar)?.Postfixes.Any(p => p.PatchMethod.DeclaringType?.FullName
            == "Utils.ToggleIconPatcher" && p.PatchMethod.DeclaringType.Assembly == start.Assembly) == true,
            "Surgery toolbar postfix missing");
        var doctor = map.mapPawns.FreeColonists.First();
        Pawn patient = null;
        Rand.PushState(17);
        try
        {
            patient = PawnGenerator.GeneratePawn(PawnKindDefOf.SpaceRefugee, Faction.OfPlayer);
            var part = patient.RaceProps.body.AllParts.First(p => p.def == BodyPartDefOf.Arm);
            var worker = new CheckWorker { recipe = new RecipeDef {
                surgeryOutcomeEffect = new SurgeryOutcomeEffectDef {
                    outcomes = new List<SurgeryOutcome> { new SurgeryOutcome_Failure {
                        chance = 1f, failure = true, totalDamage = 0 } } },
                addsHediffOnFailure = HediffDefOf.Anesthetic } };
            var before = patient.health.hediffSet.hediffs.Count;
            flag.SetValue(null, false);
            Require(worker.Check(doctor, patient, part), "Disabled mode did not execute the deterministic failure");
            Require(patient.health.hediffSet.hediffs.Count > before,
                "Unmodified surgery failure did not apply its failure hediff control");
            var afterFailure = patient.health.hediffSet.hediffs.Count;
            flag.SetValue(null, true);
            Require(!worker.Check(doctor, patient, part), "Enabled mode did not override real surgery result");
            Require(patient.health.hediffSet.hediffs.Count == afterFailure,
                "Enabled mode still executed original failure side effects");
        }
        finally
        {
            if (patient != null && !patient.Destroyed) patient.Destroy(DestroyMode.Vanish);
            flag.SetValue(null, oldFlag);
            Rand.PopState();
        }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Mod14: " + message);
    }
    private sealed class CheckWorker : Recipe_Surgery
    {
        public bool Check(Pawn doctor, Pawn patient, BodyPartRecord part) =>
            CheckSurgeryFail(doctor, patient, new List<Thing>(), part, null);
    }
}
