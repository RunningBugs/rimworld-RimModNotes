using UnityEngine;
using Verse;

namespace DontMeditateYet
{
    public class DontMeditateYetMod : Mod
    {
        public static DontMeditateYetMod Instance { get; private set; }
        
        private DontMeditateYetSettings settings;

        public DontMeditateYetMod(ModContentPack content) : base(content)
        {
            Instance = this;
            settings = GetSettings<DontMeditateYetSettings>();
        }

        public override string SettingsCategory()
        {
            return "Don't Meditate Yet";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            
            listingStandard.CheckboxLabeled(
                "Default toggle state for new maps (checked = prevent meditation by default)", 
                ref settings.defaultToggleState, 
                "When entering a new map, pawns will have this meditation toggle state by default."
            );
            
            listingStandard.Gap();
            listingStandard.Label("Note: This setting only affects new maps. Existing maps will retain their current toggle states.");
            
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            settings.Write();
        }
    }
}