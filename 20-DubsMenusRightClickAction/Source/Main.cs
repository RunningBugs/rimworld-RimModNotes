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

using Logs = Logger.Log;
using HarmonyLib;
using DubsMintMenus;
using System.Reflection;
using System.Reflection.Emit;

namespace DubsMintMenusRightClickAction
{
	[StaticConstructorOnStartup]
	public static class Start
	{
		static Start()
		{
			Harmony harmony = new Harmony("com.runningbugs.dubsmintmenusrightclickaction");
			harmony.PatchAll();
			Logs.prefix = "DubsMintMenusRightClickAction";
			Logs.Message("Loaded");
		}
	}


	[HarmonyPatch]
	public static class RightClickActionPatch
	{
		private static bool findCallToButtonInvisible = false;
		private static RecipeDef recipe = null;
		private static CodeInstruction returnCodeInstruction = null;

		public static bool Prepare(MethodBase original)
		{
			if (original == TargetMethod())
			{
				// Logs.Message("Preparing DoRow");
				findCallToButtonInvisible = false;
				return true;
			}
			return true;
		}
		public static MethodBase TargetMethod()
		{
			var type = AccessTools.TypeByName("DubsMintMenus.Patch_BillStack_DoListing");
			return AccessTools.Method(type, "DoRow");
		}

		public static bool ClickAction()
		{
			Event e = Event.current;
			if (e.type == EventType.Used && e.button == 1)
			{
				// Right-click event
				// Logs.Message("Right-click event detected");
				if (recipe != null)
				{
					foreach (var product in recipe.products)
					{
						ThingDef thingDef = product.thingDef;
						Find.WindowStack.Add(new Dialog_InfoCard(thingDef));
					}
				}
				recipe = null;
				return false;
			}
			return true;
		}

		public static bool Prefix(RecipeDef recipe)
		{
			RightClickActionPatch.recipe = recipe;
			return true;
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			int pos = -1;
			for (int i = 0; i < codes.Count; i++)
			{
				var instruction = codes[i];
				if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method && method.Name == "ButtonInvisible" && method.DeclaringType.FullName == "Verse.Widgets")
				{
					// Logs.Warning("call to ButtonInvisible found");
					findCallToButtonInvisible = true;
				}
				else if (findCallToButtonInvisible && instruction.opcode == OpCodes.Brfalse)
				{
					// Logs.Warning("call to brfalse found");
					returnCodeInstruction = instruction;
					pos = i + 1;
				}
			}

			if (pos != -1)
			{
				CodeInstruction callClickAction = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RightClickActionPatch), "ClickAction"));
				codes.Insert(pos++, callClickAction);
				{
					if (returnCodeInstruction != null) {
						codes.Insert(pos++, returnCodeInstruction);	// If ClickAction returns false, then jump to ret
					}
				}
			}
			return codes.AsEnumerable();
		}
	}

}
