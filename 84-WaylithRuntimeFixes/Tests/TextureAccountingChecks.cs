using System.Runtime.CompilerServices;
using System.Reflection;
using Waylith.Rendering;

namespace WaylithRuntimeFixes;

public static class TextureAccountingChecks
{
    private static TexturePixelData? preexisting;
    private static TexturePixelData? preexistingForTrack;

    public static void Warmup()
    {
        var data = NewData(64);
        for (int i = 0; i < 20000; i++) Touch(data);
        data.ReleaseTracked();
        for (int i = 0; i < 32; i++)
            AbandonTexture(explicitDestroy: true);
        GC.KeepAlive(data);
        preexisting = NewData(112);
        preexistingForTrack = NewData(72);
    }

    public static void Run(Action<bool, string> check)
    {
        Collect();
        long baseline = RawTotal();
        check(baseline >= 0, "raw CPU texture counter begins nonnegative");
        var abandoned = AbandonData(64, explicitRelease: false);
        check(RawTotal() == baseline + 64, "ordinary TrackCurrentBytes increments the counter");
        Collect();
        check(!abandoned[0].IsAlive && !abandoned[1].IsAlive, "ephemeron lease does not permanently retain the key or its pixels");
        check(RawTotal() == baseline, "GC-only accounting returns to baseline");

        var released = AbandonData(64, explicitRelease: true);
        Collect();
        check(!released[0].IsAlive && RawTotal() == baseline, "explicit and repeated ReleaseTracked do not double subtract");

        var changed = ChangeAndAbandon(check, baseline);
        Collect();
        check(!changed.IsAlive && RawTotal() == baseline, "zero-byte lease and size changes finalize only the remaining count");

        var kept = NewData(96);
        kept.TrackCurrentBytes();
        Collect();
        check(RawTotal() == baseline + 96, "a live key keeps its accounting lease alive");
        kept.ReleaseTracked();
        GC.KeepAlive(kept);

        var concurrent = ConcurrentUpdates();
        Collect();
        check(concurrent.All(reference => !reference.IsAlive) && RawTotal() == baseline, "different keys can register, account, release and finalize in parallel");
        var adopted = ConcurrentRegistration();
        Collect();
        check(!adopted.IsAlive && RawTotal() == baseline, "concurrent adoption of a pre-patch key arms only the stored CWT value");
        var ordinaryAdoption = AdoptThroughTrack();
        Collect();
        check(!ordinaryAdoption.IsAlive && RawTotal() == baseline, "ordinary TrackCurrentBytes adopts a key constructed before patch installation");

        var warmed = WarmedAndAbandoned();
        Collect();
        check(!warmed.IsAlive && RawTotal() == baseline, "prewarmed ordinary entry retains correct GC accounting");
        var warmedRelease = WarmedReleaseAndAbandoned();
        Collect();
        check(!warmedRelease.IsAlive && RawTotal() == baseline, "prewarmed ReleaseTracked does not leave a stale lease or a negative raw total");

        var wrapper = AbandonTexture(explicitDestroy: false);
        Collect();
        check(!wrapper[0].IsAlive && !wrapper[1].IsAlive && RawTotal() == baseline, "real Unity Texture2D creation reaches the accounting patch and GC balances it");
        var destroyed = AbandonTexture(explicitDestroy: true);
        Collect();
        check(!destroyed[0].IsAlive && RawTotal() == baseline, "prewarmed Unity Object.Destroy does not double subtract");
        check(RawTotal() >= 0, "raw CPU texture counter ends nonnegative");
    }

    private static TexturePixelData NewData(int bytes)
    {
        var data = new TexturePixelData(1, 1, LoadedTextureFormat.Rgba32, false, 1, false);
        data.Replace(1, 1, LoadedTextureFormat.Rgba32, [new byte[bytes]], 1, false);
        return data;
    }

    private static long RawTotal() => (long)typeof(TexturePixelData)
        .GetField("_currentCpuBytes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;

    private static void Collect()
    {
        for (int i = 0; i < 4; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] AbandonData(int bytes, bool explicitRelease)
    {
        var data = NewData(bytes);
        data.TrackCurrentBytes();
        var refs = new[] { new WeakReference(data), new WeakReference(data.Levels[0]) };
        if (explicitRelease)
        {
            data.ReleaseTracked();
            data.ReleaseTracked();
        }
        return refs;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ChangeAndAbandon(Action<bool, string> check, long baseline)
    {
        var data = new TexturePixelData(1, 1, LoadedTextureFormat.Rgba32, false, 1, false);
        data.TrackCurrentBytes();
        data.Replace(1, 1, LoadedTextureFormat.Rgba32, [new byte[64]], 1, false);
        data.TrackCurrentBytes();
        data.Replace(1, 1, LoadedTextureFormat.Rgba32, [new byte[128]], 1, false);
        data.TrackCurrentBytes();
        check(RawTotal() == baseline + 128, "resizing replaces rather than adds the prior count");
        data.ReleaseTracked();
        data.ReleaseTracked();
        data.Replace(1, 1, LoadedTextureFormat.Rgba32, [new byte[32]], 1, false);
        data.TrackCurrentBytes();
        check(RawTotal() == baseline + 32, "accounting can resume after zero and repeated release");
        return new WeakReference(data);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] ConcurrentUpdates()
    {
        var references = new System.Collections.Concurrent.ConcurrentBag<WeakReference>();
        Parallel.For(0, 256, i =>
        {
            var data = NewData(128);
            data.TrackCurrentBytes();
            if ((i & 1) == 0) data.ReleaseTracked();
            references.Add(new WeakReference(data));
        });
        return references.ToArray();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ConcurrentRegistration()
    {
        var data = preexisting ?? NewData(112);
        preexisting = null;
        var register = typeof(TextureAccountingPatch).GetMethod("Register", BindingFlags.NonPublic | BindingFlags.Static)!;
        Parallel.For(0, 256, _ => register.Invoke(null, [data]));
        data.TrackCurrentBytes();
        return new WeakReference(data);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AdoptThroughTrack()
    {
        var data = preexistingForTrack ?? NewData(72);
        preexistingForTrack = null;
        data.TrackCurrentBytes();
        return new WeakReference(data);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Touch(TexturePixelData data)
    {
        data.TrackCurrentBytes();
        data.ReleaseTracked();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference WarmedAndAbandoned()
    {
        var data = NewData(80);
        Touch(data);
        data.TrackCurrentBytes();
        return new WeakReference(data);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference WarmedReleaseAndAbandoned()
    {
        var data = NewData(80);
        data.TrackCurrentBytes();
        Touch(data);
        return new WeakReference(data);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] AbandonTexture(bool explicitDestroy)
    {
        var texture = new UnityEngine.Texture2D(16, 16);
        var refs = new[] { new WeakReference(texture), new WeakReference(texture.CoreTexture.CpuData) };
        if (explicitDestroy) UnityEngine.Object.Destroy(texture);
        return refs;
    }
}
