using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RW17.Validation;

public static class Mod36Checks
{
    public const string PackageId = "RunningBugs.OperationsTabWithMedRestrict";
    private static Pawn observedPawn;
    private static Rect observedRect;
    private static int selectorCalls;
    private static bool ObserveSelector(Rect rect, Pawn pawn) { observedRect = rect; observedPawn = pawn; selectorCalls++; return false; }
    private static bool StopNativeOperationsBody(float curY, ref float __result) { __result = curY; return false; }

    public static void Run(Map map)
    {
        var target = typeof(HealthCardUtility).GetMethod("DrawMedOperationsTab", BindingFlags.Static | BindingFlags.NonPublic);
        var patch = Harmony.GetPatchInfo(target)?.Prefixes.SingleOrDefault(p => p.PatchMethod.DeclaringType?.FullName == "OperationsTabWithMedRestrict.HealthCardUtility_DrawMedOperationsTab_Patch");
        Require(patch != null, "Actual medical-operations prefix missing");
        var observer = new Harmony("RW17.Validation.Mod36");
        var selector = typeof(MedicalCareUtility).GetMethod("MedicalCareSelectButton");
        var selectorSpy = typeof(Mod36Checks).GetMethod(nameof(ObserveSelector), BindingFlags.Static | BindingFlags.NonPublic);
        var stopBody = typeof(Mod36Checks).GetMethod(nameof(StopNativeOperationsBody), BindingFlags.Static | BindingFlags.NonPublic);
        var oldFont = Text.Font; var oldAnchor = Text.Anchor; var oldColor = GUI.color; var oldEvent = Event.current; var oldState = Current.ProgramState;
        Pawn pawn = null; Pawn mech = null;
        try
        {
            observer.Patch(selector, prefix: new HarmonyMethod(selectorSpy));
            observer.Patch(target, prefix: new HarmonyMethod(stopBody) { priority = Priority.Last });
            Event.current = new Event { type = EventType.Layout };
            Current.ProgramState = ProgramState.Playing;
            pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            var originalCare = pawn.playerSettings.medCare;
            float Draw(Pawn input)
            {
                selectorCalls = 0; observedPawn = null;
                return (float)target.Invoke(null, new object[] { new Rect(20f, 0f, 200f, 400f), input, input, 10f });
            }
            Require(Draw(pawn) == 37f && selectorCalls == 1 && ReferenceEquals(observedPawn, pawn) && observedRect == new Rect(104f, 10f, 96f, 23f), "Actual patched entry did not insert one correct native medicine selector and 27px row");
            Require(Text.Font == GameFont.Small && Text.Anchor == TextAnchor.UpperLeft && GUI.color == Color.white, "Prefix UI style contract changed");
            var menu = (IEnumerable<Widgets.DropdownMenuElement<MedicalCareCategory>>)typeof(MedicalCareUtility).GetMethod("MedicalCareSelectButton_GenerateMenu", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { observedPawn });
            var options = menu.ToArray();
            Require(options.Length == 6, "Actual medicine menu must provide five policies plus defaults");
            for (int i = 0; i < 5; i++)
            {
                var option = options.Single(o => o.option.Label == ((MedicalCareCategory)i).GetLabel().CapitalizeFirst());
                option.option.action();
                Require(pawn.playerSettings.medCare == (MedicalCareCategory)i, "Actual native medicine option did not mutate the selected pawn policy");
                bool[] expected = i switch { 0 or 1 => new[] { false, false, false }, 2 => new[] { true, false, false }, 3 => new[] { true, true, false }, _ => new[] { true, true, true } };
                var medicines = new[] { ThingDefOf.MedicineHerbal, ThingDefOf.MedicineIndustrial, ThingDefOf.MedicineUltratech };
                Require(medicines.Select(m => pawn.playerSettings.medCare.AllowsMedicine(m)).SequenceEqual(expected), "Actual changed policy no longer controls native medicine permission");
            }
            pawn.playerSettings.medCare = originalCare;
            var binding = new Harmony(patch.owner);
            binding.Unpatch(target, patch.PatchMethod);
            try { Require(Draw(pawn) == 10f && selectorCalls == 0, "Specific unpatched entry control unexpectedly inserted medicine selector"); }
            finally { binding.Patch(target, prefix: new HarmonyMethod(patch.PatchMethod)); }
            Current.ProgramState = ProgramState.Entry;
            Require(Draw(pawn) == 10f && selectorCalls == 0, "Non-playing program must not insert selector");
            Current.ProgramState = ProgramState.Playing;
            var playerSettings = pawn.playerSettings; pawn.playerSettings = null;
            try { Require(Draw(pawn) == 10f && selectorCalls == 0, "Missing player settings must be excluded"); }
            finally { pawn.playerSettings = playerSettings; }
            var healthState = typeof(Pawn_HealthTracker).GetField("healthState", BindingFlags.Instance | BindingFlags.NonPublic);
            var oldHealth = healthState.GetValue(pawn.health); healthState.SetValue(pawn.health, PawnHealthState.Dead);
            try { Require(Draw(pawn) == 10f && selectorCalls == 0, "Actual dead state must be excluded"); }
            finally { healthState.SetValue(pawn.health, oldHealth); }
            pawn.SetFaction(Faction.OfAncients);
            Require(Draw(pawn) == 10f && selectorCalls == 0, "Unhosted non-player human must be excluded");
            pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Guest);
            Require(Draw(pawn) == 37f && selectorCalls == 1, "Actual player-hosted guest must have medicine selector");
            pawn.guest.SetGuestStatus(null); pawn.SetFaction(Faction.OfPlayer);
            if (ModsConfig.BiotechActive)
            {
                mech = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Mech_Lifter"), Faction.OfPlayer);
                Require(Draw(mech) == 10f && selectorCalls == 0, "Non-flesh player mech must be excluded");
            }
        }
        finally
        {
            observer.Unpatch(selector, selectorSpy); observer.Unpatch(target, stopBody);
            Current.ProgramState = oldState; Text.Font = oldFont; Text.Anchor = oldAnchor; GUI.color = oldColor; Event.current = oldEvent;
            if (pawn != null && !pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
            if (mech != null && !mech.Destroyed) mech.Destroy(DestroyMode.Vanish);
            observedPawn = null; selectorCalls = 0;
        }
        Log.Message("RW17 Mod36: actual patched operations entry/row/native medicine selector wiring, real native policy actions/permissions, specific unpatch and player/host/dead/settings/state/mech controls passed; pixels/physical menu/surgery/bedded-animal/mutant pending");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Mod36: " + message);
    }
}
