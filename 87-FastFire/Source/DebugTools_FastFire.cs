using System.Collections.Generic;
using System.Diagnostics;
using LudeonTK;
using RimWorld;
using Verse;

namespace FastFire;

public static class DebugTools_FastFire
{
    private static ThingDef cachedOilSmearDef;

    [DebugAction("Fast Fire", "Spawn 10000+ varied fires", false, false, false, false, false, 0, false,
        actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnMassTestFires()
    {
        Map map = Find.CurrentMap;
        if (map == null)
        {
            return;
        }

        int targetCount = FastFireMod.Settings != null ? FastFireMod.Settings.debugSpawnTargetCount : 12000;
        if (targetCount < 10000)
        {
            targetCount = 10000;
        }

        FireMapComponent comp = map.GetComponent<FireMapComponent>();
        ThingDef oilSmear = cachedOilSmearDef ??= DefDatabase<ThingDef>.GetNamedSilentFail("Filth_OilSmear");
        int area = map.Area;
        int spawned = 0;

        Stopwatch sw = Stopwatch.StartNew();

        // Pass 1: Shuffle all cells and ignite naturally flammable cells with varied fire sizes (0.1 ~ 1.75)
        List<int> fallbackCells = new List<int>(Math.Min(area, targetCount));
        using (map.pathing.DisableIncrementalScope())
        {
            for (int i = 0; i < area && spawned < targetCount; i++)
            {
                IntVec3 c = map.cellsInRandomOrder.Get(i);
                if (comp != null && comp.HasAnyStaticFireAt(c))
                {
                    continue;
                }
                if (FireUtility.ChanceToStartFireIn(c, map) > 0f)
                {
                    float size = Rand.Range(FireMath.MinFireSize, FireMath.MaxFireSize);
                    if (FireUtility.TryStartFireIn(c, map, size, null))
                    {
                        spawned++;
                    }
                }
                else if (fallbackCells.Count < targetCount && CanUseAsFuelFallback(c, map))
                {
                    fallbackCells.Add(map.cellIndices.CellToIndex(c));
                }
            }

            // Pass 2: If map didn't have 10,000+ naturally flammable tiles (e.g., barren biome or already burned),
            // place flammable oil smear and ignite so we reliably reach 10,000+ sustained varied fires!
            for (int i = 0; i < fallbackCells.Count && spawned < targetCount; i++)
            {
                IntVec3 c = map.cellIndices.IndexToCell(fallbackCells[i]);
                if (comp != null && comp.HasAnyStaticFireAt(c))
                {
                    continue;
                }
                if (oilSmear != null)
                {
                    Thing fuel = ThingMaker.MakeThing(oilSmear);
                    GenSpawn.Spawn(fuel, c, map);
                }
                float size = Rand.Range(FireMath.MinFireSize, FireMath.MaxFireSize);
                if (FireUtility.TryStartFireIn(c, map, size, null))
                {
                    spawned++;
                }
            }
        }

        comp?.FlushPendingPathUpdates(force: true);
        sw.Stop();

        int totalNow = comp != null ? comp.TotalStaticFireCount : map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count;
        string modeName = FastFireMod.Settings != null ? FastFireMod.Settings.mode.ToString() : "Unknown";
        string msg = "FastFire_DebugSpawnedMsg".Translate(spawned, totalNow, modeName, sw.Elapsed.TotalMilliseconds.ToString("F1"));
        Messages.Message(msg, MessageTypeDefOf.NeutralEvent, historical: false);
        Log.Message($"[FastFire] {msg}");
    }

    [DebugAction("Fast Fire", "Clear all fires", false, false, false, false, false, 0, false,
        actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void ClearAllFires()
    {
        Map map = Find.CurrentMap;
        if (map == null)
        {
            return;
        }
        FireMapComponent comp = map.GetComponent<FireMapComponent>();
        int count = comp != null ? comp.TotalStaticFireCount : map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count;
        if (comp != null && FastFireMod.Settings.IsActive)
        {
            comp.ClearAllFiresForDebug();
        }
        else
        {
            Thing[] arr = map.listerThings.ThingsInGroup(ThingRequestGroup.Fire).ToArray();
            for (int i = 0; i < arr.Length; i++)
            {
                if (!arr[i].Destroyed)
                {
                    arr[i].Destroy();
                }
            }
        }
        Messages.Message("FastFire_DebugClearedMsg".Translate(count), MessageTypeDefOf.NeutralEvent, historical: false);
    }

    private static bool CanUseAsFuelFallback(IntVec3 c, Map map)
    {
        if (!c.InBounds(map) || !c.Standable(map))
        {
            return false;
        }
        TerrainDef terrain = c.GetTerrain(map);
        if (terrain == null || terrain.extinguishesFire || terrain.IsWater)
        {
            return false;
        }
        Building edifice = c.GetEdifice(map);
        if (edifice != null && edifice.FireBulwark)
        {
            return false;
        }
        List<Thing> list = map.thingGrid.ThingsListAtFast(map.cellIndices.CellToIndex(c));
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is Fire || list[i].FireBulwark || (list[i].def.category == ThingCategory.Filth && !list[i].def.filth.allowsFire))
            {
                return false;
            }
        }
        return true;
    }
}
