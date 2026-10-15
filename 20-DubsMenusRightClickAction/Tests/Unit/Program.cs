using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using DubsMintMenusRightClickAction;
using HarmonyLib;
using UnityEngine;
using Verse;

using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
public static class Program
{
    public static void Main()
    {
        const string snapshot = "/home/lisanhu/mine/workspace/rimworld/RW/17/Assemblies";
        AssemblyLoadContext.Default.Resolving += (context, name) => {
            var file = Path.Combine(snapshot, name.Name + ".dll");
            return File.Exists(file) ? context.LoadFromAssemblyPath(file) : null;
        };
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(snapshot, "UnityStubs.dll"));
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(snapshot, "Assembly-CSharp.dll"));
        Checks.Run();
    }
}
public static class Checks
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Run()
    {
        var button = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonInvisible), new[] { typeof(Rect), typeof(bool) });
        var replacement = AccessTools.Method(typeof(RightClickActionPatch), nameof(RightClickActionPatch.RecipeButton));
        var generator = new DynamicMethod("UnitLabels", typeof(void), Type.EmptyTypes).GetILGenerator();
        var label = generator.DefineLabel();
        var call = new CodeInstruction(OpCodes.Call, button);
        call.labels.Add(label);
        call.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        var original = new[] { new CodeInstruction(OpCodes.Nop), call,
            new CodeInstruction(OpCodes.Brfalse_S, label), new CodeInstruction(OpCodes.Ret) };
        var rewritten = RightClickActionPatch.Transpiler(original).ToList();
        if (rewritten.Count != 5 || rewritten[1].opcode != OpCodes.Ldarg_0
            || !rewritten[1].labels.Contains(label) || rewritten[1].blocks.Count != 1
            || !rewritten[2].Calls(replacement) || rewritten[2].labels.Count != 0
            || rewritten[3].opcode != OpCodes.Brfalse_S || call.labels.Count != 1 || call.blocks.Count != 1)
            throw new Exception("Button substitution, short branch, labels, exception boundary or original immutability failed");
        foreach (int count in new[] { 0, 2 })
        {
            try
            {
                RightClickActionPatch.Transpiler(Enumerable.Repeat(new CodeInstruction(OpCodes.Call, button), count)).ToList();
                throw new Exception("Invalid button count accepted");
            }
            catch (InvalidOperationException) { }
        }
        Console.WriteLine("PASS: actual production transpiler substitution, labels/blocks, short branch, immutable input, 0/2-match rejection");
    }
}
