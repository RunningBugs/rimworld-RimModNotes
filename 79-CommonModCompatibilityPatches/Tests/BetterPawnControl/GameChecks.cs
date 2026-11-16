using System.Collections;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace CommonModCompatibilityPatches.Tests;

public sealed class BetterPawnControlChecks(Map map) : MapComponent(map)
{
    private const string Owner = "com.RunningBugs.CommonModCompatibilityPatches";
    private const string Group = "CommonModCompatibilityPatches.BetterPawnControlStaleAssignLinksCompatibility";
    private readonly List<string> passed = new();
    private bool finished;

    private void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        passed.Add(message);
        Log.Message("BPC_ASSERT_OK " + message);
    }

    private void Run()
    {
        var group = AccessTools.TypeByName(Group);
        Require(group != null, "the new production patch group is present");
        var guestTarget = AccessTools.Method(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus), new[] { typeof(Faction), typeof(GuestStatus) });
        bool Registered(MethodBase target) => Harmony.GetPatchInfo(target)?.Prefixes
            .Concat(Harmony.GetPatchInfo(target).Postfixes).Any(p => p.owner == Owner && p.PatchMethod.DeclaringType == group) == true;
        if (Environment.GetEnvironmentVariable("BPC_ABSENT") == "1")
        {
            Require(AccessTools.TypeByName("BetterPawnControl.AssignLink") == null, "optional Better Pawn Control assembly really is absent");
            Require(!Registered(guestTarget), "no guest-status patch is registered without Better Pawn Control");
            Require(!(bool)AccessTools.Method(group, "TryApply").Invoke(null, new object[] { new Harmony("local.bpc.absent") }),
                "absent optional target returns false without applying patches");
            return;
        }

        var linkType = AccessTools.TypeByName("BetterPawnControl.AssignLink");
        var manager = AccessTools.TypeByName("BetterPawnControl.Manager`1").MakeGenericType(linkType);
        var linksField = AccessTools.Field(manager, "links");
        var colonistField = AccessTools.Field(linkType, "colonist");
        var storageType = AccessTools.TypeByName("BetterPawnControl.DataStorage");
        var storageTarget = AccessTools.Method(storageType, "ExposeData");
        var originalCallback = AccessTools.Method(AccessTools.TypeByName("BetterPawnControl.Pawn_GuestTracker_SetGuestStatus"), "Postfix");
        Require(Registered(guestTarget) && Registered(storageTarget), "both actual production targets have this group's Harmony patches");
        Require(Harmony.GetPatchInfo(guestTarget).Postfixes.Any(p => p.PatchMethod == originalCallback),
            "Better Pawn Control's original callback remains registered");

        object savedLinks = linksField.GetValue(null);
        LoadSaveMode savedMode = Scribe.mode;
        object Link(Pawn p, int zone)
        {
            object link = Activator.CreateInstance(linkType);
            colonistField.SetValue(link, p);
            AccessTools.Field(linkType, "zone").SetValue(link, zone);
            return link;
        }
        IList Links(params object[] records)
        {
            var list = (IList)Activator.CreateInstance(linksField.FieldType);
            foreach (object record in records) list.Add(record);
            linksField.SetValue(null, list);
            return list;
        }
        var prefix = AccessTools.Method(group, "GuestStatusPrefix");
        var loadPostfix = AccessTools.Method(group, "ExposeDataPostfix");
        try
        {
            Pawn pawn = map.mapPawns.FreeColonistsSpawned.First();
            object valid = Link(pawn, 73), validOtherPolicy = Link(pawn, 74), orphan = Link(null, 0);
            var list = Links(orphan, valid, null, validOtherPolicy);
            bool reproduced = false;
            try { originalCallback.Invoke(null, new object[] { pawn }); }
            catch (TargetInvocationException e) when (e.InnerException is NullReferenceException) { reproduced = true; }
            Require(reproduced, "the installed unmodified BPC callback reproduces the reported null-reference exception");

            foreach (LoadSaveMode mode in new[] { LoadSaveMode.LoadingVars, LoadSaveMode.ResolvingCrossRefs, LoadSaveMode.Saving })
            {
                Scribe.mode = mode;
                prefix.Invoke(null, null);
                loadPostfix.Invoke(null, null);
                Require(list.Count == 4 && ReferenceEquals(list[0], orphan), "unresolved links are not pruned in " + mode);
            }

            Scribe.mode = LoadSaveMode.PostLoadInit;
            storageTarget.Invoke(Activator.CreateInstance(storageType, new object[] { Find.World }), null);
            Require(list.Count == 2 && ReferenceEquals(list[0], valid) && ReferenceEquals(list[1], validOtherPolicy),
                "real DataStorage.ExposeData PostLoadInit removes only orphan/null records, preserving policy objects and order");
            Require((int)AccessTools.Field(linkType, "zone").GetValue(valid) == 73,
                "valid cached policy contents are unchanged");

            Scribe.mode = LoadSaveMode.Inactive;
            list.Insert(0, orphan);
            var outfit = pawn.outfits.CurrentApparelPolicy;
            pawn.guest.SetGuestStatus(null);
            Require(list.Count == 2 && ReferenceEquals(pawn.outfits.CurrentApparelPolicy, outfit),
                "real SetGuestStatus prunes later stale entries before BPC while preserving an existing colonist's policies");
            pawn.guest.SetGuestStatus(null);
            Require(list.Count == 2, "repeated guest-status changes are idempotent");

            // No matching cached assignment: BPC must still assign its defaults,
            // rather than simply suppressing the throwing postfix.
            Links(orphan);
            var defaultOutfit = Current.Game.outfitDatabase.DefaultOutfit();
            var customOutfit = Current.Game.outfitDatabase.MakeNewOutfit();
            pawn.outfits.CurrentApparelPolicy = customOutfit;
            pawn.guest.SetGuestStatus(null);
            Require(ReferenceEquals(pawn.outfits.CurrentApparelPolicy, defaultOutfit),
                "the original BPC default-assignment branch still executes for an uncached colonist");

            // Actual quest -> leave utility -> SetFaction -> SetGuestStatus ->
            // BPC callback -> exit lord, not just a direct helper invocation.
            Links(orphan, validOtherPolicy);
            var quest = new Quest { id = 9077 };
            quest.SetInitiallyAccepted();
            Require(quest.State == QuestState.Ongoing, "the disposable departure quest is actually accepted");
            quest.AddPart(new QuestPart_Leave
            {
                inSignal = "Quest9077.BPCChecks.Leave",
                pawns = new List<Pawn> { pawn },
                sendStandardLetter = false,
                wakeUp = true
            });
            quest.Notify_SignalReceived(new Signal("Quest9077.BPCChecks.Leave"));
            Require(pawn.GetLord()?.LordJob is LordJob_ExitMapBest,
                "a real quest leave signal completes and assigns the pawn an exit-map lord");
            Require(((IList)linksField.GetValue(null)).Count == 1,
                "quest departure preserves other valid cached policies");

            linksField.SetValue(null, null);
            prefix.Invoke(null, null);
            Scribe.mode = LoadSaveMode.PostLoadInit;
            loadPostfix.Invoke(null, null);
            Require(linksField.GetValue(null) == null, "cleanup tolerates an uninitialized list without inventing policy data");
        }
        finally
        {
            Scribe.mode = savedMode;
            linksField.SetValue(null, savedLinks);
        }
    }

    public override void MapComponentUpdate()
    {
        if (finished) { Root.Shutdown(); return; }
        if (Current.ProgramState != ProgramState.Playing || LongEventHandler.AnyEventNowOrWaiting) return;
        string error = null;
        try { Run(); }
        catch (Exception exception) { error = exception.ToString(); Log.Error("BPC_TEST_FAILED " + error); }
        finished = true;
        File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "bpc-result.json"),
            JsonSerializer.Serialize(new { passed = error == null, checks = passed, error }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
