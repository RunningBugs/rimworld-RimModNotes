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
            if (!KillingRewardMod.Settings.Enabled || !KillEligibilityAdapter.ShouldCount(__instance, dinfo))
            {
                return;
            }
            KillRewardTracker.Instance?.AddKill();
            // 击杀反馈：受害者头顶红色「祭品+1」浮字（参照原版 MISS/闪避浮字机制）。
            // 注意：Kill 结束时 pawn 已被收入尸体并脱离地图，必须用 Corpse 定位。
            Corpse corpse = __instance.Corpse;
            if (corpse?.Map != null)
            {
                MoteMaker.ThrowText(corpse.DrawPos + new Vector3(0f, 0f, 0.5f), corpse.Map,
                    "KR_Offering".Translate(), Color.red);
            }
        }
    }
}
