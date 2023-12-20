using Verse;
using RimWorld;
using Logger;
using Log = Logger.Log;

// using System.Reflection;
// using HarmonyLib;

namespace QuestItemWatch
{
    [DefOf]
    public class TemplateDefOf
    {
        public static LetterDef success_letter;
    }

    public class QuestItemWatcher : GameComponent 
    {
        public QuestItemWatcher(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            Find.QuestManager.
        }
    }

    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Log.Message("QuestItemWatch loaded successfully!");
            var harmony = new HarmonyLib.Harmony("com.runningbugs.questitemwatch");
            harmony.PatchAll();
        }
    }

}
