using RimWorld;
using RimWorld.Planet;
using Verse;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

namespace AlertUtility
{
    [StaticConstructorOnStartup]
    public static class LoadingScreen
    {
        static LoadingScreen()
        {
            Log.Message("AlertUtility loaded successfully!");
            var harmony = new Harmony("com.RunningBugs.AlertUtility");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    public static class StringExt
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }
    }

    public class AlertUtility : WorldComponent
    {
        public class Event : IExposable
        {
            public int presetGameTicksToAlert;
            public string message;

            public Event()
            {
                presetGameTicksToAlert = 0;
                message = "";
            }

            public Event(int tickTime, string msg)
            {
                presetGameTicksToAlert = tickTime;
                message = msg;
            }

            public void ExposeData()
            {
                Scribe_Values.Look(ref presetGameTicksToAlert, "RunningBugs.AlertUtility.Event.ticks");
                Scribe_Values.Look(ref message, "RunningBugs.AlertUtility.Event.message");
            }
        }

        private static int defaultInterval = 60;   //  Check on every second in the slow speed
        private static List<Event> events = new List<Event>();

        public AlertUtility(World world) : base(world)
        {
            Log.Warning("AlertUtility Initialized");
        }

        public static void Add(Event e)
        {
            events.Add(e);
        }

        public static List<Event> GetEvents()
        {
            return events;
        }

        //[HarmonyPostfix]
        public static void WorldComponentTickPostfix()
        {
            if (Find.World != null)
            {
                int ticks = Find.TickManager.TicksGame;
                if (ticks % defaultInterval == 0)
                {
                    List<Event> eventsToRemove = new List<Event>();
                    foreach (var e in events)
                    {
                        if (ticks >= e.presetGameTicksToAlert)
                        {
                            // Trigger Alert then remove this element
                            Find.LetterStack.ReceiveLetter("TimerTimeOut".Translate(e.message.Truncate(5)), e.message, LetterDefOf.NeutralEvent);
                            Find.TickManager.Pause();
                            eventsToRemove.Add(e);
                        }
                    }
                    foreach (var e in eventsToRemove)
                    {
                        events.Remove(e);
                    }
                }
            }
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            WorldComponentTickPostfix();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref events, "RunningBugs.AlertUtility.events", LookMode.Deep);
        }
    }

    public static class Log
    {
        public static void Message(string message)
        {
            Verse.Log.Message("[AlertUtility] " + message);
        }

        public static void Warning(string message)
        {
            Verse.Log.Warning("[AlertUtility] " + message);
        }

        public static void Error(string message)
        {
            Verse.Log.Error("[AlertUtility] " + message);
        }
    }
}
