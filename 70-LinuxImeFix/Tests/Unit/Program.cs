using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
public static class Program
{
 public static void Main()
 {
  const string snapshot="/home/lisanhu/mine/workspace/rimworld/RW/17/Assemblies";
  AssemblyLoadContext.Default.Resolving+=(c,n)=>File.Exists(Path.Combine(snapshot,n.Name+".dll"))?c.LoadFromAssemblyPath(Path.Combine(snapshot,n.Name+".dll")):null;
  AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(snapshot,"UnityStubs.dll"));
  AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(snapshot,"Assembly-CSharp.dll"));
  Checks.Run();
 }
}
public static class Checks
{
 private static readonly System.Collections.Generic.List<(int key,int state)> forwarded=new();
 public static int CaptureKey(int key,int code,int state) { forwarded.Add((key,state)); return 0; }
 [MethodImpl(MethodImplOptions.NoInlining)]
 public static void Run()
 {
  var type=typeof(LinuxImeFix.LinuxImeUtility); var flags=BindingFlags.NonPublic|BindingFlags.Static;
  var insert=type.GetMethod("InsertCommit",flags);
  foreach(var c in new[] { ("abcd",1,3,"中文","a中文d",3),("abcd",3,1,"X","aXd",2),("abc",-9,99,"🙂","🙂",2),("abc",1,1,"Z","aZbc",2),("",0,0,"你好","你好",2),("abc",3,3,"","abc",3) })
  {
   object[] args={c.Item1,c.Item2,c.Item3,c.Item4,0}; var actual=(string)insert.Invoke(null,args);
   if(actual!=c.Item5 || (int)args[4]!=c.Item6) throw new Exception("Commit range/Unicode caret mismatch");
  }
  var key=type.GetMethod("TryGetIBusKeyval",flags);
  foreach(var c in new[] { (UnityEngine.KeyCode.Backspace,'\b',0xff08),(UnityEngine.KeyCode.Delete,'\0',0xffff),(UnityEngine.KeyCode.LeftArrow,'\0',0xff51),(UnityEngine.KeyCode.Return,'\r',0xff0d),(UnityEngine.KeyCode.KeypadEnter,'\0',0xff0d),(UnityEngine.KeyCode.None,'a',(int)'a'),(UnityEngine.KeyCode.LeftShift,'\0',0xffe1) })
  { object[] args={c.Item1,c.Item2,0}; if(!(bool)key.Invoke(null,args)||(int)args[2]!=c.Item3) throw new Exception("IBus keysym mapping mismatch"); }
  object[] empty={UnityEngine.KeyCode.None,'\0',0}; if((bool)key.Invoke(null,empty)) throw new Exception("Empty event became IBus key");
  var native=typeof(LinuxImeFix.NativeBridge); var available=native.GetField("available",flags); var process=native.GetField("pProcessKey",flags);
  var oldAvailable=available.GetValue(null); var oldProcess=process.GetValue(null); int oldControl=UnityEngine.GUIUtility.keyboardControl;
  var poll=type.GetMethod("ForwardModifierChanges",flags); var was=(bool[])type.GetField("modifierWasDown",flags).GetValue(null);
  try
  {
   UnityEngine.InputSystem.InputSystem.ResetAllInput(); Array.Clear(was); UnityEngine.GUIUtility.keyboardControl=17001;
   process.SetValue(null,Delegate.CreateDelegate(process.FieldType,typeof(Checks).GetMethod(nameof(CaptureKey)))); available.SetValue(null,true);
   // A real low-FPS PollEvents can deliver both physical edges before OnGUI.
   UnityEngine.InputSystem.InputSystem.QueueKeyChange(UnityEngine.InputSystem.Key.LeftShift,true);
   UnityEngine.InputSystem.InputSystem.QueueKeyChange(UnityEngine.InputSystem.Key.LeftShift,false);
   if(UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift) || !UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftShift) || !UnityEngine.Input.GetKeyUp(UnityEngine.KeyCode.LeftShift))
    throw new Exception("Actual raw input must retain both edges while final held state is released");
   int tapStart=forwarded.Count; poll.Invoke(null,null); poll.Invoke(null,null);
   if(forwarded.Count!=tapStart+2 || forwarded[tapStart]!=(0xffe1,1) || forwarded[tapStart+1]!=(0xffe1,0x40000001))
    throw new Exception("Real same-frame Shift tap was lost or duplicated by repeated OnGUI polling");
   UnityEngine.InputSystem.InputSystem.BeginFrame();
   poll.Invoke(null,null);
   if(forwarded.Count!=tapStart+2) throw new Exception("Cleared real raw edge flags replayed an old tap");
   // The opposite real edge pair starts with an already-held focused modifier.
   UnityEngine.InputSystem.InputSystem.QueueKeyChange(UnityEngine.InputSystem.Key.RightShift,true); was[1]=true;
   UnityEngine.InputSystem.InputSystem.BeginFrame();
   UnityEngine.InputSystem.InputSystem.QueueKeyChange(UnityEngine.InputSystem.Key.RightShift,false);
   UnityEngine.InputSystem.InputSystem.QueueKeyChange(UnityEngine.InputSystem.Key.RightShift,true);
   int heldPairStart=forwarded.Count; poll.Invoke(null,null); poll.Invoke(null,null);
   if(forwarded.Count!=heldPairStart+2 || forwarded[heldPairStart]!=(0xffe2,0x40000001) || forwarded[heldPairStart+1]!=(0xffe2,1))
    throw new Exception("Actual same-frame held Shift release/repress was lost, reversed or duplicated");
   UnityEngine.InputSystem.InputSystem.ResetAllInput(); Array.Clear(was);
   foreach(var entry in new[] { (UnityEngine.InputSystem.Key.LeftShift,true,0xffe1,1),(UnityEngine.InputSystem.Key.RightShift,true,0xffe2,1),(UnityEngine.InputSystem.Key.LeftShift,false,0xffe1,0x40000001),(UnityEngine.InputSystem.Key.RightShift,false,0xffe2,0x40000001),(UnityEngine.InputSystem.Key.LeftCtrl,true,0xffe3,4),(UnityEngine.InputSystem.Key.LeftCtrl,false,0xffe3,0x40000004),(UnityEngine.InputSystem.Key.LeftAlt,true,0xffe9,8),(UnityEngine.InputSystem.Key.LeftAlt,false,0xffe9,0x40000008) })
   {
    int before=forwarded.Count; UnityEngine.InputSystem.InputSystem.QueueKeyChange(entry.Item1,entry.Item2); poll.Invoke(null,null); poll.Invoke(null,null);
    if(forwarded.Count!=before+1 || forwarded[^1]!=(entry.Item3,entry.Item4)) throw new Exception("Actual focused raw modifier transition, release mask or duplicate suppression failed");
   }
  }
  finally { available.SetValue(null,oldAvailable); process.SetValue(null,oldProcess); UnityEngine.InputSystem.InputSystem.ResetAllInput(); UnityEngine.GUIUtility.keyboardControl=oldControl; }
  Console.WriteLine("PASS: same-frame real raw Shift down/up, held release/repress and cleared-edge/repeated-OnGUI control; 8 actual UnityStubs raw modifier transitions with focused text control, masks, dual Shift and duplicate suppression; managed capture sink, no IBus connection");
  Console.WriteLine("PASS: 6 actual commit boundary/Unicode/selection cases; 8 actual IBus key conversion cases; no native init or desktop D-Bus calls");
  // Optional compatibility must be inactive when absent, then resolve the real
  // installed upstream contract without running DubGUI's graphical initializer.
  if(LinuxImeFix.DubsMintMenus_InputField_Patch.Prepare() || LinuxImeFix.DubsMintMenus_InputField_Patch.TargetMethod()!=null)
   throw new Exception("Absent Dubs should not create a patch target");
  const string dubsPath="/Data/SteamLibrary/steamapps/workshop/content/294100/1446523594/1.7/Assemblies/DubsMintMenus.dll";
  var dubs=AssemblyLoadContext.Default.LoadFromAssemblyPath(dubsPath);
  var dubsType=dubs.GetType("DubsMintMenus.DubGUI",throwOnError:true);
  var exact=dubsType.GetMethod("InputField",BindingFlags.Public|BindingFlags.Static,null,
   new[] { typeof(string),typeof(UnityEngine.Rect),typeof(string).MakeByRefType(),typeof(UnityEngine.Texture2D),typeof(int),typeof(bool),typeof(bool),typeof(bool),typeof(string) },null);
  if(exact==null || exact.ReturnType!=typeof(bool) || !LinuxImeFix.DubsMintMenus_InputField_Patch.Prepare()
   || LinuxImeFix.DubsMintMenus_InputField_Patch.TargetMethod()!=exact)
   throw new Exception("Actual Dubs1.7 nine-parameter optional ghost contract not selected");
  if(!exact.GetParameters()[8].IsOptional || exact.GetParameters()[8].Name!="ghost" || exact.GetParameters()[2].Name!="buff")
   throw new Exception("Actual Dubs field/ref-buff semantic contract changed");
  Console.WriteLine("PASS: absent optional Dubs gate and actual installed Dubs1.7 nine-argument metadata target; no fake upstream or GUI/native bridge execution");
 }
}
