using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FacialAnimationStatueSnapshotFix;

[StaticConstructorOnStartup]
public static class Start
{
    private const string HarmonyId = "com.RunningBugs.FacialAnimationStatueSnapshotFix";
    private const string TargetTypeName = "FacialAnimation.HarmonyPatches";
    private const string TargetMethodName = "PrefixCreateSnapshotOfPawn_HookForMods";

    static Start()
    {
        try
        {
            var harmony = new Harmony(HarmonyId);
            Type targetType = AccessTools.TypeByName(TargetTypeName);
            if (targetType == null)
            {
                Log.Error("[FacialAnimationStatueSnapshotFix] Could not find type FacialAnimation.HarmonyPatches. Make sure this mod loads after [NL] Facial Animation.");
                return;
            }

            MethodInfo target = AccessTools.Method(
                targetType,
                TargetMethodName,
                new[]
                {
                    typeof(Pawn),
                    typeof(Dictionary<string, object>).MakeByRefType()
                });

            if (target == null)
            {
                Log.Error("[FacialAnimationStatueSnapshotFix] Could not find target method FacialAnimation.HarmonyPatches.PrefixCreateSnapshotOfPawn_HookForMods(Pawn, ref Dictionary<string, object>). Facial Animation may have changed.");
                return;
            }

            MethodInfo transpiler = AccessTools.Method(typeof(Start), nameof(Transpiler));
            harmony.Patch(target, transpiler: new HarmonyMethod(transpiler));

            Log.Message("[FacialAnimationStatueSnapshotFix] Patched FacialAnimation statue snapshot dictionary write.".Colorize(Color.green));
        }
        catch (Exception ex)
        {
            Log.Error($"[FacialAnimationStatueSnapshotFix] Failed to apply patch: {ex}");
        }
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        MethodInfo dictionaryAdd = AccessTools.Method(
            typeof(Dictionary<string, object>),
            nameof(Dictionary<string, object>.Add),
            new[] { typeof(string), typeof(object) });

        MethodInfo safeAdd = AccessTools.Method(typeof(Start), nameof(SafeAddIgnoreDuplicate));

        int replacements = 0;
        for (int i = 0; i < codes.Count; i++)
        {
            if (!codes[i].Calls(dictionaryAdd))
            {
                continue;
            }

            var replacement = new CodeInstruction(OpCodes.Call, safeAdd);
            replacement.labels.AddRange(codes[i].labels);
            replacement.blocks.AddRange(codes[i].blocks);
            codes[i] = replacement;
            replacements++;
        }

        if (replacements != 1)
        {
            Log.Error($"[FacialAnimationStatueSnapshotFix] Expected to replace exactly 1 Dictionary<string, object>.Add call in Facial Animation snapshot hook, replaced {replacements}. Patch may be incompatible with this Facial Animation version.");
        }
        else
        {
            Log.Message("[FacialAnimationStatueSnapshotFix] Replaced 1 Dictionary.Add call with duplicate-tolerant add.".Colorize(Color.green));
        }

        return codes;
    }

    public static void SafeAddIgnoreDuplicate(Dictionary<string, object> dictionary, string key, object value)
    {
        if (dictionary == null || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (dictionary.ContainsKey(key))
        {
            Log.WarningOnce(
                $"[FacialAnimationStatueSnapshotFix] Ignored duplicate Facial Animation statue snapshot key: {key}. Keeping the first value.",
                key.GetHashCode() ^ 0x5A7100);
            return;
        }

        dictionary.Add(key, value);
    }
}
