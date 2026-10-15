using System.Text.Json;
using Verse;

namespace WaylithRuntimeFixes;

// Only put this assembly in the isolated test-driver MOD, never the product MOD.
public sealed class NativeWarmupMod : Mod
{
    public NativeWarmupMod(ModContentPack content) : base(content) => TextureAccountingChecks.Warmup();
}

[StaticConstructorOnStartup]
public static class NativeCheck
{
    static NativeCheck()
    {
        var results = CheckSuite.Run(native: true);
        File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "waylith-runtime-fixes-tests.json"),
            JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        if (results.Passed) Log.Message("WAYLITH_RUNTIME_FIXES_TESTS_OK");
        else Log.Error("WAYLITH_RUNTIME_FIXES_TESTS_FAILED " + results.Error);
    }
}
