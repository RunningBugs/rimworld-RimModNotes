using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HarmonyLib;
using Waylith.Rendering;

namespace WaylithRuntimeFixes;

internal sealed record CheckResult(bool Passed, string Name);
internal sealed record SuiteResult(bool Passed, string Runtime, string EngineMvid, bool Native, List<CheckResult> Checks, string? Error);

internal static class CheckSuite
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static SuiteResult Run(bool native)
    {
        var checks = new List<CheckResult>();
        void Check(bool passed, string name)
        {
            checks.Add(new(passed, name));
            Console.WriteLine((passed ? "PASS " : "FAIL ") + name);
            if (!passed) throw new InvalidOperationException(name);
        }

        string? error = null;
        try
        {
            var target = AccessTools.Method(typeof(RenderThread), nameof(RenderThread.WaitUntilIdle));
            var coreWait = AccessTools.Method(typeof(ManualResetEventSlim), nameof(ManualResetEventSlim.Wait),
                [typeof(int), typeof(CancellationToken)]);
            if (native)
            {
                Check(Harmony.GetPatchInfo(target)?.Owners.Contains(PatchInstaller.HarmonyId) == true,
                    "Game Mod constructor installed the product render patch");
                // This disposable headless process exercises independent install/rollback tests,
                // then restores the product patch. Other owners and texture leases remain intact.
                new Harmony(PatchInstaller.HarmonyId).Unpatch(target, HarmonyPatchType.Transpiler, PatchInstaller.HarmonyId);
                new Harmony(PatchInstaller.HarmonyId).Unpatch(coreWait, HarmonyPatchType.Prefix, PatchInstaller.HarmonyId);
            }
            else TextureAccountingChecks.Warmup();

            RenderWaitChecks.Run(Check);
            Check(PatchInstaller.IsVerifiedEngine(typeof(RenderThread).Module.ModuleVersionId), "Actual engine build is verified");
            Check(!PatchInstaller.IsVerifiedEngine(Guid.Empty), "Unknown engine identity is rejected");
            Check(PatchInstaller.Apply(Console.WriteLine), "Both product patches install successfully");
            TextureAccountingChecks.Run(Check);
            Check(PatchInstaller.Apply(Console.WriteLine), "Repeated installer call is safe");
            Check(Harmony.GetPatchInfo(target)?.Transpilers.Count(p => p.owner == PatchInstaller.HarmonyId) == 1,
                "Product owns exactly one render transpiler");
            Check(Harmony.GetPatchInfo(coreWait)?.Prefixes.Count(p => p.owner == PatchInstaller.HarmonyId) == 1,
                "Product owns exactly one guarded core-wait prefix");
        }
        catch (Exception exception) { error = exception.ToString(); }
        return new(error == null && checks.All(c => c.Passed), RuntimeInformation.FrameworkDescription,
            typeof(RenderThread).Module.ModuleVersionId.ToString(), native, checks, error);
    }
}
