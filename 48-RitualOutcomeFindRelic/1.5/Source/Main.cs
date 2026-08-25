using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace FindRelic;

public class FR_GameComp : GameComponent
{
    public FR_GameComp(Game game)
    {
    }

    public static string RefreshEffectDesc(string effectDesc)
    {
        // Only do the replacement if "{0}" doesn't exist
        if (!effectDesc.Contains("{0}"))
        {
            int percentIndex = effectDesc.IndexOf('%');

            if (percentIndex > 0)
            {
                // Find where the number sequence starts before the '%'
                int numStart = percentIndex;

                // Go backwards until we find a non-digit and non-decimal character
                bool foundDecimal = false;

                while (numStart > 0)
                {
                    char c = effectDesc[numStart - 1];

                    // If we find a decimal point, it's only valid if we haven't found one before
                    // and we've found at least one digit after it
                    if (c == '.' && !foundDecimal && numStart < percentIndex)
                    {
                        foundDecimal = true;
                        numStart--;
                    }
                    // If we find a digit, continue backwards
                    else if (char.IsDigit(c))
                    {
                        numStart--;
                    }
                    // If it's not a digit or valid decimal point, stop
                    else
                    {
                        break;
                    }
                }

                // Only proceed if we found at least one digit
                if (numStart < percentIndex)
                {
                    // Rebuild the string with the "{0}" replacement
                    effectDesc = effectDesc.Substring(0, numStart) +
                                 "{0}" +
                                 effectDesc.Substring(percentIndex + 1);
                }
            }
        }

        // Apply the translation with the percentage value
        return effectDesc.Translate(Settings.FindRelicChance.ToStringPercent("0"));
    }

    public override void FinalizeInit()
    {
        // Apply the translation with the percentage value
        MyDefOf.FindRelic.effectDesc = RefreshEffectDesc(MyDefOf.FindRelic.effectDesc);
    }
}

[StaticConstructorOnStartup]
public class Settings : ModSettings
{
    public static float FindRelicChance = 1f;

    public Settings()
    {
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref FindRelicChance, "FindRelicChance", 1f);
    }

}

public class SettingsUI : Mod
{
    public SettingsUI(ModContentPack content) : base(content)
    {
        this.modSettings = GetSettings<Settings>();
    }


    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listingStandard = new();
        listingStandard.Begin(inRect);
        listingStandard.Label("FindRelicChance".Translate(Settings.FindRelicChance.ToStringPercent("0")));
        Settings.FindRelicChance = listingStandard.Slider(Settings.FindRelicChance, 0f, 1f);
        listingStandard.End();

        MyDefOf.FindRelic.effectDesc = FR_GameComp.RefreshEffectDesc(MyDefOf.FindRelic.effectDesc);
    }

    public override string SettingsCategory()
    {
        return "FindRelic".Translate();
    }
}

[DefOf]
public class MyDefOf
{
    public static QuestScriptDef RelicHunt;
    public static RitualAttachableOutcomeEffectDef FindRelic;
}

