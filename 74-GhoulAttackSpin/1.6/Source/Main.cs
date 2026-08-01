using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace GhoulAttackSpin;

public enum SpinDirection
{
    Clockwise,
    Counterclockwise,
    Random
}

public sealed class GhoulAttackSpinSettings : ModSettings
{
    public SpinDirection direction = SpinDirection.Random;
    public bool enableAutoFrenzy = true;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref direction, "direction", SpinDirection.Random);
        Scribe_Values.Look(ref enableAutoFrenzy, "enableAutoFrenzy", true);
    }
}

public sealed class GhoulAttackSpinMod : Mod
{
    public static GhoulAttackSpinSettings Settings { get; private set; }

    public GhoulAttackSpinMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<GhoulAttackSpinSettings>();
        new Harmony(content.PackageId).PatchAll();
    }

    public override string SettingsCategory()
    {
        return "Ghoul Attack Spin";
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new Listing_Standard();
        listing.Begin(inRect);

        listing.Label("GAS_SettingsDirection".Translate());
        if (listing.RadioButton("GAS_DirectionClockwise".Translate(), Settings.direction == SpinDirection.Clockwise))
        {
            Settings.direction = SpinDirection.Clockwise;
        }
        if (listing.RadioButton("GAS_DirectionCounterclockwise".Translate(), Settings.direction == SpinDirection.Counterclockwise))
        {
            Settings.direction = SpinDirection.Counterclockwise;
        }
        if (listing.RadioButton("GAS_DirectionRandom".Translate(), Settings.direction == SpinDirection.Random))
        {
            Settings.direction = SpinDirection.Random;
        }

        listing.Gap();
        listing.CheckboxLabeled("GAS_SettingsAutoFrenzy".Translate(), ref Settings.enableAutoFrenzy, "GAS_SettingsAutoFrenzyDesc".Translate());

        listing.End();
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
        public float sign;
    }

    private static readonly Dictionary<int, SpinWindow> ActiveSpins = new Dictionary<int, SpinWindow>();

    internal static void StartSpin(Pawn pawn)
    {
        if (pawn == null || pawn.Destroyed || !pawn.Spawned || !pawn.IsGhoul)
        {
            return;
        }

        float sign = GhoulAttackSpinMod.Settings.direction switch
        {
            SpinDirection.Clockwise => 1f,
            SpinDirection.Counterclockwise => -1f,
            _ => Rand.Bool ? 1f : -1f
        };

        int now = Find.TickManager.TicksGame;
        ActiveSpins[pawn.thingIDNumber] = new SpinWindow
        {
            startTick = now,
            endTick = now + SpinDurationTicks,
            sign = sign
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
        angle = progress * 360f * window.sign;
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
