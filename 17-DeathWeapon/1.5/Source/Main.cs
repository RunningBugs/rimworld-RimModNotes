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


	public static class HarmonyHelper
	{
		public static FieldInfo GetFieldOrProperty<T>(this object obj, string name, out T value)
		{
			var type = obj.GetType();
			var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			if (field != null)
			{
				value = (T)field.GetValue(obj);
				return field;
			}
			var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			if (property != null)
			{
				value = (T)property.GetValue(obj);
				return null;
			}
			throw new Exception($"Field or property {name} not found in {type}");
		}

		public static T CallMethod<T>(this object instance, string methodName, ref object[] parameters, Type type = null)
		{
			type ??= instance.GetType();
			MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			return (T)methodInfo.Invoke(instance, parameters);
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
				DamageInfo dinfo = new DamageInfo(this.def.projectile.damageDef, this.def.projectile.GetDamageAmount(1f), this.ArmorPenetration, this.ExactRotation.eulerAngles.y, this.launcher, null, this.equipmentDef, DamageInfo.SourceCategory.ThingOrUnknown, this.intendedTarget.Thing);
				DeathUtility.Kill(pawn, dinfo);
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

		private static (MethodInfo, T) CallMethod<T>(object instance, Type type, string methodName, ref object[] parameters)
		{
			MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			return (methodInfo, (T)methodInfo.Invoke(instance, parameters));
		}

		private static bool ForceNoDeathNotification(Pawn p)
		{
			if (!p.forceNoDeathNotification)
			{
				return p.kindDef.forceNoDeathNotification;
			}
			return true;
		}

		public static void Kill(Pawn pawn, DamageInfo? dinfo, Hediff exactCulprit = null)
		{
			int num = 0;
			pawn.health.isBeingKilled = true;
			try
			{
				num = 1;
				IntVec3 positionHeld = pawn.PositionHeld;
				Map map = pawn.Map;
				Map map2 = (pawn.prevMap = pawn.MapHeld);
				Lord lord = pawn.GetLord();
				bool spawned = pawn.Spawned;
				bool spawnedOrAnyParentSpawned = pawn.SpawnedOrAnyParentSpawned;
				bool wasWorldPawn = pawn.IsWorldPawn();
				bool? flag = pawn.guilt?.IsGuilty;
				Caravan caravan = pawn.GetCaravan();
				bool isShambler = pawn.IsShambler;
				Building_Grave assignedGrave = null;
				if (pawn.ownership != null)
				{
					assignedGrave = pawn.ownership.AssignedGrave;
				}
				Building_Bed currentBed = pawn.CurrentBed();
				// RemoveFromHoldingContainer(ref map, ref spawned, dinfo);

				if (ModsConfig.AnomalyActive && pawn.ParentHolder is Building_HoldingPlatform { Spawned: not false } building_HoldingPlatform)
				{
					building_HoldingPlatform.Notify_PawnDied(pawn, dinfo);
					spawned = true;
					map = building_HoldingPlatform.Map;
				}
				object[] parameters = new object[] { map, spawned, dinfo };

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
					foreach (Lord lord3 in map.lordManager.lords)
					{
						if (lord3.LordJob is LordJob_Ritual lordJob_Ritual && lordJob_Ritual.pawnsDeathIgnored.Contains(pawn))
						{
							flag4 = true;
							break;
						}
					}
				}
				bool flag5 = PawnUtility.ShouldSendNotificationAbout(pawn) && (!(flag3 || flag4) || !dinfo.HasValue || dinfo.Value.Def != DamageDefOf.ExecutionCut) && !ForceNoDeathNotification(pawn);
				num = 2;
				// DoKillSideEffects(dinfo, exactCulprit, spawned);
				parameters = new object[] { dinfo, exactCulprit, spawned };
				CallMethod<object>(pawn, typeof(Pawn), "DoKillSideEffects", ref parameters);

				num = 3;
				// PreDeathPawnModifications(dinfo, map);
				parameters = new object[] { dinfo, map };
				CallMethod<object>(pawn, typeof(Pawn), "PreDeathPawnModifications", ref parameters);

				num = 4;
				// DropBeforeDying(dinfo, ref map, ref spawned);
				parameters = new object[] { dinfo, map, spawned };
				CallMethod<object>(pawn, typeof(Pawn), "DropBeforeDying", ref parameters);
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
				Lord lord2 = pawn.GetLord();
				lord2?.Notify_PawnLost(pawn, PawnLostCondition.Killed, dinfo);
				if (ModsConfig.AnomalyActive)
				{
					Find.Anomaly.Notify_PawnDied(pawn);
				}
				if (spawned)
				{
					pawn.DropAndForbidEverything();

				}
				if (spawned)
				{
					GenLeaving.DoLeavingsFor(pawn, map, DestroyMode.KillFinalize);
				}
				bool num2 = pawn.DeSpawnOrDeselect();
				if (pawn.royalty != null)
				{
					pawn.royalty.Notify_PawnKilled();
				}
				Corpse corpse = null;
				if (!PawnGenerator.IsPawnBeingGeneratedAndNotAllowsDead(pawn) && pawn.RaceProps.corpseDef != null)
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
						if (GenPlace.TryPlaceThing(corpse, positionHeld, map2, ThingPlaceMode.Direct))
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
							if (pawn.GetAttachment(ThingDefOf.Fire) is Fire fire)
							{
								FireUtility.TryStartFireIn(corpse.Position, corpse.Map, fire.CurrentSize(), fire.instigator);
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
					if (pawn.addCorpseToLord)
					{
						lord2?.AddCorpse(corpse);
					}
				}
				pawn.Drawer.renderer.SetAllGraphicsDirty();
				if ((ModsConfig.AnomalyActive && pawn.kindDef == PawnKindDefOf.Revenant) || pawn.kindDef.defName == "RevenantTrailer")
				{
					RevenantUtility.OnRevenantDeath(pawn, map);
				}
				pawn.duplicate?.Notify_PawnKilled();
				pawn.Drawer.renderer.SetAnimation(null);
				if (!pawn.Destroyed)
				{
					Map prevMap = (!(pawn is Pawn p)) ? pawn.MapHeld : (pawn.prevMap ?? pawn.MapHeld);
					pawn.Destroy(DestroyMode.KillFinalize);
					if (pawn.AllComps != null)
					{
						foreach (ThingComp comp in pawn.AllComps)
						{
							comp.Notify_Killed(prevMap, dinfo);
						}
					}
				}
				PawnComponentsUtility.RemoveComponentsOnKilled(pawn);
				pawn.health.hediffSet.DirtyCache();
				PortraitsCache.SetDirty(pawn);
				GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
				if (num2 && corpse != null && !corpse.Destroyed)
				{
					Find.Selector.Select(corpse, playSound: false, forceDesignatorDeselect: false);
				}
				num = 6;
				pawn.health.hediffSet.Notify_PawnDied(dinfo, exactCulprit);
				if (pawn.IsMutant)
				{
					pawn.mutant.Notify_Died(corpse, dinfo, exactCulprit);
				}
				pawn.genes?.Notify_PawnDied(dinfo, exactCulprit);
				pawn.HomeFaction?.Notify_MemberDied(pawn, dinfo, wasWorldPawn, flag == true, map2);
				if (corpse != null)
				{
					if (pawn.RaceProps.DeathActionWorker != null && spawned && !isShambler)
					{
						pawn.RaceProps.DeathActionWorker.PawnDied(corpse, lord);
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
					GenHostility.Notify_PawnLostForTutor(pawn, map2);
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
				if (pawn.IsMutant && pawn.mutant.Def.clearMutantStatusOnDeath && pawn.mutant.HasTurned)
				{
					pawn.mutant.Revert(beingKilled: true);
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
				pawn.Notify_DisabledWorkTypesChanged();
				Find.BossgroupManager.Notify_PawnKilled(pawn);
				if (pawn.IsCreepJoiner)
				{
					pawn.creepjoiner.Notify_CreepJoinerKilled();
				}
				pawn.health.isBeingKilled = false;
			}
			catch (Exception arg)
			{
				Log.Error($"Error while killing {pawn.ToStringSafe()} during phase {num}: {arg}");
			}
		}
	}

}
