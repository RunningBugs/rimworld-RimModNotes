using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RW17.Validation;

public static class Mod45Checks
{
    public const string PackageId = "RunningBugs.modname";
    private static readonly List<(Vector3 a, Vector3 b, SimpleColor color)> lines = new();

    public static void Run(Map map)
    {
        var draw = typeof(Pawn_StanceTracker).GetMethod("StanceTrackerDraw");
        var registered = Harmony.GetPatchInfo(draw)?.Postfixes.SingleOrDefault(p => p.PatchMethod.DeclaringType?.FullName == "TargetLine.Pawn_StanceTracker_Patch");
        Require(registered != null, "Actual stance draw postfix missing");
        var assembly = registered.PatchMethod.DeclaringType.Assembly;
        var mod = LoadedModManager.GetMod(assembly.GetType("TargetLine.ModSettingsUI", true));
        var type = assembly.GetType("TargetLine.ModSettingsData", true);
        var settings = (ModSettings)typeof(Mod).GetMethod("GetSettings").MakeGenericMethod(type).Invoke(mod, null);
        var enabled = type.GetField("showTargetLine");
        var friendlyFlag = type.GetField("showTargetLineForNonHostile");
        var hostileFlag = type.GetField("showTargetLineForHostile");
        var originalValues = new[] { enabled.GetValue(settings), friendlyFlag.GetValue(settings), hostileFlag.GetValue(settings) };
        var target = typeof(GenDraw).GetMethod("DrawLineBetween", new[] { typeof(Vector3), typeof(Vector3), typeof(SimpleColor), typeof(float) });
        var capture = typeof(Mod45Checks).GetMethod(nameof(CaptureLine), BindingFlags.Static | BindingFlags.NonPublic);
        var spy = new Harmony("rw17.validation.mod45.lines");
        Pawn friendly = null;
        Pawn hostile = null;
        try
        {
            enabled.SetValue(settings, true);
            friendlyFlag.SetValue(settings, true);
            hostileFlag.SetValue(settings, true);
            friendly = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            hostile = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfAncientsHostile);
            var cells = map.AllCells.Where(c => c.Standable(map) && !c.Fogged(map) && c.GetThingList(map).Count == 0).Take(5).ToArray();
            Require(cells.Length == 5, "Cannot create free drawing fixture cells");
            GenSpawn.Spawn(friendly, cells[0], map);
            GenSpawn.Spawn(hostile, cells[1], map);
            Require(!friendly.HostileTo(Faction.OfPlayer) && hostile.HostileTo(Faction.OfPlayer), "Actual hostility fixtures are wrong");
            spy.Patch(target, prefix: new HarmonyMethod(capture));
            friendly.jobs.curJob = JobMaker.MakeJob(JobDefOf.Goto, cells[2]);
            friendly.jobs.curJob.targetB = cells[3];
            friendly.jobs.curJob.targetC = hostile;
            Capture(friendly);
            Require(lines.Count == 3 && lines.Select(l => l.color).SequenceEqual(new[] { SimpleColor.Green, SimpleColor.Blue, SimpleColor.Cyan }), "Actual friendly target chain has wrong count/colors");
            Require(Near(lines[0].a, friendly.DrawPos) && Near(lines[0].b, cells[2].ToVector3Shifted()) && Near(lines[1].b, cells[3].ToVector3Shifted()) && Near(lines[2].b, hostile.DrawPos), "Actual Thing/cell endpoints differ");
            enabled.SetValue(settings, false);
            Capture(friendly);
            Require(lines.Count == 0, "Global disabled flag still emitted a line");
            enabled.SetValue(settings, true);
            friendlyFlag.SetValue(settings, false);
            Capture(friendly);
            Require(lines.Count == 0, "Friendly disabled flag still emitted a line");
            friendlyFlag.SetValue(settings, true);
            hostile.jobs.curJob = JobMaker.MakeJob(JobDefOf.Goto, cells[2]);
            hostile.jobs.curJob.targetB = cells[3];
            hostile.jobs.curJob.targetC = friendly;
            Capture(hostile);
            Require(lines.Count == 3 && lines.Select(l => l.color).SequenceEqual(new[] { SimpleColor.Red, SimpleColor.Orange, SimpleColor.Magenta }), "Actual hostile chain colors/count differ");
            hostileFlag.SetValue(settings, false);
            Capture(hostile);
            Require(lines.Count == 0, "Hostile disabled flag still emitted a line");
            hostileFlag.SetValue(settings, true);
            friendly.stances.SetStance(new Stance_Warmup(60, new LocalTargetInfo(hostile), null));
            Capture(friendly);
            Require(lines.Count == 1 && lines[0].color == SimpleColor.Green && Near(lines[0].b, hostile.DrawPos), "Actual warmup must emit only its focus line");
            friendly.stances.SetStance(new Stance_Mobile());
            friendly.jobs.curJob.targetB = LocalTargetInfo.Invalid;
            Capture(friendly);
            Require(lines.Count == 1, "Invalid middle target must skip both subsequent links");
            friendly.jobs.curJob = null;
            Capture(friendly);
            Require(lines.Count == 0, "No job/no warmup must not draw");
            var path = Path.Combine(GenFilePaths.SaveDataFolderPath, "Mod45SettingsFixture.xml");
            Require(!File.Exists(path) && Scribe.mode == LoadSaveMode.Inactive, "Settings fixture exists or Scribe busy");
            foreach (var field in new[] { enabled, friendlyFlag, hostileFlag })
                field.SetValue(settings, false);
            Scribe.saver.InitSaving(path, "Mod45SettingsFixture");
            settings.ExposeData();
            Scribe.saver.FinalizeSaving();
            Require(File.ReadAllText(path).Contains("showTargetLineForEnemy"), "Original hostile save key changed");
            foreach (var field in new[] { enabled, friendlyFlag, hostileFlag })
                field.SetValue(settings, true);
            Scribe.loader.InitLoading(path);
            settings.ExposeData();
            Scribe.loader.FinalizeLoading();
            Require(!(bool)enabled.GetValue(settings) && !(bool)friendlyFlag.GetValue(settings) && !(bool)hostileFlag.GetValue(settings), "Actual Scribe flags roundtrip failed");
        }
        finally
        {
            spy.Unpatch(target, capture);
            Scribe.ForceStop();
            enabled.SetValue(settings, originalValues[0]);
            friendlyFlag.SetValue(settings, originalValues[1]);
            hostileFlag.SetValue(settings, originalValues[2]);
            if (friendly != null && !friendly.Destroyed) { friendly.jobs.curJob = null; friendly.Destroy(DestroyMode.Vanish); }
            if (hostile != null && !hostile.Destroyed) { hostile.jobs.curJob = null; hostile.Destroy(DestroyMode.Vanish); }
        }
        Log.Message("RW17 Mod45: actual stance draw/job/warmup/hostility/enable/invalid endpoints and settings roundtrip passed; GenDraw arguments observed, GPU pixels pending");
    }

    private static void Capture(Pawn pawn) { lines.Clear(); pawn.stances.StanceTrackerDraw(); }
    private static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 0.001f;
    private static bool CaptureLine(Vector3 __0, Vector3 __1, SimpleColor __2) { lines.Add((__0, __1, __2)); return false; }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Mod45: " + message);
    }
}
