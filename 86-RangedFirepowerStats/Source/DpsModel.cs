using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RangedFirepowerStats;

public sealed class DpsModel
{
    public Thing Weapon { get; private init; }
    public Pawn Pawn { get; private init; }
    public Building_TurretGun Turret { get; private init; }
    public Verb_LaunchProjectile Verb { get; private init; }
    public Thing Caster => (Thing)Turret ?? Pawn;
    public int Shots => Math.Max(1, Verb.BurstShotCount);
    public int IntervalTicks => Math.Max(1, Verb.TicksBetweenBurstShots);
    public float Damage => Verb.Projectile.projectile.GetDamageAmount(Weapon);
    public float MinRange => Math.Max(1, Verb.verbProps.minRange);
    public float MaxRange => Range(false);
    public bool CanEstimate => Verb.GetType() == typeof(Verb_Shoot) &&
        Verb.Projectile.thingClass == typeof(Bullet) &&
        Verb.verbProps.ForcedMissRadius <= 0.5f && Verb.Projectile.projectile.explosionRadius <= 0;
    public bool CanCompare => Turret == null && (Pawn == null || Pawn.RaceProps.Humanlike) && ProfileStats.SpecialistsAvailable;
    public bool IgnoreEnvironment => Weapon.TryGetComp<CompUniqueWeapon>()?.IgnoreAccuracyMaluses == true;
    public int JobCadence => Pawn?.Spawned == true ? Math.Clamp(Pawn.UpdateRateTicks, 1, 15) : 1;

    public static DpsModel Create(Thing inspected, Pawn shooter = null)
    {
        if (inspected is MinifiedThing mini) inspected = mini.InnerThing;
        var turret = inspected as Building_TurretGun;
        var pawn = inspected as Pawn ?? shooter;
        if (pawn != null && StatDefOf.ShootingAccuracyPawn.Worker.IsDisabledFor(pawn)) return null;
        var weapon = turret?.gun ?? (inspected is Pawn p ? p.equipment?.Primary : inspected);
        if (weapon?.def.IsRangedWeapon != true) return null;
        var verb = (turret?.AttackVerb ?? weapon.TryGetComp<CompEquippable>()?.PrimaryVerb) as Verb_LaunchProjectile;
        // A beam/ability/custom multi-projectile verb does not necessarily use the burst model.
        if (verb is not Verb_Shoot || verb.Projectile?.projectile == null) return null;
        if (turret == null && pawn != null && verb.GetType() == typeof(Verb_Shoot) && verb.Caster != pawn)
            verb = new Verb_Shoot { caster = pawn, verbProps = verb.verbProps, verbTracker = verb.verbTracker };
        var model = new DpsModel { Weapon = weapon, Pawn = turret == null ? pawn : null, Turret = turret, Verb = verb };
        return float.IsFinite(model.MaxRange) && model.MaxRange >= model.MinRange ? model : null;
    }

    public float Range(bool weather)
    {
        var props = Verb.verbProps;
        float range = props.rangeStat == null ? props.range : (Caster ?? Weapon).GetStatValue(props.rangeStat);
        if (weather && !IgnoreEnvironment && Caster?.MapHeld is Map map && map.weatherManager.CurWeatherMaxRangeCap >= 0)
            range = Math.Min(range, map.weatherManager.CurWeatherMaxRangeCap);
        return range * Weapon.GetStatValue(StatDefOf.RangedWeapon_RangeMultiplier);
    }

    public float AimFactor(Scenario scenario)
    {
        if (Pawn != null) return ProfileStats.Read(Pawn, StatDefOf.AimingDelayFactor, scenario, Weapon);
        if (scenario == Scenario.Current || !CanCompare) return 1;
        // No pawn: retain the original mod's ideal-shooter convention, but use loaded defs.
        var role = DefDatabase<PreceptDef>.GetNamed("IdeoRole_ShootingSpecialist");
        float value = StatDefOf.AimingDelayFactor.defaultBaseValue + role.roleEffects
            .OfType<RoleEffect_PawnStatOffset>().Where(e => e.statDef == StatDefOf.AimingDelayFactor).Sum(e => e.modifier);
        if (scenario == Scenario.SpecialistTriggerHappy)
            value += DefDatabase<TraitDef>.GetNamed("ShootingAccuracy").DataAtDegree(-1).statOffsets
                .GetStatOffsetFromList(StatDefOf.AimingDelayFactor);
        return Mathf.Clamp(value, StatDefOf.AimingDelayFactor.minValue, StatDefOf.AimingDelayFactor.maxValue);
    }

    public (int warmup, int cooldown, int burst, int schedule) Timing(Scenario scenario)
    {
        if (Turret != null)
        {
            // Renewal rate: damage / mean cycle time, not mean(fast DPS, slow DPS).
            var props = Turret.def.building;
            var warmup = Math.Max(0, props.turretBurstWarmupTime.Average.SecondsToTicks());
            var cooldown = props.turretBurstCooldownTime >= 0 ? props.turretBurstCooldownTime : Verb.verbProps.defaultCooldownTime;
            return (warmup, Math.Max(1, cooldown.SecondsToTicks()), (Shots - 1) * IntervalTicks, 0);
        }
        int aimTicks = Verb.WarmupTime > 0 ? Math.Max(1, (Verb.WarmupTime * AimFactor(scenario)).SecondsToTicks()) : 0;
        int coolTicks = Math.Max(1, Pawn == null || (scenario == Scenario.Current && Weapon == Pawn.equipment?.Primary) ?
            Verb.verbProps.AdjustedCooldownTicks(Verb, Pawn) :
            (Verb.verbProps.AdjustedCooldown(Verb, null) * ProfileStats.Read(Pawn, StatDefOf.RangedCooldownFactor, scenario, Weapon)).SecondsToTicks());
        // Pawn stance/jobs precede equipment.VerbsTick. The first burst counter is
        // decremented in the same tick as WarmupComplete, so that first gap is one
        // tick shorter. Subsequent gaps still take max(1, configured interval).
        int burstTicks = Math.Max(0, (Shots - 1) * IntervalTicks - (Shots > 1 && Verb.WarmupTime > 0 ? 1 : 0));
        int cycle = aimTicks + coolTicks + burstTicks;
        // 1.7 AttackStatic starts attacks in JobTrackerTickInterval. Its cadence
        // depends on camera visibility/zoom; stances and burst shots still tick every tick.
        int schedule = (JobCadence - cycle % JobCadence) % JobCadence;
        return (aimTicks, coolTicks, burstTicks, schedule);
    }

