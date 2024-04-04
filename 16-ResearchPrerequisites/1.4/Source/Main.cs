using Verse;
using RimWorld;

// using System.Reflection;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using System;
using System.Linq;
using Verse.Sound;
using Logs = Logger.Log;
using System.Collections.Generic;

namespace ResearchPrerequisites
{
	[StaticConstructorOnStartup]
	public static class Start
	{
		static Start()
		{
			Harmony harmony = new Harmony("com.runningbugs.researchprerequisites");
			harmony.PatchAll();
		}
	}


	public class ResearchQueue : GameComponent
	{
		public static List<ResearchProjectDef> researchQueue = new List<ResearchProjectDef>();


		private bool AddResearchHandler(ResearchProjectDef research)
		{
			if (research == null)
			{
				return true;
			}

			if (research.IsFinished)
			{
				return true;
			}

			// if (!research.IsFinished && research.PrerequisitesCompleted && research.TechprintRequirementMet && research.PlayerHasAnyAppropriateResearchBench)
			if (!research.IsFinished && research.PrerequisitesCompleted && research.TechprintRequirementMet)
			{
				researchQueue.Add(research);
				return true;
			}
			else if (!research.IsFinished && research.TechprintRequirementMet && research.PlayerHasAnyAppropriateResearchBench)
			{

				List<ResearchProjectDef> prereqs = new List<ResearchProjectDef>();
				prereqs.AddRange(research.prerequisites ?? new List<ResearchProjectDef>());
				prereqs.AddRange(research.hiddenPrerequisites ?? new List<ResearchProjectDef>());
				foreach (var prereq in prereqs)
				{
					if (prereq == null || prereq.IsFinished)
					{
						continue;
					}
					AddResearchHandler(prereq);
				}
				researchQueue.Add(research);
			}

			return true;
		}

		public bool StartNewResearchQueue(ResearchProjectDef research)
		{
			researchQueue.Clear();
			bool res = AddResearchHandler(research);
			RemoveDuplicates();
			return res;
		}

		public bool InsertToResearchQueue(ResearchProjectDef research)
		{
			bool res = AddResearchHandler(research);
			RemoveDuplicates();
			return res;
		}

		private void RemoveDuplicates()
		{
			researchQueue = researchQueue.Distinct().ToList();
		}

		public ResearchQueue(Game game)
		{
		}

		public override void FinalizeInit()
		{
			string researchLabels = string.Join(", ", researchQueue.Select(x => x.label));
		}

		public ResearchProjectDef GetNextResearch()
		{
			if (researchQueue.Count == 0)
			{
				return null;
			}
			var research = researchQueue[0];
			if (research.IsFinished)
			{
				researchQueue.RemoveAt(0);
				research = GetNextResearch();
			}

			if (!research.TechprintRequirementMet) {
				researchQueue.Clear();
				return null;
			}
			return research;
		}

		public override void GameComponentTick()
		{
			/// Find.ResearchManager.currentProj is null when research finished.
			/// When finding this, we will try to start the next research in the queue.
			if (Find.ResearchManager.currentProj != null)
			{
				return;
			}

			if (researchQueue.Count == 0)
			{
				return;
			}

			Logs.Warning($"No current proj, start research queue: {string.Join(", ", researchQueue.Select(x => x.label).ToArray())}");
			var nextResearch = GetNextResearch();
			if (nextResearch != null)
			{
				Logs.Warning("Attempting to start research: " + nextResearch.label);
				Patches.AttemptBeginResearch(nextResearch);
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref researchQueue, "ResearchPrerequisites.ResearchQueue.researchQueue", LookMode.Def);
			if (researchQueue == null)
			{
				researchQueue = new List<ResearchProjectDef>();
			}
		}
	}

	[HarmonyPatch(typeof(MainTabWindow_Research))]
	public static class ResearchWindowOnClosePatch
	{
		public static MethodBase TargetMethod()
		{
			return AccessTools.Method(typeof(MainTabWindow_Research), "PostClose");
		}

		public static void Postfix()
		{
			// Log.Warning("PostClose");
			Patches.mode = Patches.ResearchButtonMode.Start;
		}
	}


