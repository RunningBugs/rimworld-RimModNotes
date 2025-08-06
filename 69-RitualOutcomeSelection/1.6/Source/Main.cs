using System.Linq;

using UnityEngine;
using Verse;
using RimWorld;

using HarmonyLib;

namespace RitualOutcomeSelection;

[StaticConstructorOnStartup]
public static class Start
{
    static Start()
    {
        Harmony harmony = new("com.RunningBugs.RitualOutcomeSelection");
        harmony.PatchAll();
        Log.Message("[RitualOutcomeSelection] patched successfully!".Colorize(Color.green));
    }
}

[HarmonyPatch(typeof(RitualOutcomeEffectWorker_FromQuality), nameof(RitualOutcomeEffectWorker_FromQuality.GetOutcome))]
public static class RitualOutcomeEffectWorker_FromQuality_GetOutcome_Patch
{
    public static RitualOutcomePossibility result = null;

    public static void Postfix(float quality, LordJob_Ritual ritual, RitualOutcomeEffectWorker_FromQuality __instance, ref RitualOutcomePossibility __result)
    {
        // Custom logic to modify the ritual outcome
        // Log.Message($"[RitualOutcomeSelection] GetOutcome called with quality: {quality}, result: {__result?.Label ?? "null"}".Colorize(Color.cyan));
        if (result != null)
        {
            __result = result;
            result = null; // Reset the result for next use
        }
    }
}


[HarmonyPatch(typeof(LordJob_Ritual), nameof(LordJob_Ritual.ApplyOutcome))]
public static class LordJob_Ritual_ApplyOutcome_Patch
{
    public static void Prefix(LordJob_Ritual __instance, float progress, bool cancelled)
    {
        if (!__instance.RitualFinished(progress, cancelled))
        {
            RitualOutcomeEffectWorker_FromQuality_GetOutcome_Patch.result = null; // Reset the result if the ritual is not finished
        }
    }
}

[HarmonyPatch(typeof(Window), nameof(Window.Close))]
public static class Dialog_BeginRitual_Close_Patch
{
    public static void Postfix(Window __instance)
    {
        if (__instance is Dialog_BeginRitual instance && 
            instance.ritual?.outcomeEffect?.def?.outcomeChances != null && 
            !instance.ritual.outcomeEffect.def.outcomeChances.NullOrEmpty())
        {
            var dialog = new Dialog_RitualOutcomeSelection(instance.ritual.outcomeEffect);
            Find.WindowStack.Add(dialog);
        }
    }
}

public class Dialog_RitualOutcomeSelection : Window
{
    public override Vector2 InitialSize => new(400f, 300f);

    private RitualOutcomeEffectWorker instance;
    private int positivityIndex = 0;

    public Dialog_RitualOutcomeSelection(RitualOutcomeEffectWorker instance)
    {
        this.instance = instance;
        draggable = true;
        forcePause = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        // Draw the dialog contents
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "RitualOutcomeSelection.AvailableRitualOutcomes".Translate());
        Text.Font = GameFont.Small;
        float y = inRect.y + 40f;
        foreach (var outcome in instance.def.outcomeChances)
        {
            if (Widgets.RadioButtonLabeled(new Rect(inRect.x, y, inRect.width, 30f), outcome.Label, positivityIndex == outcome.positivityIndex))
            {
                positivityIndex = outcome.positivityIndex;
            }
            y += 30f;
        }
        if (Widgets.ButtonText(new Rect(inRect.x, y, inRect.width, 30f), "RitualOutcomeSelection.Select".Translate()))
        {
            // Select the selected outcome
            var selectedOutcome = instance.def.outcomeChances.Where(o => o.positivityIndex == positivityIndex);
            if (selectedOutcome.Any())
            {
                RitualOutcomePossibility selected = selectedOutcome.First();
                RitualOutcomeEffectWorker_FromQuality_GetOutcome_Patch.result = selected;
                Close();
            }
            else
            {
                Messages.Message("RitualOutcomeSelection.NoOutcomeSelected".Translate(), MessageTypeDefOf.SilentInput, false);
                RitualOutcomeEffectWorker_FromQuality_GetOutcome_Patch.result = null;
                Close();
            }
        }
    }
}
