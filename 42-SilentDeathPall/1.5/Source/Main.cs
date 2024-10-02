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

// using System.Reflection;
using HarmonyLib;
using System.Reflection;

namespace SilentDeathPall;

[StaticConstructorOnStartup]
public static class Start
{
    static Start()
    {
        new Harmony("com.RunningBugs.SilentDeathPall").PatchAll();
    }
}

[HarmonyPatch(typeof(GameCondition_DeathPall), "GameConditionTick")]
public static class GameCondition_DeathPall_GameConditionTick_Patch
{
    // private static int lastSoundTick = -1;
    // private const int soundInterval = 1000;

    public static FieldInfo GetFieldInfo(this GameCondition_DeathPall instance, string fieldName)
    {
        return instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
    }

    public static T GetFieldValue<T>(this GameCondition_DeathPall instance, string fieldName)
    {
        return (T)instance.GetFieldInfo(fieldName).GetValue(instance);
    }

    public static T GetStaticFieldValue<T>(this GameCondition_DeathPall instance, string fieldName)
    {
        return (T)instance.GetType().GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    }

    public static void SetFieldValue<T>(this GameCondition_DeathPall instance, string fieldName, T value)
    {
        instance.GetFieldInfo(fieldName).SetValue(instance, value);
    }

    public static MethodInfo GetMethodInfo(this GameCondition_DeathPall instance, string methodName)
    {
        return instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
    }

    public static bool Prefix(GameCondition_DeathPall __instance)
    {
        if (Find.TickManager.TicksGame < __instance.GetFieldValue<int>("nextResurrectTick") || Find.TickManager.TicksGame % 60 != 0)
        {
            return false;
        }
        foreach (Map affectedMap in __instance.AffectedMaps)
        {
            foreach (Thing item in affectedMap.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                if (item is Corpse corpse && MutantUtility.CanResurrectAsShambler(corpse) && corpse.Age >= 15000)
                {
                    // Pawn pawn = ResurrectPawn(corpse);
                    Pawn pawn = GetMethodInfo(__instance, "ResurrectPawn").Invoke(__instance, new object[] { corpse }) as Pawn;
                    if (!pawn.Position.Fogged(affectedMap))
                    {
                        Messages.Message("DeathPallResurrectedMessage".Translate(pawn), pawn, MessageTypeDefOf.SilentInput, historical: false);
                    }
                    IntRange ResurrectIntervalRange = __instance.GetStaticFieldValue<IntRange>("ResurrectIntervalRange");
                    int nextResurrectTick = Find.TickManager.TicksGame + ResurrectIntervalRange.RandomInRange;
                    __instance.SetFieldValue("nextResurrectTick", nextResurrectTick);
                    return false;
                }
            }
        }
        return false;
    }
}
