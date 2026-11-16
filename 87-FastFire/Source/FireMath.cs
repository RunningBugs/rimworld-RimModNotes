using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FastFire;

internal static class FireMath
{
    public const float MinFireSize = 0.1f;
    public const float MaxFireSize = 1.75f;
    public const float MinSizeForSpark = 1f;
    public const float TicksBetweenSparksBase = 150f;
    public const float TicksBetweenSparksReductionPerFireSize = 40f;
    public const float MinTicksBetweenSparks = 75f;
    public const int TicksToBurnFloor = 7500;
    public const int ComplexCalcsInterval = 150;
    public const float MinSizeForIgniteMovables = 0.4f;
    public const float FireBaseGrowthPerTick = 0.00055f;
    public const int BaseSkyExtinguishDamage = 10;
    public const int VacuumExtinguishDamage = 20;
    public const float HeatPerFireSizePerInterval = 160f;
    public const float HeatFactorWhenDoorPresent = 0.15f;
    public const float SnowClearRadiusPerFireSize = 3f;
    public const float SnowClearDepthFactor = 0.1f;
    public const int FireCountParticlesOff = 15;
    public static readonly IntRange SmokeIntervalRange = new IntRange(130, 200);

    private static readonly Dictionary<TerrainDef, float> terrainFlammabilityCache = new Dictionary<TerrainDef, float>();
    private static readonly Dictionary<ThingDef, float> staticDefFlammabilityCache = new Dictionary<ThingDef, float>();
    private static bool? canUseStaticDefFlammabilityCache;

    [ThreadStatic]
    private static List<Thing> tmpFlammableList;

    private static bool CanUseStaticDefCache()
    {
        if (!canUseStaticDefFlammabilityCache.HasValue)
        {
            StatDef stat = StatDefOf.Flammability;
            canUseStaticDefFlammabilityCache = stat != null
                && stat.workerClass == typeof(StatWorker)
                && stat.parts.NullOrEmpty()
                && stat.statFactors.NullOrEmpty()
                && stat.postProcessStatFactors.NullOrEmpty()
                && stat.postProcessCurve == null;
        }
        return canUseStaticDefFlammabilityCache.Value;
    }

    public static float GetTerrainBaseFlammability(TerrainDef terrain)
    {
        if (terrain == null)
        {
            return 0f;
        }
        if (!terrainFlammabilityCache.TryGetValue(terrain, out float val))
        {
            val = terrain.GetStatValueAbstract(StatDefOf.Flammability);
            terrainFlammabilityCache[terrain] = val;
        }
        return val;
    }

    public static float GetThingFlammabilityFast(Thing thing)
    {
        if (thing is Pawn || thing.Stuff != null)
        {
            return thing.GetStatValue(StatDefOf.Flammability);
        }
        if (CanUseStaticDefCache())
        {
            ThingCategory cat = thing.def.category;
            if (cat == ThingCategory.Plant || cat == ThingCategory.Filth || (thing is ThingWithComps twc ? twc.AllComps.Count == 0 : true))
            {
                if (!staticDefFlammabilityCache.TryGetValue(thing.def, out float cached))
                {
                    cached = thing.def.BaseFlammability;
                    staticDefFlammabilityCache[thing.def] = cached;
                }
                return cached;
            }
        }
        return thing.GetStatValue(StatDefOf.Flammability);
    }

    public static float ComputeTerrainFlammabilityInCell(IntVec3 c, Map map, List<Thing> thingList, out bool fireBulwarkPresent)
    {
        fireBulwarkPresent = false;
        TerrainDef terrain = c.GetTerrain(map);
        TerrainDef foundation = map.terrainGrid.FoundationAt(c);
        float tFlamm = GetTerrainBaseFlammability(terrain);
        float fFlamm = foundation != null ? GetTerrainBaseFlammability(foundation) : 0f;
        float maxT = Mathf.Max(tFlamm, fFlamm);
        if (maxT <= 0.01f)
        {
            return 0f;
        }
        for (int i = 0; i < thingList.Count; i++)
        {
            if (thingList[i].FireBulwark)
            {
                fireBulwarkPresent = true;
                return 0f;
            }
        }
        return maxT;
    }

