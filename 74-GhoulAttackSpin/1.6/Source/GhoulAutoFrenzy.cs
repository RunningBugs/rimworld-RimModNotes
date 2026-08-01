using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GhoulAttackSpin;

/// <summary>
/// Anomaly 内容（AdrenalHeart / GhoulFrenzy）在未启用 Anomaly 时不存在，
/// 统一用 GetNamedSilentFail 取用并判空，不用 DefOf 避免启动报错。
/// </summary>
internal static class GhoulAutoFrenzyDefs
{
    internal static AbilityDef GhoulFrenzyAbility => DefDatabase<AbilityDef>.GetNamedSilentFail("GhoulFrenzy");
    internal static HediffDef GhoulFrenzyHediff => DefDatabase<HediffDef>.GetNamedSilentFail("GhoulFrenzy");
    internal static KeyBindingDef GhoulFrenzyHotkey => DefDatabase<KeyBindingDef>.GetNamedSilentFail("GhoulAttackSpin_GhoulFrenzy");
}

/// <summary>临时诊断探针：定位自动激素心脏 toggle 不显示的原因，排查后移除。</summary>
internal static class GasProbe
{
    private static readonly HashSet<int> LoggedPawns = new HashSet<int>();

    internal static void Log(string message)
    {
        Verse.Log.Message("[GhoulAttackSpin][probe] " + message);
    }

    internal static void LogOncePerPawn(Pawn pawn, string message)
    {
        if (pawn != null && LoggedPawns.Add(pawn.thingIDNumber))
        {
            Log(message);
        }
    }
}

[StaticConstructorOnStartup]
internal static class GasProbeStartup
{
    static GasProbeStartup()
    {
        bool compOnHuman = ThingDefOf.Human.comps.Any(c => c is CompProperties_GhoulAutoFrenzy);
        GasProbe.Log("comp attached to Human def: " + compOnHuman
            + "; GhoulFrenzy ability def found: " + (GhoulAutoFrenzyDefs.GhoulFrenzyAbility != null)
            + "; hotkey def found: " + (GhoulAutoFrenzyDefs.GhoulFrenzyHotkey != null)
            + "; enableAutoFrenzy setting: " + GhoulAttackSpinMod.Settings.enableAutoFrenzy);
    }
}

public sealed class CompProperties_GhoulAutoFrenzy : CompProperties
{
    public CompProperties_GhoulAutoFrenzy()
    {
        compClass = typeof(CompGhoulAutoFrenzy);
    }
}

/// <summary>
/// 参考 GhoulCommands 的实现：单个 Command_Toggle 开关「自动激素心脏」。
/// 仅当食尸鬼被征召、且拥有激素心脏（GhoulFrenzy 能力）时显示；
/// 开启后在征召状态下自动释放食尸鬼狂热。
/// </summary>
public sealed class CompGhoulAutoFrenzy : ThingComp
{
    private bool autoFrenzy;

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        AbilityDef abilityDef = GhoulAutoFrenzyDefs.GhoulFrenzyAbility;
        if (parent is Pawn probePawn && probePawn.IsGhoul)
        {
            GasProbe.LogOncePerPawn(probePawn, "CompGetGizmosExtra on ghoul " + probePawn.LabelShort
                + ": enableAutoFrenzy=" + GhoulAttackSpinMod.Settings.enableAutoFrenzy
                + ", abilityDef!=" + (abilityDef != null)
                + ", IsColonySubhumanPlayerControlled=" + probePawn.IsColonySubhumanPlayerControlled
                + ", Drafted=" + probePawn.Drafted
                + ", hasAbility=" + (probePawn.abilities?.GetAbility(abilityDef) != null));
        }

        if (!GhoulAttackSpinMod.Settings.enableAutoFrenzy || abilityDef == null)
        {
            yield break;
        }
        if (parent is not Pawn pawn || !pawn.IsGhoul || !pawn.IsColonySubhumanPlayerControlled || !pawn.Drafted)
        {
            yield break;
        }
        if (pawn.abilities?.GetAbility(abilityDef) == null)
        {
            yield break;
        }

        yield return new Command_Toggle
        {
            defaultLabel = "GAS_AutoFrenzyLabel".Translate(),
            defaultDesc = "GAS_AutoFrenzyDesc".Translate(),
            icon = abilityDef.uiIcon,
            isActive = () => autoFrenzy,
            toggleAction = () => autoFrenzy = !autoFrenzy
        };
    }

    public override void CompTickRare()
    {
        base.CompTickRare();
        if (!autoFrenzy || !GhoulAttackSpinMod.Settings.enableAutoFrenzy)
        {
            return;
        }
        AbilityDef abilityDef = GhoulAutoFrenzyDefs.GhoulFrenzyAbility;
        if (abilityDef == null || parent is not Pawn pawn || !pawn.Spawned || pawn.Dead || pawn.Downed
            || !pawn.Drafted || !pawn.IsGhoul || !pawn.IsColonySubhumanPlayerControlled)
        {
            return;
        }

        Ability ability = pawn.abilities?.GetAbility(abilityDef);
        if (ability == null || !ability.CanCast || ability.CooldownTicksRemaining > 0)
        {
            return;
        }
        HediffDef frenzyHediff = GhoulAutoFrenzyDefs.GhoulFrenzyHediff;
        if (frenzyHediff != null && pawn.health?.hediffSet != null && pawn.health.hediffSet.HasHediff(frenzyHediff))
        {
            return;
        }

        // GhoulFrenzy 是 nonInterruptingSelfCast，QueueCastingJob 会立即自我释放。
        GasProbe.LogOncePerPawn(pawn, "auto-casting GhoulFrenzy for " + pawn.LabelShort);
        ability.QueueCastingJob(pawn, pawn);
    }

    public override void PostExposeData()
    {
        Scribe_Values.Look(ref autoFrenzy, "autoFrenzy", false);
    }
}

/// <summary>
/// 为食尸鬼狂热的能力按钮挂上快捷键（Mod 设置里的按键绑定，默认 None）。
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
internal static class PawnGetGizmos_GhoulFrenzyHotkeyPatch
{
    private static void Postfix(Pawn __instance, IEnumerable<Gizmo> __result)
    {
        KeyBindingDef hotkey = GhoulAutoFrenzyDefs.GhoulFrenzyHotkey;
        AbilityDef abilityDef = GhoulAutoFrenzyDefs.GhoulFrenzyAbility;
        if (hotkey == null || abilityDef == null || __instance == null || !__instance.IsGhoul)
        {
            return;
        }

        foreach (Gizmo gizmo in __result)
        {
            if (gizmo is Command_Ability commandAbility && commandAbility.Ability?.def == abilityDef && commandAbility.hotKey == null)
            {
                commandAbility.hotKey = hotkey;
            }
        }
    }
}
