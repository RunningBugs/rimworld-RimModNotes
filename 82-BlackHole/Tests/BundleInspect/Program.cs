using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
const string snapshot="/home/lisanhu/mine/workspace/rimworld/RW/17/Assemblies";
AssemblyLoadContext.Default.Resolving+=(c,n)=>File.Exists(Path.Combine(snapshot,n.Name+".dll"))?c.LoadFromAssemblyPath(Path.Combine(snapshot,n.Name+".dll")):null;
var stubs=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(snapshot,"UnityStubs.dll"));
var readerType=stubs.GetType("UnityStubs.Internal.AssetBundleReader",true);
var unityShader=stubs.GetType("UnityStubs.Internal.UnityShaderAsset",true);
var builder=stubs.GetType("UnityStubs.Internal.AssetBundleShaders",true);
var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
var report=new System.Collections.Generic.List<object>();
foreach(var path in args)
{
 using var stream=File.OpenRead(Path.GetFullPath(path));
 var reader=readerType.GetMethod("Open").Invoke(null,new object[] { stream,Path.GetFullPath(path),false });
 try
 {
  var entries=(IEnumerable)readerType.GetField("_container",flags).GetValue(reader);
  dynamic manager=readerType.GetProperty("Manager",flags).GetValue(reader);
  foreach(var entry in entries)
  {
   var pairType=entry.GetType(); var name=(string)pairType.GetProperty("Key").GetValue(entry); var asset=pairType.GetProperty("Value").GetValue(entry); var aType=asset.GetType();
   if(aType.GetField("Class").GetValue(asset).ToString()!="Shader") continue;
   dynamic file=aType.GetField("File").GetValue(asset); dynamic pathId=aType.GetField("PathId").GetValue(asset);
   object field=manager.GetBaseField(file,pathId);
   int vulkan=(int)unityShader.GetMethod("VulkanPlatformIndex").Invoke(null,new[] { field });
   object[] readArgs={field,null}; var parsed=unityShader.GetMethod("Read").Invoke(null,readArgs);
   object plan=null; byte[] vertex=null,fragment=null; string rewriteFailure=null;
   var passForRewrite=parsed==null?null:unityShader.GetField("Pass").GetValue(parsed);
   if(passForRewrite!=null)
   {
    var planType=stubs.GetType("UnityStubs.Internal.UnityShaderBindingPlan",true);
    plan=planType.GetMethod("Build").Invoke(null,new[] { passForRewrite });
    var rewrite=stubs.GetType("UnityStubs.Internal.SpirvModule",true).GetMethod("Rewrite");
    object[] vx={passForRewrite.GetType().GetField("VertexSpirv").GetValue(passForRewrite),4,passForRewrite,plan,null};
    object[] fx={passForRewrite.GetType().GetField("FragmentSpirv").GetValue(passForRewrite),8,passForRewrite,plan,null};
    vertex=(byte[])rewrite.Invoke(null,vx); fragment=(byte[])rewrite.Invoke(null,fx); rewriteFailure=(string)vx[4]??(string)fx[4];
   }
   int uniformSize=plan==null?0:(int)plan.GetType().GetField("UniformSize").GetValue(plan);
   int storageSlots=plan==null?0:((IDictionary)plan.GetType().GetField("StorageBinding").GetValue(plan)).Count;
   var pass=parsed==null?null:unityShader.GetField("Pass").GetValue(parsed);
   int Bytes(string fieldName)=>pass==null?0:((byte[])pass.GetType().GetField(fieldName).GetValue(pass)).Length;
   var uniformNames=new System.Collections.Generic.List<string>();
   if(pass!=null) foreach(var buffer in (IEnumerable)pass.GetType().GetField("CBuffers").GetValue(pass)) foreach(var parameter in (IEnumerable)buffer.GetType().GetField("Params").GetValue(buffer)) uniformNames.Add((string)parameter.GetType().GetField("Name").GetValue(parameter));
   report.Add(new { bundle=path,asset=name,vulkan_platform_index=vulkan,parsed=parsed!=null,parse_failure=readArgs[1],vertex_rewrite_bytes=vertex?.Length ?? 0,fragment_rewrite_bytes=fragment?.Length ?? 0,rewrite_failure=rewriteFailure,uniform_bytes=uniformSize,storage_slots=storageSlots,within_engine_limits=parsed!=null && uniformSize<=512 && storageSlots<=8,realtime_uniform_present=uniformNames.Contains("_RealTimeSecs"),uniform_names=uniformNames,vertex_spirv_bytes=Bytes("VertexSpirv"),fragment_spirv_bytes=Bytes("FragmentSpirv"),scope="CPU actual importer/SPIR-V rewrite; no GPU or visual claim" });
  }
 }
 finally { ((IDisposable)reader).Dispose(); }
}
Console.WriteLine(JsonSerializer.Serialize(report,new JsonSerializerOptions { WriteIndented=true }));
if(report.Count==0) throw new Exception("No actual shader assets found");
