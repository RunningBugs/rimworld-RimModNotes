using RimWorld;
using Verse;

namespace DeathWeapon
{
    [HarmonyLib.HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.PostApplyDamage))]
    public static class PreventDeathrestAfterDeath
    {
        public static bool Prefix(Pawn_HealthTracker __instance, DamageInfo dinfo)
        {
            // A completed force-kill must not re-enter Deathless downing after worker.Apply returns.
            return !(__instance.Dead && dinfo.Def?.defName == "DeathWeapon_DeathDamage");
        }
    }

    public class DeathDamageWorker : DamageWorker
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            var result = base.Apply(dinfo, victim);
            if (victim is Pawn pawn)
            {
                DeathUtility.Kill(pawn, dinfo);
            }
            return result;
        }
    }
}
