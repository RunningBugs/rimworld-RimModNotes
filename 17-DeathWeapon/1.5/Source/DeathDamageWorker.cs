using RimWorld;
using Verse;

namespace DeathWeapon
{
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