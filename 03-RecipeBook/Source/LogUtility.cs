using Verse;

namespace _RecipeBook
{
    public static class Log
    {
        private static string prefix = "[RecipeBook] ";

        public static void Message(string message)
        {
            Verse.Log.Message(prefix + message);
        }

        public static void Warning(string message)
        {
            Verse.Log.Warning(prefix + message);
        }

        public static void Error(string message)
        {
            Verse.Log.Error(prefix + message);
        }
    }
}
