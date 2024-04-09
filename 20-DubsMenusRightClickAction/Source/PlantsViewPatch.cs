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
using HarmonyLib;
using DubsMintMenus;
using System.Reflection;
using System.Reflection.Emit;

namespace DubsMintMenusRightClickAction;

[HarmonyPatch(typeof(Dialog_FancyDanPlantSetterBob), "Clicked")]
public static class PlantsViewPatch
{
    public static bool Prefix(Dialog_FancyDanPlantSetterBob __instance, ThingDef plantDef)
    {
        Event e = Event.current;
        if (e.type == EventType.Used && e.button == 1)
        {
            Find.WindowStack.Add(new Dialog_InfoCard(plantDef));
            return false;
        }
        return true;
    }
}