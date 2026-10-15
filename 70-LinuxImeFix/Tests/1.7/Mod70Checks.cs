using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
namespace RW17.Validation;
public static class Mod70Checks
{
 public const string PackageId="RunningBugs.LinuxImeFix";
 public static void Run(Map map)
 {
  foreach(var target in new[] { AccessTools.Method(typeof(Widgets),nameof(Widgets.TextField),new[] { typeof(Rect),typeof(string) }), AccessTools.Method(typeof(Widgets),nameof(Widgets.TextArea),new[] { typeof(Rect),typeof(string),typeof(bool) }), AccessTools.Method(typeof(DevGUI),nameof(DevGUI.TextField),new[] { typeof(Rect),typeof(string) }), AccessTools.Method(typeof(UIRoot_Play),nameof(UIRoot_Play.UIRootOnGUI)), AccessTools.Method(typeof(UIRoot_Entry),nameof(UIRoot_Entry.UIRootOnGUI)) })
   if(Harmony.GetPatchInfo(target)?.Owners.Contains("com.RunningBugs.LinuxImeFix")!=true) throw new Exception("Mod70: concrete text/focus target not patched: "+target);
  var native=AccessTools.TypeByName("LinuxImeFix.NativeBridge");
  var dubs=AccessTools.TypeByName("DubsMintMenus.DubGUI");
  var optional=AccessTools.TypeByName("LinuxImeFix.DubsMintMenus_InputField_Patch");
  bool optionalPrepared=(bool)AccessTools.Method(optional,"Prepare").Invoke(null,null);
  var optionalTarget=(MethodBase)AccessTools.Method(optional,"TargetMethod").Invoke(null,null);
  if(dubs==null)
  {
   if(optionalPrepared || optionalTarget!=null) throw new Exception("Mod70: absent Dubs optional patch must stay inactive");
  }
  else
  {
   var expected=AccessTools.Method(dubs,"InputField",new[] { typeof(string),typeof(Rect),typeof(string).MakeByRefType(),typeof(Texture2D),typeof(int),typeof(bool),typeof(bool),typeof(bool),typeof(string) })
    ?? AccessTools.Method(dubs,"InputField",new[] { typeof(string),typeof(Rect),typeof(string).MakeByRefType(),typeof(Texture2D),typeof(int),typeof(bool),typeof(bool),typeof(bool) });
   if(expected!=null && (!optionalPrepared || optionalTarget!=expected || Harmony.GetPatchInfo(expected)?.Prefixes.Any(p=>p.owner=="com.RunningBugs.LinuxImeFix" && p.PatchMethod.DeclaringType==optional)!=true
    || Harmony.GetPatchInfo(expected)?.Postfixes.Any(p=>p.owner=="com.RunningBugs.LinuxImeFix" && p.PatchMethod.DeclaringType==optional)!=true))
    throw new Exception("Mod70: real Dubs input target must register the specific IME prefix and postfix");
   if(expected==null) throw new Exception("Mod70: current loaded Dubs input contract unsupported; a skipped optional patch does not prove Dubs IME compatibility");
   Console.WriteLine("RW17_MOD70_OPTIONAL_DUBS_TARGET="+expected);
  }
  if(Environment.GetCommandLineArgs().Any(a=>a.Equals("-Headless",StringComparison.OrdinalIgnoreCase)) && (bool)AccessTools.Field(native,"loadAttempted").GetValue(null)) throw new Exception("Mod70: Headless startup contacted native IBus");
  if(Environment.GetEnvironmentVariable("RW17_IME_UI")=="1")
  {
   if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IBUS_ADDRESS"))) throw new Exception("Mod70: dedicated GUI probe requires explicit isolated IBUS_ADDRESS");
   Find.WindowStack.Add(new Mod70GuiProbe());
  }
  Console.WriteLine("RW17_MOD70_CONCRETE_TEXT_PATCHES_AND_HEADLESS_IDLE_OK; real IBus/candidate/keyboard commits remain pending until GUI artifact");
 }
}
public sealed class Mod70GuiProbe : Window
{
 private string text="", lastArtifact=""; private bool focusRequested, sawPreedit, sawChineseCommit; private int maxCandidates;
 public override Vector2 InitialSize=>new Vector2(540,250);
 public Mod70GuiProbe() { forcePause=true; closeOnClickedOutside=false; closeOnAccept=false; closeOnCancel=false; doCloseX=true; absorbInputAroundWindow=true; Mod70FocusObservation.Install(); }
 public override void PostClose() { try { base.PostClose(); } finally { Mod70FocusObservation.Restore(); } }
 public override void DoWindowContents(Rect inRect)
 {
  Widgets.Label(new Rect(20,20,480,45),"Isolated Linux IBus test: type nihao and commit with Space; Shift toggles input mode.");
  GUI.SetNextControlName("RW17_LINUX_IME_PROBE");
  if(!focusRequested) { GUI.FocusControl("RW17_LINUX_IME_PROBE"); focusRequested=true; }
  if(Environment.GetEnvironmentVariable("RW17_IME_DUBS_UI")=="1")
  {
   var dubs=AccessTools.TypeByName("DubsMintMenus.DubGUI");
   var input=AccessTools.Method(dubs,"InputField",new[] { typeof(string),typeof(Rect),typeof(string).MakeByRefType(),typeof(Texture2D),typeof(int),typeof(bool),typeof(bool),typeof(bool),typeof(string) });
   if(input==null) throw new Exception("Mod70: dedicated Dubs GUI probe requires the actual 1.7 nine-argument input field");
   object[] args={"RW17_LINUX_IME_PROBE",new Rect(20,85,480,35),text,null,999,false,false,false,null};
   input.Invoke(null,args); text=(string)args[2];
  }
  else text=Widgets.TextField(new Rect(20,85,480,35),text);
  var native=AccessTools.TypeByName("LinuxImeFix.NativeBridge");
  var ready=(bool)AccessTools.Property(native,"IsReady").GetValue(null);
  var preedit=(string)AccessTools.Method(native,"GetPreedit").Invoke(null,null) ?? "";
  var count=(int)AccessTools.Method(native,"GetCandidateCount").Invoke(null,null);
  var utility=AccessTools.TypeByName("LinuxImeFix.LinuxImeUtility");
  var editor=GUIUtility.QueryStateObject(typeof(TextEditor),GUIUtility.keyboardControl) as TextEditor;
  sawPreedit |= !string.IsNullOrEmpty(preedit); maxCandidates = Math.Max(maxCandidates,count); sawChineseCommit |= text.Contains("你好",StringComparison.Ordinal);
  var artifact=JsonSerializer.Serialize(new { text,preedit,candidate_count=count,max_seen_candidates=maxCandidates,saw_preedit=sawPreedit,saw_chinese_commit=sawChineseCommit,ibus_ready=ready,keyboard_control=GUIUtility.keyboardControl,focused_control=AccessTools.Field(utility,"focusedControl").GetValue(null),saw_focused_field=AccessTools.Field(utility,"sawFocusedTextFieldThisFrame").GetValue(null),focused_control_name=GUI.GetNameOfFocusedControl(),editor_control=editor?.controlID,editor_rect=editor==null?null:new[] { editor.position.x,editor.position.y,editor.position.width,editor.position.height },focus_in_calls=Mod70FocusObservation.FocusInCalls,focus_out_calls=Mod70FocusObservation.FocusOutCalls,reset_calls=Mod70FocusObservation.ResetCalls,focus_call_trace=Mod70FocusObservation.Trace,chinese_commit=text.Contains("你好",StringComparison.Ordinal),field=Environment.GetEnvironmentVariable("RW17_IME_DUBS_UI")=="1"?"actual DubsMintMenus.DubGUI.InputField":"actual Widgets.TextField",scope="real OnGUI input observation; focus delegates forward the unchanged original once; counts start after native Init; no synthetic focus or keys; external keys must use dedicated DISPLAY" },new JsonSerializerOptions { WriteIndented=true });
  if(artifact!=lastArtifact) { File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"rw17-ime-ui.json"),artifact); lastArtifact=artifact; }
 }
}
internal static class Mod70FocusObservation
{
 internal static int FocusInCalls,FocusOutCalls,ResetCalls;
 internal static readonly List<string> Trace=new();
 private static readonly string[] Fields={"pFocusIn","pFocusOut","pReset"};
 private static readonly string[] Observers={nameof(FocusIn),nameof(FocusOut),nameof(Reset)};
 private static readonly Delegate[] Originals=new Delegate[3],Wrappers=new Delegate[3];
 private static Type native;
 internal static void Install()
 {
  native=AccessTools.TypeByName("LinuxImeFix.NativeBridge");
  for(int i=0;i<Fields.Length;i++)
  {
   var field=AccessTools.Field(native,Fields[i]);
   Originals[i]=(Delegate)field.GetValue(null);
   if(Originals[i]==null) continue;
   Wrappers[i]=Delegate.CreateDelegate(field.FieldType,AccessTools.Method(typeof(Mod70FocusObservation),Observers[i]));
   field.SetValue(null,Wrappers[i]);
  }
 }
 internal static void Restore()
 {
  if(native==null) return;
  for(int i=0;i<Fields.Length;i++)
  {
   var field=AccessTools.Field(native,Fields[i]);
   if(ReferenceEquals(field.GetValue(null),Wrappers[i])) field.SetValue(null,Originals[i]);
  }
 }
 private static void FocusIn() { FocusInCalls++; Observe("FocusIn"); Originals[0].DynamicInvoke(); }
 private static void FocusOut() { FocusOutCalls++; Observe("FocusOut"); Originals[1].DynamicInvoke(); }
 private static void Reset() { ResetCalls++; Observe("Reset"); Originals[2].DynamicInvoke(); }
 private static void Observe(string call)
 {
  if(Trace.Count>=64) return;
  var utility=AccessTools.TypeByName("LinuxImeFix.LinuxImeUtility");
  Trace.Add($"{call}:raw={Event.current?.rawType},type={Event.current?.type},keyboard={GUIUtility.keyboardControl},focused={AccessTools.Field(utility,"focusedControl").GetValue(null)},saw={AccessTools.Field(utility,"sawFocusedTextFieldThisFrame").GetValue(null)}");
 }
}