    public static float ChanceToStartFireInFast(IntVec3 c, Map map, FireMapComponent comp, SimpleCurve flammabilityChanceCurve = null)
    {
        if (!c.InBounds(map))
        {
            return 0f;
        }
        if (comp != null && comp.HasAnyStaticFireAt(c))
        {
            return 0f;
        }

        List<Thing> thingList = map.thingGrid.ThingsListAtFast(map.cellIndices.CellToIndex(c));
        bool hasFireBulwark = false;
        for (int i = 0; i < thingList.Count; i++)
        {
            Thing t = thingList[i];
            if (t is Fire)
            {
                return 0f;
            }
            if (t.FireBulwark)
            {
                hasFireBulwark = true;
            }
        }

        float num = 0f;
        if (!hasFireBulwark)
        {
            TerrainDef terrain = c.GetTerrain(map);
            TerrainDef foundation = map.terrainGrid.FoundationAt(c);
            float tFlamm = GetTerrainBaseFlammability(terrain);
            float fFlamm = foundation != null ? GetTerrainBaseFlammability(foundation) : 0f;
            float maxT = Mathf.Max(tFlamm, fFlamm);
            if (maxT > 0.01f)
            {
                num = maxT;
            }
        }

        if (!hasFireBulwark)
        {
            for (int i = 0; i < thingList.Count; i++)
            {
                Thing thing = thingList[i];
                if (thing.def.category != ThingCategory.Pawn)
                {
                    float flamm = GetThingFlammabilityFast(thing);
                    if (flamm >= 0.01f)
                    {
                        num = Mathf.Max(num, flamm);
                    }
                }
            }
        }

        if (flammabilityChanceCurve != null)
        {
            num = flammabilityChanceCurve.Evaluate(num);
        }

        if (num > 0f)
        {
            Building edifice = c.GetEdifice(map);
            if (edifice != null && edifice.def.passability == Traversability.Impassable && edifice.OccupiedRect().ContractedBy(1).Contains(c))
            {
                return 0f;
            }
            for (int j = 0; j < thingList.Count; j++)
            {
                if (thingList[j].def.category == ThingCategory.Filth && !thingList[j].def.filth.allowsFire)
                {
                    return 0f;
                }
            }
        }
        return num;
    }

    public static float SpreadIntervalForSize(float fireSize)
    {
        float num = 150f - (fireSize - 1f) * 40f;
        return num < 75f ? 75f : num;
    }

    public static int NextSmokeDelayTicks(float fireSize)
    {
        float num = fireSize / 2f;
        if (num > 1f)
        {
            num = 1f;
        }
        num = 1f - num;
        return SmokeIntervalRange.Lerped(num) + (int)(10f * Rand.Value);
    }

    public static bool VulnerableToRain(IntVec3 pos, Map map)
    {
        RoofDef roofDef = map.roofGrid.RoofAt(pos);
        if (roofDef == null)
        {
            return true;
        }
        if (roofDef.isThickRoof)
        {
            return false;
        }
        return pos.GetEdifice(map)?.def.holdsRoof ?? false;
    }

    public static float GetEffectiveVacuumForFireFast(IntVec3 c, Map map)
    {
        if (!ModsConfig.OdysseyActive || !map.Biome.inVacuum)
        {
            return 0f;
        }
        return FireUtility.GetEffectiveVacuumForFire(c, map);
    }

