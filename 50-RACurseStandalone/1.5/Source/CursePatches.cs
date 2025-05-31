using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI.Group;
using Verse.Sound;
using RimWorld;
using HarmonyLib;


namespace RACurseSA;

public static class RACurseSAUtility
{
    public static RACurseSAComponent GetGameComponent()
    {
        return Find.World.GetComponent<RACurseSAComponent>();
    }

    public static bool IfCurseActive(string curse)
    {
        if (GetGameComponent().Curse == curse) return true;
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

[HarmonyPatch(typeof(MemoryThoughtHandler))]
public static class AteWithoutTable_Patch
{
    [HarmonyTargetMethod]
    public static MethodInfo TargetMethod()
    {
        MethodInfo methodInfo = AccessTools.Method(typeof(MemoryThoughtHandler), "TryGainMemory", new Type[] { typeof(Thought_Memory), typeof(Pawn) });
        return methodInfo;
    }
    [HarmonyPrefix]
    public static void Postfix(MemoryThoughtHandler __instance, Thought_Memory newThought)
    {
        if (!__instance.pawn.IsColonist) return;

        if (!RACurseSAUtility.IfCurseActive("EatingWithoutTable"))
        {
            return;
        }
        if (newThought.def == ThoughtDefOf.AteWithoutTable)
        {
            Pawn pawn = __instance.pawn;
            Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.FoodPoisoning);
            if (firstHediffOfDef != null)
            {
                if (firstHediffOfDef.CurStageIndex != 2)
                {
                    firstHediffOfDef.Severity = HediffDefOf.FoodPoisoning.stages[2].minSeverity - 0.001f;
                }
            }
            else
            {
                pawn.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.FoodPoisoning, pawn));
            }
            if (PawnUtility.ShouldSendNotificationAbout(pawn) && MessagesRepeatAvoider.MessageShowAllowed("MessageFoodPoisoning-" + pawn.thingIDNumber, 0.1f))
            {
                Messages.Message("RA.CursePoisoning".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NegativeEvent);
            }
        }
    }
}

[HarmonyPatch(typeof(ResearchManager), "ResearchPerformed")]
public static class Research_Patch
{
    public static void Postfix()
    {
        if (!RACurseSAUtility.IfCurseActive("Research") || Find.Maps.Count == 0)
        {
            return;
        }
        foreach (Map map in Find.Maps)
        {
            List<Pawn> list = map.mapPawns.FreeColonists.FindAll(p => p.CurJobDef == JobDefOf.Research);
            if (list.Count > 0)
            {
                foreach (Pawn p in list)
                {
                    p.skills?.Learn(SkillDefOf.Intellectual, -10f);
                }
            }
        }
    }
}

[HarmonyPatch(typeof(Pawn), "Kill")]
public static class KillAnimals_Patch
{
    public static void Prefix(Pawn __instance, DamageInfo? dinfo)
    {
        if (!RACurseSAUtility.IfCurseActive("KillAnimals"))
        {
            return;
        }
        if (dinfo?.Instigator?.Faction == null)
        {
            return;
        }
        if (__instance.RaceProps.Animal && __instance.mindState?.mentalStateHandler != null && !__instance.mindState.mentalStateHandler.InMentalState)
        {
            Map map = __instance.MapHeld;
            if (map == null)
            {
                return;
            }
            Pawn chimera = PawnGenerator.GeneratePawn(PawnKindDefOf.Chimera, Faction.OfEntities);
            GenSpawn.Spawn(chimera, __instance.PositionHeld, map);
            Lord lord = LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_AssaultColony(), map);
            lord.AddPawn(chimera);
            Messages.Message("RA.CurseChimeraSpawn".Translate(), chimera, MessageTypeDefOf.NegativeEvent);
        }
    }
}

[HarmonyPatch(typeof(Pawn), "PostApplyDamage")]
public static class Wounded_Patch
{
    public static void Prefix(Pawn __instance, DamageInfo dinfo)
    {
        if (!RACurseSAUtility.IfCurseActive("Wounded"))
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
                    RACurseSADefOf.Pawn_Fleshbeast_Bulbfreak_Death.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    pawn.LeaveFilthAtPawn(pawn.RaceProps.BloodDef, 2, 16);
                }
                HealthUtility.DamageUntilDowned(pawn);
            }
        }
    }
}

[HarmonyPatch(typeof(HistoryEventsManager), "RecordEvent")]
public static class CutTree_Patch
{
    public static void Postfix(HistoryEvent historyEvent)
    {
        if (!RACurseSAUtility.IfCurseActive("CutTree"))
        {
            return;
        }
        if (historyEvent.def == HistoryEventDefOf.CutTree)
        {
            Map map = Find.Maps.First(m => m.IsPlayerHome);
            if (map == null)
            {
                return;
            }
            StorytellerComp storytellerComp = Find.Storyteller.storytellerComps.First((StorytellerComp x) => x is StorytellerComp_OnOffCycle || x is StorytellerComp_RandomMain);
            IncidentParms parms = storytellerComp.GenerateParms(IncidentCategoryDefOf.Special, map);
            parms.forced = true;
            RACurseSADefOf.ColdSnap.Worker.TryExecute(parms);
        }
    }
}
