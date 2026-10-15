using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
namespace RW17.Validation;
public static class Mod33Checks
{
    public const string PackageId = "RunningBugs.PolarisblocSecurityForceDistressCallPatch";
    private static Pawn subject;
    private static int successfulResurrections, completedKills;
    private static bool simulatedDeadWorldPawnWithoutCorpse;
    public static void ObserveResurrection(Pawn pawn, bool __result)
    {
        if (pawn == subject && __result) { successfulResurrections++; Console.WriteLine("RW17_MOD33_RESURRECT world=" + pawn.IsWorldPawn() + ";dead=" + pawn.Dead + ";corpse=" + (pawn.Corpse != null)); }
    }
    public static void ObserveKill(Pawn __instance)
    {
        if (__instance == subject) { completedKills++; Console.WriteLine("RW17_MOD33_KILL_END world=" + __instance.IsWorldPawn() + ";dead=" + __instance.Dead + ";corpse=" + (__instance.Corpse != null)); }
    }
    public static void ObserveSimulation(Pawn p)
    {
        if (p == subject) simulatedDeadWorldPawnWithoutCorpse = p.Dead && p.IsWorldPawn() && p.Corpse == null;
    }
    public static void Run(Map map)
    {
        foreach (var id in new[] { "vanya.polarisbloc.corelab.tmp", "vanya.polarisbloc.securityforce.tmp" })
            if (LoadedModManager.RunningMods.All(mod => !mod.PackageId.Equals(id, StringComparison.OrdinalIgnoreCase))) throw new Exception("Mod33: actual required installed mod is not loaded: " + id);
        var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Polarisbloc_SFSoldier");
        if (kind == null) throw new Exception("Mod33: loaded SecurityForce did not supply actual SFSoldier def");
        var method = AccessTools.Method(typeof(DistressCallUtility), nameof(DistressCallUtility.SpawnCorpses));
        if (Harmony.GetPatchInfo(method)?.Prefixes.Any(p => p.owner == "com.RunningBugs.SFDistressCallFix") == true) throw new Exception("Mod33: stale full replacement still installed");
        Rand.PushState(1733);
        Pawn ordinary = null, security = null, killer = null;
        var observer = new Harmony("RW17.Validation.Mod33.Observe");
        var resurrect = AccessTools.Method(typeof(ResurrectionUtility), nameof(ResurrectionUtility.TryResurrect), new[] { typeof(Pawn), typeof(ResurrectionParams) });
        var kill = AccessTools.Method(typeof(Pawn), nameof(Pawn.Kill), new[] { typeof(DamageInfo?), typeof(Hediff) });
        var simulate = AccessTools.Method(typeof(HealthUtility), nameof(HealthUtility.SimulateKilledByPawn));
        var resurrectionObserver = AccessTools.Method(typeof(Mod33Checks), nameof(ObserveResurrection));
        var killObserver = AccessTools.Method(typeof(Mod33Checks), nameof(ObserveKill));
        var simulationObserver = AccessTools.Method(typeof(Mod33Checks), nameof(ObserveSimulation));
        try
        {
            killer = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            ordinary = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            security = PawnGenerator.GeneratePawn(kind, Faction.OfPlayer);
            var cartridge = security.apparel.WornApparel.SingleOrDefault(a => a.def.defName == "PolarisCartridge");
            if (cartridge == null) throw new Exception("Mod33: actual SF pawn did not receive upstream resurrection Cartridge");
            // Retain the real resurrection apparel; remove armor absorption so the kill simulation can exercise death.
            foreach (var item in security.apparel.WornApparel.ToArray()) if (item != cartridge) { security.apparel.Remove(item); item.Destroy(); }
            subject = security; successfulResurrections = completedKills = 0; simulatedDeadWorldPawnWithoutCorpse = false;
            observer.Patch(resurrect, postfix: new HarmonyMethod(resurrectionObserver));
            observer.Patch(kill, postfix: new HarmonyMethod(killObserver));
            observer.Patch(simulate, postfix: new HarmonyMethod(simulationObserver));
            DistressCallUtility.SpawnCorpses(map, new[] { security, ordinary }, new[] { killer }, map.Center, 15);
            if (!cartridge.Destroyed) throw new Exception("Mod33: actual upstream Cartridge death/resurrection path was not exercised");
            if (!ordinary.Dead || ordinary.Corpse == null || !ordinary.Corpse.Spawned || ordinary.Corpse.Map != map || ordinary.Corpse.timeOfDeath != map.Parent.creationGameTicks || !ordinary.Corpse.IsForbidden(Faction.OfPlayer)) throw new Exception("Mod33: ordinary corpse pipeline/forbidden/death time regressed");
            if (successfulResurrections != 1 || completedKills < 2 || !simulatedDeadWorldPawnWithoutCorpse || !security.Dead || !security.IsWorldPawn() || security.Corpse != null) throw new Exception("Mod33: real Cartridge resurrection/re-kill/world-pawn null-corpse guard input was not demonstrated");
            Console.WriteLine("RW17_MOD33_ACTUAL_SF_CARTRIDGE_AND_VANILLA_CORPSE_PIPELINE_OK; sfDead=" + security.Dead);
        }
        finally
        {
            observer.Unpatch(resurrect, resurrectionObserver); observer.Unpatch(kill, killObserver); observer.Unpatch(simulate, simulationObserver); subject = null;
            foreach (var pawn in new[] { security, ordinary, killer }) if (pawn != null) { var corpse = pawn.Corpse; if (corpse != null && !corpse.Destroyed) corpse.Destroy(); if (!pawn.Destroyed) pawn.Destroy(); }
            Rand.PopState();
        }
    }
}
