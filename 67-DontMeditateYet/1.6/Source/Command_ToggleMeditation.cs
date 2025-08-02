using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DontMeditateYet
{
    [StaticConstructorOnStartup]
    public class Command_ToggleMeditation : Command_Toggle
    {
        private static readonly Texture2D MeditationAllowedTex = ContentFinder<Texture2D>.Get("UI/Commands/MeditationAllowed", false) ?? BaseContent.BadTex;
        private static readonly Texture2D MeditationBlockedTex = ContentFinder<Texture2D>.Get("UI/Commands/MeditationBlocked", false) ?? BaseContent.BadTex;
        
        private Pawn pawn;
        private DontMeditateYetMapComponent mapComponent;

        public Command_ToggleMeditation(Pawn pawn)
        {
            this.pawn = pawn;
            this.mapComponent = pawn.Map?.GetComponent<DontMeditateYetMapComponent>();
            
            bool canMeditate = mapComponent?.GetMeditationToggleState(pawn) ?? false;
            
            this.isActive = () => !canMeditate; // Active when meditation is BLOCKED
            this.toggleAction = () => ToggleMeditationState();
            
            this.icon = canMeditate ? MeditationAllowedTex : MeditationBlockedTex;
            this.defaultLabel = canMeditate ? "Allow Meditation" : "Block Meditation";
            this.defaultDesc = canMeditate 
                ? "This pawn will meditate automatically when psyfocus is low." 
                : "This pawn will NOT meditate automatically. Click to allow meditation.";
            
            this.Order = -99f; // Show next to the psyfocus gizmo
        }

        private void ToggleMeditationState()
        {
            if (mapComponent == null) return;
            
            bool currentState = mapComponent.GetMeditationToggleState(pawn);
            bool newState = !currentState;
            
            mapComponent.SetMeditationToggleState(pawn, newState);
            
            // Update the gizmo appearance
            this.icon = newState ? MeditationAllowedTex : MeditationBlockedTex;
            this.defaultLabel = newState ? "Allow Meditation" : "Block Meditation";
            this.defaultDesc = newState 
                ? "This pawn will meditate automatically when psyfocus is low." 
                : "This pawn will NOT meditate automatically. Click to allow meditation.";
            
            // Play a sound
            if (newState)
            {
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }
            else
            {
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }
            
            // Show a message
            string message = newState 
                ? $"{pawn.LabelShort} will now meditate automatically."
                : $"{pawn.LabelShort} will no longer meditate automatically.";
            Messages.Message(message, pawn, MessageTypeDefOf.NeutralEvent, false);
        }
    }
}