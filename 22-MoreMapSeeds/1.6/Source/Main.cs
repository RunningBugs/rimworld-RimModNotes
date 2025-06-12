using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using Verse.Noise;
using Verse.Grammar;
using RimWorld;
using RimWorld.Planet;

using System.Reflection;
using HarmonyLib;

namespace MoreMapSeeds
{
	[StaticConstructorOnStartup]
	public static class Start
	{
		private static RulePack GetRulePack(RulePackDef def)
		{
			FieldInfo field = typeof(RulePackDef).GetField("rulePack", BindingFlags.NonPublic | BindingFlags.Instance);
			return (RulePack)field.GetValue(def);
		}

		private static (FieldInfo, T) GetField<T>(object obj, string memberName)
		{
			FieldInfo field = obj.GetType().GetField(memberName, BindingFlags.NonPublic | BindingFlags.Instance);
			return (field, (T)field.GetValue(obj));
		}

		static Start()
		{
			Harmony harmony = new Harmony("com.runningbugs.moremapseeds");
			harmony.PatchAll();
			Log.Message("MoreMapSeeds Loaded");
		}
	}

	[DefOf]
	public static class RulePackDefOf
	{
		public static RulePackDef MCS_SeedGenerator;
	}


	[HarmonyPatch]
	public class Patch
	{
		[HarmonyPrefix]
		[HarmonyPatch(typeof(GenText), "RandomSeedString")]
		public static bool RandomSeedString(ref string __result)
		{
			GrammarRequest request = default(GrammarRequest);
			request.Includes.Add(RulePackDefOf.MCS_SeedGenerator);
			__result = GrammarResolver.Resolve("r_seed", request).ToLower();
			return false;
		}
	}

}