	[HarmonyPatch]
	[StaticConstructorOnStartup]
	public static class Patches
	{
		private static MainTabWindow_Research instance = null;
		private static Type type = typeof(MainTabWindow_Research);
		private static readonly Texture2D ResearchBarFillTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.8f, 0.85f));
		private static readonly Texture2D ResearchBarBGTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.1f, 0.1f, 0.1f));

		public static MethodBase TargetMethod()
		{
			return AccessTools.Method(typeof(MainTabWindow_Research), "DrawLeftRect");
		}

		private static (FieldInfo, T) GetValue<T>(object instance, string fieldName)
		{
			FieldInfo fieldInfo = type?.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			return (fieldInfo, (T)fieldInfo?.GetValue(instance));
		}

		private static T CallMethod<T>(object instance, string methodName, params object[] parameters)
		{
			MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			return (T)methodInfo.Invoke(instance, parameters);
		}

		public static void AttemptBeginResearch(ResearchProjectDef research)
		{
			CallMethod<object>(instance, "AttemptBeginResearch", new object[] { research });
		}


		public enum ResearchButtonMode
		{
			Start,
			AddToQueue
		}

		public static ResearchButtonMode mode = ResearchButtonMode.Start;
		private static void DrawLeftRect(Rect leftOutRect)
		{
			var (leftStartAreaHeightInfo, leftStartAreaHeight) = GetValue<float>(instance, "leftStartAreaHeight");
			var (selectedProjectInfo, selectedProject) = GetValue<ResearchProjectDef>(instance, "selectedProject");


			float num = leftOutRect.height - (10f + leftStartAreaHeight) - 45f;
			Rect rect = leftOutRect;
			Widgets.BeginGroup(rect);
			if (selectedProject != null)
			{
				var (leftViewDebugHeightInfo, leftViewDebugHeight) = GetValue<float>(instance, "leftViewDebugHeight");
				Rect outRect = new Rect(0f, 0f, rect.width, num - leftViewDebugHeight);

				var (leftScrollViewHeightInfo, leftScrollViewHeight) = GetValue<float>(instance, "leftScrollViewHeight");
				Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, leftScrollViewHeight);

				var (fInfo, value) = GetValue<Vector2>(instance, "leftScrollPosition");
				Widgets.BeginScrollView(outRect, ref value, viewRect);
				fInfo.SetValue(instance, value);

				float num2 = 0f;
				Text.Font = GameFont.Medium;
				GenUI.SetLabelAlign(TextAnchor.MiddleLeft);
				Rect rect2 = new Rect(0f, num2, viewRect.width - 0f, 50f);
				Widgets.LabelCacheHeight(ref rect2, selectedProject.LabelCap);
				GenUI.ResetLabelAlign();
				Text.Font = GameFont.Small;
				num2 += rect2.height;
				Rect rect3 = new Rect(0f, num2, viewRect.width, 0f);
				Widgets.LabelCacheHeight(ref rect3, selectedProject.description);
				num2 += rect3.height;
				Rect rect4 = new Rect(0f, num2, viewRect.width, 500f);


				num2 += CallMethod<float>(instance, "DrawTechprintInfo", new object[] { rect4, selectedProject });

				if ((int)selectedProject.techLevel > (int)Faction.OfPlayer.def.techLevel)
				{
					float num3 = selectedProject.CostFactor(Faction.OfPlayer.def.techLevel);
					Rect rect5 = new Rect(0f, num2, viewRect.width, 0f);
					string text = "TechLevelTooLow".Translate(Faction.OfPlayer.def.techLevel.ToStringHuman(), selectedProject.techLevel.ToStringHuman(), (1f / num3).ToStringPercent());
					if (num3 != 1f)
					{
						text += " " + "ResearchCostComparison".Translate(selectedProject.baseCost.ToString("F0"), selectedProject.CostApparent.ToString("F0"));
					}
					Widgets.LabelCacheHeight(ref rect5, text);
					num2 += rect5.height;
				}
				// num2 += DrawResearchPrereqs(rect: new Rect(0f, num2, viewRect.width, 500f), project: selectedProject);
				num2 += CallMethod<float>(instance, "DrawResearchPrereqs", new object[] { selectedProject, new Rect(0f, num2, viewRect.width, 500f) });

				// num2 += DrawResearchBenchRequirements(rect: new Rect(0f, num2, viewRect.width, 500f), project: selectedProject);
				num2 += CallMethod<float>(instance, "DrawResearchBenchRequirements", new object[] { selectedProject, new Rect(0f, num2, viewRect.width, 500f) });

				// num2 += DrawStudyRequirements(rect: new Rect(0f, num2, viewRect.width, 500f), project: selectedProject);
				num2 += CallMethod<float>(instance, "DrawStudyRequirements", new object[] { selectedProject, new Rect(0f, num2, viewRect.width, 500f) });

				Rect rect9 = new Rect(0f, num2, viewRect.width, 500f);
				// num2 += DrawUnlockableHyperlinks(rect9, selectedProject);
				num2 += CallMethod<float>(instance, "DrawUnlockableHyperlinks", new object[] { rect9, selectedProject });

				Rect rect10 = new Rect(0f, num2, viewRect.width, 500f);
				// num2 += DrawContentSource(rect10, selectedProject);
				num2 += CallMethod<float>(instance, "DrawContentSource", new object[] { rect10, selectedProject });

				Rect researchQueueInfoRect = new Rect(0f, num2, viewRect.width, 500f);
				num2 += DrawResearchQueueInfo(researchQueueInfoRect);

				num2 += 3f;

				// leftScrollViewHeight = num2;
				leftScrollViewHeightInfo.SetValue(instance, num2);

				Widgets.EndScrollView();
				Rect rect11 = new Rect(0f, outRect.yMax + 10f + leftViewDebugHeight, rect.width, leftStartAreaHeight);

				// if selected project cannot start now, try to construct the research queue and attempt to start researches
				if (selectedProject.CanStartNow && selectedProject != Find.ResearchManager.currentProj)
				{
					// leftStartAreaHeight = 68f;
					leftStartAreaHeightInfo.SetValue(instance, 68f);

					if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.LeftShift)
					{
						mode = ResearchButtonMode.AddToQueue;
					}
					else if (Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.LeftShift)
					{
						mode = ResearchButtonMode.Start;
					}

					if (mode == ResearchButtonMode.Start)
					{
						if (Widgets.ButtonText(rect11, "Research".Translate()))
						{
							AttemptBeginResearch(selectedProject);
							// CallMethod<object>(instance, "AttemptBeginResearch", new object[] { selectedProject });
							ResearchQueue researchQueue = Current.Game.GetComponent<ResearchQueue>();
							researchQueue?.StartNewResearchQueue(selectedProject);
							// var nextResearch = researchQueue?.GetNextResearch();
							// Find.ResearchManager.currentProj = nextResearch;
							// selectedProjectInfo.SetValue(instance, nextResearch);
						}
					}
					else
					{
						if (Widgets.ButtonText(rect11, "ResearchAddToQueue".Translate()))
						{
							ResearchQueue researchQueue = Current.Game.GetComponent<ResearchQueue>();
							researchQueue?.InsertToResearchQueue(selectedProject);
						}
					}
				}
				else if (!selectedProject.IsFinished && selectedProject != Find.ResearchManager.currentProj && !selectedProject.PrerequisitesCompleted && selectedProject.TechprintRequirementMet && selectedProject.PlayerHasAnyAppropriateResearchBench)
				{
					// leftStartAreaHeight = 68f;
					leftStartAreaHeightInfo.SetValue(instance, 68f);

					if (Widgets.ButtonText(rect11, "ResearchAddToQueue".Translate()))
					{
						// AttemptBeginResearch(selectedProject);
						// CallMethod<object>(instance, "AttemptBeginResearch", new object[]{selectedProject});
						ResearchQueue researchQueue = Current.Game.GetComponent<ResearchQueue>();
						// researchQueue?.StartNewResearchQueue(selectedProject);
						researchQueue?.InsertToResearchQueue(selectedProject);
						var nextResearch = researchQueue?.GetNextResearch();
						Find.ResearchManager.currentProj = nextResearch;
						selectedProjectInfo.SetValue(instance, nextResearch);
					}
				}
				else
				{
					string text2 = "";
					if (selectedProject.IsFinished)
					{
						text2 = "Finished".Translate();
						Text.Anchor = TextAnchor.MiddleCenter;
					}
					else if (selectedProject == Find.ResearchManager.currentProj)
					{
						text2 = "InProgress".Translate();
						Text.Anchor = TextAnchor.MiddleCenter;
					}
					else
					{
						text2 = "";
						if (!selectedProject.PrerequisitesCompleted)
						{
							text2 += "\n  " + "PrerequisitesNotCompleted".Translate();
						}
						if (!selectedProject.TechprintRequirementMet)
						{
							text2 += "\n  " + "InsufficientTechprintsApplied".Translate(selectedProject.TechprintsApplied, selectedProject.TechprintCount);
						}
						if (!selectedProject.PlayerHasAnyAppropriateResearchBench)
						{
							text2 += "\n  " + "MissingRequiredResearchFacilities".Translate();
						}
						if (text2.NullOrEmpty())
						{
							Log.ErrorOnce("Research " + selectedProject.defName + " locked but no reasons given", selectedProject.GetHashCode() ^ 0x5FE2BD1);
						}
						text2 = "Locked".Translate() + ":" + text2;
					}
					// leftStartAreaHeight = Mathf.Max(Text.CalcHeight(text2, rect11.width - 10f) + 10f, 68f);
					leftStartAreaHeightInfo.SetValue(instance, Mathf.Max(Text.CalcHeight(text2, rect11.width - 10f) + 10f, 68f));

					Widgets.DrawHighlight(rect11);
					Widgets.Label(rect11.ContractedBy(5f), text2);
					Text.Anchor = TextAnchor.UpperLeft;
				}
				Rect rect12 = new Rect(0f, rect11.yMax + 10f, rect.width, 35f);


				// var (_, ResearchBarFillTex) = GetValue<Texture2D>(instance, "ResearchBarFillTex");
				// var (_, ResearchBarBGTex) = GetValue<Texture2D>(instance, "ResearchBarBGTex");
				Widgets.FillableBar(rect12, selectedProject.ProgressPercent, ResearchBarFillTex, ResearchBarBGTex, doBorder: true);


				Text.Anchor = TextAnchor.MiddleCenter;
				Widgets.Label(rect12, selectedProject.ProgressApparent.ToString("F0") + " / " + selectedProject.CostApparent.ToString("F0"));
				Text.Anchor = TextAnchor.UpperLeft;
				// leftViewDebugHeight = 0f;
				leftViewDebugHeightInfo.SetValue(instance, 0f);
				if (Prefs.DevMode && selectedProject != Find.ResearchManager.currentProj && !selectedProject.IsFinished)
				{
					Text.Font = GameFont.Tiny;
					Rect rect13 = new Rect(rect11.x, outRect.yMax, 120f, 30f);
					if (Widgets.ButtonText(rect13, "Debug: Finish now"))
					{
						Find.ResearchManager.currentProj = selectedProject;
						Find.ResearchManager.FinishProject(selectedProject);
					}
					Text.Font = GameFont.Small;
					// leftViewDebugHeight = rect13.height;
					leftViewDebugHeightInfo.SetValue(instance, rect13.height);
				}
				if (Prefs.DevMode && !selectedProject.TechprintRequirementMet)
				{
					Text.Font = GameFont.Tiny;
					Rect rect14 = new Rect(rect11.x + 120f, outRect.yMax, 120f, 30f);
					if (Widgets.ButtonText(rect14, "Debug: Apply techprint"))
					{
						Find.ResearchManager.ApplyTechprint(selectedProject, null);
						SoundDefOf.TechprintApplied.PlayOneShotOnCamera();
					}
					Text.Font = GameFont.Small;
					// leftViewDebugHeight = rect14.height;
					leftViewDebugHeightInfo.SetValue(instance, rect14.height);
				}
			}
			Widgets.EndGroup();
		}

		private static Vector2 researchQueueScrollPosition = Vector2.zero;

		/**
         * Split the rect into two parts
         */
		private static (Rect, Rect) GetRow(Rect rect, float height)
		{
			return (new Rect(rect.x, rect.y, rect.width, height), new Rect(rect.x, rect.y + height, rect.width, rect.height - height));
		}

		private static float DrawResearchQueueInfo(Rect researchQueueInfoRect)
		{
			// float height = researchQueueInfoRect.height;
			float height = Text.LineHeight * (ResearchQueue.researchQueue.Count + 3);

			Rect current, remain;
			(current, remain) = GetRow(researchQueueInfoRect, Text.LineHeight * 2);

			if (Widgets.ButtonText(current, "ClearResearchQueue".Translate()))
			{
				ResearchQueue.researchQueue.Clear();
				Find.ResearchManager.currentProj = null;
			}

			string researchString = "CurrentResearchQueue".Translate() + "\n" + string.Join("\n", ResearchQueue.researchQueue.Select(x => x.label).ToArray());

			// Widgets.LabelScrollable(researchQueueInfoRect, researchString, ref researchQueueScrollPosition);
			Widgets.LabelFit(remain, researchString);

			return height;
		}

		public static bool Prefix(Rect leftOutRect, MainTabWindow_Research __instance)
		{
			instance = __instance;
			DrawLeftRect(leftOutRect);
			return false;
		}
	}

}
