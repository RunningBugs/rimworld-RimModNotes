using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod50Checks
{
    public const string PackageId = "RunningBugs.RACurseSA";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run(Map map)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "RACurseSA");
        var type = assembly.GetType("RACurseSA.RACurseSAComponent", true);
        var component = Find.World.GetComponent(type);
        Require(component != null && ModsConfig.AnomalyActive, "Actual world component/Anomaly missing");
        var settings = assembly.GetType("RACurseSA.RACurseSAMod", true).GetField("settings").GetValue(null);
        var permanent = settings.GetType().GetField("permanentCurse");
        var onlyColonists = settings.GetType().GetField("onlyApplyToColonists");
        var oldPermanent = permanent.GetValue(settings);
        var oldOnly = onlyColonists.GetValue(settings);
        var oldLetters = Find.LetterStack.LettersListForReading.ToArray();
        var oldConditions = map.gameConditionManager.ActiveConditions.ToArray();
        var oldResearch = Find.ResearchManager.GetProject();
        var oldBattle = Current.Game.battleLog;
        var spawned = new List<Pawn>();
        var priorPawns = map.mapPawns.AllPawnsSpawned.ToArray();
        var names = new[] { "EatingWithoutTable", "Research", "KillAnimals", "Wounded", "CutTree" };
        object Call(string method, params object[] args) => type.GetMethod(method).Invoke(component, args);
        bool Active(string curse) => (bool)Call("IsActive", curse);
        void Clear() => Call("Clear");
        Pawn Human()
        {
            var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer));
            GenSpawn.Spawn(pawn, CellFinder.StandableCellNear(map.Center, map, 12), map);
            spawned.Add(pawn);
            return pawn;
        }
        try
        {
            Require(names.All(n => !Active(n)), "Fixture has existing curses");
            foreach (var target in new MethodBase[] {
                AccessTools.Method(typeof(MemoryThoughtHandler), "TryGainMemory", new[] { typeof(Thought_Memory), typeof(Pawn) }),
                AccessTools.Method(typeof(ResearchManager), "ResearchPerformed"),
                AccessTools.Method(typeof(Pawn), "Kill"), AccessTools.Method(typeof(Pawn), "PostApplyDamage"),
                AccessTools.Method(typeof(HistoryEventsManager), "RecordEvent") })
                Require(Harmony.GetPatchInfo(target)?.Prefixes.Concat(Harmony.GetPatchInfo(target).Postfixes)
                    .Any(p => p.PatchMethod.DeclaringType.Assembly == assembly) == true, "Actual patch missing: " + target);

            Call("Activate", "Research", 900001); Call("Activate", "Research", 900002);
            Require(Active("Research") && !(bool)Call("Deactivate", "Research", 900001) && Active("Research"), "Overlap removed live activation");
            Require((bool)Call("Deactivate", "Research", 900002) && !Active("Research"), "Final activation not removed");
            Call("Activate", "Research", -1); Call("Activate", "", 900003);
            Require(!Active("Research"), "Invalid activation accepted");
            for (int cycle = 0; cycle < 3; cycle++)
            {
                var picks = Enumerable.Range(0, 5).Select(_ => (string)Call("TakeNextRandomCurse")).ToArray();
                Require(picks.Distinct().Count() == 5 && picks.All(names.Contains), "Random cycle lost a curse or repeated early");
            }

            var displayDef = DefDatabase<GameConditionDef>.GetNamed("RACurseSA_CurseDisplay_Research");
            var first = GameConditionMaker.MakeConditionPermanent(displayDef);
            var second = GameConditionMaker.MakeConditionPermanent(displayDef);
            map.gameConditionManager.RegisterCondition(first); map.gameConditionManager.RegisterCondition(second);
            Require(Active("Research") && !first.Expired && !second.Expired, "Native condition Init did not activate");
            int letters = Find.LetterStack.LettersListForReading.Count;
            first.End();
            Require(Active("Research") && Find.LetterStack.LettersListForReading.Count == letters, "Overlapping End sent final letter");
            second.End();
            Require(!Active("Research") && Find.LetterStack.LettersListForReading.Count == letters + 1, "Final native End did not lift curse/send letter");

            permanent.SetValue(settings, false);
            var incident = DefDatabase<IncidentDef>.GetNamed("RA_Curse_SA_Research");
            var parms = new IncidentParms { target = map, forced = true };
            Require(incident.Worker.TryExecute(parms) && Active("Research"), "Timed native incident failed");
            var timed = map.gameConditionManager.ActiveConditions.Single(c => c.GetType().FullName == "RACurseSA.GameCondition_Curse");
            Require(!timed.Permanent && timed.Duration >= 60000 && timed.Duration <= 180000, "Timed duration contract changed");
            Require(!incident.Worker.TryExecute(parms), "Explicit duplicate curse incident accepted");
            Clear(); Require(timed.Expired, "Clear did not expire timed condition"); timed.End();
            permanent.SetValue(settings, true);
            Require(incident.Worker.TryExecute(parms) && Active("Research"), "Permanent native incident failed");
            Clear();
            foreach (var condition in map.gameConditionManager.ActiveConditions.Where(c => c.def.defName.StartsWith("RACurseSA_")).ToArray()) condition.End();

            var pawn = Human();
            var memory = pawn.needs.mood.thoughts.memories;
            memory.TryGainMemory((Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.AteWithoutTable));
            Require(!pawn.health.hediffSet.HasHediff(HediffDefOf.FoodPoisoning), "Inactive dining curse affected pawn");
            Call("Activate", "EatingWithoutTable", 900010);
            memory.TryGainMemory((Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.AteWithoutTable));
            Require(pawn.health.hediffSet.HasHediff(HediffDefOf.FoodPoisoning), "Actual memory entry did not poison pawn");
            Clear();

            var researcher = Human();
            researcher.jobs.curJob = JobMaker.MakeJob(JobDefOf.Research);
            var project = DefDatabase<ResearchProjectDef>.AllDefs.First(d => d.baseCost > 0 && !d.IsFinished && d.knowledgeCategory == null);
            Find.ResearchManager.SetCurrentProject(project);
            Require(Find.ResearchManager.GetProject() == project, "Fixture research project did not become active");
            var skill = researcher.skills.GetSkill(SkillDefOf.Intellectual);
            skill.Level = 8; skill.xpSinceLastLevel = 1000;
            float TotalXp() => skill.XpTotalEarned + skill.xpSinceLastLevel;
            float before = TotalXp(); Find.ResearchManager.ResearchPerformed(0, researcher);
            Require(TotalXp() == before, "Inactive research curse changed XP");
            Call("Activate", "Research", 900011); Find.ResearchManager.ResearchPerformed(0, researcher);
            Require(TotalXp() < before, "Actual ResearchPerformed did not remove Intellectual XP"); Clear();

            var wounded = Human();
            var leg = wounded.RaceProps.body.AllParts.First(p => p.def == BodyPartDefOf.Leg);
            wounded.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 1, 999, hitPart: leg));
            Require(!wounded.Downed, "Inactive wound control downed pawn");
            Call("Activate", "Wounded", 900012);
            wounded.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 1, 999, hitPart: leg));
            Require(wounded.Downed && !wounded.Dead, "Actual wound damage did not down living pawn"); Clear();

            Call("Activate", "KillAnimals", 900013);
            var animal = PawnGenerator.GeneratePawn(PawnKindDefOf.Muffalo);
            GenSpawn.Spawn(animal, CellFinder.StandableCellNear(map.Center, map, 12), map); spawned.Add(animal);
            int chimeras = map.mapPawns.AllPawnsSpawned.Count(p => p.kindDef == PawnKindDefOf.Chimera);
            animal.Kill(new DamageInfo(DamageDefOf.Blunt, 10, instigator: pawn));
            Require(map.mapPawns.AllPawnsSpawned.Count(p => p.kindDef == PawnKindDefOf.Chimera) == chimeras + 1,
                "Actual animal Kill did not spawn one Chimera"); Clear();

            Call("Activate", "CutTree", 900014);
            Require(!map.gameConditionManager.ConditionIsActive(GameConditionDefOf.ColdSnap), "Fixture already has cold snap");
            Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.CutTree, pawn.Named(HistoryEventArgsNames.Doer)), false);
            Require(map.gameConditionManager.ConditionIsActive(GameConditionDefOf.ColdSnap), "Actual CutTree history event did not trigger cold snap"); Clear();

            Call("Activate", "Research", 900015);
            var path = Path.Combine(GenFilePaths.SaveDataFolderPath, "Mod50WorldFixture.xml");
            Scribe.saver.InitSaving(path, "Mod50Fixture"); component.ExposeData(); Scribe.saver.FinalizeSaving();
            Clear();
            Scribe.loader.InitLoading(path); component.ExposeData(); Scribe.loader.FinalizeLoading();
            Require(Active("Research") && (bool)Call("IsActivationActive", "Research", 900015), "Native component Scribe lost activation IDs");
            Require(Enumerable.Range(0, 5).Select(_ => (string)Call("TakeNextRandomCurse")).Distinct().Count() == 5, "Scribe lost exhausted cycle state");
            Clear();
        }
        finally
        {
            Scribe.ForceStop(); Clear();
            permanent.SetValue(settings, oldPermanent); onlyColonists.SetValue(settings, oldOnly);
            foreach (var c in map.gameConditionManager.ActiveConditions.Where(c => !oldConditions.Contains(c)).ToArray()) c.End();
            foreach (var p in map.mapPawns.AllPawnsSpawned.Where(p => !priorPawns.Contains(p)).ToArray()) p.Destroy(DestroyMode.Vanish);
            foreach (var p in spawned)
            {
                if (p.Corpse != null && !p.Corpse.Destroyed) p.Corpse.Destroy(DestroyMode.Vanish);
                if (!p.Destroyed) p.Destroy(DestroyMode.Vanish);
            }
            typeof(ResearchManager).GetField("currentProj", Fields).SetValue(Find.ResearchManager, oldResearch);
            Current.Game.battleLog = oldBattle;
            foreach (var l in Find.LetterStack.LettersListForReading.Where(l => !oldLetters.Contains(l)).ToArray()) Find.LetterStack.RemoveLetter(l);
        }
        Log.Message("RW17 Mod50: actual activations/random cycles/conditions/incidents/all five curse native entries/Scribe passed; UI/audio/migration/full reload pending");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
