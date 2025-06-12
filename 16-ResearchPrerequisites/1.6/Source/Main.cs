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
using System.Text;

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
			// else if (!research.IsFinished && research.TechprintRequirementMet && research.PlayerHasAnyAppropriateResearchBench)
			else if (!research.IsFinished)
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

		public static bool CanStartNow(ResearchProjectDef research)
		{
			if (research == null)
			{
				return false;
			}
			// if (!research.IsFinished && research.PrerequisitesCompleted && research.TechprintRequirementMet && (research.requiredResearchBuilding == null || research.PlayerHasAnyAppropriateResearchBench) && research.PlayerMechanitorRequirementMet && research.AnalyzedThingsRequirementsMet)
			if (!research.IsFinished && research.PrerequisitesCompleted && research.TechprintRequirementMet && research.PlayerMechanitorRequirementMet && research.AnalyzedThingsRequirementsMet)
			{
				return !research.IsHidden;
			}
			return false;
		}

		public ResearchProjectDef GetNextResearchCanStart()
		{
			if (researchQueue.Count == 0)
			{
				return null;
			}
			var research = researchQueue[0];
			if (research.IsFinished)
			{
				researchQueue.RemoveAt(0);
				research = GetNextResearchCanStart();
			}

			if (research == null)
			{
				return null;
			}

			if (!research.CanStartNow)
			{
				researchQueue.Clear();
				return null;
			}
			return research;
		}

		public override void GameComponentTick()
		{
			/// Find.ResearchManager.currentProj is null when research finished.
			/// When finding this, we will try to start the next research in the queue.
			if (Find.ResearchManager.GetProject(null) != null)
			{
				return;
			}

			if (researchQueue.Count == 0)
			{
				return;
			}

			var nextResearch = GetNextResearchCanStart();
			if (nextResearch != null)
			{
				ResearchQueueController.AttemptBeginResearch(nextResearch);
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

	public static class ResearchQueueController
	{
		public enum ResearchButtonMode
		{
			Start,
			AddToQueue
		}
		public static ResearchButtonMode mode = ResearchButtonMode.Start;
		private static bool ColonistsHaveResearchBench
		{
			get
			{
				bool result = false;
				List<Map> maps = Find.Maps;
				for (int i = 0; i < maps.Count; i++)
				{
					if (maps[i].listerBuildings.ColonistsHaveResearchBench())
					{
						result = true;
						break;
					}
				}
				return result;
			}
		}

		private static List<(BuildableDef, List<string>)> ComputeUnlockedDefsThatHaveMissingMemes(ResearchProjectDef project)
		{
			List<(BuildableDef, List<string>)> cachedDefsWithMissingMemes = new List<(BuildableDef, List<string>)>();
			if (project == null)
			{
				return cachedDefsWithMissingMemes;
			}
			if (!ModsConfig.IdeologyActive)
			{
				return cachedDefsWithMissingMemes;
			}
			if (Faction.OfPlayer.ideos?.PrimaryIdeo == null)
			{
				return cachedDefsWithMissingMemes;
			}
			foreach (Def unlockedDef in project.UnlockedDefs)
			{
				if (!(unlockedDef is BuildableDef { canGenerateDefaultDesignator: false } buildableDef))
				{
					continue;
				}
				List<string> list = null;
				foreach (MemeDef item in DefDatabase<MemeDef>.AllDefsListForReading)
				{
					if (!Faction.OfPlayer.ideos.HasAnyIdeoWithMeme(item) && item.AllDesignatorBuildables.Contains(buildableDef))
					{
						if (list == null)
						{
							list = new List<string>();
						}
						list.Add(item.LabelCap);
					}
				}
				if (list != null)
				{
					cachedDefsWithMissingMemes.Add((buildableDef, list));
				}
			}
			return cachedDefsWithMissingMemes;
		}


		private static void DoBeginResearch(ResearchProjectDef projectToStart)
		{
			// SoundDefOf.ResearchStart.PlayOneShotOnCamera();
			Find.ResearchManager.SetCurrentProject(projectToStart);
			TutorSystem.Notify_Event("StartResearchProject");
			if ((!ModsConfig.AnomalyActive || projectToStart.knowledgeCategory == null) && !ColonistsHaveResearchBench)
			{
				Messages.Message("MessageResearchMenuWithoutBench".Translate(), MessageTypeDefOf.CautionInput);
			}
		}

		public static void AttemptBeginResearch(ResearchProjectDef projectToStart)
		{
			if (projectToStart == null)
			{
				return;
			}

			List<(BuildableDef, List<string>)> list = ComputeUnlockedDefsThatHaveMissingMemes(projectToStart);
			if (!list.Any())
			{
				DoBeginResearch(projectToStart);
				return;
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("ResearchProjectHasDefsWithMissingMemes".Translate(projectToStart.LabelCap)).Append(":");
			stringBuilder.AppendLine();
			foreach (var (buildableDef, items) in list)
			{
				stringBuilder.AppendLine();
				stringBuilder.Append("  - ").Append(buildableDef.LabelCap.Colorize(ColoredText.NameColor)).Append(" (")
					.Append(items.ToCommaList())
					.Append(")");
			}
			Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(stringBuilder.ToString(), delegate
			{
				DoBeginResearch(projectToStart);
			}));
			// SoundDefOf.Tick_Low.PlayOneShotOnCamera();
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
			ResearchQueueController.mode = ResearchQueueController.ResearchButtonMode.Start;
		}
	}


	public static class HarmonyHelper
	{
		public static FieldInfo GetFieldOrProperty<T>(this object obj, string name, out T value)
		{
			var type = obj.GetType();
			var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			if (field != null)
			{
				value = (T)field.GetValue(obj);
				return field;
			}
			var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			if (property != null)
			{
				value = (T)property.GetValue(obj);
				return null;
			}
			throw new Exception($"Field or property {name} not found in {type}");
		}

		public static T CallMethod<T>(this object instance, string methodName, ref object[] parameters)
		{
			var type = instance.GetType();
			MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			return (T)methodInfo.Invoke(instance, parameters);
		}
	}


	[HarmonyPatch(typeof(MainTabWindow_Research))]
	public static class ResearchWindowDrawProjectScrollViewPatch
	{
		public static MethodBase TargetMethod()
		{
			return AccessTools.Method(typeof(MainTabWindow_Research), "DrawProjectScrollView");
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
				var currentProj = Find.ResearchManager.GetProject(null);
				Find.ResearchManager.StopProject(currentProj);
			}

			string researchString = "CurrentResearchQueue".Translate() + "\n" + string.Join("\n", ResearchQueue.researchQueue.Select(x => x.label).ToArray());

			// Widgets.LabelScrollable(researchQueueInfoRect, researchString, ref researchQueueScrollPosition);
			Widgets.LabelFit(remain, researchString);

			return height;
		}

		private static void DrawProjectScrollView(MainTabWindow_Research __instance, Rect rect)
		{
			FieldInfo leftScrollViewHeightField = __instance.GetFieldOrProperty("leftScrollViewHeight", out float leftScrollViewHeight);
			FieldInfo leftScrollPositionField = __instance.GetFieldOrProperty<Vector2>("leftScrollPosition", out Vector2 leftScrollPosition);

			Rect viewRect = new Rect(0f, 0f, rect.width - 16f, leftScrollViewHeight);
			float y = 3f;
			Widgets.BeginScrollView(rect, ref leftScrollPosition, viewRect);
			leftScrollPositionField.SetValue(__instance, leftScrollPosition);

			_ = __instance.GetFieldOrProperty("selectedProject", out ResearchProjectDef selectedProject);
			if ((int)selectedProject.techLevel > (int)Faction.OfPlayer.def.techLevel)
			{
				float num = selectedProject.CostFactor(Faction.OfPlayer.def.techLevel);
				Rect rect2 = new Rect(0f, y, viewRect.width, 0f);
				string text = "TechLevelTooLow".Translate(Faction.OfPlayer.def.techLevel.ToStringHuman(), selectedProject.techLevel.ToStringHuman(), (1f / num).ToStringPercent());
				if (num != 1f)
				{
					text += " " + "ResearchCostComparison".Translate(selectedProject.Cost.ToString("F0"), selectedProject.CostApparent.ToString("F0"));
				}
				Widgets.LabelCacheHeight(ref rect2, text);
				y += rect2.height;
			}
			Rect rect3 = new Rect(0f, y, viewRect.width, 0f);
			// DrawResearchPrerequisites(rect3, ref y, selectedProject);
			object[] parameters = new object[] { rect3, y, selectedProject };
			__instance.CallMethod<object>("DrawResearchPrerequisites", ref parameters);
			y = (float)parameters[1];

			Rect rect4 = new Rect(0f, y, viewRect.width, 500f);

			// y += DrawResearchBenchRequirements(selectedProject, rect4);
			parameters = new object[] { selectedProject, rect4 };
			y += __instance.CallMethod<float>("DrawResearchBenchRequirements", ref parameters);

			// y += DrawStudyRequirements(rect: new Rect(0f, y, viewRect.width, 500f), project: selectedProject);
			parameters = new object[] { selectedProject, new Rect(0f, y, viewRect.width, 500f) };
			y += __instance.CallMethod<float>("DrawStudyRequirements", ref parameters);

			Rect rect6 = new Rect(0f, y, viewRect.width, 500f);
			Rect visibleRect = new Rect(0f, leftScrollPosition.y, viewRect.width, rect.height);
			// y += DrawUnlockableHyperlinks(rect6, visibleRect, selectedProject);
			parameters = new object[] { rect6, visibleRect, selectedProject };
			y += __instance.CallMethod<float>("DrawUnlockableHyperlinks", ref parameters);

			Rect rect7 = new Rect(0f, y, viewRect.width, 500f);
			// y += DrawContentSource(rect7, selectedProject);
			parameters = new object[] { rect7, selectedProject };
			y += __instance.CallMethod<float>("DrawContentSource", ref parameters);

			_ = __instance.GetFieldOrProperty("curTabInt", out ResearchTabDef curTabInt);
			if (!ModsConfig.AnomalyActive || curTabInt != ResearchTabDefOf.Anomaly || selectedProject.knowledgeCategory == null)
			{
				y += DrawResearchQueueInfo(new Rect(0f, y, viewRect.width, 500f));
			}

			y += 3f;
			// leftScrollViewHeight = y;
			leftScrollViewHeightField.SetValue(__instance, y);
			Widgets.EndScrollView();
		}

		public static bool Prefix(MainTabWindow_Research __instance, Rect rect)
		{
			DrawProjectScrollView(__instance, rect);
			return false;
		}

	}



	[HarmonyPatch(typeof(MainTabWindow_Research))]
	public static class ResearchWindowDrawStartButtonPatch
	{
		public static MethodBase TargetMethod()
		{
			return AccessTools.Method(typeof(MainTabWindow_Research), "DrawStartButton");
		}

		private static void DrawStartButton(MainTabWindow_Research __instance, Rect startButRect)
		{
			FieldInfo selectedProjectField = __instance.GetFieldOrProperty("selectedProject", out ResearchProjectDef selectedProject);
			_ = __instance.GetFieldOrProperty("curTabInt", out ResearchTabDef curTabInt);
			if (selectedProject.CanStartNow && !Find.ResearchManager.IsCurrentProject(selectedProject))
			{
				if (ModsConfig.AnomalyActive && (curTabInt == ResearchTabDefOf.Anomaly || selectedProject.knowledgeCategory != null))
				{
					if (Widgets.ButtonText(startButRect, "Research".Translate()))
					{
						ResearchQueueController.AttemptBeginResearch(selectedProject);
					}
					return;
				}

				if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.LeftShift)
				{
					ResearchQueueController.mode = ResearchQueueController.ResearchButtonMode.AddToQueue;
				}
				else if (Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.LeftShift)
				{
					ResearchQueueController.mode = ResearchQueueController.ResearchButtonMode.Start;
				}
				if (ResearchQueueController.mode == ResearchQueueController.ResearchButtonMode.Start)
				{
					if (Widgets.ButtonText(startButRect, "Research".Translate()))
					{
						ResearchQueueController.AttemptBeginResearch(selectedProject);

						if (selectedProject.knowledgeCategory == null)
						{
							ResearchQueue researchQueue = Current.Game.GetComponent<ResearchQueue>();
							researchQueue?.StartNewResearchQueue(selectedProject);
						}
					}
				}
				else if (ResearchQueueController.mode == ResearchQueueController.ResearchButtonMode.AddToQueue)
				{
					if (Widgets.ButtonText(startButRect, "ResearchAddToQueue".Translate()))
					{
						ResearchQueue researchQueue = Current.Game.GetComponent<ResearchQueue>();
						researchQueue?.InsertToResearchQueue(selectedProject);
					}
				}

				return;
			}
			if (!Find.ResearchManager.IsCurrentProject(selectedProject) && !selectedProject.IsFinished && !selectedProject.IsHidden && selectedProject.knowledgeCategory == null && (!ModsConfig.AnomalyActive || curTabInt != ResearchTabDefOf.Anomaly))
			{
				//  Can add to queue
				if (Widgets.ButtonText(startButRect, "ResearchAddToQueue".Translate()))
				{
					ResearchQueue researchQueue = Current.Game.GetComponent<ResearchQueue>();
					researchQueue?.InsertToResearchQueue(selectedProject);
					var nextResearch = researchQueue?.GetNextResearchCanStart();
					ResearchQueueController.AttemptBeginResearch(nextResearch);
				}
				return;
			}


			if (Find.ResearchManager.IsCurrentProject(selectedProject))
			{
				if (Widgets.ButtonText(startButRect, "StopResearch".Translate()))
				{
					if (!ModsConfig.AnomalyActive || curTabInt != ResearchTabDefOf.Anomaly || selectedProject.knowledgeCategory != null)
					{
						ResearchQueue.researchQueue.Clear();
					}
					Find.ResearchManager.StopProject(selectedProject);
				}
				return;
			}

			FieldInfo lockedReasonsField = __instance.GetFieldOrProperty("lockedReasons", out List<string> lockedReasons);
			lockedReasons.Clear();
			string text;
			if (selectedProject.IsFinished)
			{
				text = "Finished".Translate();
				Text.Anchor = TextAnchor.MiddleCenter;
			}
			else if (Find.ResearchManager.IsCurrentProject(selectedProject))
			{
				text = "InProgress".Translate();
				Text.Anchor = TextAnchor.MiddleCenter;
			}
			else
			{
				if (!selectedProject.PrerequisitesCompleted)
				{
					lockedReasons.Add("PrerequisitesNotCompleted".Translate());
				}
				if (!selectedProject.TechprintRequirementMet)
				{
					lockedReasons.Add("InsufficientTechprintsApplied".Translate(selectedProject.TechprintsApplied, selectedProject.TechprintCount));
				}

				if ((!ModsConfig.AnomalyActive || curTabInt != ResearchTabDefOf.Anomaly) && !selectedProject.PlayerHasAnyAppropriateResearchBench)
				{
					lockedReasons.Add("MissingRequiredResearchFacilities".Translate());
				}
				if (!selectedProject.PlayerMechanitorRequirementMet)
				{
					lockedReasons.Add("MissingRequiredMechanitor".Translate());
				}
				if (!selectedProject.AnalyzedThingsRequirementsMet)
				{
					for (int i = 0; i < selectedProject.requiredAnalyzed.Count; i++)
					{
						lockedReasons.Add("NotStudied".Translate(selectedProject.requiredAnalyzed[i].LabelCap));
					}
				}
				if (lockedReasons.NullOrEmpty())
				{
					Log.ErrorOnce("Research " + selectedProject.defName + " locked but no reasons given", selectedProject.GetHashCode() ^ 0x5FE2BD1);
				}
				text = "Locked".Translate();
			}
			Widgets.DrawHighlight(startButRect);
			startButRect = startButRect.ContractedBy(4f);
			string text2 = text;
			if (!lockedReasons.NullOrEmpty())
			{
				text2 = text2 + ":\n" + lockedReasons.ToLineList("  ");
			}
			Vector2 vector = Text.CalcSize(text2);
			if (vector.x > startButRect.width || vector.y > startButRect.height)
			{
				TooltipHandler.TipRegion(startButRect.ExpandedBy(4f), text2);
				Text.Anchor = TextAnchor.MiddleCenter;
			}
			else
			{
				text = text2;
			}
			Widgets.Label(startButRect, text);
			Text.Anchor = TextAnchor.UpperLeft;

			lockedReasonsField.SetValue(__instance, lockedReasons);
		}

		public static bool Prefix(MainTabWindow_Research __instance, Rect startButRect)
		{
			DrawStartButton(__instance, startButRect);
			return false;
		}

	}
}
