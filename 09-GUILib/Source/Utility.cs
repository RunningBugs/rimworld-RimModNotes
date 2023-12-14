using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SimpleGUI
{
    [StaticConstructorOnStartup]
    public static class ToggleIconData
    {
        private static Type windowType = typeof(SampleWindow);
        private static Texture2D tex = ContentFinder<Texture2D>.Get("WindowIcon", true);
        private static string tooltip = "SampleWindowTooltip".Translate();
        private static SoundDef mouseoverSound = SoundDefOf.Mouseover_ButtonToggle;
        private static string tutorTag = null;
        private static Action action = null;

        public static Type WindowType { get => windowType; }
        public static Texture2D Tex { get => tex; }
        public static string Tooltip { get => tooltip; }
        public static SoundDef MouseoverSound { get => mouseoverSound; }
        public static string TutorTag { get => tutorTag; }
        public static Action Action { get => action; }

        public static void setupToggleIcon(Type windowType, Texture2D tex, string tooltip, SoundDef mouseoverSound,
            Action action, string tutorTag = null)
        {
            ToggleIconData.windowType = windowType;
            ToggleIconData.tex = tex;
            ToggleIconData.tooltip = tooltip;
            ToggleIconData.mouseoverSound = mouseoverSound;
            ToggleIconData.tutorTag = tutorTag;
            ToggleIconData.action = action;
            Log.Warning($"setupToggleIcon: {ToggleIconData.windowType}, {ToggleIconData.tex == null}, {ToggleIconData.tooltip}, {ToggleIconData.mouseoverSound == SoundDefOf.Mouseover_ButtonToggle}, {ToggleIconData.action == null}");
        }
    }
    [HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls", MethodType.Normal)]
    public class ToggleIconPatcher
    {

        private static bool flag = true;

        [HarmonyPostfix]
        public static void AddIcon(WidgetRow row, bool worldView)
        {
            // Log.Warning("Patch called");
            Type windowType = ToggleIconData.WindowType;
            Texture2D tex = ToggleIconData.Tex;
            string tooltip = ToggleIconData.Tooltip;
            SoundDef mouseoverSound = ToggleIconData.MouseoverSound;
            string tutorTag = ToggleIconData.TutorTag;
            Action action = ToggleIconData.Action;

            // Log.Warning($"AddIcon: {windowType}, {tex == null}, {tooltip}, {mouseoverSound == SoundDefOf.Mouseover_ButtonToggle}, {action == null}");
            if (windowType == null) throw new NullReferenceException("windowType is null");
            if (tex == null) throw new NullReferenceException("tex is null");
            if (tooltip == null) throw new NullReferenceException("tooltip is null");
            if (mouseoverSound == null) throw new NullReferenceException("mouseoverSound is null");
            if (action == null) throw new NullReferenceException("action is null");

            if (worldView) return;
            // bool flag = Find.WindowStack.IsOpen(windowType);
            // bool flag = true;
            row.ToggleableIcon(ref flag, tex, tooltip, mouseoverSound, tutorTag);
            if (flag != Find.WindowStack.IsOpen(windowType))
            {
                if (!Find.WindowStack.IsOpen(windowType))
                {
                    action();
                }
                else
                {
                    Find.WindowStack.TryRemove(windowType, false);
                }
            }
        }
    }
}