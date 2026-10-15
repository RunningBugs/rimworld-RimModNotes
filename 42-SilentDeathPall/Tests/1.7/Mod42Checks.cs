using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod42Checks
{
    public const string PackageId = "RunningBugs.SilentDeathPall";

    public static void Run(Map map)
    {
        Require(ModsConfig.AnomalyActive, "Anomaly required");
        var target = typeof(GameCondition_DeathPall).GetMethod("GameConditionTick");
        Require(Harmony.GetPatchInfo(target)?.Prefixes.Any(p => p.PatchMethod.DeclaringType?.FullName == "SilentDeathPall.GameCondition_DeathPall_GameConditionTick_Patch") == true, "Specific prefix not registered");
        var next = typeof(GameCondition_DeathPall).GetField("nextResurrectTick", BindingFlags.Instance | BindingFlags.NonPublic);
        var lastMessage = typeof(GameCondition_DeathPall).GetField("lastResurrectMessageTick", BindingFlags.Instance | BindingFlags.NonPublic);
        var messages = (List<Message>)typeof(Messages).GetField("liveMessages", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var originalMessages = messages.ToArray();
        var oldTick = Find.TickManager.TicksGame;
        var otherCorpseAges = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>()
            .Where(c => MutantUtility.CanResurrectAsShambler(c) && c.Age >= 15000).ToDictionary(c => c, c => c.Age);
        Pawn pawn = null;
        Corpse corpse = null;
        try
        {
            var cell = map.AllCells.First(c => c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && c.GetThingList(map).Count == 0);
            pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfAncients);
            pawn.Kill(null);
            corpse = pawn.Corpse ?? pawn.MakeCorpse(null, (Building_Bed)null);
            Require(corpse != null, "Could not create a real dead pawn corpse");
            GenSpawn.Spawn(corpse, cell, map);
            Require(MutantUtility.CanResurrectAsShambler(corpse), "Fixture corpse cannot become a shambler");
            var condition = (GameCondition_DeathPall)GameConditionMaker.MakeCondition(GameConditionDefOf.DeathPall, 60000);
            condition.gameConditionManager = map.gameConditionManager;
            var aligned = ((oldTick + 59) / 60) * 60;
            Find.TickManager.DebugSetTicksGame(aligned);
            foreach (var other in otherCorpseAges.Keys)
                other.Age = 0;
            Require(!map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>()
                .Any(c => c != corpse && MutantUtility.CanResurrectAsShambler(c) && c.Age >= 15000), "Could not establish a unique eligible corpse fixture");
            corpse.Age = 15000;
            next.SetValue(condition, aligned + 1);
            condition.GameConditionTick();
            Require(pawn.Dead, "Future scheduled tick resurrected early");
            next.SetValue(condition, aligned);
            Find.TickManager.DebugSetTicksGame(aligned + 1);
            condition.GameConditionTick();
            Require(pawn.Dead, "Non-60 tick resurrected");
            Find.TickManager.DebugSetTicksGame(aligned);
            corpse.Age = 14999;
            condition.GameConditionTick();
            Require(pawn.Dead, "Younger-than-cooldown corpse resurrected");
            corpse.Age = 15000;
            var previousMessageTick = (int)lastMessage.GetValue(condition);
            condition.GameConditionTick();
            Require(!pawn.Dead && pawn.IsMutant && pawn.mutant.Def == MutantDefOf.Shambler, "Actual due condition tick did not resurrect a real shambler");
            Require(messages.Any(m => !originalMessages.Contains(m) && m.def == MessageTypeDefOf.SilentInput), "Resurrection did not emit the MOD silent input message");
            Require(MessageTypeDefOf.SilentInput.sound == null, "Silent message type unexpectedly has a sound");
            Require((int)lastMessage.GetValue(condition) == previousMessageTick, "Original noisy message path unexpectedly executed");
            var nextTick = (int)next.GetValue(condition);
            Require(nextTick >= aligned + 600 && nextTick <= aligned + 1800, "Actual private resurrection interval was not preserved");
            Require(otherCorpseAges.Keys.All(c => !c.Destroyed && c.InnerPawn?.Dead == true), "Age exclusion failed to preserve an existing fixture corpse");
        }
        finally
        {
            Find.TickManager.DebugSetTicksGame(oldTick);
            foreach (var saved in otherCorpseAges)
                if (!saved.Key.Destroyed)
                    saved.Key.Age = saved.Value;
            messages.Clear();
            messages.AddRange(originalMessages);
            if (corpse != null && !corpse.Destroyed)
                corpse.Destroy(DestroyMode.Vanish);
            if (pawn != null && !pawn.Destroyed)
                pawn.Destroy(DestroyMode.Vanish);
        }
        Require(otherCorpseAges.All(saved => !saved.Key.Destroyed && saved.Key.Age == saved.Value), "Original corpse death times were not restored exactly");
        Log.Message("RW17 Mod42 excluded " + otherCorpseAges.Count + " existing eligible fixture corpses and restored original death times");
        Log.Message("RW17 Mod42: actual tick prefix early/noninterval/young/due branches, real shambler resurrection, silent message and scheduling passed; audible output not tested");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Mod42: " + message);
    }
}
