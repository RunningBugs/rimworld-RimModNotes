using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod17Checks
{
    public const string PackageId = "RunningBugs.DeathWeapon";
    public static void Run(Map map)
    {
        var damage = DefDatabase<DamageDef>.GetNamed("DeathWeapon_DeathDamage");
        var bulletDef = DefDatabase<ThingDef>.GetNamed("Bullet_DeathBullet");
        Require(damage.Worker.GetType().FullName == "DeathWeapon.DeathDamageWorker"
            && bulletDef.thingClass.FullName == "DeathWeapon.DeathBullet", "XML worker/projectile links failed");
        var launcher = map.mapPawns.FreeColonists.First();
        var guard = AccessTools.Method(AccessTools.TypeByName("DeathWeapon.PreventDeathrestAfterDeath"), "Prefix");
        var healthPost = AccessTools.Method(typeof(Pawn_HealthTracker), "PostApplyDamage");
        Require(Harmony.GetPatchInfo(healthPost)?.Prefixes.Any(p => p.PatchMethod == guard) == true,
            "Specific completed-death reentry protection was not registered");
        var faction = Find.FactionManager.AllFactions.First(f => f.def.humanlikeFaction
            && f.HostileTo(Faction.OfPlayer));
        Pawn victim = null;
        Thing corpse = null, projectile = null, rifle = null, ordinary = null;
        Rand.PushState(17);
        try
        {
            victim = MakeVictim();
            var meleeInfo = new DamageInfo(damage, 1f, instigator: launcher);
            Require((bool)guard.Invoke(null, new object[] { victim.health, meleeInfo }),
                "Alive pawn matching-damage control incorrectly skipped the original health flow");
            victim.TakeDamage(meleeInfo);
            VerifyDeath(victim, "melee damage worker");
            Require((bool)guard.Invoke(null, new object[] { victim.health,
                new DamageInfo(DamageDefOf.Bullet, 1f, instigator: launcher) }),
                "Nonmatching damage on dead pawn incorrectly skipped the original health flow");
            Require(!(bool)guard.Invoke(null, new object[] { victim.health, meleeInfo }),
                "Matching completed-death protection did not reject reentry");
            corpse = victim.Corpse;
            Cleanup();

            ordinary = ThingMaker.MakeThing(ThingDefOf.Silver);
            ordinary.TakeDamage(new DamageInfo(damage, 1f));
            Require(!ordinary.Destroyed, "Non-pawn was incorrectly force-killed");

            victim = MakeVictim();
            rifle = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_DeathRifle"));
            projectile = ThingMaker.MakeThing(bulletDef);
            GenSpawn.Spawn(projectile, EmptyCell(), map);
            ((Projectile)projectile).Launch(launcher, victim, victim, ProjectileHitFlags.IntendedTarget,
                equipment: rifle);
            AccessTools.Method(projectile.GetType(), "Impact").Invoke(projectile, new object[] { victim, false });
            VerifyDeath(victim, "ranged projectile Impact");
            corpse = victim.Corpse;
        }
        finally
        {
            Cleanup();
            if (projectile != null && !projectile.Destroyed) projectile.Destroy(DestroyMode.Vanish);
            if (rifle != null && !rifle.Destroyed) rifle.Destroy(DestroyMode.Vanish);
            if (ordinary != null && !ordinary.Destroyed) ordinary.Destroy(DestroyMode.Vanish);
            Rand.PopState();
        }
        Pawn MakeVictim()
        {
            var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.SpaceRefugee, faction);
            pawn.forceNoDeathNotification = true;
            if (ModsConfig.BiotechActive)
            {
                pawn.genes.AddGene(GeneDefOf.Deathless, true);
                Require(pawn.genes.HasActiveGene(GeneDefOf.Deathless), "Deathless gene fixture not active");
            }
            GenSpawn.Spawn(pawn, EmptyCell(), map);
            Require(!pawn.Dead && pawn.health != null && pawn.mindState != null && pawn.guilt != null,
                "Initial generated pawn is missing live health/mind/guilt state");
            return pawn;
        }
        IntVec3 EmptyCell() => GenRadial.RadialCellsAround(launcher.Position, 25f, false)
            .First(c => c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && c.GetThingList(map).Count == 0);
        void VerifyDeath(Pawn pawn, string stage)
        {
            Require(pawn.Dead && pawn.Destroyed && !pawn.health.isBeingKilled && pawn.prevMap == null,
                stage + ": force-death or transient cleanup failed");
            Require(pawn.Corpse != null && pawn.Corpse.Spawned && pawn.Corpse.Map == map
                && pawn.Corpse.InnerPawn == pawn, stage + ": corpse placement/ownership failed");
        }
        void Cleanup()
        {
            corpse ??= victim?.Corpse;
            if (corpse != null && !corpse.Destroyed) corpse.Destroy(DestroyMode.Vanish);
            if (victim != null && !victim.Destroyed) victim.Destroy(DestroyMode.Vanish);
            corpse = null;
            victim = null;
        }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Mod17: " + message);
    }
}
