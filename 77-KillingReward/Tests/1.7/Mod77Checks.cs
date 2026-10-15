using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RW17.Validation;

public static class Mod77Checks
{
    public const string PackageId = "RunningBugs.KillingReward";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Call(Assembly assembly, string type, string method, params object[] args) => assembly.GetType("KillingReward." + type, true).GetMethod(method).Invoke(null, args);

    public static void Run(Map map)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "KillingReward");
        var trackerType = assembly.GetType("KillingReward.KillRewardTracker", true);
        var tracker = Current.Game.GetComponent(trackerType);
        Require(tracker != null && Harmony.GetPatchInfo(typeof(Pawn).GetMethod("Kill"))?.Postfixes.Any(p => p.PatchMethod.DeclaringType?.FullName == "KillingReward.PawnKillPatch") == true, "Actual tracker/kill hook missing");
        var fields = new[] { "level", "progress", "pending", "traitMods", "geneMods" }.Select(n => trackerType.GetField(n, Private)).ToArray();
        var oldTracker = fields.Select(f => f.GetValue(tracker)).ToArray();
        var settings = (ModSettings)assembly.GetType("KillingReward.KillingRewardMod").GetProperty("Settings").GetValue(null);
        var settingFields = settings.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
        var oldSettings = settingFields.Select(f => f.GetValue(settings)).ToArray();
        void SetSetting(string n, object v) => settings.GetType().GetField(n).SetValue(settings, v);
        object Invoke(string n) => trackerType.GetMethod(n).Invoke(tracker, null);
        long Number(string n) => Convert.ToInt64(trackerType.GetProperty(n).GetValue(tracker));
        var oldThings = map.listerThings.AllThings.ToArray(); var oldLetters = Find.LetterStack.LettersListForReading.ToArray(); var oldWindows = Find.WindowStack.Windows.ToArray();
        var oldTick = Find.TickManager.TicksGame; var oldResearch = Current.Game.researchManager;
        var pawns = new List<Pawn>();
        var blessingDef = DefDatabase<HediffDef>.GetNamed("KillingReward_BlessingRegen");
        var originalBlessings = ((List<Pawn>)Call(assembly, "BlessingReward", "Targets")).ToDictionary(p => p, p => (hediff: p.health.hediffSet.GetFirstHediffOfDef(blessingDef), duration: p.health.hediffSet.GetFirstHediffOfDef(blessingDef)?.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear));
        Require(!Find.Targeter.IsTargeting, "Fixture targeter must be idle");
        IntVec3 Empty() => map.AllCells.First(c => c.Standable(map) && !c.Fogged(map) && c.GetThingList(map).Count == 0);
        Pawn Spawn(PawnKindDef kind, Faction faction)
        {
            var p = PawnGenerator.GeneratePawn(kind, faction); pawns.Add(p); GenSpawn.Spawn(p, Empty(), map); return p;
        }
        try
        {
            foreach (var f in fields) f.SetValue(tracker, f.FieldType == typeof(long) ? (object)0L : 0);
            SetSetting("Enabled", true); SetSetting("InitialKills", 2); SetSetting("LinearIncrement", 0);
            SetSetting("Mode", Enum.ToObject(settings.GetType().GetField("Mode").FieldType, 1));
            var player = Spawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            var enemy = Spawn(PawnKindDefOf.Colonist, Faction.OfAncientsHostile);
            Require(enemy.Faction.HostileTo(Faction.OfPlayer), "Real hostile faction fixture missing");
            var damage = new DamageInfo(DamageDefOf.Cut, 100f, instigator: player);
            Require((bool)Call(assembly, "KillEligibilityAdapter", "ShouldCount", enemy, (DamageInfo?)damage), "Actual adapter must count hostile/player instigator");
            Require(!(bool)Call(assembly, "KillEligibilityAdapter", "ShouldCount", null, (DamageInfo?)damage) && !(bool)Call(assembly, "KillEligibilityAdapter", "ShouldCount", enemy, null), "Null adapter boundary changed");
            enemy.Kill(damage);
            Require(enemy.Dead && enemy.Corpse?.Map == map && Number("Progress") == 1 && Number("Level") == 0 && Number("PendingRewards") == 0, "Actual Pawn.Kill did not count exactly one offering below threshold");
            var second = Spawn(PawnKindDefOf.Colonist, Faction.OfAncientsHostile); second.Kill(damage);
            Require(Number("Progress") == 0 && Number("Level") == 1 && Number("PendingRewards") == 1, "Actual second hostile kill did not yield one boon at exact threshold");
            var letter = Find.LetterStack.LettersListForReading.Single(l => !oldLetters.Contains(l) && l.GetType().FullName == "KillingReward.ChoiceLetter_KillingReward");
            var neutral = Spawn(PawnKindDefOf.Colonist, Faction.OfAncients); Require(!neutral.Faction.HostileTo(Faction.OfPlayer), "Neutral faction fixture not neutral"); neutral.Kill(damage);
            var environmental = Spawn(PawnKindDefOf.Colonist, Faction.OfAncientsHostile); environmental.Kill(null);
            var hostileKiller = Spawn(PawnKindDefOf.Colonist, Faction.OfAncientsHostile); var hostileVictim = Spawn(PawnKindDefOf.Colonist, Faction.OfAncientsHostile); hostileVictim.Kill(new DamageInfo(DamageDefOf.Cut, 100f, instigator: hostileKiller));
            var factionless = Spawn(PawnKindDefOf.Colonist, Faction.OfAncients); factionless.SetFaction(null); factionless.Kill(damage);
            SetSetting("Enabled", false); var disabled = Spawn(PawnKindDefOf.Colonist, Faction.OfAncientsHostile); disabled.Kill(damage); SetSetting("Enabled", true);
            Require(Number("Progress") == 0 && Number("Level") == 1 && Number("PendingRewards") == 1, "Actual neutral/environment/enemy/no-faction/disabled kills must not count");
            var choices = ((ChoiceLetter)letter).Choices.ToArray(); Require(choices.Length == 2, "Actual boon letter must offer open and close"); choices[0].action();
            var dialog = Find.WindowStack.Windows.Single(w => !oldWindows.Contains(w) && w.GetType().FullName == "KillingReward.Dialog_KillingReward");
            Require(!Find.LetterStack.LettersListForReading.Contains(letter) && dialog.InitialSize == new Vector2(640f, 600f), "Actual letter action must open reward dialog and remove letter");
            var selected = dialog.GetType().GetField("selectedPawn", Private); var unavailable = dialog.GetType().GetMethod("SelectedPawnUnavailable", Private);
            selected.SetValue(dialog, player); Require(!(bool)unavailable.Invoke(dialog, null), "User's alive-pawn guard must retain eligible target");
            selected.SetValue(dialog, enemy); Require((bool)unavailable.Invoke(dialog, null) && selected.GetValue(dialog) == null, "User's dead-pawn guard must reset stale selection");
            Require((bool)unavailable.Invoke(dialog, null), "User's null selection guard must reject");
            Find.WindowStack.TryRemove(dialog, false);
            var button = DefDatabase<MainButtonDef>.GetNamed("KillingReward").Worker;
            SetSetting("Enabled", false); Require(!button.Visible, "Disabled setting must hide main button"); SetSetting("Enabled", true);
            button.Activate(); Require(Find.WindowStack.Windows.Any(w => !oldWindows.Contains(w) && w.GetType().FullName == "KillingReward.Dialog_KillingReward"), "Actual main button Activate did not open reward dialog");
            Invoke("DevLevelUp"); Require(Number("Level") == 2 && Number("Progress") == 0 && Number("PendingRewards") == 2, "User's actual DevLevelUp must reset progress and add boon");
            Require((bool)Invoke("TryConsumeReward") && Number("PendingRewards") == 1, "Real normal reward consumption failed");
            fields[2].SetValue(tracker, 0); Require(!(bool)Invoke("TryConsumeReward"), "Empty normal boon must not consume");
            SetSetting("TraitModInitialCost", 1); SetSetting("TraitModFactor", 2f); SetSetting("TraitModMode", Enum.ToObject(settings.GetType().GetField("TraitModMode").FieldType, 0));
            fields[2].SetValue(tracker, 5); Require(Number("TraitModCost") == 1 && (bool)Invoke("TryConsumeTraitMod") && Number("PendingRewards") == 4 && Number("TraitMods") == 1 && Number("TraitModCost") == 2, "Real trait cost/counter consumption failed");
            fields[2].SetValue(tracker, 1); Require(!(bool)Invoke("TryConsumeTraitMod") && Number("TraitMods") == 1, "Insufficient trait cost must not increment");
            SetSetting("GeneModInitialCost", 2); SetSetting("GeneModIncrement", 1); SetSetting("GeneModMode", Enum.ToObject(settings.GetType().GetField("GeneModMode").FieldType, 1));
            fields[2].SetValue(tracker, 5); Require(Number("GeneModCost") == 2 && (bool)Invoke("TryConsumeGeneMod") && Number("PendingRewards") == 3 && Number("GeneMods") == 1 && Number("GeneModCost") == 3, "Real gene cost/counter consumption failed");
            fields[2].SetValue(tracker, 2); Require(!(bool)Invoke("TryConsumeGeneMod") && Number("GeneMods") == 1, "Insufficient gene cost must not increment");
            VerifyRewards(assembly, tracker, player, map, Empty);
            var animal = Spawn(DefDatabase<PawnKindDef>.GetNamed("Muffalo"), Faction.OfPlayer);
            Pawn mech = ModsConfig.BiotechActive ? Spawn(DefDatabase<PawnKindDef>.GetNamed("Mech_Lifter"), Faction.OfPlayer) : null;
            var options = (System.Collections.IEnumerable)assembly.GetType("KillingReward.BlessingReward").GetProperty("Options").GetValue(null);
            var regen = options.Cast<object>().Single(o => (HediffDef)o.GetType().GetField("hediffDef").GetValue(o) == blessingDef);
            var targets = (List<Pawn>)Call(assembly, "BlessingReward", "Targets");
            Require(targets.Contains(player) && targets.Contains(animal) && (mech == null || targets.Contains(mech)), "Real colony targets must include human/animal/mech");
            Require((int)Call(assembly, "BlessingReward", "Apply", regen) == targets.Count, "Actual blessing application count wrong");
            var blessing = player.health.hediffSet.GetFirstHediffOfDef(blessingDef); blessing.TryGetComp<HediffComp_Disappears>().ticksToDisappear = 10;
            Call(assembly, "BlessingReward", "Apply", regen);
            Require(player.health.hediffSet.hediffs.Count(h => h.def == blessingDef) == 1 && blessing.TryGetComp<HediffComp_Disappears>().ticksToDisappear == 180000, "Repeat blessing must refresh real180000 duration without duplicate");
            VerifyRegeneration(player, blessingDef, true); VerifyRegeneration(animal, blessingDef, false); if (mech != null) VerifyRegeneration(mech, blessingDef, false);
            VerifyScribe(tracker, settings, fields, settingFields);
        }
        finally
        {
            Scribe.ForceStop(); Find.Targeter.StopTargeting();
            Current.Game.researchManager = oldResearch; Find.TickManager.DebugSetTicksGame(oldTick);
            foreach (var pair in originalBlessings)
            {
                var current = pair.Key.health.hediffSet.GetFirstHediffOfDef(blessingDef);
                if (pair.Value.hediff == null && current != null) pair.Key.health.RemoveHediff(current);
                else if (pair.Value.duration.HasValue) pair.Value.hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear = pair.Value.duration.Value;
            }
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(tracker, oldTracker[i]);
            for (int i = 0; i < settingFields.Length; i++) settingFields[i].SetValue(settings, oldSettings[i]);
            foreach (var pawn in pawns.Where(p => !p.Dead && !p.Destroyed)) pawn.Destroy(DestroyMode.Vanish);
            foreach (var item in map.listerThings.AllThings.Where(t => !oldThings.Contains(t)).ToArray()) if (!item.Destroyed) item.Destroy(DestroyMode.Vanish);
            foreach (var added in Find.LetterStack.LettersListForReading.Where(l => !oldLetters.Contains(l)).ToArray()) Find.LetterStack.RemoveLetter(added);
            foreach (var added in Find.WindowStack.Windows.Where(w => !oldWindows.Contains(w)).ToArray()) Find.WindowStack.TryRemove(added, false);
        }
        Log.Message("RW17 Mod77: actual hostile player kill/negative controls/tracker/letter/mainbutton/userguards/costs/research-skill-item-trait-gene rewards/blessing human-animal-mech regeneration/timer/Scribe passed; full UI/caravan/true save-reload pending");
    }

    private static void VerifyRewards(Assembly assembly, GameComponent tracker, Pawn pawn, Map map, Func<IntVec3> empty)
    {
        Require(((List<Pawn>)Call(assembly, "SkillReward", "Candidates")).Contains(pawn) && ((List<Pawn>)Call(assembly, "TraitModReward", "Candidates")).Contains(pawn), "Real generated player must be skill/trait candidate");
        var skills = (List<SkillRecord>)Call(assembly, "SkillReward", "AvailableSkills", pawn); Require(skills.Count > 0 && skills.All(s => !s.TotallyDisabled), "Real selectable skills invalid");
        var skill = skills[0]; skill.Level = 5; Call(assembly, "SkillReward", "Apply", skill); Require(skill.Level == 8, "Real skill reward must add3");
        skill.Level = 18; Call(assembly, "SkillReward", "Apply", skill); Require(skill.Level == 20, "Real skill reward must respect cap20");
        var traits = (List<Trait>)Call(assembly, "TraitModReward", "AddableTraits", pawn); Require(traits.Count > 0, "No legal trait fixture");
        var trait = traits.First(t => t.def.exclusionTags.NullOrEmpty()); Call(assembly, "TraitModReward", "Add", pawn, trait);
        Require(((List<Trait>)Call(assembly, "TraitModReward", "RemovableTraits", pawn)).Contains(trait) && !((List<Trait>)Call(assembly, "TraitModReward", "AddableTraits", pawn)).Any(t => t.def == trait.def), "Real trait gain/filter linkage failed");
        Call(assembly, "TraitModReward", "Remove", pawn, trait); Require(!pawn.story.traits.HasTrait(trait.def), "Real trait removal failed");
        var geneType = assembly.GetType("KillingReward.GeneModReward"); Require((bool)geneType.GetProperty("Available").GetValue(null) == ModsConfig.BiotechActive, "Actual optional gene gate wrong");
        if (ModsConfig.BiotechActive)
        {
            Require(((List<Pawn>)Call(assembly, "GeneModReward", "Candidates")).Contains(pawn), "Player gene candidate missing");
            var geneDef = ((List<GeneDef>)Call(assembly, "GeneModReward", "AddableGenes", pawn)).First(d => d.geneClass == typeof(Gene));
            foreach (bool xeno in new[] { false, true })
            {
                Call(assembly, "GeneModReward", "Add", pawn, geneDef, xeno);
                var genes = (List<Gene>)Call(assembly, "GeneModReward", "RemovableGenes", pawn, xeno); var gene = genes.Single(g => g.def == geneDef);
                Require((bool)Call(assembly, "GeneModReward", "HasGene", pawn, geneDef) && !((List<GeneDef>)Call(assembly, "GeneModReward", "AddableGenes", pawn)).Contains(geneDef), "Real gene add/filter failed");
                Call(assembly, "GeneModReward", "Remove", pawn, gene); Require(!(bool)Call(assembly, "GeneModReward", "HasGene", pawn, geneDef), "Real gene removal failed");
            }
        }
        Require(((List<ThingDef>)Call(assembly, "ItemReward", "ThingsIn", ThingCategoryDefOf.ResourcesRaw)).Contains(ThingDefOf.Steel), "Actual itemcategory parent membership must include Steel");
        var cell = empty(); Call(assembly, "ItemReward", "Deliver", ThingDefOf.Steel, cell, map);
        Require(cell.GetThingList(map).Single(t => t.def == ThingDefOf.Steel).stackCount == ThingDefOf.Steel.stackLimit, "Actual delivery must spawn a full stack");
        var sword = DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword"); var swordCell = empty(); Call(assembly, "ItemReward", "Deliver", sword, swordCell, map);
        Require(swordCell.GetThingList(map).Single(t => t.def == sword).Stuff != null, "Actual made-from-stuff delivery must choose compatible defaultstuff");
        var dialog = (Window)Activator.CreateInstance(assembly.GetType("KillingReward.Dialog_KillingReward")); Find.WindowStack.Add(dialog);
        tracker.GetType().GetField("pending", Private).SetValue(tracker, 1);
        dialog.GetType().GetMethod("BeginItemTargeting", Private).Invoke(dialog, new object[] { ThingDefOf.Silver, tracker });
        Require(Find.Targeter.IsTargeting, "Actual item UI callback must begin real targeting");
        var parameters = (TargetingParameters)typeof(Targeter).GetField("targetParams", Private).GetValue(Find.Targeter); var targetCell = empty();
        Require(parameters.canTargetLocations && !parameters.canTargetPawns && parameters.validator(new TargetInfo(targetCell, map)) && !parameters.validator(new TargetInfo(IntVec3.Invalid, map)), "Actual targeting validator/flags changed");
        var action = (Action<LocalTargetInfo>)typeof(Targeter).GetField("action", Private).GetValue(Find.Targeter); action(new LocalTargetInfo(targetCell)); Find.Targeter.StopTargeting();
        Require(targetCell.GetThingList(map).Any(t => t.def == ThingDefOf.Silver && t.stackCount == ThingDefOf.Silver.stackLimit) && (int)tracker.GetType().GetField("pending", Private).GetValue(tracker) == 0, "Actual target callback must consume once and deliver full stack");
        var benchDef = DefDatabase<ThingDef>.GetNamed("SimpleResearchBench");
        var center = map.AllCells.First(c => CellRect.CenteredOn(c, 2).Cells.All(q => q.InBounds(map) && q.Standable(map) && !q.Fogged(map) && q.GetThingList(map).Count == 0));
        var bench = ThingMaker.MakeThing(benchDef, ThingDefOf.WoodLog); bench.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(bench, center, map);
        Current.Game.researchManager = new ResearchManager();
        var projects = (List<ResearchProjectDef>)Call(assembly, "ResearchReward", "Available"); Require(projects.Count > 0 && projects.All(p => !p.IsFinished && !p.IsHidden && p.CanStartNow), "Actual research reward eligibility must return startable projects with realbench");
        var synthetic = new ResearchProjectDef { defName = "RW17Mod77Completion", label = "fixture completion", description = "fixture", baseCost = 100f };
        Call(assembly, "ResearchReward", "Complete", synthetic); Require(synthetic.IsFinished && Find.ResearchManager.GetProgress(synthetic) == 100f, "Actual reward must use native FinishProject progress completion");
    }

    private static void VerifyRegeneration(Pawn pawn, HediffDef blessingDef, bool missingPart)
    {
        var blessing = pawn.health.hediffSet.GetFirstHediffOfDef(blessingDef); Require(blessing != null, "Actual group blessing missing for pawn");
        var comp = ((HediffWithComps)blessing).comps.Single(c => c.GetType().FullName == "KillingReward.HediffComp_KRRegeneration");
        foreach (var injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToArray()) pawn.health.RemoveHediff(injury);
        var wound = pawn.health.AddHediff(HediffDefOf.Misc, pawn.RaceProps.body.corePart); wound.Severity = 1f;
        int tick = Find.TickManager.TicksGame;
        while (!pawn.IsHashIntervalTick(15, 1)) Find.TickManager.DebugSetTicksGame(++tick);
        Find.TickManager.DebugSetTicksGame(tick + 1); pawn.health.HealthTickInterval(1); Require(Math.Abs(wound.Severity - 1f) < 0.001f, "Non15-hash interval must not regenerate");
        Find.TickManager.DebugSetTicksGame(tick); pawn.health.HealthTickInterval(1);
        Require(Math.Abs(wound.Severity - (1f - 350f * 0.00025f)) < 0.003f, "Actual health interval/custom comp must heal350HP/day quantum for flesh and mech");
        float before = wound.Severity;
        for (int i = 1; i <= 30; i++) { Find.TickManager.DebugSetTicksGame(tick + i); pawn.DoTick(); }
        Require(wound.Severity < before, "Actual Pawn.DoTick must reach custom regeneration interval path");
        if (missingPart)
        {
            foreach (var injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToArray()) pawn.health.RemoveHediff(injury);
            var part = pawn.health.hediffSet.GetNotMissingParts().First(p => p.def.defName == "Hand" && p.parent != null && pawn.health.hediffSet.GetFirstHediffMatchingPart<Hediff_AddedPart>(p.parent) == null);
            pawn.health.AddHediff(HediffDefOf.MissingBodyPart, part);
            Find.TickManager.DebugSetTicksGame(tick + 45); pawn.health.HealthTickInterval(1);
            Require(pawn.health.hediffSet.GetFirstHediffMatchingPart<Hediff_MissingPart>(part) == null && pawn.health.hediffSet.GetFirstHediffMatchingPart<Hediff_Injury>(part)?.Severity > 0f, "Actual regeneration must restore legal missing part with fresh heavywound");
        }
        blessing.TryGetComp<HediffComp_Disappears>().ticksToDisappear = 1; pawn.DoTick();
        Require(!pawn.health.hediffSet.HasHediff(blessingDef), "Actual health tick must remove expired blessing");
    }

    private static void VerifyScribe(GameComponent tracker, ModSettings settings, FieldInfo[] fields, FieldInfo[] settingFields)
    {
        Require(Scribe.mode == LoadSaveMode.Inactive, "Scribe busy");
        object[] expected = { 7L, 1L, 5, 2L, 3L }; for (int i = 0; i < fields.Length; i++) fields[i].SetValue(tracker, expected[i]);
        var path = Path.Combine(GenFilePaths.SaveDataFolderPath, "Mod77TrackerFixture.xml"); Require(!File.Exists(path), "Tracker fixture exists");
        Scribe.saver.InitSaving(path, "Mod77TrackerFixture"); tracker.ExposeData(); Scribe.saver.FinalizeSaving();
        foreach (var field in fields) field.SetValue(tracker, field.FieldType == typeof(long) ? (object)0L : 0);
        Scribe.loader.InitLoading(path); tracker.ExposeData(); Scribe.loader.FinalizeLoading();
        Require(fields.Select(f => f.GetValue(tracker)).SequenceEqual(expected), "Actual five tracker Scribe fields/counters failed");
        var values = settingFields.Select(f => f.GetValue(settings)).ToArray(); var settingsPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "Mod77SettingsFixture.xml"); Require(!File.Exists(settingsPath), "Settings fixture exists");
        Scribe.saver.InitSaving(settingsPath, "Mod77SettingsFixture"); settings.ExposeData(); Scribe.saver.FinalizeSaving();
        foreach (var field in settingFields) field.SetValue(settings, Activator.CreateInstance(field.FieldType));
        Scribe.loader.InitLoading(settingsPath); settings.ExposeData(); Scribe.loader.FinalizeLoading();
        Require(settingFields.Select(f => f.GetValue(settings)).SequenceEqual(values), "Actual settings/cost curves Scribe roundtrip failed");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Mod77: " + message);
    }
}