public class RitualAttachableOutcomeEffectWorker_FindRelic : RitualAttachableOutcomeEffectWorker
{
    [DebugAction("Quests", "GenerateAndSendFindRelicQuest", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void DebugAction()
    {
        GenerateAndSendQuest();
    }

    // Returns true only when a relic info subquest was actually generated,
    // so the ritual outcome text doesn't claim success when nothing happened.
    public static bool GenerateAndSendQuest()
    {
        if (!ModLister.CheckIdeology("Relic hunt rescue"))
        {
            return false;
        }

        QuestScriptDef questScriptDef = MyDefOf.RelicHunt;
        Ideo primaryIdeo = Faction.OfPlayer.ideos.PrimaryIdeo;
        if (!primaryIdeo.GetAllPreceptsOfType<Precept_Relic>().Any(p => p.CanGenerateRelic))
        {
            Messages.Message("NoRelicsToGenerate".Translate(primaryIdeo.name), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        // Find existing RelicHunt quests that are ongoing
        Quest existingRelicHuntQuest = Find.QuestManager.QuestsListForReading
            .Where(q => q.root.defName == MyDefOf.RelicHunt.defName && q.State <= QuestState.Ongoing)
            .FirstOrDefault();

        if (existingRelicHuntQuest == null)
        {
            Slate slate = new();
            // The vanilla epic quest channel passes scaled threat points; an empty slate
            // would leave points=0 and the relic site would get minimum-strength security.
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(Find.World));
            if (!questScriptDef.CanRun(slate))
            {
                Log.Error($"Cannot generate quest {questScriptDef.defName} with the current parameters.");
                return false;
            }
            try
            {
                existingRelicHuntQuest = QuestUtility.GenerateQuestAndMakeAvailable(questScriptDef, slate);
            }
            catch (System.Exception e)
            {
                Log.Error($"Error generating quest possibly due to no available relic to generate: {e}");
                return false;
            }
            QuestUtility.SendLetterQuestAvailable(existingRelicHuntQuest);
        }

        return GenerateSubquestForExistingRelicHunt(existingRelicHuntQuest);
    }

    private static bool GenerateSubquestForExistingRelicHunt(Quest relicHuntQuest)
    {
        // Find the subquest generator in the quest
        QuestPart_SubquestGenerator_RelicHunt subquestGenerator = relicHuntQuest.PartsListForReading
            .OfType<QuestPart_SubquestGenerator_RelicHunt>()
            .FirstOrDefault();

        if (subquestGenerator == null || subquestGenerator.relic == null)
        {
            return false;
        }

        return GenerateSubquest(subquestGenerator, relicHuntQuest);
    }

    private static bool GenerateSubquest(QuestPart_SubquestGenerator_RelicHunt subquestGenerator, Quest parent)
    {
        // Mirror the vanilla limits from QuestPart_SubquestGenerator.CanGenerateSubquest
        int pendingCount = parent.GetSubquests().Count(q => q.State <= QuestState.Ongoing);
        if (pendingCount >= subquestGenerator.maxActiveSubquests)
        {
            Messages.Message("TooManyOngoingOrNotAcceptedSubquests".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        int successfulCount = parent.GetSubquests(QuestState.EndedSuccess).Count();
        if (subquestGenerator.maxSuccessfulSubquests >= 0
            && successfulCount + pendingCount >= subquestGenerator.maxSuccessfulSubquests)
        {
            Messages.Message("RelicInfoGatheringComplete".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        if (TryGenerateAnySubquest(subquestGenerator, parent))
        {
            return true;
        }

        Messages.Message("FindRelicNoSubquestAvailable".Translate(), MessageTypeDefOf.RejectInput, false);
        return false;
    }

    // The vanilla 1.5 private GenerateSubquest() returns void and silently swallows
    // failures, so we generate the subquest ourselves and try every def until one runs.
    private static bool TryGenerateAnySubquest(QuestPart_SubquestGenerator_RelicHunt subquestGenerator, Quest parent)
    {
        foreach (QuestScriptDef subquestDef in subquestGenerator.subquestDefs.InRandomOrder())
        {
            Slate vars = subquestGenerator.InitSlate();
            if (!subquestDef.CanRun(vars))
            {
                continue;
            }
            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(subquestDef, vars);
            quest.parent = parent;
            if (!quest.hidden && quest.root.sendAvailableLetter)
            {
                QuestUtility.SendLetterQuestAvailable(quest);
            }
            return true;
        }

        return false;
    }

    public override void Apply(Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual, RitualOutcomePossibility outcome, out string extraOutcomeDesc, ref LookTargets letterLookTargets)
    {
        if (Rand.Chance(Settings.FindRelicChance) && GenerateAndSendQuest())
        {
            extraOutcomeDesc = def.letterInfoText;
        }
        else
        {
            extraOutcomeDesc = null;
        }
    }
}
