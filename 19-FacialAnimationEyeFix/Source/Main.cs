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

using Logs = Logger.Log;

// using System.Reflection;
// using HarmonyLib;

namespace FacialAnimationEyeFix
{
    public class TextureReDraw : GameComponent {

        bool runOnce = false;
        public TextureReDraw(Game game) {
        }

        public override void GameComponentTick() {
            if (!runOnce) {
                FacialAnimation.MyGraphicPool.RepaintAllGraphic();
                runOnce = true;
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Logs.prefix = "FacialAnimationEyeFix";
            Logs.Warning("Mod loaded!");
        }
    }

}
