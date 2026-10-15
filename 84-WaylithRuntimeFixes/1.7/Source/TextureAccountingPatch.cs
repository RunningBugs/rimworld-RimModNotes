using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using Waylith.Rendering;

namespace WaylithRuntimeFixes;

internal static class TextureAccountingPatch
{
    private static readonly ConditionalWeakTable<TexturePixelData, Lease> Leases = new();
    private static readonly object InstallLock = new();
    private static bool installed;

    private sealed class Lease
    {
        private TexturePixelData? key;
        private int armed;

        internal Lease(TexturePixelData key) => this.key = key;
        internal void Arm() => Volatile.Write(ref armed, 1);

        ~Lease()
        {
            TexturePixelData? data = Interlocked.Exchange(ref key, null);
            if (Interlocked.Exchange(ref armed, 0) != 0 && Volatile.Read(ref installed) && data is not null)
                Interlocked.Add(ref TexturePixelData._currentCpuBytes, unchecked(-(long)Interlocked.Exchange(ref data._trackedBytes, 0)));
        }
    }

    internal static bool Apply(Harmony harmony, Action<string> log)
    {
        lock (InstallLock)
        {
            if (installed) return true;
            Type type = typeof(TexturePixelData);
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            MethodInfo? set = type.GetMethod("SetTrackedBytes", instance, [typeof(ulong)]);
            MethodInfo? track = type.GetMethod(nameof(TexturePixelData.TrackCurrentBytes), instance, Type.EmptyTypes);
            MethodInfo? release = type.GetMethod(nameof(TexturePixelData.ReleaseTracked), instance, Type.EmptyTypes);
            ConstructorInfo? constructor = type.GetConstructor([typeof(int), typeof(int), typeof(LoadedTextureFormat), typeof(bool), typeof(int), typeof(bool), typeof(int)]);
            FieldInfo? tracked = type.GetField("_trackedBytes", instance);
            FieldInfo? total = type.GetField("_currentCpuBytes", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            MethodInfo add = typeof(Interlocked).GetMethod(nameof(Interlocked.Add), [typeof(long).MakeByRefType(), typeof(long)])!;
            MethodBase?[] targets = [set, track, release, constructor];
            if (type.GetMethod("Finalize", instance, Type.EmptyTypes) is not null ||
                tracked?.FieldType != typeof(ulong) || total?.FieldType != typeof(long) ||
                targets.Any(method => method is null || method.IsStatic || method.GetMethodBody() is null ||
                    (method is MethodInfo info && info.ReturnType != typeof(void))))
            {
                log("CPU texture accounting skipped: finalizer, fields, or accounting signatures differ from the verified implementation.");
                return false;
            }

            try
            {
                var instructions = PatchProcessor.GetOriginalInstructions(set!);
                if (!instructions.Any(instruction => instruction.LoadsField(tracked!)) ||
                    !instructions.Any(instruction => instruction.StoresField(tracked!)) ||
                    !instructions.Any(instruction => instruction.LoadsField(total!, byAddress: true)) ||
                    !instructions.Any(instruction => instruction.Calls(add)) ||
                    !PatchProcessor.GetOriginalInstructions(track!).Any(instruction => instruction.Calls(set!)) ||
                    !PatchProcessor.GetOriginalInstructions(release!).Any(instruction => instruction.Calls(set!)))
                {
                    log("CPU texture accounting skipped: accounting method bodies differ from the verified implementation.");
                    return false;
                }

                var registration = new HarmonyMethod(typeof(TextureAccountingPatch), nameof(Register));
                // Constructor registration covers updates already inlined into precompiled callers.
                // The entry hooks also adopt objects constructed before this patch was loaded.
                foreach (MethodBase? method in targets)
                {
                    if (method is ConstructorInfo) harmony.Patch(method, postfix: registration);
                    else harmony.Patch(method!, prefix: registration);
                }
                Volatile.Write(ref installed, true);
                log("CPU texture accounting enabled: ephemeron leases finalize the object's actual remaining balance.");
                return true;
            }
            catch (Exception error)
            {
                Volatile.Write(ref installed, false);
                foreach (MethodBase? method in targets)
                {
                    try
                    {
                        harmony.Unpatch(method!, AccessTools.Method(typeof(TextureAccountingPatch), nameof(Register)));
                    }
                    catch (Exception rollbackError)
                    {
                        log("CPU texture accounting rollback failed for " + method!.Name + ": " + rollbackError);
                    }
                }
                log("CPU texture accounting skipped: patch installation failed: " + error);
                return false;
            }
        }
    }

    private static void Register(TexturePixelData __instance)
    {
        // CWT permits value -> key references without permanently rooting the key.
        // Finalization may promote the pixels for one more GC cycle; it never disposes them.
        // Competing factories may lose; only the value actually stored is armed.
        Leases.GetValue(__instance, static key => new Lease(key)).Arm();
    }
}
