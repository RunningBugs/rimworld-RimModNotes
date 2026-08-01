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

/// <summary>
/// 「自动激素心脏」开关状态与自动释放驱动。
/// 不依赖给种族 def 挂 comp（对任何种族的食尸鬼都生效），
/// 开关状态按 pawn id 随存档持久化。
/// </summary>
public sealed class AutoFrenzyState : GameComponent
{
    private Dictionary<int, bool> enabledByPawnId = new Dictionary<int, bool>();

    public static AutoFrenzyState Instance => Current.Game?.GetComponent<AutoFrenzyState>();

    public AutoFrenzyState(Game game)
    {
    }

    public bool IsEnabled(Pawn pawn)
    {
        return pawn != null && enabledByPawnId.TryGetValue(pawn.thingIDNumber, out bool enabled) && enabled;
    }

    public void SetEnabled(Pawn pawn, bool enabled)
    {
        if (pawn != null)
        {
            enabledByPawnId[pawn.thingIDNumber] = enabled;
        }
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref enabledByPawnId, "enabledByPawnId", LookMode.Value, LookMode.Value);
        enabledByPawnId ??= new Dictionary<int, bool>();
    }

    // 战斗技能需要即时响应：每 tick 检测。
    // 但没有任何食尸鬼打开开关时直接短路，不产生扫描开销。
    public override void GameComponentTick()
    {
        if (!GhoulAttackSpinMod.Settings.enableAutoFrenzy || !AnyEnabled())
        {
            return;
        }
        AbilityDef abilityDef = GhoulAutoFrenzyDefs.GhoulFrenzyAbility;
        if (abilityDef == null)
        {
            return;
        }

        List<Map> maps = Find.Maps;
        for (int i = 0; i < maps.Count; i++)
        {
            IReadOnlyList<Pawn> pawns = maps[i].mapPawns.AllPawnsSpawned;
            for (int j = 0; j < pawns.Count; j++)
            {
                TryAutoCast(pawns[j], abilityDef);
            }
        }
    }

    private bool AnyEnabled()
    {
        foreach (KeyValuePair<int, bool> pair in enabledByPawnId)
        {
            if (pair.Value)
            {
                return true;
            }
        }
        return false;
    }

    private void TryAutoCast(Pawn pawn, AbilityDef abilityDef)
    {
        if (!IsEnabled(pawn) || !pawn.Spawned || pawn.Dead || pawn.Downed
            || !pawn.Drafted || !pawn.IsGhoul || !pawn.IsColonySubhumanPlayerControlled)
        {
            return;
        }

        // 激素心脏等植入体授予的能力在「临时能力」列表里，必须 includeTemporary。
        Ability ability = pawn.abilities?.GetAbility(abilityDef, includeTemporary: true);
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
        ability.QueueCastingJob(pawn, pawn);
    }
}

/// <summary>
/// 为食尸鬼注入「自动激素心脏」开关 gizmo（任何种族的食尸鬼均可），
/// 并为食尸鬼狂热的能力按钮挂上快捷键（Mod 设置里的按键绑定，默认 None）。
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
internal static class PawnGetGizmos_GhoulFrenzyPatch
{
    private static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
    {
        __result = Process(__instance, __result);
    }

    private static IEnumerable<Gizmo> Process(Pawn pawn, IEnumerable<Gizmo> gizmos)
    {
        AbilityDef abilityDef = GhoulAutoFrenzyDefs.GhoulFrenzyAbility;
        KeyBindingDef hotkey = GhoulAutoFrenzyDefs.GhoulFrenzyHotkey;

        foreach (Gizmo gizmo in gizmos)
        {
            if (abilityDef != null && hotkey != null
                && gizmo is Command_Ability commandAbility
                && commandAbility.Ability?.def == abilityDef
                && commandAbility.hotKey == null)
            {
                commandAbility.hotKey = hotkey;
            }
            yield return gizmo;
        }

        if (!GhoulAttackSpinMod.Settings.enableAutoFrenzy || abilityDef == null)
        {
            yield break;
        }
        if (pawn == null || !pawn.IsGhoul || !pawn.IsColonySubhumanPlayerControlled || !pawn.Drafted)
        {
            yield break;
        }
        // 激素心脏等植入体授予的能力在「临时能力」列表里，必须 includeTemporary。
        if (pawn.abilities?.GetAbility(abilityDef, includeTemporary: true) == null)
        {
            yield break;
        }
        AutoFrenzyState state = AutoFrenzyState.Instance;
        if (state == null)
        {
            yield break;
        }

        yield return new Command_Toggle
        {
            defaultLabel = "GAS_AutoFrenzyLabel".Translate(),
            defaultDesc = "GAS_AutoFrenzyDesc".Translate(),
            icon = abilityDef.uiIcon,
            isActive = () => state.IsEnabled(pawn),
            toggleAction = () => state.SetEnabled(pawn, !state.IsEnabled(pawn))
        };
    }
}
