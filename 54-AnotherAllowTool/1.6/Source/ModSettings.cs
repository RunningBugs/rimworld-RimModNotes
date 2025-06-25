using HarmonyLib;
using UnityEngine;
using Verse;


namespace AAT;


[StaticConstructorOnStartup]
public static class Start
{
    static Start()
    {
        Harmony harmony = new Harmony("com.RunningBugs.AnotherAllowTool");
        harmony.PatchAll();
        Log.Message("Another Allow Tool patched successfully.".Colorize(color: Color.green));
    }
}


public class Settings : ModSettings
{
    public override void ExposeData()
    {
    }
}

public class ModSettingsUI : Mod
{
    public static Settings Settings => LoadedModManager.GetMod<ModSettingsUI>().GetSettings<Settings>();

    public ModSettingsUI(ModContentPack content) : base(content)
    {
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
    }
}
