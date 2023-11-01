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

// using System.Reflection;
// using HarmonyLib;

namespace _HightlightEnemies
{
    public class EnemyHighlighter : MapComponent
    {
        public static bool showEnemies = false;
        public EnemyHighlighter(Map map) : base(map) { }
        public override void FinalizeInit()
        {

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
            // bool flag = Find.WindowStack.IsOpen(typeof(TimerSetWindow));
            bool status = EnemyHighlighter.showEnemies;
            row.ToggleableIcon(ref EnemyHighlighter.showEnemies, ContentFinder<Texture2D>.Get("HE/UI/Alarm", true), "HighlightEnemies".Translate(), SoundDefOf.Mouseover_ButtonToggle, (string)null);
            if (EnemyHighlighter.showEnemies)
            {
                foreach (var thing in Find.CurrentMap.spawnedThings)
                {
                    if (thing.Faction != null && thing.Faction.HostileTo(Faction.OfPlayer))
                    {
                        // thing.Map.overlayDrawer.DrawOverlay(thing, OverlayTypes.QuestionMark);
                        // var tex = GraphicUtility.ExtractInnerGraphicFor(thing.Graphic, thing).MatAt(thing.def.defaultPlacingRot).mainTexture;
                        // var 
                        var designationManager = thing.Map.designationManager;
                        var desDef = DefDatabase<DesignationDef>.GetNamed("HE_Mark");
                        
                        if (!status) {
                            //  don't add designations when the button is toggled on
                            //  we only add them once, when previously the botton was toggled off
                            GenAdj.CellsOccupiedBy(thing).ToList().ForEach(c => designationManager.AddDesignation(new Designation(c, desDef)));
                        }
                    }
                }
            }
            else
            {
                var desDef = DefDatabase<DesignationDef>.GetNamed("HE_Mark");
                foreach (Map map in Find.Maps)
                {
                    map.designationManager.RemoveAllDesignationsOfDef(desDef);
                }
            }
            // if (flag != Find.WindowStack.IsOpen(typeof(TimerSetWindow)))
            // {
            //     if (!Find.WindowStack.IsOpen(typeof(TimerSetWindow)))
            //     {
            //         TimerSetWindow.DrawWindow(AlertUtility.GetEvents());
            //     }
            //     else
            //     {
            //         Find.WindowStack.TryRemove(typeof(TimerSetWindow), false);
            //     }
            // }
        }
    }

}
