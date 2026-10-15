using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod71Checks
{
    public const string PackageId = "RunningBugs.FacialAnimationStatueSnapshotFix";
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Mod71: " + message); }
    public static void Run(Map map)
    {
        var own = LoadedModManager.RunningMods.Single(m => m.PackageId.Equals(PackageId, StringComparison.OrdinalIgnoreCase)).assemblies.loadedAssemblies.Single(a => a.GetName().Name == "FacialAnimationStatueSnapshotFix");
        var upstream = LoadedModManager.RunningMods.Single(m => m.PackageId.Equals("Nals.FacialAnimation", StringComparison.OrdinalIgnoreCase)).assemblies.loadedAssemblies.Single(a => a.GetName().Name == "FacialAnimation");
        var start = own.GetType("FacialAnimationStatueSnapshotFix.Start", true);
        var target = upstream.GetType("FacialAnimation.HarmonyPatches", true).GetMethod("PrefixCreateSnapshotOfPawn_HookForMods", All);
        var transpiler = start.GetMethod("Transpiler", All); var helper = start.GetMethod("SafeAddIgnoreDuplicate", All);
        var patch = Harmony.GetPatchInfo(target)?.Transpilers.SingleOrDefault(p => p.owner == "com.RunningBugs.FacialAnimationStatueSnapshotFix" && p.PatchMethod == transpiler);
        Require(patch != null && target.GetParameters()[0].ParameterType == typeof(Pawn) && target.GetParameters()[1].ParameterType == typeof(Dictionary<string, object>).MakeByRefType(), "specific real FA transpiler and byref signature");
        var nativeHook = typeof(CompStatue).GetMethod("CreateSnapshotOfPawn_HookForMods", All);
        Require(Harmony.GetPatchInfo(nativeHook)?.Prefixes.Any(p => p.PatchMethod == target) == true, "real FA prefix is on actual native statue hook");
        VerifyHelpers(helper, transpiler);
        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        Thing statue = null; ThingComp duplicate = null;
        try
        {
            var headType = upstream.GetType("FacialAnimation.HeadControllerComp", true);
            var head = pawn.AllComps.FirstOrDefault(c => c.GetType() == headType);
            Require(head != null, "actual pawn Def must contain real FA head controller");
            var face = headType.GetProperty("FaceTypeDefName", All);
            face.SetValue(head, "HeadNormal");
            int expectedHeads = pawn.AllComps.Count(c => c.GetType() == headType) + 1;
            duplicate = (ThingComp)Activator.CreateInstance(headType); duplicate.parent = pawn; duplicate.Initialize(head.props); face.SetValue(duplicate, "HeadSquare");
            pawn.AllComps.Add(duplicate);
            Require(expectedHeads >= 2 && pawn.AllComps.Count(c => c.GetType() == headType) == expectedHeads && (string)face.GetValue(head) == "HeadNormal" && (string)face.GetValue(duplicate) == "HeadSquare", "genuine duplicate-controller fixture with distinct loaded face Defs");
            statue = ThingMaker.MakeThing(ThingDefOf.Statue, ThingDefOf.Steel);
            var art = statue.TryGetComp<CompStatue>(); Require(art != null, "actual statue Def component");
            var snapshot = typeof(CompStatue).GetMethod("CreateSnapshotOfPawn", All);
            snapshot.Invoke(art, new object[] { pawn });
            var saved = (Dictionary<string, object>)typeof(CompStatue).GetField("additionalSavedPawnDataForMods", All).GetValue(art);
            var savedHead = saved[headType.FullName];
            var value = savedHead.GetType().GetField("Value", All);
            Require(savedHead.GetType().FullName == "FacialAnimation.HarmonyPatches+ExposableString" && (string)value.GetValue(savedHead) == "HeadNormal", "actual native snapshot retains original first head value");
            Require(pawn.AllComps.Count(c => c.GetType() == headType) == expectedHeads && (string)face.GetValue(duplicate) == "HeadSquare", "patch cannot mutate/remove duplicate pawn comps");
            snapshot.Invoke(art, new object[] { pawn });
            saved = (Dictionary<string, object>)typeof(CompStatue).GetField("additionalSavedPawnDataForMods", All).GetValue(art);
            Require((string)value.GetValue(saved[headType.FullName]) == "HeadNormal", "repeated real snapshot clears/rebuilds while keeping first");
            var existing = new Dictionary<string, object> { [headType.FullName] = savedHead };
            object[] args = { pawn, existing }; Require((bool)target.Invoke(null, args) && ReferenceEquals(existing[headType.FullName], savedHead), "actual FA patched hook preserves preexisting dictionary value");
            var harmony = new Harmony(patch.owner);
            bool originalFailed = false;
            try
            {
                harmony.Unpatch(target, transpiler);
                try { target.Invoke(null, new object[] { pawn, new Dictionary<string, object>() }); }
                catch (TargetInvocationException error) when (error.InnerException is ArgumentException) { originalFailed = true; }
                Require(originalFailed, "actual unpatched upstream duplicate Add must reproduce original exception");
            }
            finally { harmony.Patch(target, transpiler: new HarmonyMethod(transpiler) { priority = patch.priority, before = patch.before, after = patch.after }); }
            snapshot.Invoke(art, new object[] { pawn });
            saved = (Dictionary<string, object>)typeof(CompStatue).GetField("additionalSavedPawnDataForMods", All).GetValue(art);
            Require((string)value.GetValue(saved[headType.FullName]) == "HeadNormal", "restored real transpiler native snapshot succeeds");
            object[] nullPawn = { null, new Dictionary<string, object>() }; Require((bool)target.Invoke(null, nullPawn) && ((Dictionary<string, object>)nullPawn[1]).Count == 0, "upstream null pawn guard preserved");
            object[] nullDictionary = { pawn, null }; Require((bool)target.Invoke(null, nullDictionary) && nullDictionary[1] == null, "upstream null dictionary guard preserved");
        }
        finally
        {
            if (duplicate != null) pawn.AllComps.Remove(duplicate);
            if (statue != null && !statue.Destroyed) statue.Destroy();
            if (!pawn.Destroyed) pawn.Destroy();
        }
        Log.Message("RW17 Mod71: genuine FA two controller comps/native CompStatue snapshot/first ExposableString/preexisting value/repeated snapshot/no comp mutation, original upstream duplicate exception and restored exact transpiler, null guards/helper/IL metadata preservation passed; full trader/faction-leader/save/render combinations pending");
    }

    private static void VerifyHelpers(MethodInfo helper, MethodInfo transpiler)
    {
        var dictionary = new Dictionary<string, object>(); var first = new object(); var second = new object();
        helper.Invoke(null, new object[] { dictionary, "head", first }); helper.Invoke(null, new object[] { dictionary, "head", second });
        Require(dictionary.Count == 1 && ReferenceEquals(dictionary["head"], first), "safe helper first wins");
        helper.Invoke(null, new object[] { dictionary, "", second }); helper.Invoke(null, new object[] { dictionary, "  ", second }); helper.Invoke(null, new object[] { null, "head", second });
        Require(dictionary.Count == 1, "safe helper invalid dictionary/blank keys");
        helper.Invoke(null, new object[] { dictionary, "null-value", null }); Require(dictionary.ContainsKey("null-value") && dictionary["null-value"] == null, "safe helper valid null value");
        var generator = new DynamicMethod("RW17Label", typeof(void), Type.EmptyTypes).GetILGenerator();
        var label = generator.DefineLabel(); var block = new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock);
        var add = typeof(Dictionary<string, object>).GetMethod("Add", new[] { typeof(string), typeof(object) });
        var call = new CodeInstruction(OpCodes.Callvirt, add); call.labels.Add(label); call.blocks.Add(block);
        var nop = new CodeInstruction(OpCodes.Nop);
        var transformed = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { new[] { nop, call } })).ToArray();
        Require(ReferenceEquals(transformed[0], nop) && transformed[1].opcode == OpCodes.Call && Equals(transformed[1].operand, helper) && transformed[1].labels.SequenceEqual(new[] { label }) && transformed[1].blocks.SequenceEqual(new[] { block }), "transpiler exact helper call preserves labels/exception blocks and unrelated instruction");
    }
}
