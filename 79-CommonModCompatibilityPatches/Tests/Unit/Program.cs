using System.Reflection;
using System.Runtime.Loader;
const string snapshot="/home/lisanhu/mine/workspace/rimworld/RW/17/Assemblies";
const string harmony="/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/.nuget/packages/lib.harmony/3.0.0-rc.2/lib/net10.0/0Harmony.dll";
AssemblyLoadContext.Default.Resolving+=(c,n)=>n.Name=="0Harmony"?c.LoadFromAssemblyPath(harmony):File.Exists(Path.Combine(snapshot,n.Name+".dll"))?c.LoadFromAssemblyPath(Path.Combine(snapshot,n.Name+".dll")):null;
var product=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath("expansion/work/79-CommonModCompatibilityPatches/1.7/Assemblies/CommonModCompatibilityPatches.dll"));
var uri=product.GetType("CommonModCompatibilityPatches.VbeVideoPathEscapingCompatibility")!.GetMethod("Postfix")!;
var cases=new(string? before,string? after)[]{(null,null),("",""),("relative path/video.webm","relative path/video.webm"),("https://example.invalid/a.webm","https://example.invalid/a.webm"),("file:///tmp/already%20done.webm","file:///tmp/already%20done.webm"),("/tmp/a folder/b #1.webm",new Uri("/tmp/a folder/b #1.webm").AbsoluteUri),("/tmp/中文/a.webm",new Uri("/tmp/中文/a.webm").AbsoluteUri)};
foreach(var c in cases) {object?[] callArgs={c.before};uri.Invoke(null,callArgs);if((string?)callArgs[0]!=c.after)throw new Exception("Actual VBE URI production helper mismatch: "+c.before);}
var copy=product.GetType("CommonModCompatibilityPatches.ListerHaulablesSharedScanCursorCompatibility")!.GetMethod("CopyContents",BindingFlags.NonPublic|BindingFlags.Static)!;
foreach(var values in new[]{Array.Empty<int>(),new[]{0},new[]{1,2,3}}) {var source=values.ToList();var target=new List<int>{9,8};copy.Invoke(null,new object[]{target,source});if(!target.SequenceEqual(values)||!source.SequenceEqual(values))throw new Exception("Actual cursor-list copy changed source or failed replacement");source.Add(7);if(target.Count!=values.Length)throw new Exception("Production copy aliases input");}
Console.WriteLine("PASS: 7 actual production URI boundaries (no VideoPlayer/playback claim), 3 actual cursor copy/clear/source-independent cases. No game bootstrap/Harmony patching/native inputs executed.");
