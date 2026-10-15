using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using Waylith.Graphics;
using Waylith.Rendering;

namespace WaylithRuntimeFixes;

public static class RenderWaitChecks
{
    private static readonly MethodInfo Target = AccessTools.Method(typeof(RenderThread), nameof(RenderThread.WaitUntilIdle));
    private static readonly MethodInfo CoreWait = AccessTools.Method(typeof(ManualResetEventSlim), nameof(ManualResetEventSlim.Wait), [typeof(int), typeof(CancellationToken)]);
    private static int foreignCalls;
    private static int foreignCoreCalls;

    public static void Run(Action<bool, string> check)
    {
        string owner = "WaylithRuntimeFixes.Tests.Wait." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(owner);
        var foreign = new Harmony(owner + ".Other");
        var logs = new List<string>();
        try
        {
            // Warm a normal call before patching; tests call the actual engine method, never the helper.
            using (var warm = new Fixture())
                for (int i = 0; i < 16; i++) CallWait(warm.RenderThread);
            foreign.Patch(Target, prefix: new HarmonyMethod(typeof(RenderWaitChecks), nameof(ForeignPrefix)));
            foreign.Patch(CoreWait, prefix: new HarmonyMethod(typeof(RenderWaitChecks), nameof(ForeignCorePrefix)));
            check(RenderWaitPatch.Apply(harmony, logs.Add), "Render wait transpiler installs on actual Waylith IL");
            check(RenderWaitPatch.Apply(harmony, logs.Add)
                && Harmony.GetPatchInfo(CoreWait)?.Prefixes.Count(p => p.owner == owner) == 1,
                "Repeated installation owns exactly one scoped core-wait prefix");
            harmony.Unpatch(CoreWait, HarmonyPatchType.Prefix, owner);
            check(RenderWaitPatch.Apply(harmony, logs.Add)
                && Harmony.GetPatchInfo(Target)?.Transpilers.Count(p => p.owner == owner) == 1
                && Harmony.GetPatchInfo(CoreWait)?.Prefixes.Count(p => p.owner == owner) == 1,
                "Reinstalling a missing core prefix repairs the pair without duplicating the transpiler");

            using (var healthy = new Fixture())
            {
                healthy.QueueFrames();
                healthy.StartWait();
                healthy.RenderThread.Start();
                check(healthy.Proxy.Entered.Wait(1000), "Healthy renderer reached the controlled slow operation");
                check(!healthy.Done.Wait(180), "Healthy slow wait keeps waiting, without a manufactured timeout");
                healthy.Proxy.Continue.Set();
                check(healthy.Worker.Join(2000) && healthy.Done.Wait(1000), "Healthy slow wait finishes after rendering drains");
                check(healthy.Error == null && healthy.Pending == 0, "Healthy renderer does not raise an error");
            }

            CheckCorePassThrough(check);

            using (var existing = new Fixture())
            {
                existing.QueueFrames();
                var error = new RenderFailure("existing fault");
                Field(existing.RenderThread, "_failure").SetValue(existing.RenderThread, ExceptionDispatchInfo.Capture(error));
                existing.StartWait();
                check(existing.Done.Wait(1000), "Already recorded render fault escapes an unsignalled idle wait");
                check(ReferenceEquals(existing.Error, error) && existing.Pending == 2 && !existing.Idle.IsSet,
                    "Existing original error is preserved; the patch does not fake idle or clear backlog");
            }

            using (var failing = new Fixture())
            {
                failing.Proxy.Failure = new RenderFailure("fault during wait");
                failing.QueueFrames();
                failing.StartWait();
                failing.RenderThread.Start();
                check(failing.Proxy.Entered.Wait(1000) && !failing.Done.Wait(100), "Wait is active before the render operation fails");
                failing.Proxy.Continue.Set();
                check(failing.Worker.Join(2000) && failing.Done.Wait(1000), "Actual render-loop fault with backlog unblocks the waiter");
                check(ReferenceEquals(failing.Error, failing.Proxy.Failure) && failing.Pending == 1 && failing.Queue.Count == 1,
                    "Mid-wait error is forwarded unchanged while remaining frames stay unprocessed");
            }

            using (var disposed = new Fixture())
            {
                disposed.RenderThread.Dispose();
                disposed.StartWait();
                check(disposed.Done.Wait(1000) && disposed.Error is ObjectDisposedException,
                    "Waiting on an already disposed idle event preserves ObjectDisposedException");
            }
            using (var disposing = new Fixture())
            {
                // No worker is started, so this real Dispose only releases managed synchronization state.
                disposing.QueueFrames();
                disposing.StartWait();
                check(!disposing.Done.Wait(100), "Dispose boundary begins with an active unsignalled wait");
                disposing.RenderThread.Dispose();
                check(disposing.Done.Wait(1000) && disposing.Error is ObjectDisposedException,
                    "Disposal during polling surfaces ObjectDisposedException without fake GPU cleanup");
            }

            CheckMetadata(check);
            harmony.Unpatch(Target, HarmonyPatchType.All, owner);
            harmony.Unpatch(CoreWait, HarmonyPatchType.Prefix, owner);
            check(Harmony.GetPatchInfo(Target)?.Prefixes.Any(p => p.owner == foreign.Id) == true,
                "Removing this owner keeps another owner's prefix");
            int before = foreignCalls;
            // Reflection reaches the patched entry even when CallWait's warmed body was inlined.
            using (var restored = new Fixture()) Target.Invoke(restored.RenderThread, null);
            check(foreignCalls > before, "Other owner's prefix still executes after unpatch");
            check(Harmony.GetPatchInfo(CoreWait)?.Prefixes.Any(p => p.owner == foreign.Id) == true,
                "Removing this owner keeps another owner's core-wait prefix");
            before = foreignCoreCalls;
            using (var unrelated = new ManualResetEventSlim(true)) CoreWait.Invoke(unrelated, [0, CancellationToken.None]);
            check(foreignCoreCalls > before, "Other owner's core-wait prefix still executes after unpatch");

            foreign.Patch(Target, transpiler: new HarmonyMethod(typeof(RenderWaitChecks), nameof(InsertNop)) { priority = Priority.First });
            check(!RenderWaitPatch.Apply(harmony, logs.Add) && logs.Any(l => l.Contains("IL does not match")),
                "Changed upstream IL is logged and skipped");
            check(Harmony.GetPatchInfo(Target)?.Transpilers.Any(p => p.owner == owner) != true,
                "A skipped installation leaves no transpiler owned by this patch");
            check(Harmony.GetPatchInfo(CoreWait)?.Prefixes.Any(p => p.owner == owner) != true,
                "A skipped installation leaves no scoped core-wait prefix");
        }
        finally
        {
            harmony.Unpatch(Target, HarmonyPatchType.All, owner);
            harmony.Unpatch(CoreWait, HarmonyPatchType.Prefix, owner);
            foreign.Unpatch(Target, HarmonyPatchType.All, foreign.Id);
            foreign.Unpatch(CoreWait, HarmonyPatchType.Prefix, foreign.Id);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CallWait(RenderThread thread) => thread.WaitUntilIdle();

    private static void ForeignPrefix() => Interlocked.Increment(ref foreignCalls);
    private static void ForeignCorePrefix() => Interlocked.Increment(ref foreignCoreCalls);

    private static void CheckCorePassThrough(Action<bool, string> check)
    {
        using var fixture = new Fixture();
        fixture.QueueFrames();
        Field(fixture.RenderThread, "_failure").SetValue(fixture.RenderThread,
            ExceptionDispatchInfo.Capture(new RenderFailure("guard control")));
        check(!fixture.Idle.Wait(20, CancellationToken.None), "Finite waits on active idle retain their original timeout result");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        bool cancelled = false;
        try { fixture.Idle.Wait(Timeout.Infinite, cancellation.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        check(cancelled, "Cancellable active-idle wait preserves cancellation instead of forwarding renderer failure");

        using var unrelated = new ManualResetEventSlim();
        using var done = new ManualResetEventSlim();
        Exception? error = null;
        bool result = false;
        var waiter = new Thread(() =>
        {
            try { result = unrelated.Wait(Timeout.Infinite, CancellationToken.None); }
            catch (Exception failure) { error = failure; }
            finally { done.Set(); }
        }) { IsBackground = true };
        waiter.Start();
        try
        {
            check(!done.Wait(100), "Unrelated infinite wait ignores active renderer failure and keeps waiting");
            unrelated.Set();
            check(done.Wait(1000) && result && error == null, "Unrelated infinite wait completes normally when signalled");
        }
        finally
        {
            unrelated.Set();
            if (!waiter.Join(1000)) throw new InvalidOperationException("Guard-control waiter did not finish");
        }
    }

    private static IEnumerable<CodeInstruction> InsertNop(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(AccessTools.Method(typeof(ManualResetEventSlim), nameof(ManualResetEventSlim.Wait), Type.EmptyTypes)))
                yield return new CodeInstruction(OpCodes.Nop);
            yield return instruction;
        }
    }

    private static void CheckMetadata(Action<bool, string> check)
    {
        var code = PatchProcessor.GetOriginalInstructions(Target);
        var wait = code.Single(i => i.Calls(AccessTools.Method(typeof(ManualResetEventSlim), nameof(ManualResetEventSlim.Wait), Type.EmptyTypes)));
        var label = new DynamicMethod("wait_metadata", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel();
        wait.labels.Add(label);
        wait.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        wait.blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
        var result = ((IEnumerable<CodeInstruction>)AccessTools.Method(typeof(RenderWaitPatch), "Transpile").Invoke(null, [code])!).ToList();
        int index = result.FindIndex(i => Equals(i.operand, AccessTools.Method(typeof(RenderWaitPatch), "WaitWithFaultCheck")));
        check(index > 0 && result[index - 1].labels.Contains(label)
            && result[index - 1].blocks.Any(b => b.blockType == ExceptionBlockType.BeginExceptionBlock)
            && result[index].blocks.Any(b => b.blockType == ExceptionBlockType.EndExceptionBlock),
            "Transpiler preserves branch labels and exception boundary placement");
    }

    private static FieldInfo Field(object value, string name) => AccessTools.Field(value.GetType(), name);

    private sealed class Fixture : IDisposable
    {
        internal readonly RenderThread RenderThread;
        internal readonly AcquireProxy Proxy;
        internal readonly FrameRenderer Renderer = new();
        internal readonly ManualResetEventSlim Done = new();
        private readonly ManualResetEventSlim entered = new();
        private readonly object? previousActive;
        private Thread? waiter;
        internal Exception? Error;
        internal BlockingCollection<RenderFrame> Queue => (BlockingCollection<RenderFrame>)Field(RenderThread, "_frames").GetValue(RenderThread)!;
        internal ManualResetEventSlim Idle => (ManualResetEventSlim)Field(RenderThread, "_idle").GetValue(RenderThread)!;
        internal Thread Worker => (Thread)Field(RenderThread, "_thread").GetValue(RenderThread)!;
        internal int Pending => (int)Field(RenderThread, "_pendingFrames").GetValue(RenderThread)!;

        internal Fixture()
        {
            var context = DispatchProxy.Create<IRenderContext, AcquireProxy>();
            Proxy = (AcquireProxy)(object)context;
            RenderThread = new RenderThread(context, Renderer);
            previousActive = AccessTools.Field(typeof(RenderThread), "_active").GetValue(null);
            // Simulate a running renderer for the inline fallback without constructing a GPU.
            AccessTools.Field(typeof(RenderThread), "_active").SetValue(null, RenderThread);
        }

        internal void QueueFrames()
        {
            for (int i = 0; i < 2; i++)
            {
                var frame = (RenderFrame)Activator.CreateInstance(typeof(RenderFrame), nonPublic: true)!;
                Field(frame, "_pooled").SetValue(frame, false);
                Field(frame, "_owner").SetValue(frame, Renderer);
                RenderThread.Enqueue(frame);
            }
            Queue.CompleteAdding();
        }

        internal void StartWait()
        {
            waiter = new Thread(() =>
            {
                entered.Set();
                try { CallWait(RenderThread); }
                catch (Exception error) { Error = error; }
                finally { Done.Set(); }
            }) { IsBackground = true };
            waiter.Start();
            if (!entered.Wait(1000)) throw new InvalidOperationException("Waiter did not start");
        }

        public void Dispose()
        {
            // Test-only cleanup: no GPU was constructed, and Stop's GPU cleanup must not run.
            Proxy.Continue.Set();
            if (!(bool)Field(RenderThread, "_disposed").GetValue(RenderThread)!) Idle.Set();
            if (Worker.IsAlive && !Worker.Join(2000)) throw new InvalidOperationException("Test renderer did not stop");
            if (waiter?.IsAlive == true && !waiter.Join(2000)) throw new InvalidOperationException("Test waiter did not stop");
            Field(RenderThread, "_started").SetValue(RenderThread, false);
            var active = AccessTools.Field(typeof(RenderThread), "_active");
            if (ReferenceEquals(active.GetValue(null), RenderThread)) active.SetValue(null, previousActive);
            RenderThread.Dispose();
            Done.Dispose();
            entered.Dispose();
            Proxy.Entered.Dispose();
            Proxy.Continue.Dispose();
        }
    }

    public sealed class RenderFailure(string message) : Exception(message);

    public class AcquireProxy : DispatchProxy
    {
        internal readonly ManualResetEventSlim Entered = new();
        internal readonly ManualResetEventSlim Continue = new();
        internal Exception? Failure;

        protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method!.Name switch
        {
            "get_FramesInFlight" => 2,
            "get_SubmissionFrameIndex" => 0,
            "TryAcquireNextFrame" => Acquire(),
            _ => throw new InvalidOperationException("Unexpected GPU operation in synchronization test: " + method.Name)
        };

        private FrameAcquireResult Acquire()
        {
            Entered.Set();
            if (!Continue.Wait(3000)) throw new InvalidOperationException("Probe operation was not released");
            if (Failure != null) throw Failure;
            return new FrameAcquireResult { result = FrameResult.NotReady };
        }
    }
}
