using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace PolarisblocExt;

[DefOf]
public class PolarisExtResearchProjectDefOfs
{
	public static ResearchProjectDef PolarisNanoRepairHazeGenerator;
}

[DefOf]
public class PolarisblocExtThingDefOfs
{
	public static ThingDef PolarisNanoRepairHazeGenerator;
	public static ThingDef TechprofSubpersonaCore;
}

public class RequestTechCoreNotification : GameComponent
{
	public bool sendTechCoreRequestReminder = true;

	public RequestTechCoreNotification(Game game)
	{
	}

	public static bool ShouldTechCoreAvailable
	{
		get
		{
			/// the research project is defined and finished and
			/// the player doesn't have the tech core and
			/// the player doesn't have the nano repair haze generator
			return PolarisExtResearchProjectDefOfs.PolarisNanoRepairHazeGenerator != null && PolarisExtResearchProjectDefOfs.PolarisNanoRepairHazeGenerator.IsFinished && !PlayerItemAccessibilityUtility.PlayerOrQuestRewardHas(PolarisblocExtThingDefOfs.TechprofSubpersonaCore) && !PlayerItemAccessibilityUtility.PlayerOrQuestRewardHas(PolarisblocExtThingDefOfs.PolarisNanoRepairHazeGenerator);
		}
	}

	public override void GameComponentTick()
	{
		if (sendTechCoreRequestReminder && GenTicks.TicksGame % 1999 == 0 && ShouldTechCoreAvailable)   // 1999 is a prime number
		{
			/// When we are allowed to send out the reminder and
			/// the ticks is correct and tech core should be available
			Find.LetterStack.ReceiveLetter("PolarisblocExt.LetterLabelTechCoreOffer".Translate(), "PolarisblocExt.LetterTechCoreOffer".Translate(), LetterDefOf.NeutralEvent, GlobalTargetInfo.Invalid);
			sendTechCoreRequestReminder = false;
		}
	}

	public override void ExposeData()
	{
		Scribe_Values.Look(ref sendTechCoreRequestReminder, "PolarisblocExt.RequestTechCoreNotification.sendTechCoreRequestReminder", true);
	}
}


public class TechCoreGiver : IExposable, ILoadReferenceable, ICommunicable
{
	private static TechCoreGiver instance = null;

	public static TechCoreGiver Instance
	{
		get
		{
			if (instance == null)
			{
				instance = new TechCoreGiver();
			}
			return instance;
		}
	}

	public int Id = -1;

	private int lastTechCoreRequestTick = -1;

	private const int TechCoreRequestInterval = GenDate.TicksPerYear;
	// private const int TechCoreRequestInterval = 0;

	private readonly Texture2D techCoreIcon = ContentFinder<Texture2D>.Get("Things/Item/Special/SubpersonaCoreTechprof", true);

	public bool ShouldTechCoreAvailable
	{
		get
		{
			return GenTicks.TicksGame > (lastTechCoreRequestTick + TechCoreRequestInterval) && PolarisExtResearchProjectDefOfs.PolarisNanoRepairHazeGenerator != null && PolarisExtResearchProjectDefOfs.PolarisNanoRepairHazeGenerator.IsFinished;
		}
	}

	public TechCoreGiver()
	{
		if (Id == -1)
		{
			Id = Find.UniqueIDsManager.GetNextThingID();
		}
	}

	public string GetUniqueLoadID()
	{
		return "TechCoreGiver_" + this.Id;
	}

	public void ExposeData()
	{
		Scribe_Values.Look(ref this.Id, "PolarisblocExt.TechCoreGiver.Id");
		Scribe_Values.Look(ref this.lastTechCoreRequestTick, "PolarisblocExt.TechCoreGiver.lastTechCoreRequestTick", -1);
	}

	public string GetCallLabel()
	{
		return "PolarisblocExt.TechCoreGiverLabel".Translate();
	}

	public string GetInfoText()
	{
		return "PolarisblocExt.TechCoreGiverInfo".Translate();
	}

	private static int AmountSendableSilver(Map map)
	{
		return (from t in TradeUtility.AllLaunchableThingsForTrade(map)
				where t.def == ThingDefOf.Silver
				select t).Sum((Thing t) => t.stackCount);
	}

	private DiaOption OKToRoot(Pawn negotiator)
	{
		return new DiaOption("OK".Translate())
		{
			linkLateBind = ResetToRoot(negotiator)
		};
	}

	public Func<DiaNode> ResetToRoot(Pawn negotiator)
	{
		return () => DialogFor(negotiator);
	}

