using System.Runtime.Loader;
using System.Text.Json;

string snapshot = Path.GetFullPath(args.Length == 1 ? args[0] : "/home/lisanhu/mine/workspace/rimworld/RW/17/Assemblies");
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string path = Path.Combine(snapshot, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
var results = WaylithRuntimeFixes.CheckSuite.Run(native: false);
Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
return results.Passed ? 0 : 1;
