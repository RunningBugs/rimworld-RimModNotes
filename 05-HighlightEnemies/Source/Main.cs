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
using System.Reflection;
using System.Runtime.InteropServices;

// using System.Reflection;
// using HarmonyLib;

namespace _HightlightEnemies
{
    public class EnemyHighlighter : MapComponent
    {
        public bool showEnemies = false;

        private DesignationDef desDef = DefDatabase<DesignationDef>.GetNamed("HE_Mark");

        // private List<IntVec3> cells = new List<IntVec3>();
        public EnemyHighlighter(Map map) : base(map) { }

        public static EnemyHighlighter GetEnemyHighlighter() => Find.CurrentMap.GetComponent<EnemyHighlighter>();

        public override void FinalizeInit()
        {

        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            foreach (var thing in Find.CurrentMap.spawnedThings)
            {
                if (thing.Faction != null && thing.Faction.HostileTo(Faction.OfPlayer))
                {
                    var manager = thing.Map.designationManager;

                    if (showEnemies && manager.DesignationOn(thing, desDef) == null)
                    {
                        manager.AddDesignation(new Designation(thing, desDef));
                    }
                    else if (!showEnemies && manager.DesignationOn(thing, desDef) != null)
                    {
                        manager.RemoveAllDesignationsOfDef(desDef);
                    }
                }
            }
        }

        [StaticConstructorOnStartup]
        public static class Start
        {
            static Start()
            {
                Log.Message("HightLightEnemies loaded");
                var harmony = new Harmony("com.RunningBugs.AlertUtility");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
        }

        [HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls", MethodType.Normal)]
        public class ToggleIconPatcher
        {
            [HarmonyPostfix]
            public static void AddIcon(WidgetRow row, bool worldView)
            {
                if (worldView) return;

                var eh = EnemyHighlighter.GetEnemyHighlighter();

                row.ToggleableIcon(ref eh.showEnemies, ContentFinder<Texture2D>.Get("HE/UI/Alarm", true), "HighlightEnemies".Translate(), SoundDefOf.Mouseover_ButtonToggle, (string)null);
            }
        }

    }
}