using RimWorld;
using Verse;
using Verse.AI.Group;

namespace CultistKidnapping;

public sealed class PendingAbduction : IExposable
{
    public Lord lord;
    public Pawn invoker;
    public Pawn victim;
    public int tick;

    public void ExposeData()
    {
        Scribe_References.Look(ref lord, "lord");
        Scribe_References.Look(ref invoker, "invoker");
        Scribe_References.Look(ref victim, "victim");
        Scribe_Values.Look(ref tick, "tick");
    }
}

public sealed class KidnappingMapComponent(Map map) : MapComponent(map)
{
    private List<PendingAbduction> pending = new();
    private int nextRaidTick = -1;
    public int NextRaidTick => nextRaidTick;

    public void Remember(Lord lord, Pawn invoker, Pawn victim)
    {
        if (pending.Any(item => item.lord == lord)) return;
        pending.Add(new PendingAbduction { lord = lord, invoker = invoker, victim = victim,
            tick = Find.TickManager.TicksGame + 1 });
    }

    public void ScheduleRepeat()
    {
        if (!CultistKidnappingMod.Settings.repeatRaids) return;
        CultistKidnappingMod.Settings.Normalize();
        var scheduled = Find.TickManager.TicksGame + CultistKidnappingMod.Settings.intervalDays.RandomInRange * GenDate.TicksPerDay;
        // One pending follow-up per map; another success never postpones an earlier attack.
        if (nextRaidTick < 0 || scheduled < nextRaidTick) nextRaidTick = scheduled;
    }

    public override void MapComponentTick()
    {
        var now = Find.TickManager.TicksGame;
        for (var i = pending.Count - 1; i >= 0; i--)
        {
            var item = pending[i];
            if (now < item.tick) continue;
            pending.RemoveAt(i);
            if (item.lord == null || !map.lordManager.lords.Contains(item.lord) ||
                item.lord.LordJob is not LordJob_PsychicRitual || item.victim == null ||
                item.victim.Dead || item.victim.MapHeld != map ||
                !item.victim.health.hediffSet.HasHediff(HediffDefOf.DarkPsychicShock)) continue;

            // Switch at a tick boundary, after vanilla has finished applying the outcome.
            // SetJob clears CurLordToil, so its effects must be cleaned up beforehand.
            item.lord.CurLordToil?.Cleanup();
            item.lord.SetJob(new LordJob_CultistKidnap(item.victim, item.invoker));
            item.lord.GotoToil(item.lord.Graph.StartingToil);
            Messages.Message("CK_Pursuit".Translate(item.victim.Named("PAWN")), item.victim,
                MessageTypeDefOf.ThreatSmall);
        }

        if (!CultistKidnappingMod.Settings.repeatRaids) { nextRaidTick = -1; return; }
        if (nextRaidTick < 0 || now < nextRaidTick) return;
        // Vanilla retains its difficulty, scenario and incident eligibility checks.
        if (map.IsPlayerHome && ModsConfig.AnomalyActive && Faction.OfHoraxCult != null &&
            map.mapPawns.FreeColonistsSpawned.Concat(map.mapPawns.SlavesOfColonySpawned)
                .Any(pawn => !pawn.health.hediffSet.HasHediff(HediffDefOf.DarkPsychicShock)))
        {
            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.faction = Faction.OfHoraxCult;
            parms.pawnGroupKind = PawnGroupKindDefOf.PsychicRitualSiege;
            parms.points = Math.Max(IncidentDefOf.PsychicRitualSiege.minThreatPoints, parms.points);
            parms.psychicRitualDef = DefDatabase<PsychicRitualDef>.GetNamed("SkipAbduction");
            if (Find.Storyteller.TryFire(new FiringIncident(IncidentDefOf.PsychicRitualSiege, null, parms), queued: true))
            {
                nextRaidTick = -1;
                return;
            }
        }
        nextRaidTick = now + GenDate.TicksPerHour;
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref pending, "pendingAbductions", LookMode.Deep);
        Scribe_Values.Look(ref nextRaidTick, "nextKidnappingRaidTick", -1);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) pending ??= new();
    }
}
