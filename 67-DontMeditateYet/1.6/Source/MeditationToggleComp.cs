using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DontMeditateYet
{
    public class MeditationToggleComp : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // Only show the gizmo for pawns with psylink that are selected and belong to the player
            if (parent is Pawn pawn && 
                pawn.HasPsylink && 
                pawn.Faction == Faction.OfPlayer && 
                Find.Selector.SingleSelectedThing == pawn &&
                pawn.psychicEntropy != null &&
                pawn.psychicEntropy.NeedToShowGizmo())
            {
                yield return new Command_ToggleMeditation(pawn);
            }
        }
    }
}