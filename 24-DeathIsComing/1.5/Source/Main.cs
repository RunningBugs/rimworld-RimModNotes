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

namespace DIC
{
    [DefOf]
    public static class DIC_IncidentDefOf
    {
        public static IncidentDef DIC_DeathComing;
    }

    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Log.Message("DeathIsComing loaded successfully!");
        }
    }


    public class DIC_GameComponent : GameComponent
    {
        public DIC_GameComponent(Game game)
        {
        }

        public bool DeathMercy = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref DeathMercy, "DIC_GameComponent.DeathMercy", true);
        }
    }


    [HarmonyPatch(typeof(DIC_IncidentWorker_DeathComing), "CanFireNow")]
    public class ForceFirePatch {
        public static bool Prefix(ref bool __result) {
            __result = true;
            return false;
        }
    }


    class DIC_IncidentWorker_DeathComing : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return true;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            // Messages.Message("DeathComing".Translate(), MessageTypeDefOf.NeutralEvent);
            var allPawnsToChoose = new List<Pawn>();
            Find.Maps.ForEach(map =>
            {
                if (map.IsPlayerHome)
                {
                    var pawns = map.mapPawns.AllPawns.Where(p => p.Faction == Faction.OfPlayer);
                    if (pawns != null)
                    {
                        allPawnsToChoose.AddRange(pawns.Where(p => !p.health.hediffSet.HasPregnancyHediff()));
                    }
                }
            });
            allPawnsToChoose = allPawnsToChoose.Where(p => (p.IsColonist || p.IsSlaveOfColony) && !p.IsNonMutantAnimal && p.RaceProps.Humanlike).ToList();

            if (allPawnsToChoose.Count > 0)
            {
                StringBuilder log = new StringBuilder();
                allPawnsToChoose.ForEach(p => log.AppendLine(p.Name.ToString()));
                Log.Message(log.ToString());
                var pawn = allPawnsToChoose.RandomElement();
                LookTargets lookTargets = pawn.Corpse;
                // DamageInfo dinfo = new(null, 0, 0, 0, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null, false, false, QualityCategory.Normal);
                pawn.Kill(null, null);
                Messages.Message("DeathKill".Translate(pawn.Name), lookTargets, MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Messages.Message("DeathNoKill".Translate(), MessageTypeDefOf.NeutralEvent);
            }
            return true;
        }
    }

}
