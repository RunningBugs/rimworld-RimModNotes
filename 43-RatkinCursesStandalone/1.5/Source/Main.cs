using Verse;
using Verse.Sound;
using HarmonyLib;
using RimWorld.Planet;
using RimWorld;


namespace RASL
{
    public static class RAUtility
    {
        public static bool IfCurseActive(string curse)
        {
            RAComponent component = Find.World.GetComponent<RAComponent>();
            if (component.curse == curse)
            {
                return true;
            }
            return false;
        }

        public static void LeaveFilthAtPawn(this Pawn pawn, ThingDef filth, int radius, int count)
        {
            if (pawn.Map == null)
            {
                Log.Warning("Try to make filth but map is null.");
                return;
            }
            for (int i = 0; i < count; i++)
            {
                if (CellFinder.TryFindRandomCellNear(pawn.Position, pawn.Map, radius, (IntVec3 c) => c.Standable(pawn.Map) && !c.GetTerrain(pawn.Map).IsWater, out var result))
                {
                    FilthMaker.TryMakeFilth(result, pawn.Map, filth);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "PostApplyDamage")]
	public static class Wounded_Patch
	{
		public static void Prefix(Pawn __instance, DamageInfo dinfo)
		{
            if (!__instance.IsColonist && !__instance.IsSlaveOfColony)
            {
                return;
            }

			if (!RAUtility.IfCurseActive("Wounded"))
                {
                    return;
                }
			if (__instance.RaceProps.Humanlike && dinfo.Amount > 0 && dinfo.Def.harmsHealth)
			{
				if (!__instance.Spawned)
				{
					return;
				}
				Pawn pawn = __instance;
				if (!pawn.Dead && !pawn.Downed)
				{
					if (pawn.Map != null)
					{
                        SoundDef sound = DefDatabase<SoundDef>.GetNamed("Pawn_Fleshbeast_Bulbfreak_Death");
                        sound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
						pawn.LeaveFilthAtPawn(pawn.RaceProps.BloodDef, 2, 16);
					}
					HealthUtility.DamageUntilDowned(pawn);
				}
			}
		}
	}

    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Harmony harmony = new("com.RunningBugs.RASL");
            harmony.PatchAll();
            Log.Message("RatkinAnomalyCurseStandalone patched!");
        }
    }


    public class RAComponent : WorldComponent
    {
        public string curse = "Null";
        public RAComponent(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref curse, "curse", "Null");
        }
    }
}
