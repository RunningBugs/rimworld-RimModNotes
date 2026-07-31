using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace KillingReward
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class PawnKillPatch
    {
        public static void Postfix(Pawn __instance, DamageInfo? dinfo)
        {
            if (!KillEligibilityAdapter.ShouldCount(__instance, dinfo))
            {
                return;
            }
            KillRewardTracker.Instance?.AddKill();
            // 击杀反馈：受害者头顶红色「祭品+1」浮字（参照原版 MISS/闪避浮字机制）。
            if (__instance.Map != null)
            {
                MoteMaker.ThrowText(__instance.DrawPos + new Vector3(0f, 0f, 0.5f), __instance.Map,
                    "KR_Offering".Translate(), Color.red);
            }
        }
    }
}
