using HarmonyLib;
using Verse;
using Waylith.Rendering;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("WaylithRuntimeFixes.Tests")]

namespace WaylithRuntimeFixes;

public sealed class WaylithRuntimeFixesMod : Mod
{
    public WaylithRuntimeFixesMod(ModContentPack content) : base(content)
        => PatchInstaller.Apply(message => Log.Message(message));
}

internal static class PatchInstaller
{
    internal const string HarmonyId = "RunningBugs.WaylithRuntimeFixes";

    // Re-audit the engine implementation before allowing a new binary. In particular,
    // an official TexturePixelData finalizer would make our accounting unsafe.
    internal static bool IsVerifiedEngine(Guid moduleId) => moduleId == new Guid("543d4239-0c13-4a34-b1aa-d3f23a6d02d9")
        || moduleId == new Guid("16b3c822-6803-4b5d-b286-d759dbd57145");

    internal static bool Apply(Action<string> log)
    {
        void Report(string message) => log("[Waylith Runtime Fixes] " + message);
        var moduleId = typeof(RenderThread).Module.ModuleVersionId;
        if (!IsVerifiedEngine(moduleId))
        {
            Report($"Unsupported Waylith MVID {moduleId}: both fixes skipped. Re-audit this engine build first.");
            return false;
        }

        var harmony = new Harmony(HarmonyId);
        var render = RenderWaitPatch.Apply(harmony, Report);
        var accounting = TextureAccountingPatch.Apply(harmony, Report);
        Report($"Waylith {moduleId}: render wait={(render ? "active" : "skipped")}, texture accounting={(accounting ? "active" : "skipped")}.");
        return render && accounting;
    }
}
