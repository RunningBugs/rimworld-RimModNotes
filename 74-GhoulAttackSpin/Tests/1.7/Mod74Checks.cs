using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RW17.Validation;
public static class Mod74Checks
{
    public const string PackageId = "RunningBugs.GhoulAttackSpin";
    public static void Run(Map map)
    {
        Require(ModsConfig.AnomalyActive, "Actual Ghoul/Frenzy defs require Anomaly.");
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "GhoulAttackSpin");
        Type spin = assembly.GetType("GhoulAttackSpin.GhoulAttackSpinState", true);
        object settings = assembly.GetType("GhoulAttackSpin.GhoulAttackSpinMod").GetProperty("Settings").GetValue(null);
        FieldInfo direction = settings.GetType().GetField("direction"), auto = settings.GetType().GetField("enableAutoFrenzy");
        object oldDirection = direction.GetValue(settings), oldAuto = auto.GetValue(settings);
        MethodInfo getParms = AccessTools.Method(typeof(PawnRenderer), "GetDrawParms");
        foreach (var patchedMethod in new[] { AccessTools.Method(typeof(Pawn_DrawTracker), "Notify_MeleeAttackOn"), getParms, AccessTools.Method(typeof(Pawn), "GetGizmos") })
            Require(Harmony.GetPatchInfo(patchedMethod)?.Postfixes.Any(p => string.Equals(p.owner, PackageId, StringComparison.OrdinalIgnoreCase) && p.PatchMethod.DeclaringType.Assembly == assembly) == true,
                "Actual MOD postfix registered on " + patchedMethod.Name);
        var stateType = assembly.GetType("GhoulAttackSpin.AutoFrenzyState", true);
        GameComponent state = Current.Game.GetComponent(stateType);
        Require(state != null, "Actual GameComponent is registered in the current Game.");
        IntVec3 cell = CellRect.CenteredOn(map.Center, 25).FirstOrDefault(c => c.InBounds(map) && !c.Fogged(map) && c.Standable(map) &&
            c.GetThingList(map).Count == 0 && (c + IntVec3.East).InBounds(map) && (c + IntVec3.East).Standable(map), IntVec3.Invalid);
        Require(cell.IsValid, "Disposable ghoul fixture cells exist.");
        Pawn ghoul = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Ghoul, Faction.OfPlayer, forceGenerateNewPawn: true));
        Pawn human = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true, forceNoGear: true));
        Thing target = ThingMaker.MakeThing(ThingDefOf.Steel);
        MethodInfo angleMethod = AccessTools.Method(spin, "TryGetAngle");
        bool Angle(Pawn pawn, out float angle) { object[] args = { pawn, 0f }; bool exists = (bool)angleMethod.Invoke(null, args); angle = (float)args[1]; return exists; }
        PawnDrawParms Parms(Pawn pawn) => (PawnDrawParms)getParms.Invoke(pawn.Drawer.renderer,
            new object[] { Vector3.zero, 0f, Rot4.North, RotDrawMode.Fresh, PawnRenderFlags.None });
        try
        {
            auto.SetValue(settings, false);
            GenSpawn.Spawn(ghoul, cell, map); GenSpawn.Spawn(human, cell + IntVec3.West, map);
            GenSpawn.Spawn(target, cell + IntVec3.East, map); ghoul.drafter.Drafted = true;
            Require(ghoul.IsGhoul && ghoul.IsColonySubhumanPlayerControlled, "Fixture is a real player-controlled ghoul.");
            foreach (var turn in new[] { (name: "Clockwise", sign: 1f), (name: "Counterclockwise", sign: -1f) })
            {
                direction.SetValue(settings, Enum.Parse(direction.FieldType, turn.name));
                ghoul.Drawer.Notify_MeleeAttackOn(target);
                Require(Angle(ghoul, out float startAngle) && Math.Abs(startAngle) < 0.001f, "Actual melee notification starts spin at angle zero.");
                int startTick = Find.TickManager.TicksGame;
                for (int i = 0; i < 7; i++) Find.TickManager.DoSingleTick();
                Require(Find.TickManager.TicksGame - startTick == 7 && Angle(ghoul, out float quarter) && Math.Abs(quarter - 90f * turn.sign) < 0.001f,
                    "Real seven game ticks yield the configured quarter turn.");
                Vector3 forward = Parms(ghoul).matrix.MultiplyVector(Vector3.forward);
                Require(Math.Abs(forward.x - turn.sign) < 0.001f && Math.Abs(forward.z) < 0.001f,
                    "Actual GetDrawParms postfix rotates the native matrix about the up axis.");
                for (int i = 7; i < 28; i++) Find.TickManager.DoSingleTick();
                Require(Find.TickManager.TicksGame - startTick == 28 && !Angle(ghoul, out _), "Real 28-tick boundary removes the spin window.");
                Require((Parms(ghoul).matrix.MultiplyVector(Vector3.forward) - Vector3.forward).sqrMagnitude < 0.00001f,
                    "Actual render parameters return to the original rotation after expiry.");
            }
            human.Drawer.Notify_MeleeAttackOn(target); Require(!Angle(human, out _), "Human melee animation does not acquire ghoul spin state.");
            AbilityDef abilityDef = DefDatabase<AbilityDef>.GetNamed("GhoulFrenzy");
            HediffDef buffDef = DefDatabase<HediffDef>.GetNamed("GhoulFrenzy");
            BodyPartRecord heart = ghoul.RaceProps.body.AllParts.First(p => p.def == BodyPartDefOf.Heart);
            ghoul.health.AddHediff(DefDatabase<HediffDef>.GetNamed("AdrenalHeart"), heart);
            Ability ability = ghoul.abilities.GetAbility(abilityDef, includeTemporary: true);
            Require(ability != null && ghoul.abilities.GetAbility(abilityDef) == null && ability.CanCast && ability.CooldownTicksRemaining == 0,
                "A real adrenal-heart implant supplies a castable temporary ability.");
            auto.SetValue(settings, true);
            var gizmos = ghoul.GetGizmos().ToList();
            Command_Toggle toggle = gizmos.OfType<Command_Toggle>().Single(g => g.defaultLabel == "GAS_AutoFrenzyLabel".Translate());
            Require(!toggle.isActive(), "Fresh ghoul auto-frenzy defaults off."); toggle.toggleAction(); Require(toggle.isActive(), "The actual toggle updates current GameComponent state.");
            Command_Ability abilityCommand = gizmos.OfType<Command_Ability>().Single(g => g.Ability == ability);
            Require(abilityCommand.hotKey == DefDatabase<KeyBindingDef>.GetNamed("GhoulAttackSpin_GhoulFrenzy"), "The actual ability gizmo gets the configured keybinding def.");
            auto.SetValue(settings, false); state.GameComponentTick(); Require(!ghoul.health.hediffSet.HasHediff(buffDef), "Global disable prevents auto-cast.");
            auto.SetValue(settings, true); ghoul.drafter.Drafted = false; state.GameComponentTick();
            Require(!ghoul.health.hediffSet.HasHediff(buffDef), "Undrafted ghoul is not auto-cast.");
            ghoul.drafter.Drafted = true; state.GameComponentTick();
            Hediff buff = ghoul.health.hediffSet.GetFirstHediffOfDef(buffDef);
            Require(buff != null && ability.CooldownTicksRemaining > 0, "Actual component tick performs native non-interrupting self-cast and applies Frenzy/cooldown.");
            state.GameComponentTick(); Require(ReferenceEquals(buff, ghoul.health.hediffSet.GetFirstHediffOfDef(buffDef)), "Active Frenzy is not replaced by another auto-cast.");
            ghoul.health.RemoveHediff(buff); state.GameComponentTick();
            Require(!ghoul.health.hediffSet.HasHediff(buffDef), "Actual ability cooldown prevents re-cast after the buff is removed.");
            Log.Message("[RW17.Validation] Mod74 checks passed: actual_patches, real_melee_start, real_7_and_28_ticks, " +
                "two_directions, native_matrix_rotation_expiry, human_no_spin, implant_temporary_ability, actual_toggle_hotkey, global_and_draft_guards, real_self_cast, no_redose, cooldown");
        }
        finally
        {
            direction.SetValue(settings, oldDirection); auto.SetValue(settings, oldAuto);
            ((IDictionary)AccessTools.Field(stateType, "enabledByPawnId").GetValue(state)).Remove(ghoul.thingIDNumber);
            ((IDictionary)AccessTools.Field(spin, "ActiveSpins").GetValue(null)).Remove(ghoul.thingIDNumber);
            foreach (Thing thing in new Thing[] { target, ghoul, human }) if (!thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
