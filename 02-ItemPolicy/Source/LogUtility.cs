using Verse;

namespace _ItemPolicy
{
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
