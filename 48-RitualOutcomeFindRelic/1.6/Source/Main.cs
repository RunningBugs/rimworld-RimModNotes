using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LudeonTK;
using RimWorld;
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
    // Method to generate and send a quest to the player
    [DebugAction("Quests", "GenerateAndSendFindRelicQuest", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void DebugAction()
    {
        GenerateAndSendQuest();
    }

    public static void GenerateAndSendQuest()
    {
        if (!ModLister.CheckIdeology("Relic hunt rescue"))
        {
            return;
        }

        QuestScriptDef questScriptDef = MyDefOf.RelicHunt;
        Ideo primaryIdeo = Faction.OfPlayer.ideos.PrimaryIdeo;
        if ((from p in primaryIdeo.GetAllPreceptsOfType<Precept_Relic>()
             where p.CanGenerateRelic
             select p).Count() == 0)
        {
            Messages.Message("NoRelicsToGenerate".Translate(primaryIdeo.name), MessageTypeDefOf.RejectInput, false);
            return;
        }

        // Find existing RelicHunt quests that are ongoing
        Quest existingRelicHuntQuest = Find.QuestManager.QuestsListForReading
            .Where(q => q.root.defName == MyDefOf.RelicHunt.defName && q.State <= QuestState.Ongoing)
            .FirstOrDefault();

        if (existingRelicHuntQuest == null)
        {
            Slate slate = new();
            if (!questScriptDef.CanRun(slate, Find.World))
            {
                Log.Error($"Cannot generate quest {questScriptDef.defName} with the current parameters.");
                return;
            }
            try
            {
                existingRelicHuntQuest = QuestUtility.GenerateQuestAndMakeAvailable(questScriptDef, slate);
            }
            catch (System.Exception e)
            {
                Log.Error($"Error generating quest possibly due to no available relic to generate: {e}");
                return;
            }
            QuestUtility.SendLetterQuestAvailable(existingRelicHuntQuest);
        }

        GenerateSubquestForExistingRelicHunt(existingRelicHuntQuest);
    }

    private static void GenerateSubquestForExistingRelicHunt(Quest relicHuntQuest)
    {
        // Find the subquest generator in the quest
        QuestPart_SubquestGenerator_RelicHunt subquestGenerator = relicHuntQuest.PartsListForReading
            .OfType<QuestPart_SubquestGenerator_RelicHunt>()
            .FirstOrDefault();

        if (subquestGenerator == null)
        {
            return;
        }

        // // Get the relic from the quest
        // Precept_Relic relic = null;
        // foreach (QuestPart part in relicHuntQuest.PartsListForReading)
        // {
        //     if (part is QuestPart_SubquestGenerator_RelicHunt subquestGen)
        //     {
        //         relic = subquestGen.relic;
        //         break;
        //     }
        // }

        // if (relic == null)
        // {
        //     return;
        // }
        if (subquestGenerator.relic == null)
        {
            return;
        }

        // Trigger the subquest generation manually
        GenerateSubquest(subquestGenerator, relicHuntQuest);
        return;
    }

    private static void GenerateSubquest(QuestPart_SubquestGenerator_RelicHunt subquestGenerator, Quest parent)
    {
        if (parent.GetSubquests().Count(q => q.State <= QuestState.Ongoing) >= 2)
        {
            Messages.Message("TooManyOngoingOrNotAcceptedSubquests".Translate(), MessageTypeDefOf.RejectInput, false);
            return;
        }

        // MethodInfo methodInfo = typeof(QuestPart_SubquestGenerator).GetMethod("TryGenerateSubquest", BindingFlags.NonPublic | BindingFlags.Instance);
        // methodInfo.Invoke(subquestGenerator, null);
        subquestGenerator.TryGenerateSubquest();

        // MethodInfo methodInfo = typeof(QuestPart_SubquestGenerator_RelicHunt).GetMethod("GetNextSubquestDef", BindingFlags.NonPublic | BindingFlags.Instance);
        // QuestScriptDef nextSubquestDef = (QuestScriptDef)methodInfo.Invoke(subquestGenerator, null);
        // if (nextSubquestDef != null)
        // {

        //     methodInfo = typeof(QuestPart_SubquestGenerator_RelicHunt).GetMethod("InitSlate", BindingFlags.NonPublic | BindingFlags.Instance);
        //     Slate vars = (Slate)methodInfo.Invoke(subquestGenerator, null);
        //     Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(nextSubquestDef, vars);
        //     quest.parent = subquestGenerator.quest;
        //     if (!quest.hidden && quest.root.sendAvailableLetter)
        //     {
        //         QuestUtility.SendLetterQuestAvailable(quest);
        //     }
        // }
    }


    public override void Apply(Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual, RitualOutcomePossibility outcome, out string extraOutcomeDesc, ref LookTargets letterLookTargets)
    {
        if (Rand.Chance(Settings.FindRelicChance))
        {
            GenerateAndSendQuest();
            extraOutcomeDesc = def.letterInfoText;
        }
        else
        {
            extraOutcomeDesc = null;
        }
    }
}