    public float RawDps(Scenario scenario)
    {
        var t = Timing(scenario);
        return Damage * Shots * 60f / Math.Max(1, t.warmup + t.cooldown + t.burst + t.schedule);
    }

    public float WeatherFactor(bool targetRoofed)
    {
        if (Caster?.Spawned != true || IgnoreEnvironment) return 1;
        return Caster.Position.Roofed(Caster.Map) && targetRoofed ? 1 : Caster.Map.weatherManager.CurWeatherAccuracyMultiplier;
    }

    public float HitChance(float distance, Scenario scenario, bool weather = false, bool targetRoofed = false, bool weaponOnly = false)
    {
        if (!CanEstimate || distance < MinRange || distance > Range(weather)) return 0;
        if (!Verb.verbProps.canGoWild) return 1;
        float shooter = 1;
        if (!weaponOnly && Pawn != null)
            shooter = ProfileStats.HitFactor(Pawn, distance, scenario, Weapon);
        else if (!weaponOnly && Turret != null)
            shooter = ShotReport.HitFactorFromShooter(Turret, distance);
        float value = shooter * Verb.verbProps.GetHitChanceFactor(Weapon, distance) * (weather ? WeatherFactor(targetRoofed) : 1);
        return Mathf.Clamp01(Math.Max(0.0201f, value));
    }

    public float Dps(float distance, Scenario scenario, bool weather = false, bool targetRoofed = false, bool weaponOnly = false) =>
        RawDps(scenario) * HitChance(distance, scenario, weather, targetRoofed, weaponOnly);

    public IEnumerable<float> Distances()
    {
        // Include fractional endpoints. Enumerable.Range(start, count) does not accept an end index.
        yield return MinRange;
        for (int distance = (int)Math.Floor(MinRange) + 1; distance < MaxRange; distance++) yield return distance;
        if (MaxRange > MinRange) yield return MaxRange;
    }

    public (float range, float dps) Best(Scenario scenario, bool weather = false, bool targetRoofed = false, bool weaponOnly = false)
    {
        float bestRange = MinRange, bestDps = -1;
        foreach (var distance in Distances())
        {
            float dps = Dps(distance, scenario, weather, targetRoofed, weaponOnly);
            if (dps > bestDps) { bestRange = distance; bestDps = dps; }
        }
        return (bestRange, Math.Max(0, bestDps));
    }

    public float DamagePerResource
    {
        get
        {
            var fuel = Turret?.TryGetComp<CompRefuelable>();
            if (fuel == null) return 0;
            float perShot = Verb.verbProps.consumeFuelPerShot + Verb.verbProps.consumeFuelPerBurst / Shots;
            return perShot > 0 ? Damage * fuel.Props.FuelMultiplierCurrentDifficulty / perShot : 0;
        }
    }

    public float? TargetHitChance(Thing target, Scenario scenario)
    {
        if (!CanEstimate || Caster?.Spawned != true || target?.Spawned != true || target.Map != Caster.Map) return null;
        if (!Verb.CanHitTargetFrom(Caster.Position, target)) return 0;
        var report = ShotReport.HitReportFor(Caster, Verb, target);
        if (Pawn != null && (scenario != Scenario.Current || Weapon != Pawn.equipment?.Primary))
        {
            // Only this local value-type copy receives preview factors.
            object preview = report;
            if (Verb.verbProps.canGoWild)
                typeof(ShotReport).GetField("factorFromShooterAndDist", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(preview, ProfileStats.HitFactor(Pawn, (target.Position - Caster.Position).LengthHorizontal, scenario, Weapon));
            if (ModsConfig.IdeologyActive)
            {
                StatDef darkStat = DarknessCombatUtility.IsOutdoorsAndLit(target) ? StatDefOf.ShootingAccuracyOutdoorsLitOffset :
                    DarknessCombatUtility.IsOutdoorsAndDark(target) ? StatDefOf.ShootingAccuracyOutdoorsDarkOffset :
                    DarknessCombatUtility.IsIndoorsAndDark(target) ? StatDefOf.ShootingAccuracyIndoorsDarkOffset :
                    DarknessCombatUtility.IsIndoorsAndLit(target) ? StatDefOf.ShootingAccuracyIndoorsLitOffset : null;
                if (darkStat != null)
                    typeof(ShotReport).GetField("offsetFromDarkness", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(preview, ProfileStats.Read(Pawn, darkStat, scenario, Weapon));
            }
            report = (ShotReport)preview;
        }
        if (Verb.verbProps.canGoWild) return report.TotalEstimatedHitChance;
        float posture = target is Pawn p && (target.Position - Caster.Position).LengthHorizontal >= 4.5f &&
            p.GetPosture() != PawnPosture.Standing ? 0.5f : 1;
        return Mathf.Clamp01(report.PassCoverChance * posture);
    }
}
