using System.Collections;
using System.Text.Json;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RangedFirepowerStats.Tests;

public sealed class GameChecks(Map map) : MapComponent(map)
{
    private IEnumerator checks;
    private bool finished;
    private int renderedFrames;
    private readonly List<string> passed = new();
    private readonly Dictionary<string, object> samples = new();
    private static Thing watchedWeapon;
    private static readonly List<int> shotTicks = new();
    private bool Render => Environment.GetEnvironmentVariable("RFS_RENDER") == "1";
    private bool NoIdeology => !ModsConfig.IdeologyActive;

    private void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        passed.Add(message);
        Log.Message("RFS_ASSERT_OK " + message);
    }

    private void Near(float value, float expected, string message) =>
        Require(Math.Abs(value - expected) < 0.0001f, $"{message}: {value:0.#####} = {expected:0.#####}");

    private ThingWithComps Equip(Pawn pawn, string defName)
    {
        pawn.equipment.DestroyAllEquipment();
        var gun = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(defName));
        gun.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Colony);
        pawn.equipment.AddEquipment(gun);
        return gun;
    }

    private IEnumerable Run()
    {
        Require(NoIdeology == (Environment.GetEnvironmentVariable("RFS_NO_IDEOLOGY") == "1"), "real loaded Ideology state matches the requested test case");
        Require(typeof(RangedFirepowerStatsMod).Assembly.Location.Contains("/1.7/Assemblies/"), "matching 1.7 runtime assembly loaded");
        Require(DefDatabase<StatDef>.AllDefs.Count(d => d.defName.StartsWith("RFS_")) == 12, "all twelve production stats loaded");
        var pawn = map.mapPawns.FreeColonistsSpawned.First(p => !StatDefOf.ShootingAccuracyPawn.Worker.IsDisabledFor(p));
        pawn.story.traits.allTraits.Clear();
        pawn.skills.GetSkill(SkillDefOf.Shooting).Level = 10;
        pawn.drafter.Drafted = true;
        pawn.drafter.FireAtWill = false;
        pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait_Combat), JobCondition.InterruptForced);
        foreach (var cell in CellRect.FromLimits(35, 35, 75, 55).Cells)
        {
            foreach (var thing in cell.GetThingList(map).ToArray())
                if (thing is Building or Plant) { if (thing.def.destroyable) thing.Destroy(); else thing.DeSpawn(); }
            map.roofGrid.SetRoof(cell, null);
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
        }
        pawn.Position = new IntVec3(40, 0, 45);
        pawn.Notify_Teleported();
        var gun = Equip(pawn, "Gun_AssaultRifle");
        var model = DpsModel.Create(pawn);
        Require(model != null && model.Shots == 3 && model.CanEstimate, "assault rifle uses a supported three-shot burst");
        var timing = model.Timing(Scenario.Current);
        Require(timing.burst == 2 * model.Verb.TicksBetweenBurstShots - 1, "three-shot pawn burst includes two intervals minus the first-tick phase overlap");
        float expected = model.Damage * 3 * 60f / (timing.warmup + timing.cooldown + timing.burst + timing.schedule);
        Near(model.RawDps(Scenario.Current), expected, "burst damage divided by full tick cycle");

        var hasteDef = new HediffDef { defName = "RFS_TestHaste", label = "test haste", hediffClass = typeof(HediffWithComps),
            stages = [new HediffStage { statFactors = [new StatModifier { stat = StatDefOf.RangedCooldownFactor, value = 0.5f }] }] };
        var haste = pawn.health.AddHediff(hasteDef);
        Near(model.Timing(Scenario.Current).cooldown, model.Verb.verbProps.AdjustedCooldownTicks(model.Verb, pawn), "shooter ranged cooldown factor is included");
        Require(model.RawDps(Scenario.Current) > expected, "halved cooldown increases DPS");
        pawn.health.RemoveHediff(haste);

        if (!NoIdeology)
        {
            float originalAccuracy = pawn.GetStatValue(StatDefOf.ShootingAccuracyPawn);
            float originalAim = pawn.GetStatValue(StatDefOf.AimingDelayFactor);
            var originalRole = pawn.Ideo.GetRole(pawn);
            Require(originalRole == null, "disposable shooter starts without a specialist role");
            float hypotheticalAccuracy = ProfileStats.Read(pawn, StatDefOf.ShootingAccuracyPawn, Scenario.Specialist);
            float hypotheticalAim = ProfileStats.Read(pawn, StatDefOf.AimingDelayFactor, Scenario.Specialist);
            float combinedAccuracy = ProfileStats.Read(pawn, StatDefOf.ShootingAccuracyPawn, Scenario.SpecialistTriggerHappy);
            float combinedAim = ProfileStats.Read(pawn, StatDefOf.AimingDelayFactor, Scenario.SpecialistTriggerHappy);
            Require(hypotheticalAccuracy > originalAccuracy && combinedAccuracy < hypotheticalAccuracy,
                "specialist bonus and trigger-happy accuracy penalty pass through the vanilla curve");
            Near(combinedAim, 0.01f, "vanilla aiming delay has a one-percent floor");
            Require(pawn.Ideo.GetRole(pawn) == originalRole && pawn.story.traits.allTraits.Count == 0,
                "hypothetical evaluation does not change role or traits");
            Near(pawn.GetStatValue(StatDefOf.ShootingAccuracyPawn), originalAccuracy, "hypothetical accuracy is not cached on the pawn");
            Near(pawn.GetStatValue(StatDefOf.AimingDelayFactor), originalAim, "hypothetical aiming delay is not cached on the pawn");

            // Independent comparison: actually assign the role and trait in this disposable fixture.
            var role = new Precept_RoleMulti { def = DefDatabase<PreceptDef>.GetNamed("IdeoRole_ShootingSpecialist") };
            role.chosenPawns.Add(new IdeoRoleInstance(role) { pawn = pawn });
            pawn.Ideo.AddPrecept(role);
            Near(pawn.GetStatValue(StatDefOf.ShootingAccuracyPawn), hypotheticalAccuracy, "simulated specialist accuracy equals an actual assigned role");
            Near(pawn.GetStatValue(StatDefOf.AimingDelayFactor), hypotheticalAim, "simulated specialist aiming equals an actual assigned role");
            Near(ProfileStats.Read(pawn, StatDefOf.ShootingAccuracyPawn, Scenario.Specialist), hypotheticalAccuracy, "existing specialist is not counted twice");
            pawn.story.traits.GainTrait(new Trait(DefDatabase<TraitDef>.GetNamed("ShootingAccuracy"), -1));
            Near(pawn.GetStatValue(StatDefOf.ShootingAccuracyPawn), combinedAccuracy, "simulated trigger-happy accuracy equals the actual trait");
            Near(pawn.GetStatValue(StatDefOf.AimingDelayFactor), combinedAim, "simulated trigger-happy aiming equals the actual trait");
            Near(ProfileStats.Read(pawn, StatDefOf.ShootingAccuracyPawn, Scenario.SpecialistTriggerHappy), combinedAccuracy, "existing trigger-happy is not counted twice");
            pawn.story.traits.allTraits.Clear();
            pawn.Ideo.RemovePrecept(role);
            pawn.story.traits.GainTrait(new Trait(DefDatabase<TraitDef>.GetNamed("ShootingAccuracy"), 1));
            Near(ProfileStats.Read(pawn, StatDefOf.ShootingAccuracyPawn, Scenario.SpecialistTriggerHappy), combinedAccuracy, "trigger-happy replaces careful-shooter instead of stacking incompatible traits");
            pawn.story.traits.allTraits.Clear();
            Near(pawn.GetStatValue(StatDefOf.AimingDelayFactor), originalAim, "private preview workers leave the game's aiming stat unchanged");
        }
        else Require(!model.CanCompare, "specialist UI is absent without Ideology");

        var rain = DefDatabase<WeatherDef>.GetNamed("Rain");
        map.weatherManager.TransitionTo(rain);
        map.weatherManager.curWeatherAge = 4000;
        float actualWeather = map.weatherManager.CurWeatherAccuracyMultiplier;
        Require(actualWeather < 1, "fixture rain applies a real accuracy penalty after its transition");
        Near(model.WeatherFactor(false), actualWeather, "open target uses current weather accuracy");
        map.roofGrid.SetRoof(pawn.Position, RoofDefOf.RoofConstructed);
        Near(model.WeatherFactor(true), 1, "both roofed endpoints bypass weather");
        Near(model.WeatherFactor(false), actualWeather, "roofed shooter alone does not bypass rain");
        map.roofGrid.SetRoof(pawn.Position, null);
        var originalWeather = map.weatherManager.curWeather;
        var cappedWeather = (WeatherDef)AccessTools.Method(typeof(object), "MemberwiseClone").Invoke(originalWeather, null);
        cappedWeather.maxRangeCap = 12;
        map.weatherManager.curWeather = cappedWeather;
        Near(model.Range(true), model.Verb.EffectiveRange, "weather-limited range matches the native verb");
        Require(model.HitChance(20, Scenario.Current, weather: true) == 0, "table DPS is zero beyond the weather range cap");
        map.weatherManager.curWeather = originalWeather;
        float normalAccuracy = model.HitChance(12, Scenario.Current), normalDamage = model.Damage;
        gun.GetComp<CompQuality>().SetQuality(QualityCategory.Legendary, ArtGenerationContext.Colony);
        Require(model.HitChance(12, Scenario.Current) > normalAccuracy && model.Damage >= normalDamage,
            "weapon quality affects model accuracy and primary damage through native stats");
        gun.GetComp<CompQuality>().SetQuality(QualityCategory.Normal, ArtGenerationContext.Colony);
        var targetCell = pawn.Position + new IntVec3(20, 0, 0);
        float direct = ShotReport.HitReportFor(pawn, model.Verb, targetCell).AimOnTargetChance;
        Near(model.HitChance(20, Scenario.Current, weather: true), direct, "distance-table hit chance matches native ShotReport before cover in rain");
        samples["assaultRifle"] = new { damage = model.Damage, shots = model.Shots, interval = model.IntervalTicks,
            normal = model.RawDps(Scenario.Current), specialist = model.RawDps(Scenario.Specialist), trigger = model.RawDps(Scenario.SpecialistTriggerHappy),
            rain20 = model.Dps(20, Scenario.Current, weather: true) };

        var loose = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle"));
        Require(DpsModel.Create(loose) != null, "unheld weapons support ideal-shooter stats without a caster");
        var alternate = DpsModel.Create(loose, pawn);
        Require(alternate.Shots == 1 && alternate.Timing(Scenario.Current).burst == 0, "single-shot weapons have no internal burst gap");
        Require(alternate.Verb.Caster == pawn && loose.GetComp<CompEquippable>().PrimaryVerb.Caster != pawn,
            "loose weapon comparison uses a separate bound verb without rebinding the real weapon");
        float beforeSwapAim = pawn.GetStatValue(StatDefOf.AimingDelayFactor);
        var candidateDef = (ThingDef)AccessTools.Method(typeof(object), "MemberwiseClone").Invoke(loose.def, null);
        candidateDef.equippedStatOffsets = [new StatModifier { stat = StatDefOf.AimingDelayFactor, value = 0.3f }];
        loose.def = candidateDef;
        float previewAim = alternate.AimFactor(Scenario.Current);
        Near(previewAim, beforeSwapAim + 0.3f, "candidate weapon's equipped stat offsets are included without equipping it");
        pawn.equipment.Remove(gun);
        pawn.equipment.AddEquipment(loose);
        Near(previewAim, pawn.GetStatValue(StatDefOf.AimingDelayFactor), "candidate equipment preview equals actually swapping the fixture weapon");
        pawn.equipment.Remove(loose);
        pawn.equipment.AddEquipment(gun);
        Near(pawn.GetStatValue(StatDefOf.AimingDelayFactor), beforeSwapAim, "candidate preview leaves the original equipment unchanged");
        var targetPawn = map.mapPawns.FreeColonistsSpawned.First(p => p != pawn);
        targetPawn.Position = targetCell;
        targetPawn.Notify_Teleported();
        Require(alternate.TargetHitChance(targetPawn, Scenario.Current).HasValue, "loose weapon can evaluate a real target safely");
        Near(model.TargetHitChance(targetPawn, Scenario.Current).Value,
            ShotReport.HitReportFor(pawn, model.Verb, targetPawn).TotalEstimatedHitChance, "real-target estimate delegates to native ShotReport");
        if (!NoIdeology)
        {
            float specialistTarget = model.TargetHitChance(targetPawn, Scenario.Specialist).Value;
            var role = new Precept_RoleMulti { def = DefDatabase<PreceptDef>.GetNamed("IdeoRole_ShootingSpecialist") };
            role.chosenPawns.Add(new IdeoRoleInstance(role) { pawn = pawn });
            pawn.Ideo.AddPrecept(role);
            Near(specialistTarget, ShotReport.HitReportFor(pawn, model.Verb, targetPawn).TotalEstimatedHitChance,
                "local specialist ShotReport preview equals the real assigned-role report");
            float triggerTarget = model.TargetHitChance(targetPawn, Scenario.SpecialistTriggerHappy).Value;
            pawn.story.traits.GainTrait(new Trait(DefDatabase<TraitDef>.GetNamed("ShootingAccuracy"), -1));
            Near(triggerTarget, ShotReport.HitReportFor(pawn, model.Verb, targetPawn).TotalEstimatedHitChance,
                "local trigger-happy ShotReport preview equals the real trait report");
            pawn.story.traits.allTraits.Clear();
            pawn.Ideo.RemovePrecept(role);
        }

        // Nonzero min range reproduces the old Enumerable.Range bug; never search out of range.
        var nativeProps = model.Verb.verbProps;
        var testProps = (VerbProperties)AccessTools.Method(typeof(object), "MemberwiseClone").Invoke(nativeProps, null);
        testProps.minRange = 10;
        testProps.range = 25.9f;
        model.Verb.verbProps = testProps;
        Require(model.Distances().All(d => d >= 10 && d <= model.MaxRange) && model.Distances().Last() == model.MaxRange,
            "range table respects nonzero minimum and fractional maximum");
        Require(model.Best(Scenario.Current).range <= model.MaxRange, "optimal range is never outside the weapon range");
        AccessTools.Field(typeof(VerbProperties), "forcedMissRadius").SetValue(testProps, 2f);
        Require(!model.CanEstimate, "forced-scatter attacks do not claim an exact single-target expectation");
        model.Verb.verbProps = nativeProps;

        var turretDef = DefDatabase<ThingDef>.GetNamed("Turret_MiniTurret");
        var turret = (Building_TurretGun)ThingMaker.MakeThing(turretDef, turretDef.MadeFromStuff ? ThingDefOf.Steel : null);
        var turretModel = DpsModel.Create(turret);
        Require(turretModel != null && turretModel.Pawn == null, "turret uses its own gun and timing");
        Near(turretModel.Timing(Scenario.Current).warmup, turretDef.building.turretBurstWarmupTime.Average.SecondsToTicks(), "turret sustained rate uses mean warmup");
        Require(!turretModel.CanCompare, "turrets do not receive pawn specialist bonuses");
        var turretNativeProps = turretModel.Verb.verbProps;
        var turretFuelProps = (VerbProperties)AccessTools.Method(typeof(object), "MemberwiseClone").Invoke(turretNativeProps, null);
        turretFuelProps.consumeFuelPerShot = 0;
        turretFuelProps.consumeFuelPerBurst = 1;
        turretModel.Verb.verbProps = turretFuelProps;
        Near(turretModel.DamagePerResource, turretModel.Damage * turretModel.Shots * turret.TryGetComp<CompRefuelable>().Props.FuelMultiplierCurrentDifficulty,
            "per-resource efficiency includes per-burst consumption and all burst shots");
        turretModel.Verb.verbProps = turretNativeProps;
        foreach (var stat in DefDatabase<StatDef>.AllDefs.Where(d => d.defName.StartsWith("RFS_")))
        {
            var inspected = stat.defName.StartsWith("RFS_Pawn") ? (Thing)pawn : stat.defName.StartsWith("RFS_Weapon") ? gun : turret;
            var request = StatRequest.For(inspected);
            if (!stat.Worker.ShouldShowFor(request)) continue;
            float value = stat.Worker.GetValue(request);
            Require(float.IsFinite(value) && !string.IsNullOrEmpty(stat.Worker.GetExplanationUnfinalized(request, ToStringNumberSense.Absolute)),
                stat.defName + " calculates and explains without an error");
        }
        var originalStats = DefDatabase<StatDef>.AllDefs.Where(d => d.workerClass?.Assembly.GetName().Name == "RangedDPS").ToArray();
        foreach (var stat in originalStats)
        {
            var request = StatRequest.For(stat.defName.Contains("Turret") ? turret : stat.defName.Contains("Pawn") ? pawn : gun);
            Require(stat.Worker.ShouldShowFor(request), "existing mod stat remains visible: " + stat.defName);
            float before = stat.Worker.GetValue(request);
            ProfileStats.Read(pawn, StatDefOf.ShootingAccuracyPawn, Scenario.SpecialistTriggerHappy);
            Near(stat.Worker.GetValue(request), before, "existing mod stat unchanged by previews: " + stat.defName);
            var method = stat.Worker.GetType().GetMethod(nameof(StatWorker.ShouldShowFor));
            Require(Harmony.GetPatchInfo(method)?.Owners.Contains("RunningBugs.RangedFirepowerStats") != true,
                "no RangedFirepowerStats patch on existing mod worker: " + stat.defName);
        }
        foreach (var method in new[] { nameof(StatWorker.GetBaseValueFor), nameof(StatWorker.GetValueUnfinalized), nameof(StatWorker.FinalizeValue) })
            Require(Harmony.GetPatchInfo(AccessTools.Method(typeof(StatWorker), method))?.Owners.Contains("RunningBugs.RangedFirepowerStats") != true,
                "global stat calculation is not patched: " + method);

        watchedWeapon = gun;
        new Harmony("local.rangedfirepowerstats.tests").Patch(AccessTools.Method(typeof(Verb_Shoot), "TryCastShot"),
            postfix: new HarmonyMethod(typeof(GameChecks), nameof(RecordShot)));
        shotTicks.Clear();
        Require(model.Verb.TryStartCastOn(targetPawn, canHitNonTargetPawns: false), "native burst starts on the disposable map");
        int deadline = Find.TickManager.TicksGame + 900;
        while (shotTicks.Count < model.Shots && Find.TickManager.TicksGame < deadline) yield return null;
        Require(shotTicks.Count == model.Shots, "native engine fired all three burst shots");
        samples["nativeBurstTicks"] = shotTicks.ToArray();
        Require(shotTicks.Last() - shotTicks.First() == model.Timing(Scenario.Current).burst,
            "observed native burst duration equals the model: " + string.Join(",", shotTicks));

        pawn.stances.CancelBusyStanceHard();
        testProps = (VerbProperties)AccessTools.Method(typeof(object), "MemberwiseClone").Invoke(nativeProps, null);
        testProps.ticksBetweenBurstShots = 0;
        testProps.warmupTime = 0.001f;
        model.Verb.verbProps = testProps;
        AccessTools.Field(typeof(Verb), "cachedTicksBetweenBurstShots").SetValue(model.Verb, null);
        shotTicks.Clear();
        Require(model.Timing(Scenario.Current).warmup == 1 && model.IntervalTicks == 1, "sub-tick aiming and zero burst delay cannot produce infinite DPS");
        Require(model.Verb.TryStartCastOn(targetPawn, canHitNonTargetPawns: false), "minimum-interval native fixture starts");
        deadline = Find.TickManager.TicksGame + 200;
        while (shotTicks.Count < model.Shots && Find.TickManager.TicksGame < deadline) yield return null;
        samples["nativeZeroDelayTicks"] = shotTicks.ToArray();
        Require(shotTicks.Count == model.Shots && shotTicks.Last() > shotTicks.First(), "native zero-delay burst spans multiple ticks: " + string.Join(",", shotTicks));
        Require(shotTicks.Last() - shotTicks.First() == model.Timing(Scenario.Current).burst, "zero-delay burst model includes the same-tick first pair");
        model.Verb.verbProps = nativeProps;
        model.Verb.Reset();
        AccessTools.Field(typeof(Verb), "cachedTicksBetweenBurstShots").SetValue(model.Verb, null);

        var wall = GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Steel), targetCell + new IntVec3(0, 0, 2), map);
        pawn.stances.CancelBusyStanceHard();
        shotTicks.Clear();
        var attack = JobMaker.MakeJob(JobDefOf.AttackStatic, wall);
        attack.maxNumStaticAttacks = 3;
        attack.playerForced = true;
        pawn.jobs.StartJob(attack, JobCondition.InterruptForced);
        deadline = Find.TickManager.TicksGame + 1500;
        while (shotTicks.Count < 9 && Find.TickManager.TicksGame < deadline) yield return null;
        samples["nativeRepeatedBurstTicks"] = shotTicks.ToArray();
        timing = model.Timing(Scenario.Current);
        Require(shotTicks.Count == 9 && shotTicks[3] - shotTicks[0] == timing.warmup + timing.burst + timing.cooldown + timing.schedule &&
            shotTicks[6] - shotTicks[3] == timing.warmup + timing.burst + timing.cooldown + timing.schedule,
            "native AttackStatic repeated-cycle ticks equal the DPS denominator: " + string.Join(",", shotTicks));
        pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait_Combat), JobCondition.InterruptForced);
        pawn.stances.CancelBusyStanceHard();

        var unique = gun.TryGetComp<CompUniqueWeapon>();
        if (unique == null)
        {
            unique = new CompUniqueWeapon { parent = gun };
            unique.Initialize(new CompProperties_UniqueWeapon());
            gun.AllComps.Add(unique);
        }
        unique.TraitsListForReading.Clear();
        unique.TraitsListForReading.Add(new WeaponTraitDef { defName = "RFS_TestWeaponTrait", burstShotCountMultiplier = 2, burstShotSpeedMultiplier = 2,
            statFactors = [new StatModifier { stat = StatDefOf.RangedWeapon_WarmupMultiplier, value = 1.5f }] });
        StatDefOf.RangedWeapon_WarmupMultiplier.Worker.ClearCacheForThing(gun);
        AccessTools.Field(typeof(Verb), "cachedBurstShotCount").SetValue(model.Verb, null);
        AccessTools.Field(typeof(Verb), "cachedTicksBetweenBurstShots").SetValue(model.Verb, null);
        Require(model.Shots == 6 && model.IntervalTicks == 5, "weapon-instance burst-count and burst-speed traits are respected");
        Near(model.Verb.WarmupTime, nativeProps.warmupTime * 1.5f, "weapon-instance warmup multiplier is respected");
        unique.TraitsListForReading.Clear();
        StatDefOf.RangedWeapon_WarmupMultiplier.Worker.ClearCacheForThing(gun);
        AccessTools.Field(typeof(Verb), "cachedBurstShotCount").SetValue(model.Verb, null);
        AccessTools.Field(typeof(Verb), "cachedTicksBetweenBurstShots").SetValue(model.Verb, null);
        watchedWeapon = null;

        var window = new DpsWindow(pawn);
        var tsv = window.ExportTsv();
        Require(tsv.Contains('\t') && !tsv.Contains("RFS_"), "export contains localized per-cell columns");
        File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "table.tsv"), tsv);
        Require(pawn.GetGizmos().OfType<Command_Action>().Any(c => c.defaultLabel == Labels.Text("Open")), "armed pawn exposes the localized table command");
        var category = DefDatabase<StatCategoryDef>.GetNamed("RFS_RangedFirepower");
        Require(DefDatabase<StatDef>.AllDefs.Where(d => d.defName.StartsWith("RFS_")).All(d => d.category == category),
            "all twelve mod stats belong to the independent firepower category");
        Require(StatDefOf.ShootingAccuracyPawn.category == StatCategoryDefOf.PawnCombat,
            "native shooting accuracy remains in the native combat category");
        var graph = window.OpenGraph();
        Require(Find.WindowStack.WindowOfType<DpsGraphWindow>() == graph,
            "table chart link opens a separate graph window");
        var firstRow = tsv.Split('\n').First(line => line.StartsWith("1\t")).Split('\t');
        Require(Labels.Percent(graph.Data.Points[0].Hit[0]) == firstRow[1] && Labels.Number(graph.Data.Points[0].Dps[0]) == firstRow[2],
            "graph snapshot exactly matches the displayed first table row");
        Near(graph.Data.Points[^1].Distance, DpsModel.Create(pawn).MaxRange, "graph preserves the fractional range endpoint");
        Require(graph.Data.Scenarios.Count == (NoIdeology ? 1 : 3), "chart series reflect optional specialist availability");
        float captured = graph.Data.Points[0].Hit[0];
        int originalLevel = pawn.skills.GetSkill(SkillDefOf.Shooting).Level;
        pawn.skills.GetSkill(SkillDefOf.Shooting).Level = 1;
        window.Refresh();
        Near(graph.Data.Points[0].Hit[0], captured, "open chart retains its original snapshot after table refresh");
        pawn.skills.GetSkill(SkillDefOf.Shooting).Level = originalLevel;
        window.Refresh();
        graph.SetMetric(true);
        Require(graph.ShowDps, "chart switches from accuracy to expected DPS");
        graph.SetMetric(false);
        Require(!graph.ShowDps, "chart switches back to accuracy");
        graph.Close(false);

        var dpsStat = DefDatabase<StatDef>.GetNamed("RFS_PawnCurrent");
        var dpsLink = dpsStat.Worker.GetInfoCardHyperlinks(StatRequest.For(pawn)).Single(h => h.def is GraphLinkDef);
        dpsLink.ActivateHyperlink();
        var linkedGraph = Find.WindowStack.WindowOfType<DpsGraphWindow>();
        Require(linkedGraph != null && linkedGraph.ShowDps, "DPS explanation hyperlink opens the expected DPS plot");
        linkedGraph.Close(false);
        var accuracyLink = StatDefOf.ShootingAccuracyPawn.Worker.GetInfoCardHyperlinks(StatRequest.For(pawn)).Single(h => h.def is GraphLinkDef);
        accuracyLink.ActivateHyperlink();
        linkedGraph = Find.WindowStack.WindowOfType<DpsGraphWindow>();
        Require(linkedGraph != null && !linkedGraph.Data.SupportsDps && linkedGraph.Data.Points.Count == 55,
            "native precision-table hyperlink opens its own shooter-accuracy chart");
        Near(linkedGraph.Data.Points[4].Hit[0], ShotReport.HitFactorFromShooter(pawn, 5),
            "native accuracy chart matches the original precision table at five cells");
        linkedGraph.SetMetric(true);
        Require(!linkedGraph.ShowDps, "pure shooter accuracy is never relabeled as combined weapon DPS");
        linkedGraph.Close(false);
        if (Render)
        {
            if (Environment.GetEnvironmentVariable("RFS_WORKSHOP_MEDIA") == "1")
            {
                Prefs.DevMode = false;
                Prefs.AdaptiveTrainingEnabled = false;
                Find.PlaySettings.showLearningHelper = false;
                Find.UIRoot.screenshotMode.Active = true;
                pawn.Name = new NameTriple("Mira", "Mira", "Voss");
                pawn.story.title = "射手";
                pawn.health.RemoveAllHediffs();
                pawn.skills.GetSkill(SkillDefOf.Shooting).Level = 10;
                Find.CameraDriver.SetRootPosAndSize(pawn.Position.ToVector3Shifted(), 20);
                for (int frame = 0; frame < 10; frame++) yield return null;
                window.Refresh();
            }
            Find.WindowStack.Add(new Dialog_InfoCard(pawn));
            for (int frame = 0; frame < 20; frame++) yield return null;
            StatsReportUtility.SelectEntry(dpsStat);
            ((ScrollPositioner)AccessTools.Field(typeof(StatsReportUtility), "scrollPositioner").GetValue(null)).Arm();
            for (int frame = 0; frame < 10; frame++) yield return null;
            AccessTools.Field(typeof(StatsReportUtility), "scrollPositionRightPanel").SetValue(null, Vector2.zero);
            for (int frame = 0; frame < 10; frame++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "info-overview.png"));
            for (int frame = 0; frame < 10; frame++) yield return null;
            AccessTools.Field(typeof(StatsReportUtility), "scrollPositionRightPanel").SetValue(null, new Vector2(0, 10000));
            for (int frame = 0; frame < 10; frame++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "pawn-card.png"));
            for (int frame = 0; frame < 10; frame++) yield return null;
            StatsReportUtility.SelectEntry(StatDefOf.ShootingAccuracyPawn);
            ((ScrollPositioner)AccessTools.Field(typeof(StatsReportUtility), "scrollPositioner").GetValue(null)).Arm();
            AccessTools.Field(typeof(StatsReportUtility), "scrollPositionRightPanel").SetValue(null, Vector2.zero);
            for (int frame = 0; frame < 15; frame++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "accuracy-card.png"));
            for (int frame = 0; frame < 10; frame++) yield return null;
            Find.WindowStack.TryRemove(typeof(Dialog_InfoCard), false);
            Find.WindowStack.Add(window);
            while (renderedFrames++ < 25) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "table.png"));
            while (renderedFrames++ < 45) yield return null;
            Require(File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "table.png")), "rendered native window screenshot captured");
            graph = window.OpenGraph();
            if (Environment.GetEnvironmentVariable("RFS_WORKSHOP_MEDIA") == "1")
                Find.WindowStack.TryRemove(typeof(DpsWindow), false);
            for (int frame = 0; frame < 20; frame++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "graph-accuracy.png"));
            for (int frame = 0; frame < 10; frame++) yield return null;
            graph.SetMetric(true);
            for (int frame = 0; frame < 15; frame++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "graph-dps.png"));
            for (int frame = 0; frame < 10; frame++) yield return null;
            Require(File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "graph-accuracy.png")) &&
                File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "graph-dps.png")), "both native chart modes rendered and captured");
        }
    }

    private static void RecordShot(Verb_Shoot __instance, bool __result)
    {
        if (__result && __instance.EquipmentSource == watchedWeapon) shotTicks.Add(Find.TickManager.TicksGame);
    }

    public override void MapComponentUpdate()
    {
        if (finished) { Root.Shutdown(); return; }
        if (Current.ProgramState != ProgramState.Playing || LongEventHandler.AnyEventNowOrWaiting) return;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
        try
        {
            checks ??= Run().GetEnumerator();
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception exception) { Log.Error("RFS_TEST_FAILED " + exception); Finish(exception.ToString()); }
    }

    private void Finish(string error)
    {
        finished = true;
        File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "rfs-result.json"), JsonSerializer.Serialize(
            new { passed = error == null, checks = passed, samples, error }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
