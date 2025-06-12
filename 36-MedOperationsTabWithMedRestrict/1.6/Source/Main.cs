using Verse;

// using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace OperationsTabWithMedRestrict
{

    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Harmony harmony = new Harmony("com.RunningBugs.operations-tab-with-med-restrict");
            harmony.PatchAll();
            Log.Message("OperationsTabWithMedRestrict loaded!");
        }
    }

    [HarmonyPatch(typeof(HealthCardUtility))]
    [HarmonyPatch("DrawMedOperationsTab")]
    public static class HealthCardUtility_DrawMedOperationsTab_Patch
    {
        // Prefix method
        static void Prefix(Rect leftRect, Pawn pawn, ref float curY)
        {
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            bool flag2 = pawn.Faction == Faction.OfPlayer || pawn.HostFaction == Faction.OfPlayer;
            bool flag3 = pawn.NonHumanlikeOrWildMan() && pawn.InBed() && pawn.CurrentBed().Faction == Faction.OfPlayer;
            if (pawn.RaceProps.IsFlesh && (flag2 || flag3) && (!pawn.IsMutant || pawn.mutant.Def.entitledToMedicalCare) && pawn.playerSettings != null && !pawn.Dead && Current.ProgramState == ProgramState.Playing)
            {
                Rect rect5 = new Rect(0f, curY, leftRect.width, 23f);
                TooltipHandler.TipRegionByKey(rect5, "MedicineQualityDescription");
                Widgets.DrawHighlightIfMouseover(rect5);
                Rect rect6 = rect5;
                rect6.xMax = rect5.center.x - 4f;
                Rect rect7 = rect5;
                rect7.xMin = rect5.center.x + 4f;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(rect6, string.Format("{0}:", "AllowMedicine".Translate()));
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.DrawButtonGraphic(rect7);
                MedicalCareUtility.MedicalCareSelectButton(rect7, pawn);
                curY += rect5.height + 4f;
            }
        }
    }

}
