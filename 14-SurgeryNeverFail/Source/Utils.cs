using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Utils
{
    [StaticConstructorOnStartup]
    public class ToggleIconData
    {
        private Type windowType = typeof(Window);
        private Texture2D tex = ContentFinder<Texture2D>.Get("WindowIcon", true);
        private string tooltip = "";
        private SoundDef mouseoverSound = SoundDefOf.Mouseover_ButtonToggle;
        private string tutorTag = null;
        private Action action = null;

        public Type WindowType { get => windowType; }
        public Texture2D Tex { get => tex; }
        public string Tooltip { get => tooltip; }
        public SoundDef MouseoverSound { get => mouseoverSound; }
        public string TutorTag { get => tutorTag; }
        public Action Action { get => action; }

        public static ToggleIconData SetupToggleIcon(Type windowType, Texture2D tex, string tooltip, SoundDef mouseoverSound,
            Action action, string tutorTag = null)
        {
            ToggleIconData data = new ToggleIconData
            {
                windowType = windowType,
                tex = tex,
                tooltip = tooltip,
                mouseoverSound = mouseoverSound,
                tutorTag = tutorTag,
                action = action
            };
            return data;
        }
    }
    [HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls", MethodType.Normal)]
    public class ToggleIconPatcher
    {

        private static bool flag = true;
        private static ToggleIconData data = null;

        public static ToggleIconData Data { get => data; set => data = value; }
        public static bool Flag { get => flag; set => flag = value; }

        [HarmonyPostfix]
        public static void AddIcon(WidgetRow row, bool worldView)
        {
            // Log.Warning("Patch called");
            if (data != null)
            {
                Texture2D tex = data.Tex;
                string tooltip = data.Tooltip;
                SoundDef mouseoverSound = data.MouseoverSound;
                string tutorTag = data.TutorTag;

                if (tex == null) throw new NullReferenceException("tex is null");
                if (tooltip == null) throw new NullReferenceException("tooltip is null");
                if (mouseoverSound == null) throw new NullReferenceException("mouseoverSound is null");

                if (worldView) return;
                row.ToggleableIcon(ref flag, tex, tooltip, mouseoverSound, tutorTag);
            }
        }
    }
}