    public static void DoFireDamageAt(Thing targ, float fireSize, Thing instigator, Thing logSourceFallback)
    {
        int num = GenMath.RoundRandom(Mathf.Clamp(0.0125f + 0.0036f * fireSize, 0.0125f, 0.05f) * 150f);
        if (num < 1)
        {
            num = 1;
        }
        Thing effectiveInstigator = instigator ?? logSourceFallback;
        if (targ is Pawn pawn)
        {
            BattleLogEntry_DamageTaken logEntry = new BattleLogEntry_DamageTaken(pawn, RulePackDefOf.DamageEvent_Fire);
            Find.BattleLog.Add(logEntry);
            DamageInfo dinfo = new DamageInfo(DamageDefOf.Flame, num, 0f, -1f, effectiveInstigator);
            dinfo.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Outside);
            targ.TakeDamage(dinfo).AssociateWithLog(logEntry);
            if (pawn.apparel != null && pawn.apparel.WornApparel.TryRandomElement(out Apparel result))
            {
                result.TakeDamage(new DamageInfo(DamageDefOf.Flame, num, 0f, -1f, effectiveInstigator));
            }
        }
        else
        {
            targ.TakeDamage(new DamageInfo(DamageDefOf.Flame, num, 0f, -1f, effectiveInstigator));
        }
    }

    public static void DoComplexCalcsStatic(
        Map map,
        FireMapComponent comp,
        IntVec3 pos,
        int cellIndex,
        ref float fireSize,
        ref float flammabilityMax,
        Thing instigator,
        Thing logSourceFallback,
        out bool extinguished)
    {
        extinguished = false;
        bool hasDoor = false;
        List<Thing> flammableList = tmpFlammableList ??= new List<Thing>(8);
        flammableList.Clear();
        flammabilityMax = 0f;

        if (!pos.GetTerrain(map).extinguishesFire)
        {
            List<Thing> list = map.thingGrid.ThingsListAtFast(cellIndex);
            flammabilityMax = ComputeTerrainFlammabilityInCell(pos, map, list, out _);

            for (int i = 0; i < list.Count; i++)
            {
                Thing thing = list[i];
                if (thing is Building_Door)
                {
                    hasDoor = true;
                }
                if (thing is Fire)
                {
                    continue;
                }
                float statValue = GetThingFlammabilityFast(thing);
                if (statValue < 0.01f)
                {
                    continue;
                }
                flammableList.Add(thing);
                if (statValue > flammabilityMax)
                {
                    flammabilityMax = statValue;
                }
                if (fireSize > MinSizeForIgniteMovables && thing.def.category == ThingCategory.Pawn
                    && Rand.Chance(FireUtility.ChanceToAttachFireCumulative(thing, 150f)))
                {
                    thing.TryAttachFire(fireSize * 0.2f, instigator);
                }
            }
        }

        if (flammabilityMax < 0.01f)
        {
            flammableList.Clear();
            extinguished = true;
            return;
        }

        Thing victim = flammableList.Count > 0 ? flammableList.RandomElement() : null;
        flammableList.Clear();

        if (victim != null && (!(fireSize < MinSizeForIgniteMovables) || victim.def.category != ThingCategory.Pawn))
        {
            DoFireDamageAt(victim, fireSize, instigator, logSourceFallback);
        }

        // Check if cell still exists (victim death or explosion might have extinguished us)
        if (!comp.HasAnyStaticFireAt(cellIndex))
        {
            extinguished = true;
            return;
        }

        float heat = fireSize * HeatPerFireSizePerInterval;
        if (hasDoor)
        {
            heat *= HeatFactorWhenDoorPresent;
        }
        GenTemperature.PushHeat(pos, map, heat);

        if (map.snowGrid.TotalDepth > 0.001f && Rand.Value < 0.4f)
        {
            float radius = fireSize * SnowClearRadiusPerFireSize;
            WeatherBuildupUtility.AddSnowRadial(pos, map, radius, 0f - fireSize * SnowClearDepthFactor);
        }

        float effectiveVacuum = GetEffectiveVacuumForFireFast(pos, map);
        float newSize = fireSize + FireBaseGrowthPerTick * flammabilityMax * 150f * (1f - effectiveVacuum);
        if (newSize > MaxFireSize)
        {
            newSize = MaxFireSize;
        }

        if (map.weatherManager.RainRate > 0.01f && VulnerableToRain(pos, map))
        {
            newSize -= BaseSkyExtinguishDamage * 0.01f;
            if (newSize < MinFireSize)
            {
                fireSize = newSize;
                extinguished = true;
                return;
            }
        }

        if (effectiveVacuum > 0f)
        {
            newSize -= VacuumExtinguishDamage * 0.01f * effectiveVacuum;
            if (newSize < MinFireSize)
            {
                fireSize = newSize;
                extinguished = true;
                return;
            }
        }

        fireSize = newSize;
    }

    public static void TrySpreadFrom(IntVec3 pos, Map map, Thing instigator, Thing sparkLauncher, FireMapComponent comp)
    {
        IntVec3 targetCell;
        bool adjacent;
        if (Rand.Chance(0.8f))
        {
            targetCell = pos + GenRadial.ManualRadialPattern[Rand.RangeInclusive(1, 8)];
            adjacent = true;
        }
        else
        {
            targetCell = pos + GenRadial.ManualRadialPattern[Rand.RangeInclusive(10, 20)];
            adjacent = false;
        }

        if (!targetCell.InBounds(map))
        {
            return;
        }

        // Fast O(1) rejection before touching thingGrid or terrain!
        if (comp != null && comp.HasAnyStaticFireAt(targetCell))
        {
            return;
        }

        float chance = ChanceToStartFireInFast(targetCell, map, comp);
        if (chance <= 0f || !Rand.Chance(chance))
        {
            return;
        }

        if (!adjacent)
        {
            CellRect startRect = CellRect.SingleCell(pos);
            CellRect endRect = CellRect.SingleCell(targetCell);
            if (GenSight.LineOfSight(pos, targetCell, map, startRect, endRect))
            {
                ((Spark)GenSpawn.Spawn(ThingDefOf.Spark, pos, map)).Launch(sparkLauncher, targetCell, targetCell, ProjectileHitFlags.All);
            }
        }
        else
        {
            FireUtility.TryStartFireIn(targetCell, map, MinFireSize, instigator);
        }
    }
}
