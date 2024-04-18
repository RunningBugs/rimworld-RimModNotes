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

// using System.Reflection;
using HarmonyLib;

namespace LetterStackCleaner
{
    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {

            Harmony harmony = new Harmony("com.RunningBugs.LetterStackCleaner2");
            harmony.PatchAll();
            Log.Message("Mod template loaded successfully!");
        }
    }

    public class LetterStackCleaner : GameComponent
    {

        private Game game;
        public LetterStackCleaner(Game game)
        {
            this.game = game;
        }

        public override void GameComponentOnGUI()
        {
            // base.GameComponentOnGUI();
            Event ev = Event.current;
            if (ev.type == EventType.KeyUp && ev.keyCode == KeyCode.Delete)
            {
                var lettersToRemove = new List<Letter>();
                foreach (Letter letter in Find.LetterStack.LettersListForReading)
                {
                    lettersToRemove.Add(letter);
                }
                foreach (Letter letter in lettersToRemove)
                {
                    Find.LetterStack.RemoveLetter(letter);
                }
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }
    }

}
