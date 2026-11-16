using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace FastFire;

public sealed class FireMapComponent : MapComponent, IDisposable
{
    public const int BucketCount = 15;
    private const int LightChunkShift = 2; // 4x4 cells
    private const int LightChunkSize = 4;
    private const int AiChunkShift = 4; // 16x16 cells
    private const int AiChunkSize = 16;
    private const int MaxAudioSustainers = 4;
    private const float AudioMaxDistance = 72f;
    private const float AudioMaxDistanceSq = AudioMaxDistance * AudioMaxDistance;
    private const int ProxyReabsorbTicks = 600;
    private const int MaxInstancedBatch = 1023;
    private const int MaxMeshQuadsPerDraw = 16000;

    private readonly int sizeX;
    private readonly int sizeZ;
    private readonly int numCells;
    private readonly int lightChunkW;
    private readonly int lightChunkH;
    private readonly int lightChunkCount;
    private readonly int aiChunkW;
    private readonly int aiChunkH;
    private readonly int aiChunkCount;

    // 0 = none, 1 = ExtremeGrid fire, 2 = Compatible/proxy Fire Thing
    internal byte[] staticFireAtCell;
    // Count of 8 adjacent cells with staticFireAtCell == 1 (only grid fires)
    internal byte[] gridNeighborFireCount;
    // Tracked non-negative size contribution per cell so external fireSize mutations (e.g. negative Extinguish damage) never drift totals
    private float[] trackedFireSizeAtCell;
    // Reference to Fire Thing at cell when staticFireAtCell == 2
    private Fire[] thingFireAtCell;

    // ExtremeGrid SoA storage
    private float[] gridFireSize;
    private int[] gridTicksSinceSpawn;
    private int[] gridTicksSinceSpread;
    private int[] gridTicksUntilSmoke;
    private float[] gridFlammabilityMax;
    private byte[] gridBucketId;
    private int[] gridSlotInBucket;
    private readonly List<int>[] gridBuckets = new List<int>[BucketCount];
    private readonly Dictionary<int, Thing> gridInstigators = new Dictionary<int, Thing>();
    private int gridFireCount;

    // Compatible / proxy Fire Thing 15-tick buckets
    private readonly List<Fire>[] compatBuckets = new List<Fire>[BucketCount];
    private readonly Dictionary<int, (byte bucket, int slot)> compatLocator = new Dictionary<int, (byte, int)>();
    private readonly Dictionary<int, int> proxySpawnTicks = new Dictionary<int, int>();
    private readonly List<Fire> tmpProxyList = new List<Fire>();
    private readonly List<Fire> pawnAttachedFires = new List<Fire>();

    // Lighting chunks (4x4)
    private ushort[] lightChunkFireCount;
#if RW17
    private int[] lightChunkGlowerHandle;
#else
    private CompGlower[] lightChunkDummyGlowers;
#endif

    // AI / density chunks (16x16)
    internal ushort[] aiChunkFireCount;
    internal float[] aiChunkFireSizeSum;
    internal int totalStaticFireCount;
    internal double totalStaticFireSizeSum;
    internal int homeAreaUnfoggedFireCount;
    private int cachedFirstHomeFireCell = -1;

    // Pathing debounce
    private byte[] pathDirtyFlags;
    private readonly List<int> pathDirtyCells = new List<int>(256);
    private int lastPathFlushTick = -1;

    // Batch despawn in Compatible mode
    private int batchDespawnDepth;
    private readonly HashSet<Thing> pendingBatchRemovedThings = new HashSet<Thing>();
    private readonly HashSet<Region> pendingDirtyRegions = new HashSet<Region>();

    // Internal flags
    internal bool isSpawningProxy;
    internal bool suppressPathDirty;
    internal FireOptimizationMode lastAppliedMode;
    private bool lastClusterLighting;
    private bool lastBatchedRendering;
    private bool lastAudioAggregation;
    private bool initializedAfterLoad;

    // Reservations tracking for fires so unreserved Fire.Destroy skips O(reservations) scans
    internal readonly HashSet<int> reservedFireThingIds = new HashSet<int>();
    internal readonly HashSet<int> physicalReservedFireThingIds = new HashSet<int>();

    // Clustered Audio
    private sealed class ClusterSizeReporter : ISizeReporter
    {
        public float CachedSize;
        public float CurrentSize() => CachedSize;
    }

    private struct AudioClusterSlot
    {
        public int chunkIndex;
        public Sustainer sustainer;
        public ClusterSizeReporter reporter;
    }

    private readonly AudioClusterSlot[] audioSlots = new AudioClusterSlot[MaxAudioSustainers];
    private readonly int[] tmpTopAudioChunks = new int[MaxAudioSustainers];
    private readonly float[] tmpTopAudioDistSq = new float[MaxAudioSustainers];

    // Batched Rendering Buffers
    private Matrix4x4[][] instancedBatches;
    private int[] instancedBatchCounts;
    private MaterialPropertyBlock sharedPropBlock;
    private static readonly Vector4[] WhiteColors1023 = CreateWhiteColors1023();

    private List<Vector3>[] meshVerts;
    private List<Vector2>[] meshUvs;
    private List<int>[] meshTris;
    private List<Mesh>[] pooledMeshes;

    // Scribe temporary arrays
    private int[] savedCellIndices;
    private float[] savedFireSizes;
    private int[] savedTicksSinceSpawn;
    private List<int> savedInstCells;
    private List<Thing> savedInstThings;
    private FireOptimizationMode savedMode = FireOptimizationMode.ExtremeGrid;

    private static readonly List<Thing> tmpThingSnapshot = new List<Thing>();
    private static readonly List<Fire> tmpFoundFires = new List<Fire>(32);

    public int GridFireCount => gridFireCount;
    public int TotalStaticFireCount => totalStaticFireCount;
    public int PawnAttachedFireCount => pawnAttachedFires.Count;
    public bool IsInBatchDespawn => batchDespawnDepth > 0;

    public void RegisterPawnAttachedFire(Fire fire)
    {
        if (fire != null && !pawnAttachedFires.Contains(fire))
        {
            pawnAttachedFires.Add(fire);
        }
    }

    public void DeregisterPawnAttachedFire(Fire fire)
    {
        if (fire != null)
        {
            pawnAttachedFires.Remove(fire);
        }
    }

    private static Vector4[] CreateWhiteColors1023()
    {
        var arr = new Vector4[MaxInstancedBatch];
        Vector4 white = Color.white;
        for (int i = 0; i < arr.Length; i++)
        {
            arr[i] = white;
        }
        return arr;
    }

    public FireMapComponent(Map map) : base(map)
    {
        sizeX = map.Size.x;
        sizeZ = map.Size.z;
        numCells = sizeX * sizeZ;
        lightChunkW = (sizeX + LightChunkSize - 1) >> LightChunkShift;
        lightChunkH = (sizeZ + LightChunkSize - 1) >> LightChunkShift;
        lightChunkCount = lightChunkW * lightChunkH;
        aiChunkW = (sizeX + AiChunkSize - 1) >> AiChunkShift;
        aiChunkH = (sizeZ + AiChunkSize - 1) >> AiChunkShift;
        aiChunkCount = aiChunkW * aiChunkH;

        staticFireAtCell = new byte[numCells];
        gridNeighborFireCount = new byte[numCells];
        trackedFireSizeAtCell = new float[numCells];
        thingFireAtCell = new Fire[numCells];

        gridFireSize = new float[numCells];
        gridTicksSinceSpawn = new int[numCells];
        gridTicksSinceSpread = new int[numCells];
        gridTicksUntilSmoke = new int[numCells];
        gridFlammabilityMax = new float[numCells];
        gridBucketId = new byte[numCells];
        gridSlotInBucket = new int[numCells];

        lightChunkFireCount = new ushort[lightChunkCount];
#if RW17
        lightChunkGlowerHandle = new int[lightChunkCount];
#else
        lightChunkDummyGlowers = new CompGlower[lightChunkCount];
#endif
        aiChunkFireCount = new ushort[aiChunkCount];
        aiChunkFireSizeSum = new float[aiChunkCount];
        pathDirtyFlags = new byte[numCells];

        for (int i = 0; i < BucketCount; i++)
        {
            gridBuckets[i] = new List<int>(64);
            compatBuckets[i] = new List<Fire>(64);
        }

        for (int i = 0; i < MaxAudioSustainers; i++)
        {
            audioSlots[i].chunkIndex = -1;
            audioSlots[i].reporter = new ClusterSizeReporter();
        }

        lastAppliedMode = FastFireMod.Settings.mode;
        lastClusterLighting = FastFireMod.Settings.UseClusterLighting;
        lastBatchedRendering = FastFireMod.Settings.UseBatchedRendering;
        lastAudioAggregation = FastFireMod.Settings.UseAudioAggregation;

        map.events.CellFogChanged += OnCellFogChanged;
    }

    public bool HasAnyStaticFireAt(IntVec3 c)
    {
        if ((uint)c.x >= (uint)sizeX || (uint)c.z >= (uint)sizeZ)
        {
            return false;
        }
        return staticFireAtCell[map.cellIndices.CellToIndex(c)] != 0;
    }

    public bool HasAnyStaticFireAt(int idx)
    {
        return (uint)idx < (uint)numCells && staticFireAtCell[idx] != 0;
    }

    public bool HasGridFireAt(IntVec3 c)
    {
        if ((uint)c.x >= (uint)sizeX || (uint)c.z >= (uint)sizeZ)
        {
            return false;
        }
        return staticFireAtCell[map.cellIndices.CellToIndex(c)] == 1;
    }

    public bool HasGridFireAt(int idx)
    {
        return (uint)idx < (uint)numCells && staticFireAtCell[idx] == 1;
    }

    public float GetStaticFireSizeAt(int idx)
    {
        byte state = staticFireAtCell[idx];
        if (state == 1)
        {
            return gridFireSize[idx];
        }
        if (state == 2)
        {
            Fire f = thingFireAtCell[idx];
            return f != null ? f.fireSize : 0f;
        }
        return 0f;
    }

    private int LightChunkIndex(int x, int z) => (z >> LightChunkShift) * lightChunkW + (x >> LightChunkShift);
    private int AiChunkIndex(int x, int z) => (z >> AiChunkShift) * aiChunkW + (x >> AiChunkShift);

    // =====================================================================
    // Grid Fire Lifecycle
    // =====================================================================

