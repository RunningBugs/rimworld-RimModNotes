using System.Runtime.CompilerServices;

namespace Logger
{
    public static class Log
    {
        public static string prefix = "ArmorRacksForceWearFix";
        public static void Message(string msg, bool details = false, [CallerFilePath] string fileName = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            string message = details ? $"[ {prefix} {fileName}:{lineNumber} {memberName} ] " + msg : $"[ {prefix} ] " + msg;
            Verse.Log.Message(message);
        }

        public static void Warning(string msg, bool details = false, [CallerFilePath] string fileName = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            string message = details ? $"[ {prefix} {fileName}:{lineNumber} {memberName} ] " + msg : $"[ {prefix} ] " + msg;
            Verse.Log.Warning(message);
        }

        public static void Error(string msg, bool details = false, [CallerFilePath] string fileName = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            string message = details ? $"[ {prefix} {fileName}:{lineNumber} {memberName} ] " + msg : $"[ {prefix} ] " + msg;
            Verse.Log.Error(message);
        }
    }
}