using System.Collections.Generic;
using Verse;

namespace DontMeditateYet
{
    public class DontMeditateYetMapComponent : MapComponent
    {
        private Dictionary<int, bool> pawnMeditationToggleStates = new Dictionary<int, bool>();
        
        public DontMeditateYetMapComponent(Map map) : base(map)
        {
        }

        public bool GetMeditationToggleState(Pawn pawn)
        {
            if (pawnMeditationToggleStates.TryGetValue(pawn.thingIDNumber, out bool state))
            {
                return state;
            }
            
            // Default to the mod settings default value
            var settings = DontMeditateYetMod.Instance?.GetSettings<DontMeditateYetSettings>();
            return settings?.defaultToggleState ?? false;
        }

        public void SetMeditationToggleState(Pawn pawn, bool canMeditate)
        {
            pawnMeditationToggleStates[pawn.thingIDNumber] = canMeditate;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pawnMeditationToggleStates, "pawnMeditationToggleStates", LookMode.Value, LookMode.Value);
            
            if (pawnMeditationToggleStates == null)
            {
                pawnMeditationToggleStates = new Dictionary<int, bool>();
            }
        }
    }
}