    public bool TryAddGridFire(IntVec3 cell, float fireSize, Thing instigator, int ticksSinceSpawnVal = 0)
    {
        int idx = map.cellIndices.CellToIndex(cell);
        if (staticFireAtCell[idx] != 0)
        {
            return false;
        }

        staticFireAtCell[idx] = 1;
        gridFireSize[idx] = fireSize;
        gridTicksSinceSpawn[idx] = ticksSinceSpawnVal;
        gridTicksSinceSpread[idx] = (int)(FireMath.SpreadIntervalForSize(fireSize) * Rand.Value);
        gridTicksUntilSmoke[idx] = FireMath.NextSmokeDelayTicks(fireSize);
        gridFlammabilityMax[idx] = 0.5f;
        if (instigator != null)
        {
            gridInstigators[idx] = instigator;
        }

        int currentTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
        int bucket = (currentTick + Rand.RangeInclusive(1, BucketCount - 1)) % BucketCount;
        gridBucketId[idx] = (byte)bucket;
        gridSlotInBucket[idx] = gridBuckets[bucket].Count;
        gridBuckets[bucket].Add(idx);
        gridFireCount++;

        UpdateGridNeighborCounts(cell, +1);
        OnStaticFireAdded(cell, idx, fireSize);
        return true;
    }

    public bool ExtinguishGridFireAt(IntVec3 cell)
    {
        if (!cell.InBounds(map))
        {
            return false;
        }
        return ExtinguishGridFireAt(map.cellIndices.CellToIndex(cell), cell);
    }

    public bool ExtinguishGridFireAt(int idx, IntVec3 cell)
    {
        if (staticFireAtCell[idx] != 1)
        {
            return false;
        }

        float oldSize = gridFireSize[idx];
        staticFireAtCell[idx] = 0;
        gridFireSize[idx] = 0f;
        gridInstigators.Remove(idx);

        int bucket = gridBucketId[idx];
        int slot = gridSlotInBucket[idx];
        List<int> list = gridBuckets[bucket];
        int lastIdx = list.Count - 1;
        if (slot != lastIdx)
        {
            int movedCell = list[lastIdx];
            list[slot] = movedCell;
            gridSlotInBucket[movedCell] = slot;
        }
        list.RemoveAt(lastIdx);
        gridFireCount--;

        UpdateGridNeighborCounts(cell, -1);
        OnStaticFireRemoved(cell, idx, oldSize);
        return true;
    }

    public void ApplyExtinguishDamageGridAt(IntVec3 cell, float amount)
    {
        if (!cell.InBounds(map))
        {
            return;
        }
        int idx = map.cellIndices.CellToIndex(cell);
        if (staticFireAtCell[idx] != 1)
        {
            return;
        }
        float oldSize = gridFireSize[idx];
        float newSize = oldSize - amount * 0.01f;
        if (newSize < FireMath.MinFireSize)
        {
            ExtinguishGridFireAt(idx, cell);
        }
        else
        {
            gridFireSize[idx] = newSize;
            UpdateStaticFireSizeDelta(cell, oldSize, newSize);
        }
    }

    public Fire MaterializeProxyFireAt(IntVec3 cell)
    {
        int idx = map.cellIndices.CellToIndex(cell);
        byte state = staticFireAtCell[idx];
        if (state == 2)
        {
            return thingFireAtCell[idx];
        }
        if (state != 1)
        {
            return null;
        }

        float size = gridFireSize[idx];
        int spawnedTicks = gridTicksSinceSpawn[idx];
        int spreadTicks = gridTicksSinceSpread[idx];
        int smokeTicks = gridTicksUntilSmoke[idx];
        float flammMax = gridFlammabilityMax[idx];
        gridInstigators.TryGetValue(idx, out Thing instigator);

        bool prevSuppress = suppressPathDirty;
        suppressPathDirty = true;
        try
        {
            ExtinguishGridFireAt(idx, cell);
        }
        finally
        {
            suppressPathDirty = prevSuppress;
        }

        Fire proxy = (Fire)ThingMaker.MakeThing(ThingDefOf.Fire);
        proxy.fireSize = size;
        proxy.ticksSinceSpawn = spawnedTicks;
        proxy.ticksSinceSpread = spreadTicks;
        proxy.ticksUntilSmoke = smokeTicks;
        proxy.flammabilityMax = flammMax;
        proxy.instigator = instigator;

        isSpawningProxy = true;
        try
        {
            GenSpawn.Spawn(proxy, cell, map, Rot4.North);
        }
        finally
        {
            isSpawningProxy = false;
        }

        if (proxy.Spawned)
        {
            proxySpawnTicks[proxy.thingIDNumber] = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
        }
        return proxy;
    }

    // =====================================================================
    // Thing Fire Registration (Compatible Mode & Proxy Fires in Extreme Mode)
    // =====================================================================

    public void RegisterStaticThingFire(Fire fire, bool respawningAfterLoad)
    {
        if (fire.parent != null || !fire.Spawned)
        {
            return;
        }
        IntVec3 pos = fire.Position;
        int idx = map.cellIndices.CellToIndex(pos);
        if (thingFireAtCell[idx] == fire)
        {
            return;
        }

        staticFireAtCell[idx] = 2;
        thingFireAtCell[idx] = fire;

        if (FastFireMod.Settings.IsActive && !compatLocator.ContainsKey(fire.thingIDNumber))
        {
            int currentTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            int bucket = (currentTick + Rand.RangeInclusive(1, BucketCount - 1)) % BucketCount;
            int slot = compatBuckets[bucket].Count;
            compatBuckets[bucket].Add(fire);
            compatLocator[fire.thingIDNumber] = ((byte)bucket, slot);
        }

        OnStaticFireAdded(pos, idx, fire.fireSize);

        if (FastFireMod.Settings.UseGridEngine && !isSpawningProxy && !respawningAfterLoad && initializedAfterLoad)
        {
            // Third-party mod or debug tool directly GenSpawned a Fire Thing while ExtremeGrid is active.
            // Absorb on next tick or keep as proxy; keeping short-lived proxy avoids breaking caller's Thing ref immediately.
            proxySpawnTicks[fire.thingIDNumber] = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
        }
    }

    public void DeregisterStaticThingFire(Fire fire, IntVec3 pos)
    {
        if (!pos.InBounds(map))
        {
            return;
        }
        int idx = map.cellIndices.CellToIndex(pos);
        if (thingFireAtCell[idx] == fire)
        {
            thingFireAtCell[idx] = null;
            if (staticFireAtCell[idx] == 2)
            {
                staticFireAtCell[idx] = 0;
            }
            OnStaticFireRemoved(pos, idx, fire.fireSize);
        }

        if (compatLocator.TryGetValue(fire.thingIDNumber, out var loc))
        {
            compatLocator.Remove(fire.thingIDNumber);
            List<Fire> list = compatBuckets[loc.bucket];
            int lastIdx = list.Count - 1;
            if (loc.slot <= lastIdx)
            {
                if (loc.slot != lastIdx)
                {
                    Fire moved = list[lastIdx];
                    list[loc.slot] = moved;
                    compatLocator[moved.thingIDNumber] = (loc.bucket, loc.slot);
                }
                list.RemoveAt(lastIdx);
            }
        }
        proxySpawnTicks.Remove(fire.thingIDNumber);
    }

    // =====================================================================
    // Shared Spatial Accounting (Lighting, AI Chunks, HomeArea, Pathing)
    // =====================================================================

    private void OnStaticFireAdded(IntVec3 pos, int cellIndex, float fireSize)
    {
        float safeSize = Mathf.Clamp(fireSize, 0f, 100f);
        trackedFireSizeAtCell[cellIndex] = safeSize;
        totalStaticFireCount++;
        totalStaticFireSizeSum += safeSize;

        int aiIdx = AiChunkIndex(pos.x, pos.z);
        aiChunkFireCount[aiIdx]++;
        aiChunkFireSizeSum[aiIdx] += safeSize;

        if (FastFireMod.Settings.UseClusterLighting)
        {
            IncrementLightChunk(pos);
        }

        if (map.areaManager?.Home != null && map.areaManager.Home[pos] && !map.fogGrid.IsFogged(cellIndex))
        {
            homeAreaUnfoggedFireCount++;
            if (cachedFirstHomeFireCell < 0)
            {
                cachedFirstHomeFireCell = cellIndex;
            }
        }

        if (!suppressPathDirty && FastFireMod.Settings.UsePathingDebounce)
        {
            MarkPathDirtyAround(pos);
        }
    }

    private void OnStaticFireRemoved(IntVec3 pos, int cellIndex, float fireSize)
    {
        float tracked = trackedFireSizeAtCell[cellIndex];
        trackedFireSizeAtCell[cellIndex] = 0f;
        totalStaticFireCount = Math.Max(0, totalStaticFireCount - 1);
        totalStaticFireSizeSum = Math.Max(0.0, totalStaticFireSizeSum - tracked);

        int aiIdx = AiChunkIndex(pos.x, pos.z);
        if (aiChunkFireCount[aiIdx] > 0)
        {
            aiChunkFireCount[aiIdx]--;
        }
        aiChunkFireSizeSum[aiIdx] = Math.Max(0f, aiChunkFireSizeSum[aiIdx] - tracked);

        if (FastFireMod.Settings.UseClusterLighting)
        {
            DecrementLightChunk(pos);
        }

        if (map.areaManager?.Home != null && map.areaManager.Home[pos] && !map.fogGrid.IsFogged(cellIndex))
        {
            homeAreaUnfoggedFireCount = Math.Max(0, homeAreaUnfoggedFireCount - 1);
            if (cachedFirstHomeFireCell == cellIndex)
            {
                cachedFirstHomeFireCell = -1;
            }
        }

        if (!suppressPathDirty && FastFireMod.Settings.UsePathingDebounce)
        {
            MarkPathDirtyAround(pos);
        }
    }

