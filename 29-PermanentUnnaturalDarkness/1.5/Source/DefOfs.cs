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
// using HarmonyLib;

namespace PermanentDarkness
{
    [DefOf]
    public static class WeatherDefs
    {
        public static WeatherDef PermanentDarkness_Stage1;
        public static WeatherDef PermanentDarkness_Stage2;
        public static WeatherDef Rain;
    }

    [DefOf]
    public class GameConditionDefs
    {
        public static GameConditionDef PermanentDarkness;
    }

    [DefOf]
    public static class HediffDefs
    {
        public static HediffDef PD_DarknessExposure;
    }

    [DefOf]
    public static class RulePackDefs
    {
        public static RulePackDef PermanentDarkness;
    }
}
