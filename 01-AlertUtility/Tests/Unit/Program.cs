using System;
using AlertUtility;

if (StringExt.Truncate(null, 5) != null || StringExt.Truncate("", 5) != ""
    || StringExt.Truncate("abc", 5) != "abc" || StringExt.Truncate("abc", 3) != "abc"
    || StringExt.Truncate("abcdef", 3) != "abc..." || StringExt.Truncate("abc", 0) != "...")
    throw new Exception("StringExt.Truncate edge case failed");
try { StringExt.Truncate("abc", -1); throw new Exception("Expected negative limit exception"); }
catch (ArgumentOutOfRangeException) { }
Console.WriteLine("PASS: 7 production StringExt.Truncate edge cases");
