using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
namespace RW17.Validation;
public static class Mod15Checks
{
    public const string PackageId = "RunningBugs.MusicPatch";
    public static void Run(Map map)
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MusicalInstruments");
        var managerType = asm.GetType("MusicalInstruments.PerformanceManager", true);
        var musicType = asm.GetType("MusicalInstruments.Comp_PlayingMusic", true);
        var start = managerType.GetMethod("StartPlaying", new[] { typeof(Pawn), typeof(Thing), typeof(Thing), typeof(bool) });
        var stop = managerType.GetMethod("StopPlaying", new[] { typeof(Pawn), typeof(Thing) });
        var manager = map.components.Single(c => c.GetType() == managerType);
        if (Harmony.GetPatchInfo(start)?.Postfixes.Any(p => p.PatchMethod.DeclaringType?.FullName == "MusicPatch.MusicInstrumentsPatch") != true) throw new Exception("Mod15: specific upstream start patch absent");
        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        var def = DefDatabase<ThingDef>.GetNamed("FrameDrum");
        var instrument = (ThingWithComps)ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
        var comp = instrument.AllComps.Single(c => c.GetType() == musicType);
        var notebook = (IDictionary)musicType.GetField("Notebook").GetValue(null);
        var current = AccessTools.Field(musicType, "currentPlayer");
        Rand.PushState(1715);
        try
        {
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(map.Center, map, 15), map);
            GenSpawn.Spawn(instrument, CellFinder.RandomClosewalkCellNear(pawn.Position, map, 4), map);
            if (notebook.Contains(pawn) || (Pawn)current.GetValue(comp) != null) throw new Exception("Mod15: fixture not initially idle");
            start.Invoke(manager, new object[] { pawn, instrument, instrument, false });
            if (!notebook.Contains(pawn) || notebook[pawn] != comp || (Pawn)current.GetValue(comp) != pawn || !(bool)managerType.GetMethod("HasPerformer").Invoke(manager,new object[] { pawn })) throw new Exception("Mod15: actual upstream start did not register native playback/performance");
            if (instrument.AllComps.Count(c => c.GetType().Name == "Comp_PlayingMusic") != 1) throw new Exception("Mod15: duplicate playback component");
            stop.Invoke(manager, new object[] { pawn, instrument });
            if (notebook.Contains(pawn) || current.GetValue(comp) != null || (bool)managerType.GetMethod("HasPerformer").Invoke(manager,new object[] { pawn })) throw new Exception("Mod15: real upstream stop left playback/performance");
        }
        finally
        {
            if (notebook.Contains(pawn)) stop.Invoke(manager, new object[] { pawn, instrument });
            instrument.Destroy(); pawn.Destroy(); Rand.PopState();
        }
        Console.WriteLine("RW17_MOD15_REAL_PERFORMANCE_PLAYBACK_STATE_OK; audible sound/rendering/reload remains pending");
    }
}
