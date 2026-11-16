using RimWorld;
using UnityEngine;
using Verse;

namespace RangedFirepowerStats;

public enum Scenario { Current, Specialist, SpecialistTriggerHappy }

// Private preview workers never replace game workers or patch stat getters.
public static class ProfileStats
{
    public static bool SpecialistsAvailable => ModsConfig.IdeologyActive &&
        DefDatabase<PreceptDef>.GetNamedSilentFail("IdeoRole_ShootingSpecialist") != null;

    public static float Read(Pawn pawn, StatDef stat, Scenario scenario, Thing weapon = null)
    {
        var worker = new PreviewWorker(pawn, scenario, weapon);
        if (!worker.Changes(stat)) return stat.Worker.GetValue(StatRequest.For(pawn));
        worker.InitSetStat(stat);
        return worker.GetValue(StatRequest.For(pawn));
    }

    public static float HitFactor(Pawn pawn, float distance, Scenario scenario, Thing weapon = null)
    {
        if ((scenario == Scenario.Current || !SpecialistsAvailable || !pawn.RaceProps.Humanlike) &&
            (weapon == null || weapon == pawn.equipment?.Primary))
            return ShotReport.HitFactorFromShooter(pawn, distance);
        float accuracy = Read(pawn, StatDefOf.ShootingAccuracyPawn, scenario, weapon);
        var close = Read(pawn, StatDefOf.ShootingAccuracyFactor_Touch, scenario, weapon);
        var shortRange = Read(pawn, StatDefOf.ShootingAccuracyFactor_Short, scenario, weapon);
        var medium = Read(pawn, StatDefOf.ShootingAccuracyFactor_Medium, scenario, weapon);
        var far = Read(pawn, StatDefOf.ShootingAccuracyFactor_Long, scenario, weapon);
        float factor = distance <= 3 ? close : distance <= 12 ? Mathf.Lerp(close, shortRange, (distance - 3) / 9) :
            distance <= 25 ? Mathf.Lerp(shortRange, medium, (distance - 12) / 13) :
            distance <= 40 ? Mathf.Lerp(medium, far, (distance - 25) / 15) : far;
        return Mathf.Max(Mathf.Pow(accuracy, distance) * factor, 0.0201f);
    }

    private sealed class PreviewWorker : StatWorker
    {
        private readonly Pawn pawn;
        private readonly Scenario scenario;
        private readonly Thing weapon;
        private readonly PreceptDef oldRole;
        private readonly PreceptDef newRole;
        private readonly Trait oldTrait;
        private readonly TraitDegreeData newTrait;

        public PreviewWorker(Pawn pawn, Scenario scenario, Thing weapon)
        {
            this.pawn = pawn;
            this.weapon = weapon;
            this.scenario = pawn?.RaceProps.Humanlike == true && SpecialistsAvailable ? scenario : Scenario.Current;
            if (this.scenario == Scenario.Current) return;
            oldRole = pawn.Ideo?.GetRole(pawn)?.def;
            newRole = DefDatabase<PreceptDef>.GetNamed("IdeoRole_ShootingSpecialist");
            oldTrait = pawn.story?.traits?.allTraits.FirstOrDefault(t => t.def.defName == "ShootingAccuracy" && !t.Suppressed);
            newTrait = DefDatabase<TraitDef>.GetNamed("ShootingAccuracy").DataAtDegree(-1);
        }

        private static float RoleOffset(PreceptDef role, StatDef valueStat) => role?.roleEffects?
            .OfType<RoleEffect_PawnStatOffset>().Where(e => e.statDef == valueStat).Sum(e => e.modifier) ?? 0;
        private static float RoleFactor(PreceptDef role, StatDef valueStat) => role?.roleEffects?
            .OfType<RoleEffect_PawnStatFactor>().Where(e => e.statDef == valueStat)
            .Aggregate(1f, (value, e) => value * e.modifier) ?? 1;

        private float Offset(StatDef valueStat)
        {
            float delta = 0;
            if (pawn != null && weapon != null && weapon != pawn.equipment?.Primary)
                delta += StatOffsetFromGear(weapon, valueStat) -
                    (pawn.equipment?.Primary is Thing current ? StatOffsetFromGear(current, valueStat) : 0);
            if (scenario == Scenario.Current) return delta;
            delta += RoleOffset(newRole, valueStat) - RoleOffset(oldRole, valueStat);
            if (scenario == Scenario.SpecialistTriggerHappy)
                delta += newTrait.statOffsets.GetStatOffsetFromList(valueStat) - (oldTrait?.OffsetOfStat(valueStat) ?? 0);
            return delta;
        }

        private float Factor(StatDef valueStat)
        {
            if (scenario == Scenario.Current) return 1;
            float ratio = RoleFactor(newRole, valueStat) / RoleFactor(oldRole, valueStat);
            if (scenario == Scenario.SpecialistTriggerHappy)
                ratio *= newTrait.statFactors.GetStatFactorFromList(valueStat) / (oldTrait?.MultiplierOfStat(valueStat) ?? 1);
            return ratio;
        }

        public bool Changes(StatDef valueStat) => Offset(valueStat) != 0 || Factor(valueStat) != 1;

        public override float GetBaseValueFor(StatRequest request) => stat.Worker.GetBaseValueFor(request) + Offset(stat);

        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true) =>
            base.GetValueUnfinalized(req, applyPostProcess) * Factor(stat);

        public override void FinalizeValue(StatRequest req, ref float value, bool applyPostProcess) =>
            stat.Worker.FinalizeValue(req, ref value, applyPostProcess);
    }
}
