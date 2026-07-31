using HarmonyLib;
using RimWorld;
using Verse;

namespace KillingReward
{
    /// <summary>
    /// 记录玩家派系小人造成的伤害，供击杀归属补窗使用。
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class PawnDamagePatch
    {
        public static void Postfix(Pawn __instance, DamageInfo dinfo)
        {
            if (__instance?.Faction == null || !__instance.Faction.HostileTo(Faction.OfPlayer))
            {
                return;
            }
            if (dinfo.Instigator is Pawn instigator && instigator.Faction != null && instigator.Faction.IsPlayer)
            {
                PlayerDamageTracker.NotifyPlayerDamaged(__instance);
            }
        }
    }
}
