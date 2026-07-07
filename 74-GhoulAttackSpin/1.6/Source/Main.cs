using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace GhoulAttackSpin;

public sealed class GhoulAttackSpinMod : Mod
{
    public GhoulAttackSpinMod(ModContentPack content) : base(content)
    {
        new Harmony(content.PackageId).PatchAll();
    }
}

internal static class GhoulAttackSpinState
{
    // Vanilla melee jitter starts at 0.5 cells and decays by 0.018 per tick,
    // so it visually lasts about 28 ticks. Match that window and rotate once.
    internal const int SpinDurationTicks = 28;

    private struct SpinWindow
    {
        public int startTick;
        public int endTick;
    }

    private static readonly Dictionary<int, SpinWindow> ActiveSpins = new Dictionary<int, SpinWindow>();

    internal static void StartSpin(Pawn pawn)
    {
        if (pawn == null || pawn.Destroyed || !pawn.Spawned || !pawn.IsGhoul)
        {
            return;
        }

        int now = Find.TickManager.TicksGame;
        ActiveSpins[pawn.thingIDNumber] = new SpinWindow
        {
            startTick = now,
            endTick = now + SpinDurationTicks
        };
    }

    internal static bool TryGetAngle(Pawn pawn, out float angle)
    {
        angle = 0f;
        if (pawn == null || !ActiveSpins.TryGetValue(pawn.thingIDNumber, out SpinWindow window))
        {
            return false;
        }

        int now = Find.TickManager.TicksGame;
        if (now >= window.endTick || pawn.Destroyed || !pawn.Spawned)
        {
            ActiveSpins.Remove(pawn.thingIDNumber);
            return false;
        }

        float progress = Mathf.Clamp01((now - window.startTick) / (float)SpinDurationTicks);
        angle = progress * 360f;
        return true;
    }
}

[HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.Notify_MeleeAttackOn))]
internal static class PawnDrawTracker_NotifyMeleeAttackOn_GhoulAttackSpinPatch
{
    private static void Postfix(Pawn ___pawn)
    {
        GhoulAttackSpinState.StartSpin(___pawn);
    }
}

[HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
internal static class PawnRenderer_GetDrawParms_GhoulAttackSpinPatch
{
    private static void Postfix(ref PawnDrawParms __result)
    {
        Pawn pawn = __result.pawn;
        if (pawn == null || !pawn.IsGhoul || !GhoulAttackSpinState.TryGetAngle(pawn, out float angle))
        {
            return;
        }

        __result.matrix *= Matrix4x4.Rotate(Quaternion.AngleAxis(angle, Vector3.up));
    }
}
