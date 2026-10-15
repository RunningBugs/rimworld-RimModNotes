using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Waylith.Rendering;

namespace WaylithRuntimeFixes;

internal static class RenderWaitPatch
{
    private static readonly MethodInfo Target = AccessTools.Method(typeof(RenderThread), nameof(RenderThread.WaitUntilIdle));
    private static readonly MethodInfo Wait = AccessTools.Method(typeof(ManualResetEventSlim), nameof(ManualResetEventSlim.Wait), Type.EmptyTypes);
    private static readonly MethodInfo CoreWait = AccessTools.Method(typeof(ManualResetEventSlim), nameof(ManualResetEventSlim.Wait), [typeof(int), typeof(CancellationToken)]);
    private static readonly MethodInfo Throw = AccessTools.Method(typeof(RenderThread), nameof(RenderThread.ThrowIfFaulted));
    private static readonly FieldInfo Idle = AccessTools.Field(typeof(RenderThread), "_idle");
    private static readonly FieldInfo Active = AccessTools.Field(typeof(RenderThread), "_active");
    private static readonly MethodInfo Replacement = AccessTools.Method(typeof(RenderWaitPatch), nameof(WaitWithFaultCheck));
    private static readonly MethodInfo Transpiler = AccessTools.Method(typeof(RenderWaitPatch), nameof(Transpile));
    private static readonly MethodInfo CorePrefix = AccessTools.Method(typeof(RenderWaitPatch), nameof(CoreWaitPrefix));
    private static Action<string>? report;
    private static bool replaced;

    internal static bool Apply(Harmony harmony, Action<string> log)
    {
        report = log;
        if (Target == null || Wait == null || CoreWait?.ReturnType != typeof(bool) || Throw == null
            || Idle?.FieldType != typeof(ManualResetEventSlim) || Active?.FieldType != typeof(RenderThread) || !Active.IsStatic)
        {
            log("Render wait fix skipped: expected methods or idle field are missing.");
            return false;
        }
        bool ownsWait = Harmony.GetPatchInfo(Target)?.Transpilers.Any(p => p.owner == harmony.Id && p.PatchMethod == Transpiler) == true;
        bool ownsCore = Harmony.GetPatchInfo(CoreWait)?.Prefixes.Any(p => p.owner == harmony.Id && p.PatchMethod == CorePrefix) == true;
        if (ownsWait && ownsCore)
            return true;
        try
        {
            if (!ownsWait && FindWait(PatchProcessor.GetCurrentInstructions(Target)) < 0)
            {
                log("Render wait fix skipped: WaitUntilIdle IL does not match the verified wait/check sequence.");
                RollBack(harmony, log);
                return false;
            }
            if (!ownsWait)
            {
                replaced = false;
                harmony.Patch(Target, transpiler: new HarmonyMethod(Transpiler) { priority = Priority.Last });
                if (!replaced)
                {
                    RollBack(harmony, log);
                    return false;
                }
            }
            if (!ownsCore) harmony.Patch(CoreWait, prefix: new HarmonyMethod(CorePrefix) { priority = Priority.Last });
            return true;
        }
        catch (Exception error)
        {
            log("Render wait fix could not be installed: " + error);
            RollBack(harmony, log);
            return false;
        }
    }

    private static void RollBack(Harmony harmony, Action<string> log)
    {
        // Remove this pair only; never remove another owner's patches.
        foreach (var (method, kind) in new[] { (Target, HarmonyPatchType.Transpiler), (CoreWait, HarmonyPatchType.Prefix) })
            if (method != null)
                try { harmony.Unpatch(method, kind, harmony.Id); }
                catch (Exception error) { log("Render wait fix cleanup failed: " + error); }
    }

    private static int FindWait(IList<CodeInstruction> code)
    {
        var matches = Enumerable.Range(0, code.Count).Where(i => code[i].Calls(Wait)).ToArray();
        if (matches.Length != 1) return -1;
        int i = matches[0];
        return i >= 2 && i + 2 < code.Count && code[i - 2].opcode == OpCodes.Ldarg_0
            && code[i - 1].opcode == OpCodes.Ldfld && Equals(code[i - 1].operand, Idle)
            && code[i + 1].opcode == OpCodes.Ldarg_0 && code[i + 2].Calls(Throw) ? i : -1;
    }

    private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.Select(instruction => new CodeInstruction(instruction)).ToList();
        int i = FindWait(code);
        if (i < 0)
        {
            report?.Invoke("Render wait fix skipped during patch rebuild: wait/check IL sequence changed.");
            return code;
        }
        var call = code[i];
        var instance = new CodeInstruction(OpCodes.Ldarg_0);
        instance.labels.AddRange(call.labels);
        call.labels.Clear();
        // Begin boundaries move before the added argument; end boundaries remain after the call.
        foreach (var block in call.blocks.ToArray())
            if (block.blockType != ExceptionBlockType.EndExceptionBlock)
            {
                instance.blocks.Add(block);
                call.blocks.Remove(block);
            }
        call.opcode = OpCodes.Call;
        call.operand = Replacement;
        code.Insert(i, instance);
        replaced = true;
        return code;
    }

    private static void WaitWithFaultCheck(ManualResetEventSlim idle, RenderThread thread)
    {
        Volatile.Read(ref thread._failure)?.Throw();
        while (!idle.Wait(50)) Volatile.Read(ref thread._failure)?.Throw();
        Volatile.Read(ref thread._failure)?.Throw();
        // The original method's post-wait ThrowIfFaulted remains in place.
    }

    private static bool CoreWaitPrefix(ManualResetEventSlim __instance, int __0, CancellationToken __1, ref bool __result)
    {
        // A compiled caller may already contain an inline copy of WaitUntilIdle.
        // The core hook is restricted to the active renderer's infinite, non-cancellable wait.
        if (__0 != Timeout.Infinite || __1.CanBeCanceled) return true;
        var active = Volatile.Read(ref RenderThread._active);
        if (active == null || !ReferenceEquals(__instance, active._idle)) return true;
        WaitWithFaultCheck(__instance, active); // Its finite 50ms waits pass through this prefix.
        __result = true;
        return false;
    }
}
