using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RW17.Validation;
public static class Mod72Checks
{
    public const string PackageId = "RunningBugs.RimLocksmith";
    public static void Run(Map map)
    {
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "RimLocksmith");
        Type utility = assembly.GetType("RunningBugs.RimLocksmith.RimLocksmithUtility", true);
        object settings = assembly.GetType("RunningBugs.RimLocksmith.RimLocksmithMod").GetField("Settings").GetValue(null);
        FieldInfo manage = settings.GetType().GetField("ManageColonyPawns"); object oldManage = manage.GetValue(settings);
        var targets = new[] { AccessTools.Method(typeof(Building_Door), "PawnCanOpen"),
            AccessTools.PropertyGetter(typeof(MainTabWindow_Inspect), "ShouldShowPaneContents"),
            AccessTools.PropertyGetter(typeof(MainTabWindow_Inspect), "CurTabs") };
        foreach (MethodInfo target in targets)
            Require(Harmony.GetPatchInfo(target)?.Postfixes.Any(p => p.owner == "runningbugs.rimlocksmith" && p.PatchMethod.DeclaringType.Assembly == assembly) == true,
                "Actual postfix registered on " + target.Name);
        IntVec3 cell = CellRect.CenteredOn(map.Center, 25).FirstOrDefault(c => c.InBounds(map) && !c.Fogged(map) && c.Standable(map) &&
            c.GetThingList(map).Count == 0 && GenAdj.CellsAdjacent8Way(c, Rot4.North, IntVec2.One).All(n => n.InBounds(map) && n.Standable(map)), IntVec3.Invalid);
        Require(cell.IsValid, "A disposable native door fixture exists.");
        Faction visitorFaction = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f != Faction.OfPlayer && !f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction);
        Require(visitorFaction != null, "A real non-hostile human faction exists.");
        var door = (Building_Door)ThingMaker.MakeThing(ThingDefOf.Door, ThingDefOf.WoodLog);
        var pawns = new List<Pawn>(); Lord tradeLord = null;
        Pawn Make(PawnKindDef kind, Faction faction)
        {
            Pawn p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, forceGenerateNewPawn: true,
                forceNoBackstory: true, forceNoGear: true)); pawns.Add(p);
            IntVec3 pawnCell = CellRect.CenteredOn(cell, 12).First(c => c.InBounds(map) && !c.Fogged(map) && c.Standable(map) && c.GetThingList(map).Count == 0);
            GenSpawn.Spawn(p, pawnCell, map);
            Require(p.Spawned && p.roping != null, "Fixture Pawn has completed native SpawnSetup and has its real rope tracker.");
            return p;
        }
        try
        {
            manage.SetValue(settings, true); door.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(door, cell, map);
            object comp = door.AllComps.Single(c => c.GetType().FullName == "RunningBugs.RimLocksmith.CompRimLocksmithDoor");
            object config = comp.GetType().GetProperty("Config").GetValue(comp);
            void Set(string field, object value) { config.GetType().GetField(field).SetValue(config, value); comp.GetType().GetMethod("NotifyChanged").Invoke(comp, null); }
            Require(door.GetInspectTabs().Any(t => t.GetType().FullName == "RunningBugs.RimLocksmith.ITab_RimLocksmith"), "XML attaches the actual lock inspect tab.");
            Pawn colonist = Make(PawnKindDefOf.Colonist, Faction.OfPlayer);
            Set("AllowColonists", true); Require(door.PawnCanOpen(colonist), "Allowed real colonist opens the native door.");
            Set("AllowColonists", false); Require(!door.PawnCanOpen(colonist), "Configured denial narrows native colonist access.");
            manage.SetValue(settings, false); Require(door.PawnCanOpen(colonist), "ManageColonyPawns off actually bypasses colony restrictions.");
            manage.SetValue(settings, true);
            Pawn visitor = Make(PawnKindDefOf.Colonist, visitorFaction);
            Set("AllowGuests", true); Require(door.PawnCanOpen(visitor), "A real friendly faction visitor retains native access.");
            Set("AllowGuests", false); Require(!door.PawnCanOpen(visitor), "Guest setting narrows actual visitor access.");
            Pawn prisoner = Make(PawnKindDefOf.Colonist, visitorFaction);
            prisoner.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
            Require(!door.PawnCanOpen(prisoner), "A real unreleased prisoner remains denied by native code.");
            prisoner.guest.Released = true; Require(door.PawnCanOpen(prisoner), "The native released-prisoner exception survives guest denial.");
            Pawn cow = Make(DefDatabase<PawnKindDef>.GetNamed("Cow"), Faction.OfPlayer);
            Require(cow.FenceBlocked && !door.PawnCanOpen(cow), "Allowed colony animal config does not bypass the actual pen-animal restriction.");
            Pawn dog = Make(DefDatabase<PawnKindDef>.GetNamed("Husky"), Faction.OfPlayer);
            Set("AnimalAccess", Enum.Parse(config.GetType().GetField("AnimalAccess").FieldType, "All"));
            Require(!dog.FenceBlocked && door.PawnCanOpen(dog), "A real non-pen colony animal retains native access.");
            Set("AnimalAccess", Enum.Parse(config.GetType().GetField("AnimalAccess").FieldType, "None"));
            Require(!door.PawnCanOpen(dog), "AnimalAccess.None restricts an actually native-allowed animal.");
            Pawn pack = Make(DefDatabase<PawnKindDef>.GetNamed("Cow"), visitorFaction);
            tradeLord = LordMaker.MakeNewLord(visitorFaction, new LordJob_TradeWithColony(visitorFaction, cell), map, new[] { pack });
            Require(pack.CanOpenAnyDoor && pack.FenceBlocked, "The real trade lord gives its pack animal the native open-any-door privilege.");
            Set("AllowGuests", true); Require(door.PawnCanOpen(pack), "A permitted actual trade pack animal opens the door.");
            Set("AllowGuests", false); Require(!door.PawnCanOpen(pack), "The actual trade-pack privilege is intentionally controlled by the guest setting.");
            Pawn wild = Make(DefDatabase<PawnKindDef>.GetNamed("Husky"), null);
            Require(!door.PawnCanOpen(wild), "Native wild-animal denial is never widened.");
            object cloned = config.GetType().GetMethod("Clone").Invoke(config, null);
            comp.GetType().GetMethod("SetConfig").Invoke(comp, new[] { cloned, (object)true });
            object assigned = comp.GetType().GetProperty("Config").GetValue(comp);
            Require(!ReferenceEquals(assigned, cloned) && (bool)assigned.GetType().GetField("UserConfigured").GetValue(assigned),
                "Actual comp assignment clones policy and marks the explicit edit.");
            Log.Message("[RW17.Validation] Mod72 checks passed: actual_postfixes, native_door_comp_itab, colonist_allow_deny, " +
                "manage_gate, guest_allow_deny, prisoner_native_denial, released_exception, pen_guard, animal_mode, real_trade_pack, wild_native_denial, config_clone");
        }
        finally
        {
            manage.SetValue(settings, oldManage);
            if (tradeLord != null) map.lordManager.RemoveLord(tradeLord);
            foreach (Pawn pawn in pawns) if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
            if (!door.Destroyed) door.Destroy(DestroyMode.Vanish);
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
