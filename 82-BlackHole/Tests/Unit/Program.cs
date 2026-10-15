using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using BlackHole;
using RimWorld;
using Verse;
public static class Program
{
 public static void Main()
 {
  const string dir="/home/lisanhu/mine/workspace/rimworld/RW/17/Assemblies";
  AssemblyLoadContext.Default.Resolving+=(c,n)=>File.Exists(Path.Combine(dir,n.Name+".dll"))?c.LoadFromAssemblyPath(Path.Combine(dir,n.Name+".dll")):null;
  AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dir,"UnityStubs.dll")); AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dir,"Assembly-CSharp.dll")); Checks.Run();
 }
}
public static class Checks
{
 [MethodImpl(MethodImplOptions.NoInlining)]
 public static void Run()
 {
  var shared=new ThingDef { defName="UnitOnly",category=ThingCategory.Item,stackLimit=75 };
  var first=new Thing { def=shared,stackCount=10 }; var second=new Thing { def=shared,stackCount=8 }; var outsider=new Thing { def=shared,stackCount=1 };
  var a=new TransferableOneWay(); a.things.Add(first); var b=new TransferableOneWay(); b.things.Add(second); var hole=new CompBlackHole();
  hole.AddToTheToLoadList(a,4); hole.AddToTheToLoadList(b,3);
  if(hole.leftToLoad.Count!=2 || !ReferenceEquals(hole.leftToLoad[0],a) || !ReferenceEquals(hole.leftToLoad[1],b) || a.CountToTransfer!=4 || b.CountToTransfer!=3) throw new Exception("Distinct same-def transferables merged or their requested counts changed");
  if(hole.FirstThingLeftToLoad!=first || !hole.AnythingLeftToLoad || !hole.LeftToLoadContains(second) || hole.LeftToLoadContains(outsider)) throw new Exception("Cargo membership/order query mismatch");
  a.AdjustTo(0); if(hole.FirstThingLeftToLoad!=second) throw new Exception("Zero-count cargo still chosen first"); b.things.Clear(); if(hole.FirstThingLeftToLoad!=null || hole.AnythingLeftToLoad) throw new Exception("Missing actual thing treated as loadable cargo");
  hole.AddToTheToLoadList(new TransferableOneWay(),0); hole.AddToTheToLoadList(new TransferableOneWay(),-1); if(hole.leftToLoad.Count!=2) throw new Exception("Nonpositive cargo requests mutated queue");
  var graphic=new Graphic_Twinkle(); var sub=typeof(Graphic_Collection).GetField("subGraphics",BindingFlags.NonPublic|BindingFlags.Instance); var frame=typeof(Graphic_Twinkle).GetMethod("FrameIndex",BindingFlags.NonPublic|BindingFlags.Instance);
  int cases=0; foreach(int count in new[] { 1,6,29 }) { sub.SetValue(graphic,new Graphic[count]); foreach(int id in new[] { 0,1,-1,int.MaxValue,int.MinValue }) { int ix=(int)frame.Invoke(graphic,new object[] { new Thing { thingIDNumber=id } }); if(ix<0||ix>=count) throw new Exception("Actual frame selector overflow escaped valid bounds"); cases++; } }
  Console.WriteLine("PASS: actual cargo same-def segregation/count/membership/order/empty/nonpositive regression groups; "+cases+" actual frame-index overflow/range cases (SDK time=0, no animation claim)");
 }
}
