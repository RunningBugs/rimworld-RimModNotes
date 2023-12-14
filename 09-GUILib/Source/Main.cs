using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using Verse.Noise;
using Verse.Grammar;
using RimWorld;
using RimWorld.Planet;
using HarmonyLib;

// using System.Reflection;
// using HarmonyLib;

namespace SimpleGUI
{
    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Log.prefix = "SimpleGUI";
            Log.Message("SimpleGUI loaded successfully!");

            ToggleIconData.setupToggleIcon(typeof(SampleWindow), ContentFinder<Texture2D>.Get("WindowIcon", true), "SampleWindowTooltip".Translate(), SoundDefOf.Mouseover_ButtonToggle, delegate
            {
                Find.WindowStack.Add(new SampleWindow());
            });

            Harmony harmony = new Harmony("com.RunningBugs.SimpleGUI");
            harmony.PatchAll();
        }
    }

}