    private void UpdateStaticFireSizeDelta(IntVec3 pos, float oldSize, float newSize)
    {
        int cellIndex = map.cellIndices.CellToIndex(pos);
        float prevTracked = trackedFireSizeAtCell[cellIndex];
        float nextTracked = Mathf.Clamp(newSize, 0f, 100f);
        float delta = nextTracked - prevTracked;
        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }
        trackedFireSizeAtCell[cellIndex] = nextTracked;
        totalStaticFireSizeSum = Math.Max(0.0, totalStaticFireSizeSum + delta);
        int aiIdx = AiChunkIndex(pos.x, pos.z);
        aiChunkFireSizeSum[aiIdx] = Math.Max(0f, aiChunkFireSizeSum[aiIdx] + delta);
    }

    private void UpdateGridNeighborCounts(IntVec3 center, int delta)
    {
        for (int i = 0; i < 8; i++)
        {
            IntVec3 n = center + GenAdj.AdjacentCells[i];
            if ((uint)n.x < (uint)sizeX && (uint)n.z < (uint)sizeZ)
            {
                int nIdx = map.cellIndices.CellToIndex(n);
                int next = gridNeighborFireCount[nIdx] + delta;
                gridNeighborFireCount[nIdx] = (byte)Mathf.Clamp(next, 0, 8);
            }
        }
    }

    private void IncrementLightChunk(IntVec3 pos)
    {
        int cIdx = LightChunkIndex(pos.x, pos.z);
        if (lightChunkFireCount[cIdx] == 0)
        {
            RegisterClusterGlower(cIdx, pos);
        }
        lightChunkFireCount[cIdx]++;
    }

    private void DecrementLightChunk(IntVec3 pos)
    {
        int cIdx = LightChunkIndex(pos.x, pos.z);
        if (lightChunkFireCount[cIdx] == 0)
        {
            return;
        }
        lightChunkFireCount[cIdx]--;
        if (lightChunkFireCount[cIdx] == 0)
        {
            DeregisterClusterGlower(cIdx);
        }
    }

    private void RegisterClusterGlower(int chunkIdx, IntVec3 anchorCell)
    {
        if (map.glowGrid == null)
        {
            return;
        }
        ColorInt fireColor = new ColorInt(184, 136, 83, 0);
        const float clusterGlowRadius = 5.5f;
#if RW17
        if (lightChunkGlowerHandle[chunkIdx] == 0)
        {
            lightChunkGlowerHandle[chunkIdx] = map.glowGrid.RegisterCustomGlower(anchorCell, clusterGlowRadius, fireColor);
        }
#else
        if (lightChunkDummyGlowers[chunkIdx] == null)
        {
            var dummy = new ThingWithComps
            {
                def = ThingDefOf.Fire,
                thingIDNumber = -100000 - chunkIdx
            };
            dummy.SetPositionDirect(anchorCell);
            var glower = new CompGlower
            {
                parent = dummy,
                props = clusterGlowerProps ??= new CompProperties_Glower
                {
                    compClass = typeof(CompGlower),
                    glowRadius = clusterGlowRadius,
                    glowColor = fireColor
                }
            };
            glower.glowOnInt = true;
            map.glowGrid.RegisterGlower(glower);
            lightChunkDummyGlowers[chunkIdx] = glower;
        }
#endif
    }

#if !RW17
    private static CompProperties_Glower clusterGlowerProps;
