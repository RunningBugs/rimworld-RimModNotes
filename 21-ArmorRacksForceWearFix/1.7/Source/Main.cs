using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ArmorRacks;
using HarmonyLib;
using RimWorld;
using Verse;
namespace Template;
[StaticConstructorOnStartup]
public static class Start
{
    static Start() { new Harmony("com.runningbugs.ArmorRacksPatches").PatchAll(typeof(Start).Assembly); Logger.Log.Message("loaded!"); }
}
[HarmonyPatch]
public static class ArmorRacksPatches
{
    private static readonly MethodInfo OriginalWear = AccessTools.Method(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear), new[] { typeof(Apparel), typeof(bool), typeof(bool) });
    public static MethodBase TargetMethod() => typeof(ArmorRacks.Jobs.JobDriverTransferToRack)
        .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
        .Single(m => m.Name.StartsWith("<MakeNewToils>b__", StringComparison.Ordinal)
            && PatchProcessor.GetOriginalInstructions(m).Any(i => i.Calls(OriginalWear)));
    public static void WearAndForce(Pawn_ApparelTracker tracker, Apparel apparel, bool dropReplaced, bool locked)
    {
        tracker.Wear(apparel, dropReplaced, locked);
        if (apparel.Wearer != null && LoadedModManager.GetMod<ArmorRacksMod>().GetSettings<ArmorRacksModSettings>().EquipSetForced)
            apparel.Wearer.outfits.forcedHandler.SetForced(apparel, true);
    }
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int matches = 0;
        var replacement = AccessTools.Method(typeof(ArmorRacksPatches), nameof(WearAndForce));
        var rewritten = instructions.Select(original => {
            var copy = new CodeInstruction(original);
            if (copy.Calls(OriginalWear)) { copy.opcode = OpCodes.Call; copy.operand = replacement; matches++; }
            return copy;
        }).ToList();
        if (matches != 1) throw new InvalidOperationException("Expected one ArmorRacks transfer Wear call, found " + matches);
        return rewritten;
    }
}
