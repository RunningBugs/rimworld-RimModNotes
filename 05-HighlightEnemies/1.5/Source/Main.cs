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
using System.Threading.Tasks;
using System.Net;
using HugsLib.Utils;

// using System.Reflection;
// using HarmonyLib;

namespace _HightlightEnemies
{
    public class EnemyHighlighter : GameComponent
    {
        public bool showEnemies = true;
        private Task lastTask = Task.CompletedTask;

        private DesignationDef desDef = DefDatabase<DesignationDef>.GetNamed("HE_Mark");

        public EnemyHighlighter(Game game) { }

        public override void FinalizeInit()
        {
        }

        public override void GameComponentTick()
        {
            if (showEnemies)
            {
                Highlight();
            }
            else
            {
                DeHighlight();
            }
        }


        public void Highlight()
        {
            var manager = Find.CurrentMap.designationManager;

            if (lastTask.IsCompleted)
            {
                lastTask = Task.Run(() =>
                {
                    var things = Find.CurrentMap.attackTargetsCache.TargetsHostileToColony.Select(t => t.Thing).ToList();
                    var hostileThings = new HashSet<Thing>();
                    foreach (var thing in things)
                    {

                        if (GenHostility.HostileTo(thing, Faction.OfPlayer) && manager.DesignationOn(thing, desDef) == null)
                        {
                            hostileThings.Add(thing);
                        }
                    }

                    var buildings = Find.CurrentMap.listerBuildings.allBuildingsNonColonist.Where(t => t is Building && t.def.building.combatPower > 0).ToList();
                    foreach (var thing in buildings)
                    {

                        if (GenHostility.HostileTo(thing, Faction.OfPlayer) && manager.DesignationOn(thing, desDef) == null)
                        {
                            hostileThings.Add(thing);
                        }
                    }
                    return hostileThings;
                }).ContinueWith((hostileThings) =>
                {
                    foreach (var thing in hostileThings.Result)
                    {
                        if (!thing.HasDesignation(desDef))
                        {
                            manager.AddDesignation(new Designation(thing, desDef));
                        }
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }


        }

        public void DeHighlight()
        {
            var manager = Find.CurrentMap.designationManager;
            manager.RemoveAllDesignationsOfDef(desDef);
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

                var eh = Current.Game.GetComponent<EnemyHighlighter>();
                if (eh != null)
                {
                    row.ToggleableIcon(ref eh.showEnemies, ContentFinder<Texture2D>.Get("HE/UI/Alarm", true), "HighlightEnemies".Translate(), SoundDefOf.Mouseover_ButtonToggle, (string)null);
                }
            }
        }

    }
}