#endif

    private void DeregisterClusterGlower(int chunkIdx)
    {
        if (map.glowGrid == null)
        {
            return;
        }
#if RW17
        int handle = lightChunkGlowerHandle[chunkIdx];
        if (handle != 0)
        {
            map.glowGrid.DeregisterCustomGlower(handle);
            lightChunkGlowerHandle[chunkIdx] = 0;
        }
#else
        CompGlower glower = lightChunkDummyGlowers[chunkIdx];
        if (glower != null)
        {
            glower.glowOnInt = false;
            map.glowGrid.DeRegisterGlower(glower);
            lightChunkDummyGlowers[chunkIdx] = null;
        }
#endif
    }

    public void MarkPathDirtyAround(IntVec3 center)
    {
        for (int i = 0; i < 9; i++)
        {
            IntVec3 c = center + GenAdj.AdjacentCellsAndInside[i];
            if ((uint)c.x < (uint)sizeX && (uint)c.z < (uint)sizeZ)
            {
                int idx = map.cellIndices.CellToIndex(c);
                if (pathDirtyFlags[idx] == 0)
                {
                    pathDirtyFlags[idx] = 1;
                    pathDirtyCells.Add(idx);
                }
            }
        }
    }

    public void FlushPendingPathUpdates(bool force = false)
    {
        if (pathDirtyCells.Count == 0)
        {
            return;
        }
        int ticksGame = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
        if (!force && ticksGame - lastPathFlushTick < 15)
        {
            return;
        }
        lastPathFlushTick = ticksGame;

        for (int i = 0; i < pathDirtyCells.Count; i++)
        {
            int idx = pathDirtyCells[i];
            pathDirtyFlags[idx] = 0;
            IntVec3 c = map.cellIndices.IndexToCell(idx);
            map.pathing.RecalculatePerceivedPathCostAt(c);
        }
        pathDirtyCells.Clear();
    }

    // =====================================================================
    // Tick & Simulation Loop
    // =====================================================================

    public override void MapComponentTick()
    {
        if (!initializedAfterLoad)
        {
            return;
        }

        if (FastFireMod.Settings.UseAudioAggregation)
        {
            MaintainActiveAudioClusters();
        }

        if (!FastFireMod.Settings.IsActive)
        {
            return;
        }

        int ticksGame = Find.TickManager.TicksGame;
        int bucket = ticksGame % BucketCount;

        // Tick grid fires (ExtremeGrid mode)
        List<int> gBucket = gridBuckets[bucket];
        for (int i = gBucket.Count - 1; i >= 0; i--)
        {
            TickGridFireAt(gBucket[i]);
        }

        // Tick static Fire Things in our bucket (Compatible mode + proxies in ExtremeGrid)
        List<Fire> cBucket = compatBuckets[bucket];
        if (cBucket.Count > 0)
        {
            BeginBatchDespawnScope();
            try
            {
                for (int i = cBucket.Count - 1; i >= 0; i--)
                {
                    Fire fire = cBucket[i];
                    if (fire == null || fire.Destroyed || !fire.Spawned)
                    {
                        continue;
                    }
                    TickCompatibleFire(fire, BucketCount);
                }
            }
            finally
            {
                EndBatchDespawnScope();
            }
        }

        if (FastFireMod.Settings.UsePathingDebounce && ticksGame % 15 == 0)
        {
            FlushPendingPathUpdates(force: true);
        }

        if (FastFireMod.Settings.UseAudioAggregation && ticksGame % 15 == 2)
        {
            RefreshClusteredAudioTargets();
        }
    }

    private void TickGridFireAt(int idx)
    {
        if (staticFireAtCell[idx] != 1)
        {
            return;
        }

        IntVec3 pos = map.cellIndices.IndexToCell(idx);
        int ticksSpawn = gridTicksSinceSpawn[idx] + BucketCount;
        gridTicksSinceSpawn[idx] = ticksSpawn;

        float fireSize = gridFireSize[idx];

        // Particles
        int smokeTicks = gridTicksUntilSmoke[idx] - BucketCount;
        if (smokeTicks <= 0)
        {
            if (totalStaticFireCount < FireMath.FireCountParticlesOff)
            {
                FleckMaker.ThrowSmoke(pos.ToVector3Shifted(), map, fireSize);
            }
            if (fireSize > 0.5f && (totalStaticFireCount < 40 || Rand.Value < 0.12f))
            {
                FleckMaker.ThrowFireGlow(pos.ToVector3Shifted(), map, fireSize);
            }
            smokeTicks = FireMath.NextSmokeDelayTicks(fireSize);
        }
        gridTicksUntilSmoke[idx] = smokeTicks;

        if (totalStaticFireCount < FireMath.FireCountParticlesOff && fireSize > 0.7f && Rand.Value < fireSize * 0.01f)
        {
            FleckMaker.ThrowMicroSparks(pos.ToVector3Shifted(), map);
        }

        // Spread
        if (fireSize > 1f)
        {
            int spreadTicks = gridTicksSinceSpread[idx] + BucketCount;
            if ((float)spreadTicks >= FireMath.SpreadIntervalForSize(fireSize))
            {
                gridInstigators.TryGetValue(idx, out Thing instigator);
                FireMath.TrySpreadFrom(pos, map, instigator, instigator, this);
                spreadTicks = 0;
            }
            gridTicksSinceSpread[idx] = spreadTicks;
        }

        // Complex calcs every ~150 ticks, staggered by cell index to avoid synchronized spikes
        if ((((ticksSpawn / BucketCount) + idx) % 10) == 0)
        {
            gridInstigators.TryGetValue(idx, out Thing instigator);
            float oldSize = fireSize;
            float flammMax = gridFlammabilityMax[idx];
            FireMath.DoComplexCalcsStatic(
                map, this, pos, idx,
                ref fireSize, ref flammMax,
                instigator, null, out bool extinguished);

            if (extinguished)
            {
                ExtinguishGridFireAt(idx, pos);
                return;
            }
            gridFlammabilityMax[idx] = flammMax;
            gridFireSize[idx] = fireSize;
            UpdateStaticFireSizeDelta(pos, oldSize, fireSize);
        }

        // Burn floor
        if (ticksSpawn >= FireMath.TicksToBurnFloor)
        {
            TerrainDef terrain = pos.GetTerrain(map);
            TerrainDef foundation = map.terrainGrid.FoundationAt(idx);
            if (FireMath.GetTerrainBaseFlammability(terrain) > 0.01f || (foundation != null && FireMath.GetTerrainBaseFlammability(foundation) > 0.01f))
            {
                if (pos.TerrainFlammableNow(map))
                {
                    map.terrainGrid.Notify_TerrainBurned(pos);
                }
            }
        }
    }

    private void TickCompatibleFire(Fire fire, int delta)
    {
        fire.ticksSinceSpawn += delta;
        IntVec3 pos = fire.Position;
        int idx = map.cellIndices.CellToIndex(pos);

        // If ExtremeGrid mode is active, check whether an unreserved proxy can be absorbed back into FireGrid
        if (FastFireMod.Settings.UseGridEngine
            && proxySpawnTicks.TryGetValue(fire.thingIDNumber, out int spawnTick)
            && Find.TickManager.TicksGame - spawnTick >= ProxyReabsorbTicks
            && !IsFireActivelyTargetedOrReserved(fire))
        {
            AbsorbThingFireIntoGrid(fire);
            return;
        }

        // Particles (same vanilla thresholds, plus FireGlow throttling)
        fire.ticksUntilSmoke -= delta;
        if (fire.ticksUntilSmoke <= 0)
        {
            if (totalStaticFireCount < FireMath.FireCountParticlesOff)
            {
                FleckMaker.ThrowSmoke(fire.DrawPos, map, fire.fireSize);
            }
            if (fire.fireSize > 0.5f && fire.parent == null && (totalStaticFireCount < 40 || Rand.Value < 0.12f))
            {
                FleckMaker.ThrowFireGlow(pos.ToVector3Shifted(), map, fire.fireSize);
            }
            fire.ticksUntilSmoke = FireMath.NextSmokeDelayTicks(fire.fireSize);
        }
        if (totalStaticFireCount < FireMath.FireCountParticlesOff && fire.fireSize > 0.7f && Rand.Value < fire.fireSize * 0.01f)
        {
            FleckMaker.ThrowMicroSparks(fire.DrawPos, map);
        }

        // Spread
        if (fire.fireSize > 1f)
        {
            fire.ticksSinceSpread += delta;
            if ((float)fire.ticksSinceSpread >= FireMath.SpreadIntervalForSize(fire.fireSize))
            {
                FireMath.TrySpreadFrom(pos, map, fire.instigator, fire, this);
                fire.ticksSinceSpread = 0;
            }
        }

        // Complex calcs every 150 ticks, staggered by thingIDNumber to match vanilla hash spread
        if ((((fire.ticksSinceSpawn / delta) + fire.thingIDNumber) % 10) == 0)
        {
            float oldSize = fire.fireSize;
            float newSize = oldSize;
            float flammMax = fire.flammabilityMax;
            FireMath.DoComplexCalcsStatic(
                map, this, pos, idx,
                ref newSize, ref flammMax,
                fire.instigator, fire, out bool extinguished);

            if (extinguished)
            {
                if (!fire.Destroyed)
                {
                    fire.Destroy();
                }
                return;
            }
            fire.flammabilityMax = flammMax;
            fire.fireSize = newSize;
            UpdateStaticFireSizeDelta(pos, oldSize, newSize);
        }

        if (fire.ticksSinceSpawn >= FireMath.TicksToBurnFloor)
        {
            TerrainDef terrain = pos.GetTerrain(map);
            TerrainDef foundation = map.terrainGrid.FoundationAt(idx);
            if (FireMath.GetTerrainBaseFlammability(terrain) > 0.01f || (foundation != null && FireMath.GetTerrainBaseFlammability(foundation) > 0.01f))
            {
                if (pos.TerrainFlammableNow(map))
                {
                    map.terrainGrid.Notify_TerrainBurned(pos);
                }
            }
        }
    }

    private bool IsFireActivelyTargetedOrReserved(Fire fire)
    {
        if (map.reservationManager.IsReserved(fire))
        {
            return true;
        }
        IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn p = pawns[i];
            Job cur = p.CurJob;
            if (cur == null)
            {
                continue;
            }
            if (cur.targetA.Thing == fire)
            {
                return true;
            }
            List<LocalTargetInfo> q = cur.targetQueueA;
            if (q != null)
            {
                for (int j = 0; j < q.Count; j++)
                {
                    if (q[j].Thing == fire)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private void AbsorbThingFireIntoGrid(Fire fire)
    {
        if (fire == null || fire.Destroyed || !fire.Spawned || fire.parent != null)
        {
            return;
        }
        IntVec3 pos = fire.Position;
        int idx = map.cellIndices.CellToIndex(pos);
        float size = fire.fireSize;
        int spawnedTicks = fire.ticksSinceSpawn;
        int spreadTicks = fire.ticksSinceSpread;
        int smokeTicks = fire.ticksUntilSmoke;
        float flammMax = fire.flammabilityMax;
        Thing instigator = fire.instigator;

        bool prevSuppress = suppressPathDirty;
        suppressPathDirty = true;
        try
        {
            fire.Destroy();
            if (staticFireAtCell[idx] == 0)
            {
                TryAddGridFire(pos, size, instigator, spawnedTicks);
                gridTicksSinceSpread[idx] = spreadTicks;
                gridTicksUntilSmoke[idx] = smokeTicks;
                gridFlammabilityMax[idx] = flammMax;
            }
        }
        finally
        {
            suppressPathDirty = prevSuppress;
        }
    }

    // =====================================================================
    // Batch Despawn Acceleration for Compatible Mode
    // =====================================================================

    public void BeginBatchDespawnScope()
    {
        batchDespawnDepth++;
    }

    public void EndBatchDespawnScope()
    {
        if (batchDespawnDepth <= 0)
        {
            return;
        }
        batchDespawnDepth--;
        if (batchDespawnDepth == 0)
        {
            FlushBatchDespawn();
        }
    }

    public bool TryDeferBatchDespawnRemoval(Thing thing)
    {
        if (batchDespawnDepth <= 0 || !(thing is Fire { parent: null }))
        {
            return false;
        }
        pendingBatchRemovedThings.Add(thing);
        return true;
    }

    public void RecordDirtyRegionForBatchDespawn(Region region)
    {
        if (region != null)
        {
            pendingDirtyRegions.Add(region);
        }
    }

    private void FlushBatchDespawn()
    {
        if (pendingBatchRemovedThings.Count == 0)
        {
            return;
        }

        List<Thing> spawnedList = ((ThingOwner<Thing>)map.spawnedThings).innerList;
        if (pendingBatchRemovedThings.Count < 8)
        {
            foreach (Thing t in pendingBatchRemovedThings)
            {
                spawnedList.Remove(t);
                map.listerThings.Remove(t);
            }
            foreach (Region reg in pendingDirtyRegions)
            {
                ListerThings rl = reg.ListerThings;
                foreach (Thing t in pendingBatchRemovedThings)
                {
                    if (rl.Contains(t))
                    {
                        rl.Remove(t);
                    }
                }
            }
            pendingBatchRemovedThings.Clear();
            pendingDirtyRegions.Clear();
            return;
        }

        // Single-pass O(N) batch removal instead of O(K * N) individual List.Remove calls!
        spawnedList.RemoveAll(t => pendingBatchRemovedThings.Contains(t));

        RemoveAllFromLister(map.listerThings, ThingDefOf.Fire, pendingBatchRemovedThings);

        foreach (Region reg in pendingDirtyRegions)
        {
            RemoveAllFromLister(reg.ListerThings, ThingDefOf.Fire, pendingBatchRemovedThings);
        }

        pendingBatchRemovedThings.Clear();
        pendingDirtyRegions.Clear();
    }

    private static void RemoveAllFromLister(ListerThings lister, ThingDef def, HashSet<Thing> toRemove)
    {
        if (lister.listsByDef.TryGetValue(def, out List<Thing> defList) && defList.Count > 0)
        {
            defList.RemoveAll(t => toRemove.Contains(t));
        }

        List<Thing> everythingList = lister.listsByGroup[(uint)ThingRequestGroup.Everything];
        if (everythingList != null && everythingList.Count > 0)
        {
            if (everythingList.RemoveAll(t => toRemove.Contains(t)) > 0)
            {
                lister.stateHashByGroup[(uint)ThingRequestGroup.Everything]++;
            }
        }

        List<Thing> fireGroupList = lister.listsByGroup[(uint)ThingRequestGroup.Fire];
        if (fireGroupList != null && fireGroupList.Count > 0)
        {
            if (fireGroupList.RemoveAll(t => toRemove.Contains(t)) > 0)
            {
                lister.stateHashByGroup[(uint)ThingRequestGroup.Fire]++;
            }
        }
    }

    // =====================================================================
    // Clustered Camera-Local Audio
    // =====================================================================

    private void MaintainActiveAudioClusters()
    {
        for (int i = 0; i < MaxAudioSustainers; i++)
        {
            Sustainer s = audioSlots[i].sustainer;
            if (s != null && !s.Ended)
            {
                s.Maintain();
            }
        }
    }

    private void RefreshClusteredAudioTargets()
    {
        if (Find.CurrentMap != map || WorldRendererUtility.WorldSelected || totalStaticFireCount == 0 || Find.SoundRoot?.sustainerManager == null)
        {
            EndAllAudioClusters();
            return;
        }

        IntVec3 camPos = Find.CameraDriver.MapPosition;
        int minCx = Math.Max(0, (camPos.x - (int)AudioMaxDistance) >> AiChunkShift);
        int maxCx = Math.Min(aiChunkW - 1, (camPos.x + (int)AudioMaxDistance) >> AiChunkShift);
        int minCz = Math.Max(0, (camPos.z - (int)AudioMaxDistance) >> AiChunkShift);
        int maxCz = Math.Min(aiChunkH - 1, (camPos.z + (int)AudioMaxDistance) >> AiChunkShift);

        for (int k = 0; k < MaxAudioSustainers; k++)
        {
            tmpTopAudioChunks[k] = -1;
            tmpTopAudioDistSq[k] = float.MaxValue;
        }

        for (int cz = minCz; cz <= maxCz; cz++)
        {
            int centerZ = Math.Min(sizeZ - 1, (cz << AiChunkShift) + (AiChunkSize >> 1));
            int dz = centerZ - camPos.z;
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                int cIdx = cz * aiChunkW + cx;
                if (aiChunkFireCount[cIdx] == 0)
                {
                    continue;
                }
                int centerX = Math.Min(sizeX - 1, (cx << AiChunkShift) + (AiChunkSize >> 1));
                int dx = centerX - camPos.x;
                float distSq = dx * dx + dz * dz;
                if (distSq > AudioMaxDistanceSq)
                {
                    continue;
                }

                // Insert into top 4 nearest
                for (int k = 0; k < MaxAudioSustainers; k++)
                {
                    if (distSq < tmpTopAudioDistSq[k])
                    {
                        for (int m = MaxAudioSustainers - 1; m > k; m--)
                        {
                            tmpTopAudioChunks[m] = tmpTopAudioChunks[m - 1];
                            tmpTopAudioDistSq[m] = tmpTopAudioDistSq[m - 1];
                        }
                        tmpTopAudioChunks[k] = cIdx;
                        tmpTopAudioDistSq[k] = distSq;
                        break;
                    }
                }
            }
        }

        // End slots whose chunk is no longer in top 4
        for (int sIdx = 0; sIdx < MaxAudioSustainers; sIdx++)
        {
            int chunk = audioSlots[sIdx].chunkIndex;
            if (chunk < 0)
            {
                continue;
            }
            bool keep = false;
            for (int k = 0; k < MaxAudioSustainers; k++)
            {
                if (tmpTopAudioChunks[k] == chunk)
                {
                    keep = true;
                    break;
                }
            }
            if (!keep)
            {
                if (audioSlots[sIdx].sustainer != null && !audioSlots[sIdx].sustainer.Ended)
                {
                    audioSlots[sIdx].sustainer.End();
                }
                audioSlots[sIdx].sustainer = null;
                audioSlots[sIdx].chunkIndex = -1;
            }
        }

        // Update existing or spawn missing
        for (int k = 0; k < MaxAudioSustainers; k++)
        {
            int targetChunk = tmpTopAudioChunks[k];
            if (targetChunk < 0)
            {
                continue;
            }
            int foundSlot = -1;
            int freeSlot = -1;
            for (int sIdx = 0; sIdx < MaxAudioSustainers; sIdx++)
            {
                if (audioSlots[sIdx].chunkIndex == targetChunk)
                {
                    foundSlot = sIdx;
                    break;
                }
                if (audioSlots[sIdx].chunkIndex < 0 && freeSlot < 0)
                {
                    freeSlot = sIdx;
                }
            }

            int useSlot = foundSlot >= 0 ? foundSlot : freeSlot;
            if (useSlot < 0)
            {
                continue;
            }

            audioSlots[useSlot].reporter.CachedSize = aiChunkFireSizeSum[targetChunk];
            if (audioSlots[useSlot].sustainer == null || audioSlots[useSlot].sustainer.Ended)
            {
                int cz = targetChunk / aiChunkW;
                int cx = targetChunk - cz * aiChunkW;
                IntVec3 center = new IntVec3(
                    Math.Min(sizeX - 1, (cx << AiChunkShift) + (AiChunkSize >> 1)),
                    0,
                    Math.Min(sizeZ - 1, (cz << AiChunkShift) + (AiChunkSize >> 1)));

                if (!center.Fogged(map))
                {
                    SoundInfo info = SoundInfo.InMap(new TargetInfo(center, map), MaintenanceType.PerTick);
                    Sustainer sus = SoundDefOf.FireBurning.TrySpawnSustainer(info);
                    if (sus != null)
                    {
                        if (sus.externalParams.sizeAggregator == null)
                        {
                            sus.externalParams.sizeAggregator = new SoundSizeAggregator();
                        }
                        sus.externalParams.sizeAggregator.RegisterReporter(audioSlots[useSlot].reporter);
                        audioSlots[useSlot].chunkIndex = targetChunk;
                        audioSlots[useSlot].sustainer = sus;
                    }
                }
            }
        }
    }

    private void EndAllAudioClusters()
    {
        for (int i = 0; i < MaxAudioSustainers; i++)
        {
            if (audioSlots[i].sustainer != null && !audioSlots[i].sustainer.Ended)
            {
                audioSlots[i].sustainer.End();
            }
            audioSlots[i].sustainer = null;
            audioSlots[i].chunkIndex = -1;
        }
    }

    // =====================================================================
    // Batched / Instanced Fire Rendering
    // =====================================================================

    public void DrawBatchedFires()
    {
        if (totalStaticFireCount == 0)
        {
            return;
        }
        if (gridFireCount == 0 && !FastFireMod.Settings.UseBatchedRendering)
        {
            return;
        }

        if (!(ThingDefOf.Fire.graphic is Graphic_Collection collection) || collection.subGraphics == null || collection.subGraphics.Length == 0)
        {
            return;
        }

        Graphic[] subGraphics = collection.subGraphics;
        int subCount = subGraphics.Length;
        EnsureRenderBuffers(subCount);

        CellRect viewRect = Find.CameraDriver.CurrentViewRect.ExpandedBy(1);
        if (WorldComponent_GravshipController.GravshipRenderInProgess)
        {
            viewRect = viewRect.Encapsulate(WorldComponent_GravshipController.GravshipRenderBounds);
        }
        viewRect.ClipInsideMap(map);
        if (viewRect.IsEmpty)
        {
            return;
        }

        int minCx = viewRect.minX >> AiChunkShift;
        int maxCx = viewRect.maxX >> AiChunkShift;
        int minCz = viewRect.minZ >> AiChunkShift;
        int maxCz = viewRect.maxZ >> AiChunkShift;

        int ticksGame = Find.TickManager.TicksGame;
        float altitude = ThingDefOf.Fire.Altitude;
        IntVec3[] radialPattern = GenRadial.RadialPattern;
        int radialLen = radialPattern.Length;
        float invMaxRadius = 0.05f / GenRadial.MaxRadialPatternRadius;
        FogGrid fogGrid = map.fogGrid;
        bool useInstancing = ShouldUseInstancing();
        bool batchModeRender = FastFireMod.Settings.UseBatchedRendering;

        if (!useInstancing)
        {
            for (int s = 0; s < subCount; s++)
            {
                meshVerts[s].Clear();
                meshUvs[s].Clear();
                meshTris[s].Clear();
                dynamicMeshCursor[s] = 0;
            }
        }
        else
        {
            for (int s = 0; s < subCount; s++)
            {
                instancedBatchCounts[s] = 0;
            }
        }

        for (int cz = minCz; cz <= maxCz; cz++)
        {
            int zStart = Math.Max(viewRect.minZ, cz << AiChunkShift);
            int zEnd = Math.Min(viewRect.maxZ, ((cz + 1) << AiChunkShift) - 1);
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                int aiIdx = cz * aiChunkW + cx;
                if (aiChunkFireCount[aiIdx] == 0)
                {
                    continue;
                }

                int xStart = Math.Max(viewRect.minX, cx << AiChunkShift);
                int xEnd = Math.Min(viewRect.maxX, ((cx + 1) << AiChunkShift) - 1);

                for (int z = zStart; z <= zEnd; z++)
                {
                    int rowBase = z * sizeX;
                    for (int x = xStart; x <= xEnd; x++)
                    {
                        int idx = rowBase + x;
                        byte state = staticFireAtCell[idx];
                        if (state == 0 || fogGrid.IsFogged(idx))
                        {
                            continue;
                        }
                        if (state == 2 && !batchModeRender)
                        {
                            continue;
                        }

                        float fireSize;
                        int seed;
                        if (state == 1)
                        {
                            fireSize = gridFireSize[idx];
                            seed = ((x * 73856093) ^ (z * 19349663)) & 0x7FFFFFFF;
                        }
                        else
                        {
                            Fire f = thingFireAtCell[idx];
                            if (f == null || f.Destroyed)
                            {
                                continue;
                            }
                            fireSize = f.fireSize;
                            seed = f.thingIDNumber;
                        }

                        int num = ticksGame + Mathf.Abs(seed ^ 0x80FD52);
                        int num2 = num / 15;
                        int subIdx = Mathf.Abs(num2 ^ (seed * 391)) % subCount;
                        float drawScale = Mathf.Min(fireSize / 1.2f, 1.2f);
                        IntVec3 pat = radialPattern[num2 % radialLen];
                        float jx = pat.x * invMaxRadius * fireSize;
                        float jz = pat.z * invMaxRadius * fireSize;
                        float cxPos = x + 0.5f + jx;
                        float czPos = z + 0.5f + jz;

                        if (useInstancing)
                        {
                            int count = instancedBatchCounts[subIdx];
                            Matrix4x4[] batch = instancedBatches[subIdx];
                            batch[count] = Matrix4x4.TRS(new Vector3(cxPos, altitude, czPos), Quaternion.identity, new Vector3(drawScale, 1f, drawScale));
                            count++;
                            if (count == MaxInstancedBatch)
                            {
                                Material mat = subGraphics[subIdx].MatSingle;
                                mat.enableInstancing = true;
                                Graphics.DrawMeshInstanced(MeshPool.plane10, 0, mat, batch, count, sharedPropBlock, ShadowCastingMode.Off, receiveShadows: false, 0);
                                count = 0;
                            }
                            instancedBatchCounts[subIdx] = count;
                        }
                        else
                        {
                            float half = drawScale * 0.5f;
                            List<Vector3> verts = meshVerts[subIdx];
                            List<Vector2> uvs = meshUvs[subIdx];
                            List<int> tris = meshTris[subIdx];
                            int vBase = verts.Count;
                            verts.Add(new Vector3(cxPos - half, altitude, czPos - half));
                            verts.Add(new Vector3(cxPos - half, altitude, czPos + half));
                            verts.Add(new Vector3(cxPos + half, altitude, czPos + half));
                            verts.Add(new Vector3(cxPos + half, altitude, czPos - half));
                            uvs.Add(new Vector2(0f, 0f));
                            uvs.Add(new Vector2(0f, 1f));
                            uvs.Add(new Vector2(1f, 1f));
                            uvs.Add(new Vector2(1f, 0f));
                            tris.Add(vBase);
                            tris.Add(vBase + 1);
                            tris.Add(vBase + 2);
                            tris.Add(vBase);
                            tris.Add(vBase + 2);
                            tris.Add(vBase + 3);

                            if ((verts.Count >> 2) >= MaxMeshQuadsPerDraw)
                            {
                                FlushDynamicMeshChunk(subIdx, subGraphics[subIdx].MatSingle);
                            }
                        }
                    }
                }
            }
        }

        if (useInstancing)
        {
            for (int s = 0; s < subCount; s++)
            {
                int count = instancedBatchCounts[s];
                if (count > 0)
                {
                    Material mat = subGraphics[s].MatSingle;
                    mat.enableInstancing = true;
                    Graphics.DrawMeshInstanced(MeshPool.plane10, 0, mat, instancedBatches[s], count, sharedPropBlock, ShadowCastingMode.Off, receiveShadows: false, 0);
                    instancedBatchCounts[s] = 0;
                }
            }
        }
        else
        {
            for (int s = 0; s < subCount; s++)
            {
                if (meshVerts[s].Count > 0)
                {
                    FlushDynamicMeshChunk(s, subGraphics[s].MatSingle);
                }
            }
        }
    }

    private static bool ShouldUseInstancing()
    {
#if RW17
        return SystemInfo.supportsInstancing;
#else
        return false;
#endif
    }

    private void EnsureRenderBuffers(int subCount)
    {
        if (instancedBatches != null && instancedBatches.Length == subCount)
        {
            return;
        }
        instancedBatches = new Matrix4x4[subCount][];
        instancedBatchCounts = new int[subCount];
        meshVerts = new List<Vector3>[subCount];
        meshUvs = new List<Vector2>[subCount];
        meshTris = new List<int>[subCount];
        pooledMeshes = new List<Mesh>[subCount];
        dynamicMeshCursor = new int[subCount];
        for (int i = 0; i < subCount; i++)
        {
            instancedBatches[i] = new Matrix4x4[MaxInstancedBatch];
            meshVerts[i] = new List<Vector3>(1024);
            meshUvs[i] = new List<Vector2>(1024);
            meshTris[i] = new List<int>(1536);
            pooledMeshes[i] = new List<Mesh>(2);
        }
        sharedPropBlock = new MaterialPropertyBlock();
        sharedPropBlock.SetVectorArray("_Color", WhiteColors1023);
    }

    private int[] dynamicMeshCursor;

    private void FlushDynamicMeshChunk(int subIdx, Material mat)
    {
        List<Mesh> list = pooledMeshes[subIdx];
        int mIdx = dynamicMeshCursor[subIdx]++;
        while (list.Count <= mIdx)
        {
            Mesh m = new Mesh();
            m.MarkDynamic();
            list.Add(m);
        }
        Mesh mesh = list[mIdx];
        mesh.Clear();
        mesh.SetVertices(meshVerts[subIdx]);
        mesh.SetUVs(0, meshUvs[subIdx]);
        mesh.SetTriangles(meshTris[subIdx], 0);
        Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, 0);
        meshVerts[subIdx].Clear();
        meshUvs[subIdx].Clear();
        meshTris[subIdx].Clear();
    }

    // =====================================================================
    // AI / Watcher / Alert / Firefighting Helpers
    // =====================================================================

    public int GetNearbyAiChunkFireCount(IntVec3 center, int radius)
    {
        int minCx = Math.Max(0, (center.x - radius) >> AiChunkShift);
        int maxCx = Math.Min(aiChunkW - 1, (center.x + radius) >> AiChunkShift);
        int minCz = Math.Max(0, (center.z - radius) >> AiChunkShift);
        int maxCz = Math.Min(aiChunkH - 1, (center.z + radius) >> AiChunkShift);
        int sum = 0;
        for (int cz = minCz; cz <= maxCz; cz++)
        {
            int row = cz * aiChunkW;
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                sum += aiChunkFireCount[row + cx];
            }
        }
        return sum;
    }

    public bool TryGetFirstHomeAreaUnfoggedFireCell(out IntVec3 cell)
    {
        if (homeAreaUnfoggedFireCount <= 0)
        {
            cell = IntVec3.Invalid;
            return false;
        }
        Area_Home home = map.areaManager?.Home;
        if (home == null)
        {
            cell = IntVec3.Invalid;
            return false;
        }

        if (cachedFirstHomeFireCell >= 0
            && cachedFirstHomeFireCell < numCells
            && staticFireAtCell[cachedFirstHomeFireCell] != 0)
        {
            IntVec3 c = map.cellIndices.IndexToCell(cachedFirstHomeFireCell);
            if (home[c] && !map.fogGrid.IsFogged(cachedFirstHomeFireCell))
            {
                cell = c;
                return true;
            }
        }

        // Re-scan active fires to locate a valid home area fire
        foreach (IntVec3 c in home.ActiveCells)
        {
            int idx = map.cellIndices.CellToIndex(c);
            if (staticFireAtCell[idx] != 0 && !map.fogGrid.IsFogged(idx))
            {
                cachedFirstHomeFireCell = idx;
                cell = c;
                return true;
            }
        }

        homeAreaUnfoggedFireCount = 0;
        cachedFirstHomeFireCell = -1;
        cell = IntVec3.Invalid;
        return false;
    }

    public void OnHomeAreaCellChanged(IntVec3 c, bool newVal)
    {
        int idx = map.cellIndices.CellToIndex(c);
        if (staticFireAtCell[idx] == 0 || map.fogGrid.IsFogged(idx))
        {
            return;
        }
        if (newVal)
        {
            homeAreaUnfoggedFireCount++;
            if (cachedFirstHomeFireCell < 0)
            {
                cachedFirstHomeFireCell = idx;
            }
        }
        else
        {
            homeAreaUnfoggedFireCount = Math.Max(0, homeAreaUnfoggedFireCount - 1);
            if (cachedFirstHomeFireCell == idx)
            {
                cachedFirstHomeFireCell = -1;
            }
        }
    }

    private void OnCellFogChanged(IntVec3 c, bool fogged)
    {
        int idx = map.cellIndices.CellToIndex(c);
        if (staticFireAtCell[idx] == 0 || map.areaManager?.Home == null || !map.areaManager.Home[c])
        {
            return;
        }
        if (!fogged)
        {
            homeAreaUnfoggedFireCount++;
            if (cachedFirstHomeFireCell < 0)
            {
                cachedFirstHomeFireCell = idx;
            }
        }
        else
        {
            homeAreaUnfoggedFireCount = Math.Max(0, homeAreaUnfoggedFireCount - 1);
            if (cachedFirstHomeFireCell == idx)
            {
                cachedFirstHomeFireCell = -1;
            }
        }
    }

    public bool TryFindBestHomeAreaFireTarget(Pawn pawn, out Thing bestFireThing, out IntVec3 bestGridCell)
    {
        bestFireThing = null;
        bestGridCell = IntVec3.Invalid;

        IntVec3 pawnPos = pawn.Position;
        Area_Home home = map.areaManager.Home;
        bool canFightFires = !pawn.WorkTagIsDisabled(WorkTags.Firefighting);
        float bestDistSq = float.MaxValue;
        TraverseParms tp = TraverseParms.For(pawn, Danger.Deadly);

        // 1. Check pawn-attached fires (usually 0..5 items)
        for (int i = pawnAttachedFires.Count - 1; i >= 0; i--)
        {
            Fire f = pawnAttachedFires[i];
            if (f == null || f.Destroyed || !f.Spawned || f.parent == null)
            {
                pawnAttachedFires.RemoveAt(i);
                continue;
            }
            float distSq = (pawnPos - f.Position).LengthHorizontalSquared;
            if (distSq >= bestDistSq)
            {
                continue;
            }
            if (!f.IsForbidden(pawn) && WorkGiver_FightFires_ValidateFast(pawn, f))
            {
                bestDistSq = distSq;
                bestFireThing = f;
            }
        }

        // 2. If pawn can fight ground fires and Home has unfogged fires, search spatial 16x16 AI chunks (works for BOTH Grid and Compatible Fire Things!)
        if (canFightFires && totalStaticFireCount > 0 && homeAreaUnfoggedFireCount > 0)
        {
            for (int cz = 0; cz < aiChunkH; cz++)
            {
                int zMin = cz << AiChunkShift;
                int zMax = Math.Min(sizeZ - 1, zMin + AiChunkSize - 1);
                int clampZ = Math.Clamp(pawnPos.z, zMin, zMax);
                int dzMin = pawnPos.z - clampZ;
                for (int cx = 0; cx < aiChunkW; cx++)
                {
                    int aiIdx = cz * aiChunkW + cx;
                    if (aiChunkFireCount[aiIdx] == 0)
                    {
                        continue;
                    }
                    int xMin = cx << AiChunkShift;
                    int xMax = Math.Min(sizeX - 1, xMin + AiChunkSize - 1);
                    int clampX = Math.Clamp(pawnPos.x, xMin, xMax);
                    int dxMin = pawnPos.x - clampX;
                    if (dxMin * dxMin + dzMin * dzMin >= bestDistSq)
                    {
                        continue;
                    }

                    for (int z = zMin; z <= zMax; z++)
                    {
                        int row = z * sizeX;
                        for (int x = xMin; x <= xMax; x++)
                        {
                            int idx = row + x;
                            byte state = staticFireAtCell[idx];
                            if (state == 0)
                            {
                                continue;
                            }
                            IntVec3 c = new IntVec3(x, 0, z);
                            float distSq = (pawnPos - c).LengthHorizontalSquared;
                            if (distSq >= bestDistSq)
                            {
                                continue;
                            }
                            if (!home[c] || c.IsForbidden(pawn))
                            {
                                continue;
                            }
                            if (state == 2)
                            {
                                Fire f = thingFireAtCell[idx];
                                if (f != null && !f.Destroyed && f.Spawned && WorkGiver_FightFires_ValidateFast(pawn, f))
                                {
                                    bestDistSq = distSq;
                                    bestFireThing = f;
                                    bestGridCell = IntVec3.Invalid;
                                }
                                continue;
                            }
                            if (IsCellFireBeingHandled(c, pawn))
                            {
                                continue;
                            }
                            if (!map.reachability.CanReach(pawnPos, c, PathEndMode.Touch, tp))
                            {
                                continue;
                            }
                            bestDistSq = distSq;
                            bestFireThing = null;
                            bestGridCell = c;
                        }
                    }
                }
            }
        }

        return bestFireThing != null || bestGridCell.IsValid;
    }

    private bool WorkGiver_FightFires_ValidateFast(Pawn pawn, Fire fire)
    {
        if (fire.parent is Pawn pawn2)
        {
            if (pawn2 == pawn)
            {
                return false;
            }
            if ((pawn2.Faction == null || pawn2.Faction != pawn.Faction)
                && (pawn2.HostFaction == null || (pawn2.HostFaction != pawn.Faction && pawn2.HostFaction != pawn.HostFaction)))
            {
                return false;
            }
            if (!map.areaManager.Home[fire.Position] && IntVec3Utility.ManhattanDistanceFlat(pawn.Position, pawn2.Position) > 15)
            {
                return false;
            }
            if (!pawn.CanReach(pawn2, PathEndMode.Touch, Danger.Deadly))
            {
                return false;
            }
        }
        else
        {
            if (pawn.WorkTagIsDisabled(WorkTags.Firefighting))
            {
                return false;
            }
            if (!map.areaManager.Home[fire.Position])
            {
                return false;
            }
            if (!pawn.CanReach(fire, PathEndMode.Touch, Danger.Deadly))
            {
                return false;
            }
        }
        if ((pawn.Position - fire.Position).LengthHorizontalSquared > 225 && !pawn.CanReserve(fire))
        {
            return false;
        }
        if (WorkGiver_FightFires.FireIsBeingHandled(fire, pawn))
        {
            return false;
        }
        return true;
    }

    private bool IsCellFireBeingHandled(IntVec3 cell, Pawn potentialHandler)
    {
        // Check if any respected reserver is already handling a fire Thing within 5 cells of cell (matches WorkGiver_FightFires.FireIsBeingHandled)
        List<ReservationManager.Reservation> resList = map.reservationManager.ReservationsReadOnly;
        for (int i = 0; i < resList.Count; i++)
        {
            ReservationManager.Reservation r = resList[i];
            if (r.Target.HasThing && r.Target.Thing is Fire f && f.Spawned)
            {
                if (r.Claimant != null && r.Claimant.Position.InHorDistOf(cell, 5f)
                    && (r.Claimant == potentialHandler || !potentialHandler.HostileTo(r.Claimant)))
                {
                    if (map.reservationManager.FirstRespectedReserver(f, potentialHandler)?.Position.InHorDistOf(cell, 5f) == true)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public Thing FindClosestReachableFireForGenClosest(
        IntVec3 root,
        PathEndMode peMode,
        TraverseParms traverseParams,
        float maxDistance,
        Predicate<Thing> validator)
    {
        float maxDistSq = maxDistance >= 9999f ? float.MaxValue : maxDistance * maxDistance;
        float bestDistSq = maxDistSq;
        Thing bestReal = null;
        IntVec3 bestGridCell = IntVec3.Invalid;

        List<Thing> realFires = map.listerThings.ThingsOfDef(ThingDefOf.Fire);
        for (int i = 0; i < realFires.Count; i++)
        {
            Thing t = realFires[i];
            if (!t.Spawned || t.Destroyed)
            {
                continue;
            }
            float distSq = (root - t.Position).LengthHorizontalSquared;
            if (distSq > bestDistSq)
            {
                continue;
            }
            if (!map.reachability.CanReach(root, t, peMode, traverseParams))
            {
                continue;
            }
            if (validator == null || validator(t))
            {
                bestDistSq = distSq;
                bestReal = t;
            }
        }

        if (gridFireCount > 0)
        {
            // Collect candidate grid cells in range, sorted loosely via chunk pruning
            // Use a small list of candidate cells closer than bestDistSq
            List<(int cellIdx, float distSq)> candidates = new List<(int, float)>(32);
            for (int cz = 0; cz < aiChunkH; cz++)
            {
                int zMin = cz << AiChunkShift;
                int zMax = Math.Min(sizeZ - 1, zMin + AiChunkSize - 1);
                int clampZ = Math.Clamp(root.z, zMin, zMax);
                int dzMin = root.z - clampZ;
                for (int cx = 0; cx < aiChunkW; cx++)
                {
                    int aiIdx = cz * aiChunkW + cx;
                    if (aiChunkFireCount[aiIdx] == 0)
                    {
                        continue;
                    }
                    int xMin = cx << AiChunkShift;
                    int xMax = Math.Min(sizeX - 1, xMin + AiChunkSize - 1);
                    int clampX = Math.Clamp(root.x, xMin, xMax);
                    int dxMin = root.x - clampX;
                    if (dxMin * dxMin + dzMin * dzMin > bestDistSq)
                    {
                        continue;
                    }

                    for (int z = zMin; z <= zMax; z++)
                    {
                        int row = z * sizeX;
                        for (int x = xMin; x <= xMax; x++)
                        {
                            int idx = row + x;
                            if (staticFireAtCell[idx] != 1)
                            {
                                continue;
                            }
                            float distSq = (root.x - x) * (root.x - x) + (root.z - z) * (root.z - z);
                            if (distSq <= bestDistSq)
                            {
                                candidates.Add((idx, distSq));
                            }
                        }
                    }
                }
            }

            if (candidates.Count > 0)
            {
                candidates.Sort((a, b) => a.distSq.CompareTo(b.distSq));
                int tested = 0;
                for (int i = 0; i < candidates.Count && tested < 64; i++)
                {
                    var (idx, distSq) = candidates[i];
                    if (distSq >= bestDistSq)
                    {
                        break;
                    }
                    IntVec3 c = map.cellIndices.IndexToCell(idx);
                    if (!map.reachability.CanReach(root, c, peMode, traverseParams))
                    {
                        continue;
                    }
                    if (validator == null)
                    {
                        bestGridCell = c;
                        bestDistSq = distSq;
                        break;
                    }
                    tested++;
                    Fire proxy = MaterializeProxyFireAt(c);
                    if (proxy != null)
                    {
                        if (validator(proxy))
                        {
                            return proxy;
                        }
                        // Rejected by validator (e.g. already in targetQueue or reserved): keep or reabsorb cleanly
                        if (!IsFireActivelyTargetedOrReserved(proxy))
                        {
                            AbsorbThingFireIntoGrid(proxy);
                        }
                    }
                }
            }
        }

        if (bestGridCell.IsValid)
        {
            return MaterializeProxyFireAt(bestGridCell);
        }
        return bestReal;
    }

    public List<Fire> CollectNearbyFiresForFloatMenu(IntVec3 center, int maxCount = 16)
    {
        tmpFoundFires.Clear();
        if (center.InBounds(map) && HasGridFireAt(center))
        {
            Fire centerProxy = MaterializeProxyFireAt(center);
            if (centerProxy != null)
            {
                tmpFoundFires.Add(centerProxy);
            }
        }

        int radius = 14;
        List<(int idx, float distSq)> gridCandidates = new List<(int, float)>(32);

        int xMin = Math.Max(0, center.x - radius);
        int xMax = Math.Min(sizeX - 1, center.x + radius);
        int zMin = Math.Max(0, center.z - radius);
        int zMax = Math.Min(sizeZ - 1, center.z + radius);

        for (int z = zMin; z <= zMax; z++)
        {
            int row = z * sizeX;
            for (int x = xMin; x <= xMax; x++)
            {
                int idx = row + x;
                byte state = staticFireAtCell[idx];
                if (state == 0)
                {
                    continue;
                }
                float dSq = (center.x - x) * (center.x - x) + (center.z - z) * (center.z - z);
                if (dSq > radius * radius)
                {
                    continue;
                }
                if (state == 2 && thingFireAtCell[idx] != null && !thingFireAtCell[idx].Destroyed)
                {
                    tmpFoundFires.Add(thingFireAtCell[idx]);
                }
                else if (state == 1)
                {
                    gridCandidates.Add((idx, dSq));
                }
            }
        }

        gridCandidates.Sort((a, b) => a.distSq.CompareTo(b.distSq));
        int budget = Math.Max(0, maxCount - tmpFoundFires.Count);
        for (int i = 0; i < gridCandidates.Count && i < budget; i++)
        {
            IntVec3 c = map.cellIndices.IndexToCell(gridCandidates[i].idx);
            Fire proxy = MaterializeProxyFireAt(c);
            if (proxy != null)
            {
                tmpFoundFires.Add(proxy);
            }
        }

        tmpFoundFires.Shuffle();
        int centerIdx = tmpFoundFires.FindIndex(f => f.Position == center);
        if (centerIdx >= 0)
        {
            tmpFoundFires.Swap(0, centerIdx);
        }
        return tmpFoundFires;
    }

    public bool TryCheckNearbyFiresForFlee(IntVec3 origin, int minFiresNearby, int distToFire, int radius, out Fire closestFire)
    {
        closestFire = null;
        if (GetNearbyAiChunkFireCount(origin, radius) < minFiresNearby)
        {
            return false;
        }

        float maxRadiusSq = radius * radius;
        float distToFireSq = distToFire * distToFire;
        float closestDistSq = float.MaxValue;
        int closestIdx = -1;
        int count = 0;

        int xMin = Math.Max(0, origin.x - radius);
        int xMax = Math.Min(sizeX - 1, origin.x + radius);
        int zMin = Math.Max(0, origin.z - radius);
        int zMax = Math.Min(sizeZ - 1, origin.z + radius);

        for (int z = zMin; z <= zMax; z++)
        {
            int dz = z - origin.z;
            int row = z * sizeX;
            for (int x = xMin; x <= xMax; x++)
            {
                int idx = row + x;
                if (staticFireAtCell[idx] == 0)
                {
                    continue;
                }
                int dx = x - origin.x;
                float dSq = dx * dx + dz * dz;
                if (dSq > maxRadiusSq)
                {
                    continue;
                }
                count++;
                if (dSq < closestDistSq)
                {
                    closestDistSq = dSq;
                    closestIdx = idx;
                }
            }
        }

        if (closestIdx >= 0 && closestDistSq <= distToFireSq && count >= minFiresNearby)
        {
            if (staticFireAtCell[closestIdx] == 2)
            {
                closestFire = thingFireAtCell[closestIdx];
            }
            else
            {
                closestFire = MaterializeProxyFireAt(map.cellIndices.IndexToCell(closestIdx));
            }
            return closestFire != null;
        }
        return false;
    }

    public bool HasAnyFireNearDropCell(IntVec3 center, float distToFires, int threshold = 5)
    {
        int r = Mathf.CeilToInt(distToFires);
        if (GetNearbyAiChunkFireCount(center, r) < threshold)
        {
            return false;
        }
        float maxSq = distToFires * distToFires;
        int count = 0;
        int xMin = Math.Max(0, center.x - r);
        int xMax = Math.Min(sizeX - 1, center.x + r);
        int zMin = Math.Max(0, center.z - r);
        int zMax = Math.Min(sizeZ - 1, center.z + r);
        for (int z = zMin; z <= zMax; z++)
        {
            int dz = z - center.z;
            int row = z * sizeX;
            for (int x = xMin; x <= xMax; x++)
            {
                if (staticFireAtCell[row + x] != 0)
                {
                    int dx = x - center.x;
                    if (dx * dx + dz * dz <= maxSq)
                    {
                        count++;
                        if (count >= threshold)
                        {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    public void ClearAllFiresForDebug()
    {
        // Clear Grid fires
        for (int b = 0; b < BucketCount; b++)
        {
            List<int> list = gridBuckets[b];
            for (int i = list.Count - 1; i >= 0; i--)
            {
                int idx = list[i];
                ExtinguishGridFireAt(idx, map.cellIndices.IndexToCell(idx));
            }
        }

        // Clear all Fire Things
        BeginBatchDespawnScope();
        try
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
        finally
        {
            EndBatchDespawnScope();
        }
        FlushPendingPathUpdates(force: true);
    }

    // =====================================================================
    // Save / Load & Live Settings Sync
    // =====================================================================

    public override void ExposeData()
    {
        base.ExposeData();
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            savedMode = FastFireMod.Settings.mode;
            int count = gridFireCount;
            if (count > 0)
            {
                savedCellIndices = new int[count];
                savedFireSizes = new float[count];
                savedTicksSinceSpawn = new int[count];
                savedInstCells = null;
                savedInstThings = null;
                int ptr = 0;
                for (int b = 0; b < BucketCount; b++)
                {
                    List<int> list = gridBuckets[b];
                    for (int i = 0; i < list.Count; i++)
                    {
                        int idx = list[i];
                        savedCellIndices[ptr] = idx;
                        savedFireSizes[ptr] = gridFireSize[idx];
                        savedTicksSinceSpawn[ptr] = gridTicksSinceSpawn[idx];
                        ptr++;
                        if (gridInstigators.TryGetValue(idx, out Thing inst) && inst != null && !inst.Destroyed)
                        {
                            savedInstCells ??= new List<int>();
                            savedInstThings ??= new List<Thing>();
                            savedInstCells.Add(idx);
                            savedInstThings.Add(inst);
                        }
                    }
                }
            }
            else
            {
                savedCellIndices = null;
                savedFireSizes = null;
                savedTicksSinceSpawn = null;
                savedInstCells = null;
                savedInstThings = null;
            }
        }

        Scribe_Values.Look(ref savedMode, "savedMode", FireOptimizationMode.ExtremeGrid);

        byte[] cellBytes = Scribe.mode == LoadSaveMode.Saving ? IntArrayToBytes(savedCellIndices) : null;
        byte[] sizeBytes = Scribe.mode == LoadSaveMode.Saving ? FloatArrayToBytes(savedFireSizes) : null;
        byte[] tickBytes = Scribe.mode == LoadSaveMode.Saving ? IntArrayToBytes(savedTicksSinceSpawn) : null;

        DataExposeUtility.LookByteArray(ref cellBytes, "gridCells");
        DataExposeUtility.LookByteArray(ref sizeBytes, "gridSizes");
        DataExposeUtility.LookByteArray(ref tickBytes, "gridTicks");
        Scribe_Collections.Look(ref savedInstCells, "instCells", LookMode.Value);
        Scribe_Collections.Look(ref savedInstThings, "instThings", LookMode.Reference);

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            savedCellIndices = BytesToIntArray(cellBytes);
            savedFireSizes = BytesToFloatArray(sizeBytes);
            savedTicksSinceSpawn = BytesToIntArray(tickBytes);
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (savedCellIndices != null && savedFireSizes != null && savedTicksSinceSpawn != null)
            {
                int n = Math.Min(savedCellIndices.Length, Math.Min(savedFireSizes.Length, savedTicksSinceSpawn.Length));
                for (int i = 0; i < n; i++)
                {
                    int idx = savedCellIndices[i];
                    if ((uint)idx < (uint)numCells)
                    {
                        IntVec3 c = map.cellIndices.IndexToCell(idx);
                        suppressPathDirty = true;
                        TryAddGridFire(c, savedFireSizes[i], null, savedTicksSinceSpawn[i]);
                        suppressPathDirty = false;
                    }
                }
                if (savedInstCells != null && savedInstThings != null)
                {
                    int instN = Math.Min(savedInstCells.Count, savedInstThings.Count);
                    for (int i = 0; i < instN; i++)
                    {
                        int idx = savedInstCells[i];
                        Thing inst = savedInstThings[i];
                        if ((uint)idx < (uint)numCells && inst != null && staticFireAtCell[idx] == 1)
                        {
                            gridInstigators[idx] = inst;
                        }
                    }
                }
            }
            savedCellIndices = null;
            savedFireSizes = null;
            savedTicksSinceSpawn = null;
            savedInstCells = null;
            savedInstThings = null;
        }
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        initializedAfterLoad = true;
        ApplyCurrentSettingsToMap(isInitialLoad: true);
    }

    public static void SyncAllMapsToCurrentSettings()
    {
        if (Current.ProgramState != ProgramState.Playing || Find.Maps == null)
        {
            return;
        }
        List<Map> maps = Find.Maps;
        for (int i = 0; i < maps.Count; i++)
        {
            maps[i].GetComponent<FireMapComponent>()?.ApplyCurrentSettingsToMap(isInitialLoad: false);
        }
    }

    public void ApplyCurrentSettingsToMap(bool isInitialLoad)
    {
        FireOptimizationMode targetMode = FastFireMod.Settings.mode;
        bool wantClusterLighting = FastFireMod.Settings.UseClusterLighting;
        bool wantBatchedRender = FastFireMod.Settings.UseBatchedRendering;
        bool wantAudioAgg = FastFireMod.Settings.UseAudioAggregation;

        // 1. Handle mode transition between Grid <-> Thing (fire positions don't change, so suppress redundant path dirtying)
        suppressPathDirty = true;
        try
        {
            if (targetMode == FireOptimizationMode.ExtremeGrid)
            {
                // Absorb all non-proxy ground Fire Things into Grid
                if (lastAppliedMode != FireOptimizationMode.ExtremeGrid)
                {
                    proxySpawnTicks.Clear();
                }
                tmpThingSnapshot.Clear();
                List<Thing> fires = map.listerThings.ThingsOfDef(ThingDefOf.Fire);
                for (int i = 0; i < fires.Count; i++)
                {
                    if (fires[i] is Fire { parent: null } f && !proxySpawnTicks.ContainsKey(f.thingIDNumber))
                    {
                        tmpThingSnapshot.Add(f);
                    }
                }
                if (tmpThingSnapshot.Count > 0)
                {
                    BeginBatchDespawnScope();
                    try
                    {
                        for (int i = 0; i < tmpThingSnapshot.Count; i++)
                        {
                            AbsorbThingFireIntoGrid((Fire)tmpThingSnapshot[i]);
                        }
                    }
                    finally
                    {
                        EndBatchDespawnScope();
                        tmpThingSnapshot.Clear();
                    }
                }
            }
            else if (gridFireCount > 0)
            {
                // Switching from ExtremeGrid to Compatible or Vanilla: materialize all Grid fires into real Fire Things!
                List<(IntVec3 cell, float size, int ticksSpawn, int ticksSpread, int ticksSmoke, float flammMax, Thing inst)> toSpawn = new List<(IntVec3, float, int, int, int, float, Thing)>(gridFireCount);
                for (int b = 0; b < BucketCount; b++)
                {
                    List<int> list = gridBuckets[b];
                    for (int i = 0; i < list.Count; i++)
                    {
                        int idx = list[i];
                        gridInstigators.TryGetValue(idx, out Thing inst);
                        toSpawn.Add((
                            map.cellIndices.IndexToCell(idx),
                            gridFireSize[idx],
                            gridTicksSinceSpawn[idx],
                            gridTicksSinceSpread[idx],
                            gridTicksUntilSmoke[idx],
                            gridFlammabilityMax[idx],
                            inst));
                    }
                }

                // Clear grid fires cleanly
                for (int b = 0; b < BucketCount; b++)
                {
                    List<int> list = gridBuckets[b];
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        int idx = list[i];
                        ExtinguishGridFireAt(idx, map.cellIndices.IndexToCell(idx));
                    }
                }

                using (map.pathing.DisableIncrementalScope())
                {
                    for (int i = 0; i < toSpawn.Count; i++)
                    {
                        var item = toSpawn[i];
                        Fire f = (Fire)ThingMaker.MakeThing(ThingDefOf.Fire);
                        f.fireSize = item.size;
                        f.ticksSinceSpawn = item.ticksSpawn;
                        f.ticksSinceSpread = item.ticksSpread;
                        f.ticksUntilSmoke = item.ticksSmoke;
                        f.flammabilityMax = item.flammMax;
                        f.instigator = item.inst;
                        GenSpawn.Spawn(f, item.cell, map, Rot4.North);
                    }
                }
            }
        }
        finally
        {
            suppressPathDirty = false;
        }

        // 2. Sync TickList, DynamicDrawManager, and Cluster Lighting state for all static Fire Things on mode/sub-toggle changes
        if (!isInitialLoad || targetMode != lastAppliedMode || wantClusterLighting != lastClusterLighting || wantBatchedRender != lastBatchedRendering)
        {
            SyncStaticThingRegistrations(targetMode, wantClusterLighting, wantBatchedRender);
        }

        if (!wantAudioAgg)
        {
            EndAllAudioClusters();
        }

        lastAppliedMode = targetMode;
        lastClusterLighting = wantClusterLighting;
        lastBatchedRendering = wantBatchedRender;
        lastAudioAggregation = wantAudioAgg;

        if (isInitialLoad)
        {
            FlushPendingPathUpdates(force: true);
        }
    }

    private void SyncStaticThingRegistrations(FireOptimizationMode targetMode, bool wantClusterLighting, bool wantBatchedRender)
    {
        bool active = targetMode != FireOptimizationMode.Vanilla;
        List<Thing> fires = map.listerThings.ThingsOfDef(ThingDefOf.Fire);

        // Rebuild cluster glowers cleanly
        for (int c = 0; c < lightChunkCount; c++)
        {
            lightChunkFireCount[c] = 0;
            DeregisterClusterGlower(c);
        }

        if (wantClusterLighting)
        {
            for (int b = 0; b < BucketCount; b++)
            {
                List<int> list = gridBuckets[b];
                for (int i = 0; i < list.Count; i++)
                {
                    IncrementLightChunk(map.cellIndices.IndexToCell(list[i]));
                }
            }
        }

        for (int i = 0; i < fires.Count; i++)
        {
            if (!(fires[i] is Fire { parent: null } f) || !f.Spawned)
            {
                continue;
            }

            // TickList registration
            if (active)
            {
                Find.TickManager.tickListNormal.DeregisterThing(f);
                if (!compatLocator.ContainsKey(f.thingIDNumber))
                {
                    int bucket = (Find.TickManager.TicksGame + Rand.RangeInclusive(1, BucketCount - 1)) % BucketCount;
                    compatLocator[f.thingIDNumber] = ((byte)bucket, compatBuckets[bucket].Count);
                    compatBuckets[bucket].Add(f);
                }
            }
            else
            {
                if (compatLocator.ContainsKey(f.thingIDNumber))
                {
                    DeregisterFromCompatBucketOnly(f);
                }
                Find.TickManager.tickListNormal.RegisterThing(f);
            }

            // DynamicDrawManager registration
            if (wantBatchedRender)
            {
                map.dynamicDrawManager.DeRegisterDrawable(f);
            }
            else if (!map.dynamicDrawManager.drawThings.Contains(f))
            {
                map.dynamicDrawManager.RegisterDrawable(f);
            }

            // GlowGrid registration
            CompGlower glower = f.GetComp<CompGlower>();
            if (wantClusterLighting)
            {
                if (glower != null && glower.glowOnInt)
                {
                    glower.glowOnInt = false;
                    map.glowGrid.DeRegisterGlower(glower);
                }
                IncrementLightChunk(f.Position);
            }
            else
            {
                if (glower != null && !glower.glowOnInt)
                {
                    glower.glowOnInt = true;
                    map.glowGrid.RegisterGlower(glower);
                }
            }
        }
    }

    private void DeregisterFromCompatBucketOnly(Fire f)
    {
        if (compatLocator.TryGetValue(f.thingIDNumber, out var loc))
        {
            compatLocator.Remove(f.thingIDNumber);
            List<Fire> list = compatBuckets[loc.bucket];
            int lastIdx = list.Count - 1;
            if (loc.slot <= lastIdx)
            {
                if (loc.slot != lastIdx)
                {
                    Fire moved = list[lastIdx];
                    list[loc.slot] = moved;
                    compatLocator[moved.thingIDNumber] = (loc.bucket, loc.slot);
                }
                list.RemoveAt(lastIdx);
            }
        }
    }

    private static byte[] IntArrayToBytes(int[] arr)
    {
        if (arr == null || arr.Length == 0)
        {
            return null;
        }
        byte[] bytes = new byte[arr.Length * sizeof(int)];
        Buffer.BlockCopy(arr, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static int[] BytesToIntArray(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }
        int[] arr = new int[bytes.Length / sizeof(int)];
        Buffer.BlockCopy(bytes, 0, arr, 0, arr.Length * sizeof(int));
        return arr;
    }

    private static byte[] FloatArrayToBytes(float[] arr)
    {
        if (arr == null || arr.Length == 0)
        {
            return null;
        }
        byte[] bytes = new byte[arr.Length * sizeof(float)];
        Buffer.BlockCopy(arr, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] BytesToFloatArray(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }
        float[] arr = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, arr, 0, arr.Length * sizeof(float));
        return arr;
    }

    public void Dispose()
    {
        EndAllAudioClusters();
        if (pooledMeshes != null)
        {
            for (int i = 0; i < pooledMeshes.Length; i++)
            {
                if (pooledMeshes[i] == null)
                {
                    continue;
                }
                for (int j = 0; j < pooledMeshes[i].Count; j++)
                {
                    if (pooledMeshes[i][j] != null)
                    {
                        UnityEngine.Object.Destroy(pooledMeshes[i][j]);
                    }
                }
            }
            pooledMeshes = null;
        }
    }
}
