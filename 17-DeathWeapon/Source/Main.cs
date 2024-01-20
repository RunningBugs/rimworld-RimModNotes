using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using Verse.Noise;
using Verse.Grammar;
using RimWorld;
using RimWorld.Planet;

using System.Reflection;
using HarmonyLib;

using Logs = Logger.Log;

namespace DeathWeapon
{
    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Harmony harmony = new Harmony("com.RunningBugs.DeathWeapon");
            harmony.PatchAll();
            Logs.Warning("DeathWeapon loaded successfully!");
        }
    }


    class DeathBullet : Bullet
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            blockedByShield = false;
            base.Impact(hitThing);
            if (hitThing is Pawn pawn)
            {
                // DamageInfo dinfo = new DamageInfo(this.def.projectile.damageDef, this.def.projectile.GetDamageAmount(1f), this.ArmorPenetration, this.ExactRotation.eulerAngles.y, this.launcher, null, this.equipmentDef, DamageInfo.SourceCategory.ThingOrUnknown, this.intendedTarget);
                DeathUtility.Kill(pawn, null);
            }
        }
    }


    public static class DeathUtility
    {
        private static (FieldInfo, T) GetValue<T>(object instance, Type type, string fieldName)
        {
            FieldInfo fieldInfo = type?.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return (fieldInfo, (T)fieldInfo?.GetValue(instance));
        }

        private static (MethodInfo, T) CallMethod<T>(object instance, Type type, string methodName, params object[] parameters)
        {
            MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return (methodInfo, (T)methodInfo.Invoke(instance, parameters));
        }

        private static bool ForceNoDeathNotification(Pawn p) {
            if (!p.forceNoDeathNotification)
			{
				return p.kindDef.forceNoDeathNotification;
			}
			return true;
        }

        public static void Kill(Pawn pawn, DamageInfo? dinfo, Hediff exactCulprit = null)
        {
            int num = 0;
            try
            {
                num = 1;
                IntVec3 positionHeld = pawn.PositionHeld;
                Map map = pawn.Map;
                Map mapHeld = pawn.MapHeld;
                bool spawned = pawn.Spawned;
                bool spawnedOrAnyParentSpawned = pawn.SpawnedOrAnyParentSpawned;
                bool wasWorldPawn = pawn.IsWorldPawn();
                bool? flag = pawn.guilt?.IsGuilty;
                Caravan caravan = pawn.GetCaravan();
                Building_Grave assignedGrave = null;
                if (pawn.ownership != null)
                {
                    assignedGrave = pawn.ownership.AssignedGrave;
                }
                Building_Bed currentBed = pawn.CurrentBed();
                ThingOwner thingOwner = null;
                bool inContainerEnclosed = pawn.InContainerEnclosed;
                if (inContainerEnclosed)
                {
                    thingOwner = pawn.holdingOwner;
                    thingOwner.Remove(pawn);
                }
                bool flag2 = false;
                bool flag3 = false;
                bool flag4 = false;
                if (Current.ProgramState == ProgramState.Playing && map != null)
                {
                    flag2 = map.designationManager.DesignationOn(pawn, DesignationDefOf.Hunt) != null;
                    flag3 = pawn.ShouldBeSlaughtered();
                    foreach (Lord lord in map.lordManager.lords)
                    {
                        if (lord.LordJob is LordJob_Ritual lordJob_Ritual && lordJob_Ritual.pawnsDeathIgnored.Contains(pawn))
                        {
                            flag4 = true;
                            break;
                        }
                    }
                }
                bool flag5 = PawnUtility.ShouldSendNotificationAbout(pawn) && (!(flag3 || flag4) || !dinfo.HasValue || dinfo.Value.Def != DamageDefOf.ExecutionCut) && !ForceNoDeathNotification(pawn);
                float num2 = 0f;
                Thing attachment = pawn.GetAttachment(ThingDefOf.Fire);
                if (attachment != null)
                {
                    num2 = ((Fire)attachment).CurrentSize();
                }
                num = 2;
                // pawn.DoKillSideEffects(dinfo, exactCulprit, spawned);
                CallMethod<object>(pawn, typeof(Pawn), "DoKillSideEffects", dinfo, exactCulprit, spawned);
                num = 3;
                // pawn.PreDeathPawnModifications(dinfo, map);
                CallMethod<object>(pawn, typeof(Pawn), "PreDeathPawnModifications", dinfo, map);
                num = 4;
                // pawn.DropBeforeDying(dinfo, ref map, ref spawned);
                object[] parameters = {dinfo, map, spawned};
                CallMethod<object>(pawn, typeof(Pawn), "DropBeforeDying", parameters);
                map = (Map)parameters[1];
                spawned = (bool)parameters[2];

                num = 5;
                pawn.health.SetDead();
                if (pawn.health.deflectionEffecter != null)
                {
                    pawn.health.deflectionEffecter.Cleanup();
                    pawn.health.deflectionEffecter = null;
                }
                if (pawn.health.woundedEffecter != null)
                {
                    pawn.health.woundedEffecter.Cleanup();
                    pawn.health.woundedEffecter = null;
                }
                caravan?.Notify_MemberDied(pawn);
                pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.Killed, dinfo);
                if (spawned)
                {
                    pawn.DropAndForbidEverything();
                }
                if (spawned)
                {
                    GenLeaving.DoLeavingsFor(pawn, map, DestroyMode.KillFinalize);
                }
                bool num3 = pawn.DeSpawnOrDeselect();
                if (pawn.royalty != null)
                {
                    pawn.royalty.Notify_PawnKilled();
                }
                Corpse corpse = null;
                if (!PawnGenerator.IsPawnBeingGeneratedAndNotAllowsDead(pawn))
                {
                    if (inContainerEnclosed)
                    {
                        corpse = pawn.MakeCorpse(assignedGrave, currentBed);
                        if (!thingOwner.TryAdd(corpse))
                        {
                            corpse.Destroy();
                            corpse = null;
                        }
                    }
                    else if (spawnedOrAnyParentSpawned)
                    {
                        if (pawn.holdingOwner != null)
                        {
                            pawn.holdingOwner.Remove(pawn);
                        }
                        corpse = pawn.MakeCorpse(assignedGrave, currentBed);
                        if (GenPlace.TryPlaceThing(corpse, positionHeld, mapHeld, ThingPlaceMode.Direct))
                        {
                            corpse.Rotation = pawn.Rotation;
                            if (HuntJobUtility.WasKilledByHunter(pawn, dinfo))
                            {
                                ((Pawn)dinfo.Value.Instigator).Reserve(corpse, ((Pawn)dinfo.Value.Instigator).CurJob);
                            }
                            else if (!flag2 && !flag3)
                            {
                                corpse.SetForbiddenIfOutsideHomeArea();
                            }
                            if (num2 > 0f)
                            {
                                FireUtility.TryStartFireIn(corpse.Position, corpse.Map, num2);
                            }
                        }
                        else
                        {
                            corpse.Destroy();
                            corpse = null;
                        }
                    }
                    else if (caravan != null && caravan.Spawned)
                    {
                        corpse = pawn.MakeCorpse(assignedGrave, currentBed);
                        caravan.AddPawnOrItem(corpse, addCarriedPawnToWorldPawnsIfAny: true);
                    }
                    else if (pawn.holdingOwner != null || pawn.IsWorldPawn())
                    {
                        Corpse.PostCorpseDestroy(pawn);
                    }
                    else
                    {
                        corpse = pawn.MakeCorpse(assignedGrave, currentBed);
                    }
                }
                if (corpse != null)
                {
                    Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup);
                    Hediff firstHediffOfDef2 = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Scaria);
                    CompRottable comp;
                    if ((comp = corpse.GetComp<CompRottable>()) != null && ((firstHediffOfDef != null && Rand.Value < firstHediffOfDef.Severity) || (firstHediffOfDef2 != null && Rand.Chance(Find.Storyteller.difficulty.scariaRotChance))))
                    {
                        comp.RotImmediately();
                    }
                }
                if (!pawn.Destroyed)
                {
                    pawn.Destroy(DestroyMode.KillFinalize);
                }
                PawnComponentsUtility.RemoveComponentsOnKilled(pawn);
                pawn.health.hediffSet.DirtyCache();
                PortraitsCache.SetDirty(pawn);
                GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
                if (num3 && corpse != null)
                {
                    Find.Selector.Select(corpse, playSound: false, forceDesignatorDeselect: false);
                }
                num = 6;
                pawn.health.hediffSet.Notify_PawnDied();
                pawn.genes?.Notify_PawnDied();
                pawn.HomeFaction?.Notify_MemberDied(pawn, dinfo, wasWorldPawn, flag == true, mapHeld);
                if (corpse != null)
                {
                    if (pawn.RaceProps.DeathActionWorker != null && spawned)
                    {
                        pawn.RaceProps.DeathActionWorker.PawnDied(corpse);
                    }
                    if (Find.Scenario != null)
                    {
                        Find.Scenario.Notify_PawnDied(corpse);
                    }
                }
                if (pawn.Faction != null && pawn.Faction.IsPlayer)
                {
                    BillUtility.Notify_ColonistUnavailable(pawn);
                }
                if (spawnedOrAnyParentSpawned)
                {
                    GenHostility.Notify_PawnLostForTutor(pawn, mapHeld);
                }
                if (pawn.Faction != null && pawn.Faction.IsPlayer && Current.ProgramState == ProgramState.Playing)
                {
                    Find.ColonistBar.MarkColonistsDirty();
                }
                pawn.psychicEntropy?.Notify_PawnDied();
                try
                {
                    pawn.Ideo?.Notify_MemberDied(pawn);
                    pawn.Ideo?.Notify_MemberLost(pawn, map);
                }
                catch (Exception ex)
                {
                    Log.Error("Error while notifying ideo of pawn death: " + ex);
                }
                if (flag5)
                {
                    pawn.health.NotifyPlayerOfKilled(dinfo, exactCulprit, caravan);
                }
                Find.QuestManager.Notify_PawnKilled(pawn, dinfo);
                Find.FactionManager.Notify_PawnKilled(pawn);
                Find.IdeoManager.Notify_PawnKilled(pawn);
                if (ModsConfig.BiotechActive && MechanitorUtility.IsMechanitor(pawn))
                {
                    Find.History.Notify_MechanitorDied();
                }
                Find.BossgroupManager.Notify_PawnKilled(pawn);
            }
            catch (Exception arg)
            {
                Log.Error($"Error while killing {pawn.ToStringSafe()} during phase {num}: {arg}");
            }
        }
    }

}
