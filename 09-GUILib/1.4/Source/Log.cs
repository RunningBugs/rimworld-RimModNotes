using System.Runtime.CompilerServices;

namespace SimpleGUI
{
    public static class Log
    {
        public static string prefix = "Logger";
        public static void Message(string msg, [CallerFilePath] string fileName = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Verse.Log.Message($"[ {prefix} {fileName}:{lineNumber} {memberName} ] " + msg);
        }

        public static void Warning(string msg, [CallerFilePath] string fileName = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Verse.Log.Warning($"[ {prefix} {fileName}:{lineNumber} {memberName} ] " + msg);
        }

        public static void Error(string msg, [CallerFilePath] string fileName = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
            Verse.Log.Error($"[ {prefix} {fileName}:{lineNumber} {memberName} ] " + msg);
        }
    }
}
