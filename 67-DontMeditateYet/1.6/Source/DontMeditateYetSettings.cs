using Verse;

namespace DontMeditateYet
{
    public class DontMeditateYetSettings : ModSettings
    {
        public bool defaultToggleState = false; // Default: allow meditation (vanilla behavior)

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref defaultToggleState, "defaultToggleState", false);
        }
    }
}