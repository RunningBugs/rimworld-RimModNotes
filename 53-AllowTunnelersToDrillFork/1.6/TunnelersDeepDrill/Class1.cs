using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using System.Linq;

namespace TunelerFix
{
	// This class will basically tell harmony to do it's thing so if you forget it the code below wont even run
	[StaticConstructorOnStartup]
	public class TunelerFix
	{
		static TunelerFix()
		{
			Harmony harmony = new Harmony("Porio.TunnulerFix.fork");
			harmony.PatchAll();
			Log.Message("TunelerFix patched successfully.");
		}
	}

	//the [HarmonyPatch] is a tag to tell harmony what to do with this part of the code, in this case use it to modify Rimworld code
	[HarmonyPatch]
	public static class XPFix
	{
		//this method is used to point to the method you need to change, in my case it was a bit special as the method i am targeting is a nested one so i had to use AccessTools.Inner
		static MethodBase TargetMethod()
		{
			return AccessTools.Method(AccessTools.Inner(typeof(JobDriver_OperateDeepDrill), "<>c__DisplayClass1_0"), "<MakeNewToils>b__1");
		}

		/*
		*	The target IL code is
		    .method assembly hidebysig 
			instance void '<MakeNewToils>b__1' (
				int32 delta
			) cil managed 
		{
			// Method begins at RVA 0x23c4ec
			// Header size: 12
			// Code size: 72 (0x48)
			.maxstack 5
			.locals init (
				[0] class Verse.Pawn
			)

			IL_0000: ldarg.0
			IL_0001: ldfld class Verse.AI.Toil RimWorld.JobDriver_OperateDeepDrill/'<>c__DisplayClass1_0'::work
			IL_0006: ldfld class Verse.Pawn Verse.AI.Toil::actor
			IL_000b: stloc.0
			IL_000c: ldloc.0
			IL_000d: callvirt instance class Verse.AI.Job Verse.Pawn::get_CurJob()
			IL_0012: ldflda valuetype Verse.LocalTargetInfo Verse.AI.Job::targetA
			IL_0017: call instance class Verse.Thing Verse.LocalTargetInfo::get_Thing()
			IL_001c: castclass Verse.Building
			IL_0021: callvirt instance !!0 Verse.ThingWithComps::GetComp<class RimWorld.CompDeepDrill>()
			IL_0026: ldloc.0
			IL_0027: ldarg.1
			IL_0028: callvirt instance void RimWorld.CompDeepDrill::DrillWorkDone(class Verse.Pawn, int32)
			IL_002d: ldloc.0
			IL_002e: ldfld class RimWorld.Pawn_SkillTracker Verse.Pawn::skills
			IL_0033: ldsfld class RimWorld.SkillDef RimWorld.SkillDefOf::Mining
			IL_0038: ldc.r4 0.065
			IL_003d: ldarg.1
			IL_003e: conv.r4
			IL_003f: mul
			IL_0040: ldc.i4.0
			IL_0041: ldc.i4.0
			IL_0042: callvirt instance void RimWorld.Pawn_SkillTracker::Learn(class RimWorld.SkillDef, float32, bool, bool)
			IL_0047: ret
		} // end of method '<>c__DisplayClass1_0'::'<MakeNewToils>b__1'
		*/

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
		{
			var codes = new List<CodeInstruction>(instructions);
			// var oldCodes = new List<CodeInstruction>(codes);
			Label skipLabel = il.DefineLabel();

			int anchorIdx = -1;
			// First iterate through the codes. Find out ldc.r4 0.065
			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].opcode == OpCodes.Ldc_R4 && Math.Abs((float)codes[i].operand - 0.065f) < 0.001f)
				{
					anchorIdx = i;
					break;
				}
			}

			// Now we want to add a label to the ret line
			for (int i = anchorIdx + 1; i < codes.Count; i++)
			{
				if (codes[i].opcode == OpCodes.Ret)
				{
					codes[i].labels.Add(skipLabel);
					break;
				}
			}

			// Now we know we want to do a null check before IL_002d, which is at anchorIdx - 3
			// The il code we want to insert is
			// ldloc.0
			// ldfld class RimWorld.Pawn_SkillTracker Verse.Pawn::skills
			// brfalse.s {skipLabel}
			var toInsert = new List<CodeInstruction>{
				new CodeInstruction(OpCodes.Ldloc_0),
				new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Pawn), "skills")),
				new CodeInstruction(OpCodes.Brfalse_S, skipLabel)
			};

			// Now we can insert the code
			codes.InsertRange(anchorIdx - 3, toInsert);

			// Before returning the modified codes, we want to log it so we can see what it looks like
			// Log.Message($"TunnelerFix: Modified IL instructions:\n{string.Join("\n", codes.Select((code, i) => $"IL_{i:X4}: {code}"))}");
			return codes;
		}
	}

	// // Fallback patch in case the transpiler fails - prevents crashes when mechs try to gain XP
	// [HarmonyPatch(typeof(Pawn_SkillTracker), nameof(Pawn_SkillTracker.Learn))]
	// public static class SkillLearnSafetyPatch
	// {
	// 	static bool Prefix(Pawn_SkillTracker __instance)
	// 	{
	// 		// Only allow XP gain if the skill tracker actually exists (not null for mechs)
	// 		if (__instance == null)
	// 		{
	// 			Log.Message("TunnelerFix: Prevented null skill tracker from gaining XP (likely a mech)");
	// 			return false;
	// 		}
	// 		return true;
	// 	}
	// }
}