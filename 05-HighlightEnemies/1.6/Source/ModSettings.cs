using Verse;
using RimWorld;
using UnityEngine;

namespace _HightlightEnemies;

public class HE_ModSettings : ModSettings
{
    public static bool markEnemiesByDefault = true;
    public static bool markEnemiesInvisible = true;
    public static bool markEnemiesInFog = true;
    public override void ExposeData()
    {
        Scribe_Values.Look(ref markEnemiesByDefault, "HE.markEnemiesByDefault", true);
        Scribe_Values.Look(ref markEnemiesInvisible, "HE.markEnemiesInvisible", true);
        Scribe_Values.Look(ref markEnemiesInFog, "HE.markEnemiesInFog", true);
    }
}

class HE_Mod : Mod 
{
    HE_ModSettings settings;

    public HE_Mod(ModContentPack content) : base(content)
    {
        settings = GetSettings<HE_ModSettings>();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listingStandard = new Listing_Standard();
        listingStandard.Begin(inRect);
        listingStandard.CheckboxLabeled("MarkEnemiesByDefault".Translate(), ref HE_ModSettings.markEnemiesByDefault);
        listingStandard.CheckboxLabeled("MarkEnemiesInvisible".Translate(), ref HE_ModSettings.markEnemiesInvisible);
        listingStandard.CheckboxLabeled("MarkEnemiesInFog".Translate(), ref HE_ModSettings.markEnemiesInFog);
        listingStandard.End();
        settings.Write();
    }

    public override string SettingsCategory()
    {
        return "HEName".Translate();
    }
}