	private DiaOption RequestTechCoreQuest(Map map, Pawn negotiator)
	{
		TaggedString taggedString = "PolarisblocExt.RequestTechCoreInformation".Translate(PolarisblocExtThingDefOfs.TechprofSubpersonaCore.label, 1500.ToString());

		bool num = PlayerItemAccessibilityUtility.ItemStashHas(PolarisblocExtThingDefOfs.TechprofSubpersonaCore);
		Slate slate = new Slate();
		slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(Find.World));
		slate.Set("askerIsNull", var: true);
		slate.Set("itemStashSingleThing", PolarisblocExtThingDefOfs.TechprofSubpersonaCore);
		bool flag = QuestScriptDefOf.OpportunitySite_ItemStash.CanRun(slate);
		if (num || !flag)
		{
			DiaOption diaOption2 = new DiaOption(taggedString);
			diaOption2.Disable("NoKnownAICore".Translate(1500));
			return diaOption2;
		}
		if (AmountSendableSilver(map) < 1500)
		{
			DiaOption diaOption3 = new DiaOption(taggedString);
			diaOption3.Disable("NeedSilverLaunchable".Translate(1500));
			return diaOption3;
		}
		return new DiaOption(taggedString)
		{
			action = delegate
			{
				Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(QuestScriptDefOf.OpportunitySite_ItemStash, slate);
				if (!quest.hidden && quest.root.sendAvailableLetter)
				{
					QuestUtility.SendLetterQuestAvailable(quest);
				}
				TradeUtility.LaunchThingsOfType(ThingDefOf.Silver, 1500, map, null);
				lastTechCoreRequestTick = GenTicks.TicksGame;
				// Current.Game.GetComponent<GameComponent_OnetimeNotification>().sendAICoreRequestReminder = false;
			},
			link = new DiaNode("PolarisblocExt.RequestTechCoreInformationResult".Translate().CapitalizeFirst())
			{
				options = { OKToRoot(negotiator) }
			}
		};
	}

	private DiaNode DialogFor(Pawn negotiator)
	{
		Map map = negotiator.Map;
		DiaNode root = new("PolarisblocExt.RequestTechCoreGreeting".Translate());

		if (map != null && map.IsPlayerHome)
		{
			if (ShouldTechCoreAvailable)
			{
				AddAndDecorateOption(RequestTechCoreQuest(map, negotiator), needsSocial: true);
			}
		}

		AddAndDecorateOption(new DiaOption("(" + "Disconnect".Translate() + ")")
		{
			resolveTree = true
		}, needsSocial: false);
		return root;
		void AddAndDecorateOption(DiaOption opt, bool needsSocial)
		{
			if (needsSocial && negotiator.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
			{
				opt.Disable("WorkTypeDisablesOption".Translate(SkillDefOf.Social.label));
			}
			root.options.Add(opt);
		}
	}

	public void TryOpenComms(Pawn negotiator)
	{
		Dialog_Negotiation dialog_Negotiation = new(negotiator, this, DialogFor(negotiator), radioMode: true)
		{
			soundAmbient = SoundDefOf.RadioComms_Ambience
		};
		Find.WindowStack.Add(dialog_Negotiation);
	}

	public Faction GetFaction()
	{
		return null;
	}

	public FloatMenuOption CommFloatMenuOption(RimWorld.Building_CommsConsole console, Pawn negotiator)
	{
		Log.Warning("CommFloatMenuOption");
		if (!ShouldTechCoreAvailable || !RequestTechCoreNotification.ShouldTechCoreAvailable)
		{
			return null;
		}

		string text = "CallOnRadio".Translate(GetCallLabel());
		return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, delegate
		{
			console.GiveUseCommsJob(negotiator, this);
		}, techCoreIcon, Color.white, MenuOptionPriority.InitiateSocial), negotiator, console);
	}
}

[StaticConstructorOnStartup]
public static class Start
{
	static Start()
	{
		new Harmony("com.RunningBugs.PolarisblocExt").PatchAll();
	}
}

[HarmonyPatch(typeof(Building_CommsConsole), "GetFloatMenuOptions")]
public static class Patch_Building_CommsConsole_GetFloatMenuOptions
{
	public static IEnumerable<FloatMenuOption> Postfix(IEnumerable<FloatMenuOption> __result, Building_CommsConsole __instance, Pawn myPawn)
	{
		TechCoreGiver techCoreGiver = TechCoreGiver.Instance;
		if (techCoreGiver.ShouldTechCoreAvailable)
		{
			FloatMenuOption floatMenuOption = techCoreGiver.CommFloatMenuOption(__instance, myPawn);
			if (floatMenuOption != null)
			{
				yield return floatMenuOption;
			}
		}

		foreach (var item in __result)
		{
			yield return item;
		}
	}
}

