using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace CultistKidnapping;

[HarmonyPatch(typeof(PsychicRitualToil_SkipAbduction), nameof(PsychicRitualToil_SkipAbduction.ApplyOutcome))]
public static class AbductionOutcomePatch
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var skip = AccessTools.Method(typeof(SkipUtility), nameof(SkipUtility.SkipTo));
        var capture = AccessTools.Method(typeof(AbductionOutcomePatch), nameof(SkipAndRemember));
        if (codes.Count(code => code.Calls(skip)) != 1)
            throw new InvalidOperationException("CultistKidnapping: expected one SkipTo call in the AI abduction outcome.");

        foreach (var code in codes)
        {
            if (!code.Calls(skip)) { yield return code; continue; }
            // Preserve the original stack/return value and pass the exact ritual and invoker.
            yield return new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(code).MoveBlocksFrom(code);
            yield return new CodeInstruction(OpCodes.Ldarg_2);
            yield return new CodeInstruction(OpCodes.Call, capture);
        }
    }

    public static Thing SkipAndRemember(Thing thing, IntVec3 cell, Map map, PsychicRitual ritual, Pawn invoker)
    {
        var result = SkipUtility.SkipTo(thing, cell, map);
        if (result is Pawn victim && ritual.def?.defName == "SkipAbduction" &&
            ritual.lord?.faction == Faction.OfHoraxCult && invoker.HostileTo(victim))
            map.GetComponent<KidnappingMapComponent>().Remember(ritual.lord, invoker, victim);
        return result;
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
public static class KidnapExitPatch
{
    public static void Prefix(Pawn __instance, out LordJob_CultistKidnap __state)
    {
        __state = __instance.GetLord()?.LordJob as LordJob_CultistKidnap;
        if (__state == null || __instance.carryTracker?.CarriedThing != __state.Victim)
            __state = null;
    }

    public static void Postfix(Pawn __instance, LordJob_CultistKidnap __state)
    {
        // ExitMap removes the carrier from its lord before calling the vanilla Kidnap tracker.
        if (__state != null && !__instance.Spawned &&
            __instance.Faction.kidnapped.KidnappedPawnsListForReading.Contains(__state.Victim))
            __state.Notify_Kidnapped();
    }
}
