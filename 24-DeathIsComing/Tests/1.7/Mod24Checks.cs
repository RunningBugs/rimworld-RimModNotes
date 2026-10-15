using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod24Checks
{
    public const string PackageId = "RunningBugs.DeathIsComing";

    public static void Run(Map map)
    {
        var def = DefDatabase<IncidentDef>.GetNamed("DIC_DeathComing");
        Require(def.workerClass.FullName == "DIC.DIC_IncidentWorker_DeathComing" && def.baseChance == 0f, "Original worker/manual incident probability changed");
        Require(Harmony.GetPatchInfo(typeof(IncidentWorker).GetMethod("CanFireNow"))?.Prefixes
            .Any(p => p.PatchMethod.DeclaringType?.FullName == "DIC.ForceFirePatch") != true, "Unused inherited ForceFirePatch was unexpectedly activated globally");
        var componentType = def.workerClass.Assembly.GetType("DIC.DIC_GameComponent", true);
        var component = Current.Game.GetComponent(componentType);
        var mercy = componentType.GetField("DeathMercy");
        Require(component != null && (bool)mercy.GetValue(component), "GameComponent missing or initial serialized flag default changed");
        var fixture = Path.Combine(GenFilePaths.SaveDataFolderPath, "Mod24Fixture.xml");
        Require(!File.Exists(fixture) && Scribe.mode == LoadSaveMode.Inactive, "Serialization fixture already exists or Scribe is busy");
        try
        {
            mercy.SetValue(component, false);
            Scribe.saver.InitSaving(fixture, "Mod24Fixture");
            component.ExposeData();
            Scribe.saver.FinalizeSaving();
            Require(File.ReadAllText(fixture).Contains("DIC_GameComponent.DeathMercy"), "Original save key was not written");
            mercy.SetValue(component, true);
            Scribe.loader.InitLoading(fixture);
            component.ExposeData();
            Scribe.loader.FinalizeLoading();
            Require(!(bool)mercy.GetValue(component), "Actual Scribe roundtrip did not restore false flag");
        }
        finally
        {
            Scribe.ForceStop();
            mercy.SetValue(component, true);
        }
        var parents = Find.Maps.Where(m => m.IsPlayerHome).Select(m => m.Parent).Distinct()
            .Select(p => (parent: p, faction: p.Faction)).ToArray();
        try
        {
            foreach (var saved in parents)
                saved.parent.SetFaction(Faction.OfAncients);
            Require(!Find.Maps.Any(m => m.IsPlayerHome), "Cannot establish no-home-map branch in this disposable fixture");
            Require(def.Worker.TryExecute(new IncidentParms { target = map, forced = true }), "Actual no-candidate worker branch failed");
        }
        finally
        {
            foreach (var saved in parents)
                saved.parent.SetFaction(saved.faction);
        }
        Pawn pregnant = null;
        try
        {
            pregnant = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, fixedGender: Gender.Female));
            pregnant.health.AddHediff(HediffDefOf.PregnantHuman);
            var cell = map.AllCells.First(c => c.Standable(map) && !c.Fogged(map) && c.GetThingList(map).Count == 0);
            GenSpawn.Spawn(pregnant, cell, map);
            Require(pregnant.health.hediffSet.HasPregnancyHediff(), "Pregnancy exclusion fixture did not initialize");
            Require("{0}".Formatted(pregnant.Name.Named("PAWN")).ToString() == string.Format("{0}", pregnant.Name), "NamedArgument changed the original positional Name rendering");
            var before = Find.Maps.Where(m => m.IsPlayerHome).SelectMany(m => m.mapPawns.AllPawns)
                .Where(p => p.Faction == Faction.OfPlayer && !p.health.hediffSet.HasPregnancyHediff() && (p.IsColonist || p.IsSlaveOfColony) && p.RaceProps.Humanlike && !p.Dead).ToArray();
            Require(before.Length > 0, "Disposable map has no eligible test colonist");
            Require(def.Worker.TryExecute(new IncidentParms { target = map, forced = true }), "Actual eligible worker branch failed");
            Require(before.Count(p => p.Dead) == 1, "Actual worker must kill exactly one eligible pawn");
            Require(!pregnant.Dead, "Actual worker killed excluded pregnant pawn");
        }
        finally
        {
            if (pregnant != null && !pregnant.Destroyed)
                pregnant.Destroy(DestroyMode.Vanish);
        }
        Log.Message("RW17 Mod24: manual incident, inactive inherited prefix, actual GameComponent Scribe roundtrip, no-candidate and one-death/pregnancy branches passed in disposable map");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Mod24: " + message);
    }
}
