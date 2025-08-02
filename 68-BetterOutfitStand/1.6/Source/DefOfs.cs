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

namespace BetterOutfitStand;

[DefOf]
public static class DefOfs
{
    public static JobDef UseOutfitStand_Better;
}