using System;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using RimWorld;
using Template;
public static class Program
{
    public static void Main()
    {
        const string dir = "/home/lisanhu/mine/workspace/rimworld/RW/17/Assemblies";
        AssemblyLoadContext.Default.Resolving += (c,n) => File.Exists(Path.Combine(dir,n.Name+".dll")) ? c.LoadFromAssemblyPath(Path.Combine(dir,n.Name+".dll")) : null;
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dir,"UnityStubs.dll"));
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dir,"Assembly-CSharp.dll"));
        Checks.Run();
    }
}
public static class Checks
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Run()
    {
        var wear = AccessTools.Method(typeof(Pawn_ApparelTracker), "Wear", new[] { typeof(Apparel), typeof(bool), typeof(bool) });
        var replacement = AccessTools.Method(typeof(ArmorRacksPatches), "WearAndForce");
        var gen = new DynamicMethod("Labels", typeof(void), Type.EmptyTypes).GetILGenerator(); var label = gen.DefineLabel();
        var call = new CodeInstruction(OpCodes.Callvirt, wear); call.labels.Add(label); call.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        var rewritten = ArmorRacksPatches.Transpiler(new[] { new CodeInstruction(OpCodes.Nop), call, new CodeInstruction(OpCodes.Ret) }).ToList();
        if (rewritten.Count != 3 || rewritten[1].opcode != OpCodes.Call || !rewritten[1].Calls(replacement) || rewritten[1].labels.Single()!=label || rewritten[1].blocks.Count!=1 || !call.Calls(wear) || call.opcode!=OpCodes.Callvirt) throw new Exception("Exact signature substitution / metadata / immutable input failed");
        foreach (int n in new[] { 0, 2 }) { try { ArmorRacksPatches.Transpiler(Enumerable.Repeat(call,n)).ToList(); throw new Exception("Ambiguous IL accepted"); } catch (InvalidOperationException) { } }
        var actualTarget = ArmorRacksPatches.TargetMethod();
        var actualCode = PatchProcessor.GetOriginalInstructions(actualTarget);
        if (actualTarget.Name != "<MakeNewToils>b__2_0" || !ArmorRacksPatches.Transpiler(actualCode).Any(i => i.Calls(replacement))) throw new Exception("Actual installed transfer delegate / real IL mapping failed");
        Console.WriteLine("PASS: real production Wear transpiler stack-preserving substitution, labels/blocks, immutable input, 0/2 rejection");
    }
}
